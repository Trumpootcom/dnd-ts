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
    string Type)
{
    /// <summary>Parses a portrait path using encounter_token_name_size_type.ext.</summary>
    public static PortraitFileName Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fileName = System.IO.Path.GetFileName(path);
        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var fields = stem.Split('_');
        if (fields.Length < 5)
            throw InvalidFormat(fileName, "expected encounter_token_name_size_type.ext with a nonempty name");

        var encounterId = fields[0].Trim();
        var tokenId = fields[1].Trim();
        var nameParts = fields[2..^2];
        var name = string.Join('_', nameParts).Trim();
        var size = fields[^2].Trim();
        var type = fields[^1].Trim();

        if (encounterId.Length == 0 || tokenId.Length == 0 || name.Length == 0)
            throw InvalidFormat(fileName, "encounter ID, token ID, and name must not be empty");
        return new PortraitFileName(
            System.IO.Path.GetFullPath(path),
            encounterId,
            PortraitTextFormatter.FormatTokenId(tokenId),
            PortraitTextFormatter.FormatName(name),
            TitleCase(size),
            TitleCase(type));
    }

    private static string TitleCase(string value) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());

    private static FormatException InvalidFormat(string fileName, string reason) =>
        new($"Invalid portrait filename '{fileName}': {reason}. Expected format: encounter_token_name_size_type.ext.");
}
