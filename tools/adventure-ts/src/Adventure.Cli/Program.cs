using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

try
{
    switch (args)
    {
        case ["--help"] or ["-h"] or []:
            Console.WriteLine("""
                Usage: process_adventure "<title>" [--date YYMMDD] [--template <name-or-path>]
                       process_adventure --help | --version | --templates

                Run from the workspace containing in_maps/ and in_portraits/.
                Build PDFs in out/<YYMMDD> - <title>/, replacing prior output on success.
                Review the PDFs before approving the move of originals to bak/<YYMMDD>-<TitleWithoutSpaces>/.
                The date defaults to today in local time. The default adventure template is T000.
                The template selects the map and portrait templates, separate or merged PDFs,
                and merge order (encounter or type).
                For matching encounters, lineup title bands show the adventure title and map name.
                """);
            return 0;
        case ["--version"]:
            Console.WriteLine($"dnd-ts {VersionOf(Assembly.GetExecutingAssembly())}");
            Console.WriteLine($"dmsh-ts {VersionOf(typeof(RenderCommands).Assembly)}");
            Console.WriteLine($"plmh-ts {VersionOf(typeof(PortraitCommands).Assembly)}");
            return 0;
        case ["--templates"]:
            foreach (var file in Directory.EnumerateFiles(AdventureTemplate.BundledDirectory, "*.json").Order())
            {
                var template = AdventureTemplate.Load(file);
                Console.WriteLine($"{Path.GetFileName(file)} ({template.Name})");
            }
            return 0;
    }

    var title = args[0].Trim();
    if (title.StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException("Provide an adventure title before the options. See --help.");
    if (title.Length == 0 || title.EndsWith('.') ||
        title.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character)))
        throw new ArgumentException("The adventure title must be usable in a folder name.");

    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 1; index < args.Length; index += 2)
    {
        if (args[index] is not ("--date" or "--template") || index + 1 >= args.Length ||
            string.IsNullOrWhiteSpace(args[index + 1]) || !options.TryAdd(args[index], args[index + 1]))
            throw new ArgumentException($"Invalid or repeated option '{args[index]}'. See --help.");
    }

    var date = options.GetValueOrDefault("--date") ?? DateTime.Today.ToString("yyMMdd", CultureInfo.InvariantCulture);
    if (!Regex.IsMatch(date, @"\A[0-9]{6}\z") ||
        !DateOnly.TryParseExact("20" + date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        throw new ArgumentException("Use a valid date in YYMMDD format.");

    var selected = AdventureTemplate.Load(options.GetValueOrDefault("--template") ?? "T000");
    return AdventureBuild.Run(Directory.GetCurrentDirectory(), title, date, selected);
}
catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or JsonException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static string VersionOf(Assembly assembly) =>
    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

internal sealed record AdventureTemplate(
    [property: JsonRequired, JsonPropertyName("name")] string Name,
    [property: JsonRequired, JsonPropertyName("map_template")] string MapTemplate,
    [property: JsonRequired, JsonPropertyName("portraits_template")] string PortraitsTemplate,
    [property: JsonRequired, JsonPropertyName("individualPDF")] bool IndividualPdf,
    [property: JsonPropertyName("mergeOrder")] string MergeOrder = "encounter")
{
    public static string BundledDirectory => Path.Combine(AppContext.BaseDirectory, "Templates", "Adventure");

    public static AdventureTemplate Load(string argument)
    {
        var filename = Path.HasExtension(argument) ? argument : argument + ".json";
        var path = File.Exists(argument) ? argument
            : Path.GetFileName(filename) == filename ? Path.Combine(BundledDirectory, filename) : filename;
        using var stream = File.OpenRead(path);
        var template = JsonSerializer.Deserialize<AdventureTemplate>(stream);
        if (template is null || string.IsNullOrWhiteSpace(template.Name) ||
            string.IsNullOrWhiteSpace(template.MapTemplate) || string.IsNullOrWhiteSpace(template.PortraitsTemplate) ||
            template.MergeOrder is not ("encounter" or "type"))
            throw new JsonException($"Invalid adventure template: {path}. Use 'encounter' or 'type' for mergeOrder.");
        return template;
    }
}
