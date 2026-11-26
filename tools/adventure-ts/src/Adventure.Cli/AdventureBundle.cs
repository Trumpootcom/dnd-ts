using System.IO.Compression;
using System.Security.Cryptography;

internal static class AdventureBundle
{
    private const string Extension = ".dndts";

    public static int Export(string root, string adventurePath, string output, bool verbose)
    {
        var destination = BundlePath(root, output);
        var files = new AdventureBundleFiles(root);
        var manifest = files.AddAdventure(adventurePath);
        AdventureFile.Load(adventurePath, files);

        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(adventurePath, manifest);
                foreach (var (entry, path) in files.Resources)
                    zip.CreateEntryFromFile(path, entry);
            }
            File.Move(temporary, destination);
        }
        finally
        {
            File.Delete(temporary);
        }
        Console.WriteLine($"Exported {destination}");
        if (verbose)
        {
            Console.WriteLine(Path.GetFileName(destination));
            WriteTree(files.Resources.Keys.Prepend(manifest), "");
        }
        return 0;
    }

    private static void WriteTree(IEnumerable<string> entries, string indent)
    {
        var groups = entries.Select(entry => entry.Split('/', 2))
            .GroupBy(parts => parts[0], StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            var last = index == groups.Length - 1;
            var children = group.Where(parts => parts.Length == 2).Select(parts => parts[1]).ToArray();
            Console.WriteLine($"{indent}{(last ? "└── " : "├── ")}{group.Key}{(children.Length == 0 ? "" : "/")}");
            if (children.Length > 0)
                WriteTree(children, indent + (last ? "    " : "│   "));
        }
    }

    public static int Build(string root, string bundlePath, Func<AdventureFile, int> build)
    {
        var source = BundlePath(root, bundlePath);
        var temporary = Directory.CreateTempSubdirectory("dnd-ts-");
        try
        {
            var adventurePath = Extract(source, temporary.FullName);
            var files = new AdventureBundleFiles(temporary.FullName);
            var adventure = AdventureFile.Load(adventurePath, files);
            var result = build(adventure);
            if (result == 0 && files.Resources.Count > 0 &&
                ConfirmImport(files))
                Import(root, files);
            return result;
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    private static string Extract(string source, string directory)
    {
        using var zip = ZipFile.OpenRead(source);
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? adventure = null;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            var isDirectory = name.EndsWith('/');
            if (isDirectory)
                name = name[..^1];
            AdventureBundleFiles.ValidateEntry(name);
            if (!entries.Add(name))
                throw new InvalidDataException($"Duplicate bundle entry '{name}'.");

            if (isDirectory)
            {
                if (!AdventureBundleFiles.IsBundleDirectory(name))
                    throw new InvalidDataException($"Unexpected bundle directory '{name}'.");
                Directory.CreateDirectory(Path.Combine(directory, name));
                continue;
            }

            var path = Path.Combine(directory, name);
            if (AdventureBundleFiles.IsAdventure(name))
            {
                if (adventure is not null)
                    throw new InvalidDataException("A bundle must contain exactly one adventure file.");
                adventure = path;
            }
            else if (!AdventureBundleFiles.IsResource(name))
            {
                throw new InvalidDataException($"Unexpected bundle file '{name}'.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path);
        }
        return adventure ?? throw new InvalidDataException("The bundle has no adventure file under adventures/.");
    }

    private static void Import(string root, AdventureBundleFiles files)
    {
        foreach (var (name, source) in files.Resources)
        {
            var destination = Path.Combine(root, name);
            AdventureBundleFiles.CheckForLinks(root, name);
            var exists = File.Exists(destination);
            if (exists && SameContents(source, destination))
                continue;
            if (exists && !Confirm($"Replace '{destination}' with bundle resource '{name}'?"))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary);
                File.Move(temporary, destination, overwrite: exists);
            }
            finally
            {
                File.Delete(temporary);
            }
            Console.WriteLine($"Imported {name}");
        }
    }

    private static bool ConfirmImport(AdventureBundleFiles files)
    {
        var message = $"Import {files.Resources.Count} resources into library/?";
        Console.Write($"{message} [y/N/v] (v to view) ");
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (answer != "v")
            return answer is "y" or "yes";

        WriteTree(files.Resources.Keys, "");
        return Confirm(message);
    }

    private static bool Confirm(string message)
    {
        Console.Write($"{message} [y/N] ");
        return Console.ReadLine()?.Trim().ToLowerInvariant() is "y" or "yes";
    }

    private static bool SameContents(string source, string destination)
    {
        using var left = File.OpenRead(source);
        using var right = File.OpenRead(destination);
        return left.Length == right.Length && SHA256.HashData(left).AsSpan().SequenceEqual(SHA256.HashData(right));
    }

    private static string BundlePath(string root, string path)
    {
        if (!path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use a .dndts bundle path.");
        return Path.GetFullPath(path, root);
    }
}

internal sealed class AdventureBundleFiles(string root)
{
    private static readonly string[] ResourceDirectories =
        ["library/locations", "library/maps", "library/creatures", "library/portraits"];

    public Dictionary<string, string> Resources { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string AddAdventure(string path)
    {
        var entry = EntryName(path);
        if (!IsAdventure(entry))
            throw new ArgumentException("The exported adventure must be under the workspace's adventures/ folder.");
        return entry;
    }

    public string Resolve(string directory, string reference)
    {
        if (Path.IsPathRooted(reference) || reference.Contains('\\') || reference.Contains(':'))
            throw new ArgumentException($"Bundle references must use relative paths with forward slashes: '{reference}'.");
        var path = Path.GetFullPath(reference, directory);
        var entry = EntryName(path);
        if (!IsResource(entry))
            throw new ArgumentException($"Bundle resources must be under the library folders: '{reference}'.");
        if (Resources.TryGetValue(entry, out var previous) && previous != path)
            throw new ArgumentException($"Bundle resource paths differ only by case: '{previous}' and '{path}'.");
        Resources[entry] = path;
        return path;
    }

    private string EntryName(string path)
    {
        var entry = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
        ValidateEntry(entry);
        CheckForLinks(root, entry);
        return entry;
    }

    public static bool IsAdventure(string name) => name.StartsWith("adventures/", StringComparison.Ordinal) &&
        name.EndsWith(AdventureFile.Extension, StringComparison.OrdinalIgnoreCase);

    public static bool IsResource(string name) =>
        ResourceDirectories.Any(directory => name.StartsWith(directory + "/", StringComparison.Ordinal));

    public static bool IsBundleDirectory(string name) => name is "adventures" or "library" ||
        name.StartsWith("adventures/", StringComparison.Ordinal) || IsResource(name) ||
        ResourceDirectories.Contains(name, StringComparer.Ordinal);

    public static void ValidateEntry(string name)
    {
        foreach (var part in name.Split('/'))
        {
            var stem = part.Split('.')[0].TrimEnd().ToUpperInvariant();
            if (!AdventureFile.IsFolderName(part) || part.EndsWith(' ') ||
                stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT"))
                    && "123456789¹²³".Contains(stem[3])))
                throw new InvalidDataException($"Invalid bundle path '{name}'.");
        }
    }

    public static void CheckForLinks(string root, string entry)
    {
        var path = root;
        foreach (var part in entry.Split('/'))
        {
            path = Path.Combine(path, part);
            if (AdventureFile.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException($"Bundle paths cannot use links: '{path}'.");
        }
    }
}
