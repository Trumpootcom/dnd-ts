using System.Globalization;
using System.Text.Json;
using Dmsh.Core;

internal static class TemplateFiles
{
    public const string DefaultName = "T000.json";
    private static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "Templates");

    public static (MapTemplate Template, string FileName) Read(string? path)
    {
        if (path is not null && !Path.HasExtension(path) && !File.Exists(path))
            path += ".json";
        var name = path ?? DefaultName;
        var installedPath = Path.Combine(DirectoryPath, name);
        using var json = path is not null && (Path.GetFileName(path) != path || File.Exists(path))
            ? File.OpenRead(path)
            : File.Exists(installedPath) ? File.OpenRead(installedPath)
            : name == DefaultName ? OpenBundledDefault() : File.OpenRead(name);
        return (MapTemplate.Read(json), Path.GetFileName(name));
    }

    public static int List()
    {
        var files = Directory.Exists(DirectoryPath)
            ? Directory.EnumerateFiles(DirectoryPath)
                .Where(path => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        if (!files.Any(path => string.Equals(Path.GetFileName(path), DefaultName, StringComparison.Ordinal)))
            files.Insert(0, Path.Combine(DirectoryPath, DefaultName));

        var exitCode = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            try
            {
                using var json = name == DefaultName && !File.Exists(file)
                    ? OpenBundledDefault() : File.OpenRead(file);
                var template = MapTemplate.Read(json);
                if (string.IsNullOrWhiteSpace(template.Template) || template.Page is not { } page
                    || template.Grid is not { } grid || string.IsNullOrWhiteSpace(page.Size)
                    || string.IsNullOrWhiteSpace(page.Orientation) || string.IsNullOrWhiteSpace(grid.LineStyle)
                    || string.IsNullOrWhiteSpace(grid.Color))
                    throw new JsonException("Expected a template name, paper size and orientation, grid line style and color.");

                var unit = grid.InchesPerFiveFeet == 1 ? "inch" : "inches";
                var (worldWidth, worldHeight) = SheetPlan.GetUsableWorldSize(template);
                Console.WriteLine(FormattableString.Invariant($"""
                    File:   {name} ({template.Template})
                    Paper:  {Display(page.Size)} - {Display(page.Orientation)} - {page.Rows}x{page.Columns} Array
                    Grid:   {grid.InchesPerFiveFeet:G} {unit} - {grid.LineWidthPoints:G}pt {Display(grid.LineStyle)} {Display(grid.Color)}
                    World:  {worldWidth:F1} ft x {worldHeight:F1} ft

                    """));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                Console.Error.WriteLine($"{name}: {error.Message}");
                exitCode = 1;
            }
        }
        return exitCode;
    }

    private static string Display(string value) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());

    private static Stream OpenBundledDefault() =>
        typeof(TemplateFiles).Assembly.GetManifestResourceStream(DefaultName)
        ?? throw new InvalidOperationException($"The built-in template {DefaultName} is missing.");
}
