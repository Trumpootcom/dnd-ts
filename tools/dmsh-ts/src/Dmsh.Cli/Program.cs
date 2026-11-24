using Dmsh.Core;
using System.Globalization;

if (args is ["--version"] or ["-v"])
{
    Console.WriteLine(ApplicationVersion.Current.DisplayName);
    Console.WriteLine($"Built: {ApplicationVersion.Current.BuildTimestampUtc?.ToString("u") ?? "unknown"}");
    return;
}

if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"])
{
    CommandHelp.WriteOverview(Console.Out);
    return;
}

if (args is ["help", _] or [_, "--help"] or [_, "-h"])
{
    var command = args[0] == "help" ? args[1] : args[0];
    if (!CommandHelp.TryWriteCommand(command, Console.Out))
    {
        Console.Error.WriteLine($"Unknown command '{command}'. Run with --help to list commands.");
        Environment.ExitCode = 2;
    }
    return;
}

try
{
    if (args is ["--templates"])
    {
        Environment.ExitCode = TemplateFiles.List();
        return;
    }

    var batch = args[0] == "batch";
    var options = ReadExportOptions(args, batch);
    double? worldWidth = null;
    if (options.TryGetValue("--width", out var widthValue))
    {
        if (!double.TryParse(widthValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var feet)
            || !double.IsFinite(feet) || feet <= 0)
            throw new ArgumentException("--width must be a positive, finite number in feet.");
        worldWidth = feet;
    }

    var templatePath = options.GetValueOrDefault("--template");
    Console.WriteLine(ApplicationVersion.Current.DisplayName);
    Environment.ExitCode = batch
        ? RenderCommands.Batch(options.GetValueOrDefault("--incoming", "incoming"),
            options.GetValueOrDefault("--output", "output"), templatePath, worldWidth)
        : RenderCommands.Render(args[^1], templatePath, worldWidth);
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
}

static Dictionary<string, string> ReadExportOptions(string[] arguments, bool batch)
{
    if (!batch && arguments[^1].StartsWith('-'))
        throw new ArgumentException("Pass the image filename last. Run with --help for usage.");

    var end = batch ? arguments.Length : arguments.Length - 1;
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = batch ? 1 : 0; i < end; i += 2)
    {
        var option = arguments[i];
        if (option is not ("--template" or "--width") && !(batch && option is ("--incoming" or "--output")))
            throw new ArgumentException($"Unknown option '{option}'. Run with --help for usage.");
        if (i + 1 >= end || string.IsNullOrWhiteSpace(arguments[i + 1]) || arguments[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing value for {option}.");
        if (!options.TryAdd(option, arguments[i + 1]))
            throw new ArgumentException($"Specify {option} only once.");
    }

    return options;
}
