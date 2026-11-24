using PdfSharp.Drawing;

namespace Dmsh.Core;

internal static class GridLinePattern
{
    // Alternating stroke and gap lengths in 64ths of a cell edge.
    private static readonly Dictionary<string, int[]> Patterns = new(StringComparer.Ordinal)
    {
        ["dash-1"] = [16, 32, 16],
        ["dash-2"] = [8, 16, 16, 16, 8],
        ["dash-3"] = [5, 11, 10, 12, 10, 11, 5],
        ["dash-4"] = [4, 8, 8, 8, 8, 8, 8, 8, 4],
        ["dash-5"] = [3, 6, 6, 7, 6, 8, 6, 7, 6, 6, 3],
        ["dash-6"] = [3, 5, 5, 6, 5, 6, 5, 6, 5, 6, 5, 5, 2],
        ["dash-8"] = [2, .. Enumerable.Repeat(4, 15), 2],
        ["dash-10"] = [2, 3, 3, 3, 3, 3, 3, 4, 3, 4, 3, 4, 3, 4, 3, 3, 3, 3, 3, 3, 1],
        ["dash-16"] = [1, .. Enumerable.Repeat(2, 31), 1],
        ["corners"] = [8, 48, 8]
    };

    public static string Normalize(string style) => style switch
    {
        "line" => "solid",
        "dash32" => "dash-1",
        "dash16" => "dash-2",
        "dash12" => "dash-3",
        "dash8" or "dash" => "dash-4",
        "dash4" => "dash-8",
        "dash2" => "dash-16",
        "corner" => "corners",
        _ => style
    };

    public static void Apply(XPen pen, string style, double edgeLength, double startIndex, double viewScale)
    {
        if (style == "solid")
            return;

        double[] dashes;
        double phase;
        if (style is "dash1" or "dash3")
        {
            // Legacy patterns use fixed PDF points, even in the overview.
            dashes = style == "dash1" ? [2, 2] : [8, 4];
            var period = dashes[0] + dashes[1];
            phase = ((startIndex * edgeLength * viewScale % period) + period) % period;
            var scaledPenWidth = pen.Width * viewScale;
            pen.DashPattern = dashes.Select(length => length / scaledPenWidth).ToArray();
            pen.DashOffset = phase / scaledPenWidth;
            return;
        }

        if (!Patterns.TryGetValue(style, out var pattern))
            throw new ArgumentException($"Unsupported grid line style: {style}");

        var unit = edgeLength / 64;
        // Combine the adjacent end strokes at the next intersection.
        dashes = new double[pattern.Length - 1];
        dashes[0] = (pattern[0] + pattern[^1]) * unit;
        for (var index = 1; index < dashes.Length; index++)
            dashes[index] = pattern[index] * unit;
        phase = pattern[^1] * unit;
        // PDFsharp multiplies dash lengths and phase by the pen width.
        pen.DashPattern = dashes.Select(length => length / pen.Width).ToArray();
        pen.DashOffset = phase / pen.Width;
    }
}
