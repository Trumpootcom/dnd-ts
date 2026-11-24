using Plmh.Core;

internal static class PortraitCommands
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp"
    };

    public static int Batch(string? templateArgument)
    {
        var root = FindProjectRoot();
        var incoming = Path.Combine(root, "in_portraits");
        if (!Directory.Exists(incoming))
        {
            Console.Error.WriteLine($"Portrait input directory not found: {DisplayPath(root, incoming)}");
            return 1;
        }

        PortraitTemplate template;
        string templatePath;
        try
        {
            (template, templatePath) = ReadTemplate(templateArgument, root);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        WriteTemplateStatus(template, templatePath, root);

        var output = Path.Combine(root, "out");
        Directory.CreateDirectory(output);
        var portraits = new List<PortraitFileName>();
        var exitCode = 0;
        foreach (var image in Directory.GetFiles(incoming)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var portrait = PortraitFileName.Parse(image);
                portraits.Add(portrait);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Processing image: {DisplayPath(root, image)} !{error.Message.ReplaceLineEndings(" ")}");
                exitCode = 1;
            }
        }

        var ordered = portraits
            .OrderBy(item => item.EncounterId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TokenId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string? currentEncounter = null;
        var currentGroup = new List<PortraitFileName>();
        foreach (var portrait in ordered)
        {
            if (currentEncounter is not null
                && !string.Equals(currentEncounter, portrait.EncounterId, StringComparison.OrdinalIgnoreCase))
            {
                if (!FinishEncounter(currentEncounter, currentGroup))
                    exitCode = 1;
                currentGroup = [];
            }

            currentEncounter = portrait.EncounterId;
            currentGroup.Add(portrait);
        }

        if (currentEncounter is not null && !FinishEncounter(currentEncounter, currentGroup))
            exitCode = 1;
        return exitCode;

        bool FinishEncounter(string encounterId, List<PortraitFileName> encounterPortraits)
        {
            try
            {
                var safeEncounter = SafeFilePart(encounterId);
                var pdf = Path.Combine(output, $"encounter_{safeEncounter.ToLowerInvariant()}.pdf");
                return RenderGroup(template, templatePath, encounterPortraits, pdf, root);
            }
            catch (Exception error)
            {
                foreach (var portrait in encounterPortraits)
                    Console.Error.WriteLine($"Processing image: {DisplayPath(root, portrait.Path)} !{error.Message.ReplaceLineEndings(" ")}");
                return false;
            }
        }
    }

    public static int Single(string imagePath, string? templateArgument)
    {
        var root = FindProjectRoot();
        try
        {
            var (template, templatePath) = ReadTemplate(templateArgument, root);
            WriteTemplateStatus(template, templatePath, root);
            var portrait = PortraitFileName.Parse(Path.GetFullPath(imagePath));
            var pdf = Path.ChangeExtension(portrait.Path, ".pdf");
            return RenderGroup(template, templatePath, [portrait], pdf, root) ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static bool RenderGroup(PortraitTemplate template, string templatePath,
        IReadOnlyList<PortraitFileName> portraits, string pdf, string root)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(pdf)!, $".{Path.GetFileName(pdf)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                PortraitPdfExporter.Export(template, templatePath, portraits, output);
            File.Move(temporary, pdf, overwrite: true);
            foreach (var portrait in portraits)
                Console.WriteLine($"Processing image: {DisplayPath(root, portrait.Path)} -> {DisplayPath(root, pdf)}");
            return true;
        }
        catch (Exception error)
        {
            var message = error is InvalidDataException ? "Bad image file. " + error.Message : error.Message;
            foreach (var portrait in portraits)
                Console.Error.WriteLine($"Processing image: {DisplayPath(root, portrait.Path)} !{message.ReplaceLineEndings(" ")}");
            return false;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static (PortraitTemplate Template, string Path) ReadTemplate(string? argument, string root)
    {
        var templatePath = ResolveTemplatePath(argument, root);
        using var input = File.OpenRead(templatePath);
        return (PortraitTemplate.Read(input), templatePath);
    }

    private static string ResolveTemplatePath(string? argument, string root)
    {
        var name = string.IsNullOrWhiteSpace(argument) ? "T000" : argument.Trim();
        var direct = Path.GetFullPath(name, Directory.GetCurrentDirectory());
        if (File.Exists(direct))
            return direct;

        string[] fileNames = Path.HasExtension(name) ? [name] : [name + ".jsonc", name + ".json"];
        var directories = new[]
        {
            Path.Combine(root, "tools", "plmh-ts", "src", "Plmh.Cli", "Templates"),
            Path.Combine(AppContext.BaseDirectory, "Templates"),
            AppContext.BaseDirectory
        };
        var candidates = from directory in directories
                         from fileName in fileNames
                         select Path.Combine(directory, fileName);
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"Template not found: {name}");
    }

    private static void WriteTemplateStatus(PortraitTemplate template, string path, string root) =>
        Console.WriteLine($"Using Template: {DisplayPath(root, path)} ({template.Template})");

    private static string SafeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Where(character => !invalid.Contains(character)
            && character is not ('/' or '\\' or ':')).ToArray());
        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
            throw new ArgumentException($"Encounter ID '{value}' cannot be used in an output filename.");
        return safe;
    }

    private static string FindProjectRoot()
    {
        foreach (var searchPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (DirectoryInfo? directory = new(searchPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "plmh-ts", "src", "Plmh.Cli", "Plmh.Cli.csproj")))
                    return directory.FullName;
                if (Directory.Exists(Path.Combine(directory.FullName, "in_portraits")))
                    return directory.FullName;
            }
        }
        return Directory.GetCurrentDirectory();
    }

    private static string DisplayPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return Path.GetFileName(relative) == relative ? $".{Path.DirectorySeparatorChar}{relative}" : relative;
    }
}
