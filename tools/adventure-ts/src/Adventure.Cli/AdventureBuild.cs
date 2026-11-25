using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

internal static class AdventureBuild
{
    private static readonly Regex MapEncounter = new(@"(?:^|[ _-])e(?<number>[0-9]+)(?=$|[ _-])", RegexOptions.IgnoreCase);
    private static readonly Regex PortraitEncounter = new(@"^e(?<number>[0-9]+)$", RegexOptions.IgnoreCase);
    private static readonly Regex MapDimensions = new(@"[0-9]+[xX][0-9]+\z");

    public static int Run(string root, string title, string date, AdventureTemplate template)
    {
        var lockRoot = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        var lockKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lockRoot)));
        using var mutex = new Mutex(false, $"dnd-ts-adventure-{lockKey}");
        try
        {
            if (!mutex.WaitOne(0))
                throw new IOException("Another adventure build is running in this workspace.");
        }
        catch (AbandonedMutexException)
        {
            // The previous process exited while holding the lock. This process now owns it.
        }

        try
        {
            return Build(root, title, date, template);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static int Build(string root, string title, string date, AdventureTemplate template)
    {
        var maps = Inputs(Path.Combine(root, "in_maps"), RenderCommands.IsImage);
        var portraits = Inputs(Path.Combine(root, "in_portraits"), PortraitCommands.IsImage);
        if (maps.Length + portraits.Length == 0)
        {
            Console.Error.WriteLine("No supported images in in_maps/ or in_portraits/.");
            return 1;
        }

        var name = $"{date} - {title}";
        var outputRoot = Path.Combine(root, "out");
        var destination = Path.Combine(outputRoot, name);
        var archive = Path.Combine(root, "bak", date + "-" + string.Concat(title.Where(c => !char.IsWhiteSpace(c))));
        var inputs = maps.Concat(portraits).Select(SourceFile.Read).ToArray();
        Console.WriteLine($"Adventure: {name} | Date: {date} | Template: {template.Name}");
        Directory.CreateDirectory(outputRoot);
        var stage = Directory.CreateDirectory(Path.Combine(outputRoot, ".adventure-" + Guid.NewGuid().ToString("N")));
        try
        {
            // Stage beside the destination so publication uses directory renames.
            var outputStage = Directory.CreateDirectory(Path.Combine(stage.FullName, "publish"));
            var mapOutput = Path.Combine(stage.FullName, "maps");
            var portraitOutput = Path.Combine(stage.FullName, "portraits");
            var mapStatus = maps.Length == 0 ? 0 :
                RenderCommands.BuildAdventureMaps(maps, mapOutput, template.MapTemplate, root);
            var mapNames = maps.Select(path => Path.GetFileNameWithoutExtension(path))
                .Select(stem => (Encounter: EncounterNumber(stem, MapEncounter),
                    Name: MapDimensions.Replace(stem, "").TrimEnd(' ', '_', '-')))
                .Where(map => map.Encounter.HasValue && map.Name.Length > 0)
                .ToLookup(map => map.Encounter, map => map.Name);
            var portraitStatus = portraits.Length == 0 ? 0 :
                PortraitCommands.BuildAdventurePortraits(portraits, portraitOutput, template.PortraitsTemplate, root,
                    encounter =>
                    {
                        var names = mapNames[EncounterNumber(encounter, PortraitEncounter)]
                            .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
                        return names.Length == 1 ? $"{title} - {names[0]}" : null;
                    });
            if (mapStatus != 0 || portraitStatus != 0)
            {
                Console.Error.WriteLine("Adventure build failed. Review the errors above.");
                return 1;
            }
            CheckSources(inputs);

            var pdfs = GetPdfs(mapOutput, isPortrait: false).Concat(GetPdfs(portraitOutput, isPortrait: true)).ToArray();
            if (template.IndividualPdf)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pdf in pdfs)
                {
                    var file = Path.GetFileName(pdf.Path);
                    if (!names.Add(file))
                        throw new IOException($"More than one output would be named '{file}'.");
                    File.Move(pdf.Path, Path.Combine(outputStage.FullName, file));
                }
            }
            else
            {
                Merge(pdfs, Path.Combine(outputStage.FullName, name + ".pdf"), title, date, template.MergeOrder);
            }

            var filenames = Directory.GetFiles(outputStage.FullName).Select(Path.GetFileName)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
            Publish(outputStage.FullName, destination);
            Console.WriteLine($"Review the PDFs in {Path.GetRelativePath(root, destination)}:");
            foreach (var filename in filenames)
                Console.WriteLine($"  {filename}");

            CheckArchive(inputs, archive);
            Console.Write($"Archive all source images to {Path.GetRelativePath(root, archive)}? [y/N] ");
            var answer = Console.ReadLine()?.Trim();
            if (string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Archive(inputs, archive);
                Console.WriteLine($"Archived {inputs.Length} source images.");
            }
            else
            {
                Console.WriteLine("Source images remain in the input folders.");
            }
            return 0;
        }
        finally
        {
            CleanDirectory(stage.FullName);
        }
    }

    private static string[] Inputs(string directory, Func<string, bool> isImage) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory).Where(isImage).ToArray() : [];

    private static IEnumerable<PdfOutput> GetPdfs(string directory, bool isPortrait)
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(directory, "*.pdf"))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            var encounter = isPortrait ? EncounterNumber(stem["encounter_".Length..], PortraitEncounter)
                : EncounterNumber(stem, MapEncounter);
            yield return new PdfOutput(path, isPortrait, encounter);
        }
    }

    private static BigInteger? EncounterNumber(string name, Regex pattern)
    {
        var match = pattern.Match(name);
        return match.Success ? BigInteger.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture) : null;
    }

    private static void Merge(PdfOutput[] pdfs, string destination, string title, string date, string order)
    {
        var ordered = order == "type"
            ? pdfs.OrderBy(pdf => pdf.IsPortrait).ThenBy(pdf => pdf.Encounter is null).ThenBy(pdf => pdf.Encounter)
            : pdfs.OrderBy(pdf => pdf.Encounter is null)
                .ThenBy(pdf => pdf.Encounter)
                .ThenBy(pdf => pdf.Encounter is null ? !pdf.IsPortrait : pdf.IsPortrait);
        using var merged = new PdfDocument();
        merged.Info.Title = title;
        merged.Info.Elements.SetString("/AdventureDate", date);
        foreach (var pdf in ordered.ThenBy(pdf => Path.GetFileName(pdf.Path), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(pdf => Path.GetFileName(pdf.Path), StringComparer.Ordinal))
        {
            try
            {
                using var original = PdfReader.Open(pdf.Path, PdfDocumentOpenMode.Import);
                foreach (var page in original.Pages)
                    merged.AddPage(page);
            }
            catch (Exception error)
            {
                throw new IOException($"Could not import '{Path.GetFileName(pdf.Path)}': {error.Message}", error);
            }
        }
        try
        {
            merged.Save(destination);
        }
        catch (Exception error)
        {
            throw new IOException($"Could not save the merged adventure PDF: {error.Message}", error);
        }
    }

    private static void Publish(string stage, string destination)
    {
        if (!Directory.Exists(destination))
        {
            Directory.Move(stage, destination);
            return;
        }
        if (File.GetAttributes(destination).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException($"Adventure output is a link: {destination}");

        var previous = Path.Combine(Path.GetDirectoryName(destination)!, ".previous-adventure-" + Guid.NewGuid().ToString("N"));
        Directory.Move(destination, previous);
        try
        {
            Directory.Move(stage, destination);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try
            {
                Directory.Move(previous, destination);
            }
            catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"Publication failed. The previous output is at '{previous}': {restoreError.Message}", error);
            }
            throw;
        }
        CleanDirectory(previous);
    }

    private static void CheckArchive(SourceFile[] inputs, string archive)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(archive))
            throw new IOException($"Archive path is a file: {archive}");
        if (Directory.Exists(archive))
            names.UnionWith(Directory.EnumerateFileSystemEntries(archive).Select(path => Path.GetFileName(path)));
        foreach (var input in inputs)
        {
            var name = Path.GetFileName(input.Path);
            if (!names.Add(name))
                throw new IOException($"Archive filename conflict: '{name}'. Source images remain in place.");
        }
    }

    private static void CheckSources(SourceFile[] inputs)
    {
        foreach (var input in inputs)
            if (SourceFile.Read(input.Path) != input)
                throw new IOException($"Source changed during the run: '{input.Path}'.");
    }

    private static void Archive(SourceFile[] inputs, string archive)
    {
        CheckSources(inputs);
        CheckArchive(inputs, archive);
        Directory.CreateDirectory(archive);
        var moved = new Stack<(string Source, string Destination)>();
        try
        {
            foreach (var input in inputs)
            {
                var destination = Path.Combine(archive, Path.GetFileName(input.Path));
                File.Move(input.Path, destination);
                moved.Push((input.Path, destination));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            var recovery = new List<string>();
            foreach (var (source, destination) in moved)
            {
                try
                {
                    File.Move(destination, source);
                }
                catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException)
                {
                    recovery.Add($"Could not return '{destination}' to '{source}': {rollbackError.Message}");
                }
            }
            throw new IOException(string.Join(Environment.NewLine, new[] { $"Archive failed: {error.Message}" }.Concat(recovery)), error);
        }
    }

    private static void CleanDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not remove '{path}': {error.Message}");
        }
    }

    private sealed record PdfOutput(string Path, bool IsPortrait, BigInteger? Encounter);

    private sealed record SourceFile(string Path, long Length, DateTime LastWriteUtc)
    {
        public static SourceFile Read(string path)
        {
            var info = new FileInfo(path);
            return new SourceFile(path, info.Length, info.LastWriteTimeUtc);
        }
    }
}
