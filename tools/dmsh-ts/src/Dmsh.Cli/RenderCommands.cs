using Dmsh.Core;
using System.Globalization;
using System.Text.RegularExpressions;

public static class RenderCommands
{
    private static readonly Regex FilenameDimensions = new(@"(?<width>[0-9]+)[xX][0-9]+\z");

    public static int Render(string image, string? templatePath, double? worldWidth)
    {
        var template = ReadTemplate(templatePath, worldWidth);
        var pdf = Path.ChangeExtension(Path.GetFullPath(image), ".pdf");
        return RenderImage(template, Path.GetFullPath(image), pdf, FindProjectRoot()) ? 0 : 1;
    }

    public static int Batch(string? incoming, string? output, string? templatePath, double? worldWidth)
    {
        var template = ReadTemplate(templatePath, worldWidth);
        var projectRoot = FindProjectRoot();
        incoming = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            incoming ?? Path.Combine(projectRoot, "in_maps")));
        output = Path.GetFullPath(output ?? Path.Combine(projectRoot, "out"));
        return RenderBatch(Directory.GetFiles(incoming).Where(IsImage), output, template, projectRoot);
    }

    public static int BuildAdventureMaps(IReadOnlyList<string> images, string output, string templatePath, string root) =>
        RenderBatch(images, output, ReadTemplate(templatePath, null,
            Path.Combine(AppContext.BaseDirectory, "Templates", "Dmsh")), root);

    public static int BuildAdventureMaps(
        IEnumerable<(string Image, string PdfName, double? WidthFeet)> maps,
        string output, string templatePath, string root) =>
        BuildAdventureMaps(maps.Select(map => (map.Image, map.PdfName, map.WidthFeet, (string?)null)),
            output, templatePath, root);

    public static int BuildAdventureMaps(
        IEnumerable<(string Image, string PdfName, double? WidthFeet, string? OverviewTitle)> maps,
        string output, string templatePath, string root)
    {
        var template = ReadTemplate(templatePath, null, Path.Combine(AppContext.BaseDirectory, "Templates", "Dmsh"));
        Directory.CreateDirectory(output);
        var exitCode = 0;
        foreach (var (image, pdfName, widthFeet, overviewTitle) in maps)
        {
            var effective = widthFeet is { } feet ? template with { WorldWidthFeet = feet } : template;
            if (!RenderImage(effective, image, Path.Combine(output, pdfName), root,
                readFilenameWidth: false, overviewTitle: overviewTitle))
                exitCode = 1;
        }
        return exitCode;
    }

    private static int RenderBatch(IEnumerable<string> images, string output, MapTemplate template,
        string projectRoot)
    {
        var groups = images
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);

        var exitCode = 0;
        foreach (var group in groups)
        {
            if (group.Count() > 1)
            {
                foreach (var imagePath in group)
                    Console.Error.WriteLine($"Processing image: {DisplayPath(projectRoot, imagePath)} !Multiple images would produce {group.Key}.pdf");
                exitCode = 1;
                continue;
            }

            var image = group.Single();
            var pdf = Path.Combine(output, Path.ChangeExtension(Path.GetFileName(image), ".pdf"));
            if (!RenderImage(template, image, pdf, projectRoot))
                exitCode = 1;
        }
        return exitCode;
    }

    private static bool RenderImage(MapTemplate template, string image, string pdf,
        string projectRoot, bool readFilenameWidth = true, string? overviewTitle = null)
    {
        var status = $"Processing image: {DisplayPath(projectRoot, image)}";
        try
        {
            if (readFilenameWidth && FilenameDimensions.Match(Path.GetFileNameWithoutExtension(image)) is { Success: true } dimensions)
            {
                if (!double.TryParse(dimensions.Groups["width"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var feet) || !double.IsFinite(feet) || feet <= 0)
                    throw new ArgumentException("The width in the image filename must be a positive, finite number in feet.");
                template = template with { WorldWidthFeet = feet };
            }

            Save(template, image, pdf, overviewTitle);
            status += $" -> {DisplayPath(projectRoot, pdf)}";
            Console.WriteLine(status);
            return true;
        }
        catch (Exception error)
        {
            var message = error is InvalidDataException ? "Bad image file. " + error.Message : error.Message;
            Console.Error.WriteLine($"{status} !{message.ReplaceLineEndings(" ")}");
            return false;
        }
    }

    private static string FindProjectRoot()
    {
        var searchPaths = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        string? standaloneRoot = null;
        foreach (var searchPath in searchPaths)
        {
            for (DirectoryInfo? directory = new(searchPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "dmsh-ts", "src", "Dmsh.Cli", "Dmsh.Cli.csproj")))
                    return directory.FullName;
                if (standaloneRoot is null && File.Exists(Path.Combine(directory.FullName, "src", "Dmsh.Cli", "Dmsh.Cli.csproj")))
                    standaloneRoot = directory.FullName;
            }
        }
        return standaloneRoot ?? Directory.GetCurrentDirectory();
    }

    private static string DisplayPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return Path.GetFileName(relative) == relative ? $".{Path.DirectorySeparatorChar}{relative}" : relative;
    }

    public static bool IsImage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp";

    private static MapTemplate ReadTemplate(string? path, double? worldWidth, string? bundledDirectory = null)
    {
        var (template, fileName) = TemplateFiles.Read(path, bundledDirectory);
        Console.WriteLine($"Using Template: {fileName} ({template.Template})");
        return worldWidth is { } feet ? template with { WorldWidthFeet = feet } : template;
    }

    private static void Save(MapTemplate template, string image, string pdf, string? overviewTitle)
    {
        if (!IsImage(image))
            throw new ArgumentException("Supported images are JPEG, PNG, BMP and static WebP.");
        if (Directory.Exists(pdf))
            throw new IOException($"Output path is a directory: {pdf}");

        var temporary = Path.Combine(Path.GetDirectoryName(pdf)!, $".dmsh-{Guid.NewGuid():N}.tmp");
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
        {
            try
            {
                MapPdfExporter.Export(template, image, stream, overviewTitle);
                stream.Flush(flushToDisk: true);
            }
            catch
            {
                stream.Dispose();
                File.Delete(temporary);
                throw;
            }
        }
        try
        {
            File.Move(temporary, pdf, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"Could not replace '{pdf}': {error.Message} Close the PDF if it is open in a viewer and retry.", error);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
