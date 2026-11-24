using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Plmh.Core;

internal sealed class PdfTextCanvas(PdfPage page)
{
    private const string Marker = "PLMH_TEXT_";
    private static readonly FontMetrics Metrics = LoadMetrics();
    private readonly List<string> commands = [];

    private sealed record Face(int[] Widths, Dictionary<int, int> Kern, int Height);
    private sealed record FontMetrics(Dictionary<int, byte> Encoding, Face[] Faces);

    public static string Normalize(string text)
    {
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
            result.Append(rune.Value is 9 or 10 or 13 ? " " :
                Metrics.Encoding.ContainsKey(rune.Value) ? rune.ToString() : "?");
        return result.ToString();
    }

    public static double Measure(string text, double size, bool bold)
    {
        var face = Metrics.Faces[bold ? 1 : 0];
        var bytes = Encode(text);
        var width = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            width += face.Widths[bytes[i]];
            if (i + 1 < bytes.Length)
                width += face.Kern.GetValueOrDefault(bytes[i] * 256 + bytes[i + 1]);
        }
        return width * size / 1000;
    }

    public static double Height(double size, bool bold) =>
        Metrics.Faces[bold ? 1 : 0].Height * size / 1000;

    public static double MeasureWidth(string text, double size, bool bold) => Measure(text, size, bold);
    public static double FontHeight(bool bold) => Metrics.Faces[bold ? 1 : 0].Height;
    public static double DescentFraction(bool bold) => bold ? 0.21 : 0.207;

    // Label drawing uses page coordinates. Markers retain layer order in
    // PDFsharp's graphics stream without relying on platform font resolution.
    public void Draw(XGraphics graphics, string text, double size, bool bold, XColor color,
        double x, double baseline, bool rotated = false, double widthScale = 1)
    {
        if (text.Length == 0 || size <= 0)
            return;
        var matrix = rotated ? "0 1 -1 0" : "1 0 0 1";
        var font = bold ? "/PlmhHelveticaBold" : "/PlmhHelvetica";
        var hex = Convert.ToHexString(Encode(text));
        graphics.WriteComment(Marker + commands.Count.ToString(CultureInfo.InvariantCulture));
        commands.Add(FormattableString.Invariant(
            $"q {color.R / 255d:0.######} {color.G / 255d:0.######} {color.B / 255d:0.######} rg BT {font} {size:0.######} Tf {widthScale * 100:0.######} Tz {matrix} {x:0.######} {page.Height.Point - baseline:0.######} Tm <{hex}> Tj ET Q"));
    }

    public void DrawSmallCaps(XGraphics graphics, string text, double size, bool bold, XColor color,
        double x, double baseline, double maxWidth, double smallScale = 0.72)
    {
        if (text.Length == 0 || size <= 0 || maxWidth <= 0) return;

        var runs = new List<(string Text, double Size)>();
        var current = new StringBuilder();
        var currentSize = size;
        var firstLetterInWord = true;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                Append(character, size);
                firstLetterInWord = true;
                continue;
            }

            var isLetter = char.IsLetter(character);
            var glyphSize = isLetter && !firstLetterInWord ? size * smallScale : size;
            Append(char.ToUpperInvariant(character), glyphSize);
            if (isLetter) firstLetterInWord = false;
        }
        Flush();

        var measuredWidth = runs.Sum(run => Measure(run.Text, run.Size, bold));
        var widthScale = Math.Min(1, maxWidth / measuredWidth);
        var drawX = x + (maxWidth - measuredWidth * widthScale) / 2;
        foreach (var run in runs)
        {
            Draw(graphics, run.Text, run.Size, bold, color, drawX, baseline, widthScale: widthScale);
            drawX += Measure(run.Text, run.Size, bold) * widthScale;
        }

        void Append(char character, double glyphSize)
        {
            if (current.Length > 0 && currentSize != glyphSize) Flush();
            if (current.Length == 0) currentSize = glyphSize;
            current.Append(character);
        }

        void Flush()
        {
            if (current.Length == 0) return;
            runs.Add((current.ToString(), currentSize));
            current.Clear();
        }
    }

    public void Finish()
    {
        if (commands.Count == 0)
            return;
        var resources = GetOrAddDictionary(page, "/Resources");
        var fonts = GetOrAddDictionary(resources, "/Font");
        AddFont(fonts, "/PlmhHelvetica", "/Helvetica");
        AddFont(fonts, "/PlmhHelveticaBold", "/Helvetica-Bold");

        foreach (var content in page.Contents)
        {
            var stream = content.Stream!;
            var source = Encoding.ASCII.GetString(stream.Value);
            var replaced = Regex.Replace(source, @"(?m)^% PLMH_TEXT_([0-9]+)\r?$",
                match => commands[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)]);
            stream.Value = Encoding.ASCII.GetBytes(replaced);
        }
    }

    private static PdfDictionary GetOrAddDictionary(PdfDictionary parent, string key)
    {
        if (parent.Elements.GetDictionary(key) is { } existing)
            return existing;
        var dictionary = new PdfDictionary(parent.Owner);
        parent.Elements[key] = dictionary;
        return dictionary;
    }

    private static void AddFont(PdfDictionary fonts, string key, string baseFont)
    {
        var font = new PdfDictionary(fonts.Owner);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", baseFont);
        font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        fonts.Elements[key] = font;
    }

    private static byte[] Encode(string text) => Normalize(text).EnumerateRunes()
        .Select(rune => Metrics.Encoding[rune.Value]).ToArray();

    private static FontMetrics LoadMetrics()
    {
        using var stream = typeof(PdfTextCanvas).Assembly.GetManifestResourceStream("Plmh.Core.HelveticaMetrics.json")!;
        return JsonSerializer.Deserialize<FontMetrics>(stream)!;
    }
}
