using System.Globalization;

namespace Plmh.Core;

/// <summary>Applies the display capitalization rules for portrait fields.</summary>
public static class PortraitTextFormatter
{
    /// <summary>Formats a token identifier in uppercase using invariant rules.</summary>
    public static string FormatTokenId(string tokenId) =>
        NormalizeWhitespace(tokenId).ToUpperInvariant();

    /// <summary>Formats a name in title case while preserving word separators.</summary>
    public static string FormatName(string name) => FormatTitleCase(name);

    /// <summary>Formats creature size and type in title case.</summary>
    public static string FormatSizeAndType(string size, string type) =>
        $"{FormatTitleCase(size)} {FormatTitleCase(type)}";

    private static string FormatTitleCase(string value)
    {
        var normalized = NormalizeWhitespace(value);
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private static string NormalizeWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
