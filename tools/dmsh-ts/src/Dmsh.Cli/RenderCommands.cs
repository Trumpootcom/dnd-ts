using Dmsh.Core;
using System.Globalization;
using System.Text.RegularExpressions;

internal static class RenderCommands
{
    private static readonly Regex FilenameDimensions = new(@"(?<width>[0-9]+)[xX][0-9]+\z");

    public static int Render(string image, string? templatePath, double? worldWidth)
    {
        var template = ReadTemplate(templatePath, worldWidth);
        var pdf = Path.ChangeExtension(Path.GetFullPath(image), ".pdf");
        return RenderImage(template, Path.GetFullPath(image), pdf, FindProjectRoot()) ? 0 : 1;
    }

    public static int Batch(string incoming, string output, string? templatePath, double? worldWidth)
    {
        var template = ReadTemplate(templatePath, worldWidth);
        incoming = Path.TrimEndingDirectorySeparator(Path.GetFullPath(incoming));
        output = Path.GetFullPath(output);
        var parent = Path.GetDirectoryName(incoming)
            ?? throw new ArgumentException("The incoming directory must have a parent for the processed folder.");
        var processed = Path.Combine(parent, "processed");
        if (string.Equals(incoming, processed, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The incoming directory cannot also be the processed directory.");
        var projectRoot = FindProjectRoot();
        var groups = Directory.GetFiles(incoming).Where(IsImage)
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(processed);

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
            var archivedImage = Path.Combine(processed, Path.GetFileName(image));
            if (!RenderImage(template, image, pdf, projectRoot, archivedImage))
                exitCode = 1;
        }
        return exitCode;
    }

    private static bool RenderImage(MapTemplate template, string image, string pdf,
        string projectRoot, string? archivedImage = null)
    {
        var status = $"Processing image: {DisplayPath(projectRoot, image)}";
        try
        {
            if (archivedImage is not null && (File.Exists(archivedImage) || Directory.Exists(archivedImage)))
                throw new IOException($"Processed path already exists: {DisplayPath(projectRoot, archivedImage)}");

            var dimensions = FilenameDimensions.Match(Path.GetFileNameWithoutExtension(image));
            if (dimensions.Success)
            {
                if (!double.TryParse(dimensions.Groups["width"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var feet) || !double.IsFinite(feet) || feet <= 0)
                    throw new ArgumentException("The width in the image filename must be a positive, finite number in feet.");
                template = template with { WorldWidthFeet = feet };
            }

            Save(template, image, pdf);
            status += $" -> {DisplayPath(projectRoot, pdf)}";
            if (archivedImage is not null)
                File.Move(image, archivedImage);

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
        var current = Directory.GetCurrentDirectory();
        for (DirectoryInfo? directory = new(current); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "src", "Dmsh.Cli", "Dmsh.Cli.csproj")))
                return directory.FullName;
        return current;
    }

    private static string DisplayPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return Path.GetFileName(relative) == relative ? $".{Path.DirectorySeparatorChar}{relative}" : relative;
    }

    private static bool IsImage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp";

    private static MapTemplate ReadTemplate(string? path, double? worldWidth)
    {
        var (template, fileName) = TemplateFiles.Read(path);
        Console.WriteLine($"Using Template: {fileName} ({template.Template})");
        return worldWidth is { } feet ? template with { WorldWidthFeet = feet } : template;
    }

    private static void Save(MapTemplate template, string image, string pdf)
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
                MapPdfExporter.Export(template, image, stream);
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
