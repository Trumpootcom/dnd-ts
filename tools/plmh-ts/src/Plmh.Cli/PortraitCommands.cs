using Plmh.Core;

public static class PortraitCommands
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp"
    };

    public static int Batch(string? templateArgument, int imageDpi = PortraitPdfExporter.DefaultImageDpi)
    {
        var root = FindProjectRoot();
        var incoming = Path.Combine(root, "in_portraits");
        if (!Directory.Exists(incoming))
        {
            Console.Error.WriteLine($"Portrait input directory not found: {DisplayPath(root, incoming)}");
            return 1;
        }

        var images = Directory.GetFiles(incoming).Where(IsImage);
        return RenderBatch(images, Path.Combine(root, "out"), templateArgument, root, imageDpi, strict: false);
    }

    public static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    public static int BuildAdventurePortraits(IReadOnlyList<string> images, string output, string templatePath, string root) =>
        BuildAdventurePortraits(images, output, templatePath, root, null);

    public static int BuildAdventurePortraits(IReadOnlyList<string> images, string output, string templatePath, string root,
        Func<string, string?>? lineupTitleForEncounter) =>
        RenderBatch(images, output, templatePath, root, PortraitPdfExporter.DefaultImageDpi, strict: true,
            lineupTitleForEncounter: lineupTitleForEncounter);

    public static int BuildAdventurePortraits(IReadOnlyList<PortraitFileName> portraits, string output,
        string templatePath, string root, Func<string, string?> lineupTitleForEncounter) =>
        RenderBatch([], output, templatePath, root, PortraitPdfExporter.DefaultImageDpi, strict: true,
            lineupTitleForEncounter: lineupTitleForEncounter, explicitPortraits: portraits);

    private static int RenderBatch(IEnumerable<string> images, string output, string? templateArgument,
        string root, int imageDpi, bool strict, Func<string, string?>? lineupTitleForEncounter = null,
        IReadOnlyList<PortraitFileName>? explicitPortraits = null)
    {
        PortraitTemplate template;
        string templatePath;
        try
        {
            var bundled = strict ? Path.Combine(AppContext.BaseDirectory, "Templates", "Plmh") : null;
            (template, templatePath) = ReadTemplate(templateArgument, root, bundled);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        WriteTemplateStatus(template, templatePath, root);

        Directory.CreateDirectory(output);
        var portraits = explicitPortraits?.ToList() ?? [];
        var exitCode = 0;
        var outputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in images
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
                if (strict && !outputNames.Add(pdf))
                    throw new IOException($"Multiple encounters would produce {Path.GetFileName(pdf)}.");
                return RenderGroup(template, templatePath, encounterPortraits, pdf, root, imageDpi,
                    lineupTitleForEncounter?.Invoke(encounterId));
            }
            catch (Exception error)
            {
                foreach (var portrait in encounterPortraits)
                    Console.Error.WriteLine($"Processing image: {DisplayPath(root, portrait.Path)} !{error.Message.ReplaceLineEndings(" ")}");
                return false;
            }
        }
    }

    public static int Single(string imagePath, string? templateArgument, int imageDpi)
    {
        var root = FindProjectRoot();
        try
        {
            var (template, templatePath) = ReadTemplate(templateArgument, root);
            WriteTemplateStatus(template, templatePath, root);
            var portrait = PortraitFileName.Parse(Path.GetFullPath(imagePath));
            var pdf = Path.ChangeExtension(portrait.Path, ".pdf");
            return RenderGroup(template, templatePath, [portrait], pdf, root, imageDpi) ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static bool RenderGroup(PortraitTemplate template, string templatePath,
        IReadOnlyList<PortraitFileName> portraits, string pdf, string root, int imageDpi, string? lineupTitle = null)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(pdf)!, $".{Path.GetFileName(pdf)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                PortraitPdfExporter.Export(template, templatePath, portraits, output, imageDpi, lineupTitle);
            try
            {
                File.Move(temporary, pdf, overwrite: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Could not save {DisplayPath(root, pdf)}. {error.Message}");
                if (File.Exists(pdf))
                    Console.Error.WriteLine("If the PDF is open in a viewer or preview pane, close it and rerun the command.");
                return false;
            }
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

    public static void ListTemplates()
    {
        var templates = BundledTemplateDirectories(FindProjectRoot())
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory))
            .Where(path => Path.GetExtension(path) is ".json" or ".jsonc")
            .DistinctBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(path =>
            {
                using var input = File.OpenRead(path);
                var template = PortraitTemplate.Read(input);
                return $"""
                    File:  {Path.GetFileName(path)} ({template.Template})
                    Paper: {DisplayPaperValue(template.Page.Size)} - {DisplayPaperValue(template.Page.Orientation)}
                    DPI:   {PortraitPdfExporter.DefaultImageDpi}
                    """;
            })
            .ToArray();

        if (templates.Length == 0)
            throw new FileNotFoundException("No bundled portrait templates found.");

        Console.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, templates));
    }

    private static string DisplayPaperValue(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();

    private static string[] BundledTemplateDirectories(string root, string? bundledDirectory = null) =>
    [
        Path.Combine(root, "tools", "plmh-ts", "src", "Plmh.Cli", "Templates"),
        bundledDirectory ?? Path.Combine(AppContext.BaseDirectory, "Templates")
    ];

    private static (PortraitTemplate Template, string Path) ReadTemplate(string? argument, string root, string? bundledDirectory = null)
    {
        var templatePath = ResolveTemplatePath(argument, root, bundledDirectory);
        using var input = File.OpenRead(templatePath);
        return (PortraitTemplate.Read(input), templatePath);
    }

    private static string ResolveTemplatePath(string? argument, string root, string? bundledDirectory)
    {
        var name = string.IsNullOrWhiteSpace(argument) ? "T000" : argument.Trim();
        var direct = Path.GetFullPath(name, Directory.GetCurrentDirectory());
        if (File.Exists(direct))
            return direct;

        string[] fileNames = Path.HasExtension(name) ? [name] : [name + ".jsonc", name + ".json"];
        var directories = BundledTemplateDirectories(root, bundledDirectory).Append(AppContext.BaseDirectory);
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
