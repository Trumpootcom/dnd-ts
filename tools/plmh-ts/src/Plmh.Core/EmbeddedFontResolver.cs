using System.Buffers.Binary;
using PdfSharp.Fonts;

namespace Plmh.Core;

internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string LibreBaskerville = "Libre Baskerville";
    public const string LibreBaskervilleBold = "Libre Baskerville Bold";
    public const string CinzelExtraBold = "Cinzel ExtraBold";
    public const string GeorgiaBold = "Georgia Bold";

    private static readonly object RegistrationLock = new();
    private static readonly IReadOnlyDictionary<string, Lazy<byte[]>> Fonts =
        new Dictionary<string, Lazy<byte[]>>
        {
            ["Cinzel-ExtraBold"] = new(() => ReadFontBytes("Cinzel-ExtraBold")),
            ["LibreBaskerville-Regular"] = new(() => ReadFontBytes("LibreBaskerville-Regular")),
            ["LibreBaskerville-Bold"] = new(() => ReadFontBytes("LibreBaskerville-Bold")),
            ["Georgia-Bold"] = new(ReadGeorgiaBold)
        };

    public static bool IsLibreBaskerville(string familyName) =>
        familyName is LibreBaskerville or LibreBaskervilleBold;

    public static bool IsEmbedded(string familyName) =>
        IsLibreBaskerville(familyName) || familyName is CinzelExtraBold or GeorgiaBold;

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (isItalic)
            return null;
        if (string.Equals(familyName, LibreBaskerville, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(isBold ? "LibreBaskerville-Bold" : "LibreBaskerville-Regular");
        if (string.Equals(familyName, CinzelExtraBold, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo("Cinzel-ExtraBold");
        if (string.Equals(familyName, GeorgiaBold, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo("Georgia-Bold");
        return null;
    }

    public byte[]? GetFont(string faceName) => Fonts.GetValueOrDefault(faceName)?.Value;

    public static void Register()
    {
        lock (RegistrationLock)
        {
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
        }
    }

    private static byte[] ReadFontBytes(string faceName)
    {
        using var stream = typeof(EmbeddedFontResolver).Assembly
            .GetManifestResourceStream($"Plmh.Core.Fonts.{faceName}.ttf")
            ?? throw new InvalidOperationException($"Embedded font '{faceName}' was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[] ReadGeorgiaBold()
    {
        var paths = OperatingSystem.IsWindows()
            ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "georgiab.ttf") }
            : new[]
            {
                "/System/Library/Fonts/Supplemental/Georgia Bold.ttf",
                "/Library/Fonts/Georgia Bold.ttf",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Fonts", "Georgia Bold.ttf")
            };
        var path = paths.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Georgia Bold requires an installed Georgia Bold TrueType font.");
        var bytes = File.ReadAllBytes(path);
        if (!AllowsSubsetEmbedding(bytes))
            throw new InvalidOperationException("The installed Georgia Bold face does not permit editable PDF subset embedding.");
        return bytes;
    }

    private static bool AllowsSubsetEmbedding(ReadOnlySpan<byte> font)
    {
        if (font.Length < 12 || BinaryPrimitives.ReadUInt32BigEndian(font) != 0x00010000)
            return false;
        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(font[4..]);
        if (tableCount > (font.Length - 12) / 16)
            return false;
        for (var index = 0; index < tableCount; index++)
        {
            var table = font.Slice(12 + index * 16, 16);
            if (!table[..4].SequenceEqual("OS/2"u8))
                continue;
            var offset = BinaryPrimitives.ReadUInt32BigEndian(table[8..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(table[12..]);
            if (length < 10 || offset > font.Length - 10)
                return false;
            var flags = BinaryPrimitives.ReadUInt16BigEndian(font[((int)offset + 8)..]);
            const int editableEmbedding = 0x0008;
            const int embeddingLevel = 0x000F;
            const int noSubsettingOrBitmapOnly = 0x0300;
            return (flags & noSubsettingOrBitmapOnly) == 0
                && ((flags & embeddingLevel) == 0 || (flags & editableEmbedding) != 0);
        }
        return false;
    }
}
