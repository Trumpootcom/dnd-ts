using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Plmh.Core;

internal sealed record AdventureFile(string Title, string? Date, List<AdventureMap> Maps,
    List<PortraitFileName> Portraits, Dictionary<string, (BigInteger Number, string Name)> Encounters)
{
    public const string Extension = ".adventure.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static bool Exists(string path)
    {
        try
        {
            File.GetAttributes(path);
            return true;
        }
        catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static AdventureFile Load(string path)
    {
        var document = Read<Document>(path);
        var directory = Path.GetDirectoryName(path)!;
        var errors = new List<string>();
        var maps = new List<AdventureMap>();
        var portraits = new List<PortraitFileName>();
        var encounters = new Dictionary<string, (BigInteger Number, string Name)>(StringComparer.OrdinalIgnoreCase);
        var mapOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var title = document.Title?.Trim() ?? "";
        if (!IsFolderName(title))
            errors.Add("The adventure title must be usable in a folder name.");
        if (document.Date is { } date && !IsDate(date))
            errors.Add("Use a valid adventure date in YYMMDD format.");

        foreach (var encounter in document.Encounters ?? [])
        {
            var id = encounter?.Id?.Trim() ?? "";
            if (!Regex.IsMatch(id, @"\AENC[1-9][0-9]*\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                errors.Add($"Invalid encounter ID '{id}'. Use an ID such as ENC1.");
                continue;
            }
            id = id.ToUpperInvariant();
            var number = BigInteger.Parse(id.AsSpan(3), CultureInfo.InvariantCulture);
            var encounterName = encounter!.Name?.Trim() ?? title;
            if (!IsFolderName(encounterName))
                errors.Add($"The name for encounter '{id}' must be usable in a filename.");
            if (!encounters.TryAdd(id, (number, encounterName)))
                errors.Add($"Duplicate encounter ID '{id}'.");

            foreach (var map in encounter!.Maps ?? [])
            {
                var context = $"{id} map '{map?.Location ?? map?.File}'";
                try
                {
                    var imageDirectory = directory;
                    var file = map?.File;
                    var widthFeet = map?.WidthFeet;
                    if (map?.Location is { } reference)
                    {
                        if (file is not null || widthFeet is not null)
                            throw new ArgumentException("A location reference takes its file and widthFeet from the library entry.");
                        var path = ResolvePath(directory, reference);
                        var libraryMap = Read<LibraryMap>(path);
                        imageDirectory = Path.GetDirectoryName(path)!;
                        file = libraryMap.File;
                        widthFeet = libraryMap.WidthFeet
                            ?? throw new ArgumentException("A library map must specify widthFeet.");
                    }

                    var image = ImagePath(imageDirectory, file, RenderCommands.IsImage);
                    var name = map?.Name?.Trim() ?? Path.GetFileNameWithoutExtension(image);
                    if (string.IsNullOrWhiteSpace(name))
                        throw new ArgumentException("Map name must not be empty.");
                    if (widthFeet is { } width && (!double.IsFinite(width) || width <= 0))
                        throw new ArgumentException("widthFeet must be a positive, finite number.");
                    var pdfName = $"{id} - {Path.GetFileNameWithoutExtension(image)}.pdf";
                    if (!IsFolderName(pdfName) || !mapOutputs.Add(pdfName))
                        throw new ArgumentException($"Invalid or duplicate output name '{pdfName}'.");
                    maps.Add(new AdventureMap(id, image, name, pdfName, widthFeet));
                }
                catch (Exception error) when (IsInputError(error))
                {
                    errors.Add($"{context}: {error.Message}");
                }
            }

            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var portrait in encounter.Portraits ?? [])
            {
                var token = portrait?.TokenId?.Trim() ?? "";
                var context = $"{id} portrait '{token}'";
                try
                {
                    if (token.Length == 0)
                        throw new ArgumentException("Token ID must be nonempty.");
                    var quantity = portrait!.Qty ?? 1;
                    if (quantity < 1)
                        throw new ArgumentException("qty must be a positive integer.");

                    var resolved = ResolvePortrait(directory, id, token, portrait);
                    for (var index = 0; index < quantity; index++)
                    {
                        var tokenId = portrait.Qty.HasValue
                            ? resolved.TokenId + (index + 1).ToString(CultureInfo.InvariantCulture)
                            : resolved.TokenId;
                        if (!tokens.Add(tokenId))
                        {
                            errors.Add($"{context}: Duplicate token ID '{tokenId}' in this encounter.");
                            continue;
                        }
                        portraits.Add(resolved with
                        {
                            TokenId = tokenId,
                            IncludeInLineup = index == 0,
                            LineupTokenId = portrait.Qty.HasValue ? resolved.TokenId : null
                        });
                    }
                }
                catch (Exception error) when (IsInputError(error))
                {
                    errors.Add($"{context}: {error.Message}");
                }
            }
        }

        if (maps.Count + portraits.Count == 0 && errors.Count == 0)
            errors.Add("The adventure must include at least one map or portrait.");
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid adventure file '{path}':{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors));
        return new AdventureFile(title, document.Date, maps, portraits, encounters);
    }

    private static PortraitFileName ResolvePortrait(string directory, string id, string token, Portrait portrait)
    {
        Creature? creature = null;
        string? creatureDirectory = null;
        if (portrait.Creature is { } reference)
        {
            var path = ResolvePath(directory, reference);
            creature = Read<Creature>(path);
            creatureDirectory = Path.GetDirectoryName(path);
        }

        var image = portrait.File is { } file
            ? ImagePath(directory, file, PortraitCommands.IsImage)
            : ImagePath(creatureDirectory ?? directory, creature?.Portrait, PortraitCommands.IsImage);
        var name = portrait.Name ?? creature?.Name ?? Path.GetFileNameWithoutExtension(image);
        var size = portrait.Size ?? creature?.Size;
        var type = portrait.Type ?? creature?.Type;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(size) || string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Portrait name, size and type must be nonempty.");
        return PortraitFileName.FromMetadata(image, id, token, name.Trim(), size.Trim(), type.Trim(),
            portrait.Height ?? creature?.Height);
    }

    private static T Read<T>(string path) where T : class
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new JsonException("Expected a JSON object.");
        }
        catch (JsonException error)
        {
            throw new JsonException($"Invalid JSON in '{path}': {error.Message}", error);
        }
    }

    private static string ImagePath(string directory, string? file, Func<string, bool> isImage)
    {
        var path = ResolvePath(directory, file);
        if (!isImage(path))
            throw new ArgumentException($"Unsupported image format: '{path}'.");
        using var image = File.OpenRead(path);
        return path;
    }

    private static string ResolvePath(string directory, string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
            throw new ArgumentException("An image, map, or creature file path is required.");
        return Path.GetFullPath(file, directory);
    }

    private static bool IsInputError(Exception error) =>
        error is ArgumentException or IOException or UnauthorizedAccessException or JsonException or NotSupportedException;

    public static bool IsFolderName(string value) => value.Length > 0 && !value.EndsWith('.') &&
        !value.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character));

    public static bool IsDate(string date) => Regex.IsMatch(date, @"\A[0-9]{6}\z") &&
        DateOnly.TryParseExact("20" + date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private sealed record Document(string? Title, string? Date, List<Encounter?>? Encounters);
    private sealed record Encounter(string? Id, string? Name, List<EncounterMap?>? Maps, List<Portrait?>? Portraits);
    private sealed record EncounterMap(string? File, string? Name, double? WidthFeet, string? Location);
    private sealed record LibraryMap(string? File, double? WidthFeet);
    private sealed record Portrait(string? File, string? Creature, string? TokenId, string? Name,
        string? Size, string? Type, string? Height, int? Qty);
    private sealed record Creature(string? Name, string? Size, string? Type, string? Portrait, string? Height);
}

internal sealed record AdventureMap(string EncounterId, string Image, string Name, string PdfName, double? WidthFeet);
