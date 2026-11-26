using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

try
{
    switch (args)
    {
        case ["--help"] or ["-h"] or []:
            Console.WriteLine("""
                Usage: process_adventure "<title>" [--date YYMMDD] [--template <name-or-path>]
                       process_adventure <file.adventure.json> [--date YYMMDD] [--template <name-or-path>]
                       process_adventure -f <project.dndts> [--date YYMMDD] [--template <name-or-path>]
                       process_adventure "<title-or-adventure-file>" --export <project.dndts> [--verbose]
                       process_adventure --help | --version | --templates

                Run from the workspace. A title first looks in adventures/<title>.adventure.json.
                Export packages the adventure file and its referenced library files.
                Use --verbose with --export to print the bundle's file tree.
                Build a bundle with -f and optionally import its library resources after success.
                Enter v at the import prompt to view the resource tree before confirming.
                If absent, the title uses the staged images in in_maps/ and in_portraits/.
                Adventure files resolve location, image, and creature paths relative to the adventure file.
                A location entry supplies widthFeet and resolves its image relative to the location JSON file.
                Creature default portraits are relative to their creature JSON file.
                In portrait entries, qty numbers copies from 1 using tokenId as the prefix.
                Each qty group shows its tokenId prefix in the lineup and prints every numbered card.
                Build PDFs in out/<YYMMDD> - <title>/, replacing prior output on success.
                If the build publishes exactly one PDF, open it in the system's default viewer.
                Adventure-file PDFs use ENC1 - <encounter name> - Maps.pdf or
                ENC1 - <encounter name> - Portraits.pdf, or one - Adventure.pdf per encounter
                in merged mode. An omitted encounter name defaults to the adventure title.
                Adventure-file builds leave library files in place and run without an archive prompt.
                Staged builds offer to move originals to bak/<YYMMDD>-<TitleWithoutSpaces>/.
                The date comes from --date, the adventure file, or today's local date, in that order.
                The default adventure template is T000.
                The template selects the map and portrait templates, separate or merged PDFs,
                and merge order (encounter or type).
                Adventure-file map overviews and portrait title bands show
                <adventure title> - <encounter ID> - <encounter name>.
                Portrait height comes from the encounter entry, the creature file, or the
                portrait template's default by creature size, in that order.
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

    var argument = args[0].Trim();
    var fromBundle = argument == "-f";
    if (fromBundle && (args.Length < 2 || string.IsNullOrWhiteSpace(args[1])))
        throw new ArgumentException("Provide a .dndts bundle path after -f.");
    if (argument.StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException("Provide an adventure title or file before the options. See --help.");
    var explicitFile = argument.EndsWith(AdventureFile.Extension, StringComparison.OrdinalIgnoreCase);
    if (!fromBundle && !explicitFile && !AdventureFile.IsFolderName(argument))
        throw new ArgumentException("The adventure title must be usable in a folder name.");

    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    var verbose = false;
    for (var index = fromBundle ? 2 : 1; index < args.Length; index++)
    {
        var option = args[index];
        if (option == "--verbose" && !verbose)
        {
            verbose = true;
            continue;
        }
        if (option is not ("--date" or "--template" or "--export") || index + 1 >= args.Length ||
            string.IsNullOrWhiteSpace(args[index + 1]) || !options.TryAdd(option, args[++index]))
            throw new ArgumentException($"Invalid or repeated option '{option}'. See --help.");
    }

    var root = Directory.GetCurrentDirectory();
    if (verbose && !options.ContainsKey("--export"))
        throw new ArgumentException("--verbose requires --export.");
    if (options.TryGetValue("--export", out var exportPath) && (fromBundle || options.Count != 1))
        throw new ArgumentException("--export requires an adventure title or file and accepts only --verbose.");
    if (fromBundle)
        return AdventureBundle.Build(root, args[1], Build);

    var adventurePath = explicitFile ? Path.GetFullPath(argument, root)
        : Path.Combine(root, "adventures", argument + AdventureFile.Extension);
    if (exportPath is not null)
        return AdventureBundle.Export(root, adventurePath, exportPath, verbose);
    return Build(explicitFile || AdventureFile.Exists(adventurePath) ? AdventureFile.Load(adventurePath) : null);

    int Build(AdventureFile? adventure)
    {
        var date = options.GetValueOrDefault("--date") ?? adventure?.Date
            ?? DateTime.Today.ToString("yyMMdd", CultureInfo.InvariantCulture);
        if (!AdventureFile.IsDate(date))
            throw new ArgumentException("Use a valid date in YYMMDD format.");

        var selected = AdventureTemplate.Load(options.GetValueOrDefault("--template") ?? "T000");
        return AdventureBuild.Run(root, adventure?.Title ?? argument, date, selected, adventure);
    }
}
catch (Exception error) when (error is ArgumentException or IOException or InvalidDataException or
    UnauthorizedAccessException or JsonException or NotSupportedException)
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
