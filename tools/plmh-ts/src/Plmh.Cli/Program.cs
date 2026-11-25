using Plmh.Core;

var applicationVersion = ApplicationVersion.Current;
var displayName = applicationVersion.DisplayName;

if (args is ["--version"] or ["-v"])
{
    Console.WriteLine(displayName);
    Console.WriteLine($"Built: {applicationVersion.BuildTimestampUtc?.ToString("u") ?? "unknown"}");
    return;
}

if (args is ["--help"] or ["-h"] or ["help"])
{
    Console.WriteLine($"""
        {displayName}
        Portrait Layout Manager Headless Tool Suite

        macOS launcher: sh bin/process_portraits.sh [command or image]

        Usage: Plmh.Cli [batch] [--template <name-or-path>] [--dpi <value>]
               Plmh.Cli [--template <name-or-path>] [--dpi <value>] <portrait-path>

          batch                 Process images in <workspace>/in_portraits.
          <portrait-path>       Create a PDF beside one portrait image.
          --template <value>    Select a template; T000 is the default.
          --dpi <value>         Target image DPI (default {PortraitPdfExporter.DefaultImageDpi}). Use 0 to disable downsampling.
          --help, -h            Show this help.
          --version, -v         Show the version and UTC build time.
          --templates           List bundled templates with paper settings and default image DPI.

        Batch groups portraits by encounter and writes one PDF per encounter to <workspace>/out.
        Source portraits are retained.
        """);
    return;
}

try
{
    if (args is ["--templates"])
    {
        PortraitCommands.ListTemplates();
        return;
    }

    Console.WriteLine(displayName);
    if (args.Length == 0)
    {
        Environment.ExitCode = PortraitCommands.Batch(null);
        return;
    }

    var batch = args[0] == "batch";
    string? template = null;
    int? dpi = null;
    var positional = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--template")
        {
            if (template is not null || i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                throw new ArgumentException("Specify --template once with a value.");
            template = args[++i];
            continue;
        }
        if (args[i] == "--dpi")
        {
            if (dpi is not null || i + 1 >= args.Length || !int.TryParse(args[++i], out var value) || value < 0)
                throw new ArgumentException("Specify --dpi once with a nonnegative whole number.");
            dpi = value;
            continue;
        }
        if (batch && i == 0)
            continue;
        if (args[i].StartsWith("-", StringComparison.Ordinal))
            throw new ArgumentException($"Unknown option '{args[i]}'. Run with --help for usage.");
        positional.Add(args[i]);
    }

    if (batch)
    {
        if (positional.Count != 0)
            throw new ArgumentException("Batch mode does not take a portrait path.");
        Environment.ExitCode = PortraitCommands.Batch(template, dpi ?? PortraitPdfExporter.DefaultImageDpi);
    }
    else
    {
        if (positional.Count != 1)
            throw new ArgumentException("Pass one portrait image path. Run with --help for usage.");
        Environment.ExitCode = PortraitCommands.Single(positional[0], template, dpi ?? PortraitPdfExporter.DefaultImageDpi);
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
}
