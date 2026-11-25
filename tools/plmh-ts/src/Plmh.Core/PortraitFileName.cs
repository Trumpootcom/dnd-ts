using System.Globalization;
using System.IO;

namespace Plmh.Core;

/// <summary>Metadata parsed from a portrait filename.</summary>
public sealed record PortraitFileName(
    string Path,
    string EncounterId,
    string TokenId,
    string Name,
    string Size,
    string Type,
    double? HeightInches = null)
{
    public bool IncludeInLineup { get; init; } = true;
    public string? LineupTokenId { get; init; }

    /// <summary>Parses a portrait path using underscore-delimited metadata fields.</summary>
    public static PortraitFileName Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fileName = System.IO.Path.GetFileName(path);
        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var fields = stem.Split('_');
        if (fields.Length < 4)
            throw InvalidFormat(fileName, "expected encounter_token_name_size_type.ext with a nonempty name");

        var encounterId = fields[0].Trim();
        var tokenId = fields[1].Trim();
        var nameParts = fields.Length == 4 ? fields[2..3] : fields[2..^2];
        var name = string.Join('_', nameParts).Trim();
        var size = fields.Length == 4 ? string.Empty : fields[^2].Trim();
        var type = fields[^1].Trim();

        // Also accept filenames where the size and multiword type are in one
        // underscore-delimited field, e.g. "..._medium Chondathan male.png".
        if (fields.Length == 4)
        {
            var metadata = fields[^1].Trim();
            var separator = metadata.IndexOf(' ');
            if (separator <= 0 || separator == metadata.Length - 1)
                throw InvalidFormat(fileName, "expected size and type after the name");
            size = metadata[..separator].Trim();
            type = metadata[(separator + 1)..].Trim();
        }

        var heightInches = ParseHeight(ref size, fileName);

        if (encounterId.Length == 0 || tokenId.Length == 0 || name.Length == 0)
            throw InvalidFormat(fileName, "encounter ID, token ID, and name must not be empty");
        return new PortraitFileName(
            System.IO.Path.GetFullPath(path),
            encounterId,
            PortraitTextFormatter.FormatTokenId(tokenId),
            PortraitTextFormatter.FormatName(name),
            TitleCase(size),
            TitleCase(type),
            heightInches);
    }

    public static PortraitFileName FromMetadata(string path, string encounterId, string tokenId,
        string name, string size, string type, string? height) =>
        new(path, encounterId, PortraitTextFormatter.FormatTokenId(tokenId),
            PortraitTextFormatter.FormatName(name), TitleCase(size), TitleCase(type),
            height is null ? null : ParseHeightValue(height, System.IO.Path.GetFileName(path)));

    private static double? ParseHeight(ref string size, string fileName)
    {
        var open = size.LastIndexOf('(');
        if (open < 0) return null;
        if (size[^1] != ')')
            throw InvalidFormat(fileName, "height must use the feet-inches format, such as medium(5-0)");

        var heightInches = ParseHeightValue(size[(open + 1)..^1], fileName);
        size = size[..open].Trim();
        if (size.Length == 0)
            throw InvalidFormat(fileName, "size must not be empty");
        return heightInches;
    }

    private static double ParseHeightValue(string height, string fileName)
    {
        var parts = height.Split('-');
        int feet;
        int inches;
        if (parts.Length == 1
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out feet))
        {
            inches = 0;
        }
        else if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out feet)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out inches))
        {
            // Explicit feet-inches form.
        }
        else
        {
            throw InvalidFormat(fileName, "height must use feet or feet-inches format, such as medium(5) or medium(5-0)");
        }

        if (feet < 0 || inches is < 0 or > 11 || (feet == 0 && inches == 0))
            throw InvalidFormat(fileName, "height must use feet-inches format with 0 to 11 inches, such as medium(5-0)");

        return feet * 12d + inches;
    }

    private static string TitleCase(string value) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());

    private static FormatException InvalidFormat(string fileName, string reason) =>
        new($"Invalid portrait filename '{fileName}': {reason}. Expected format: encounter_token_name_size_type.ext.");
}
