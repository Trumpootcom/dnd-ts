namespace Plmh.Core;

/// <summary>A rectangle in background-canvas pixel coordinates.</summary>
public readonly record struct PixelRegion(double Left, double Top, double Width, double Height);

/// <summary>A text field rectangle in PDF page coordinates.</summary>
public readonly record struct TextPlacement(double X, double Baseline, double FontSize, double HorizontalScale);

/// <summary>Calculates centered text layout for a template text region.</summary>
public static class TextFieldLayout
{
    /// <summary>
    /// Converts a source-image region into page coordinates, fits font height to
    /// the region height, and horizontally scales the text to fit its width.
    /// </summary>
    public static TextPlacement Fit(string text, PixelRegion region, double pageX, double pageY,
        double pixelsToPoints, bool bold = false, double verticalPadding = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateRegion(region.Left, region.Top, region.Width, region.Height);
        if (!double.IsFinite(pageX) || !double.IsFinite(pageY))
            throw new ArgumentOutOfRangeException(nameof(pageX), "Page coordinates must be finite.");
        if (!double.IsFinite(pixelsToPoints) || pixelsToPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelsToPoints), "Scale must be positive and finite.");
        if (!double.IsFinite(verticalPadding) || verticalPadding < 0 || verticalPadding * 2 >= region.Height)
            throw new ArgumentOutOfRangeException(nameof(verticalPadding), "Padding must be nonnegative and less than half the region height.");

        var safeText = PdfTextCanvas.Normalize(text);
        var height = (region.Height - verticalPadding * 2) * pixelsToPoints;
        var width = region.Width * pixelsToPoints;
        var fontSize = height * 1000 / PdfTextCanvas.FontHeight(bold);
        var measuredWidth = PdfTextCanvas.MeasureWidth(safeText, fontSize, bold);
        var horizontalScale = measuredWidth > width ? width / measuredWidth : 1;
        var renderedWidth = measuredWidth * horizontalScale;
        var renderedHeight = PdfTextCanvas.FontHeight(bold) * fontSize / 1000;

        // PDF text uses a baseline. Center the Helvetica glyph box in the target area.
        var x = pageX + region.Left * pixelsToPoints + (width - renderedWidth) / 2;
        var baseline = pageY + region.Top * pixelsToPoints
            + (region.Height * pixelsToPoints - renderedHeight) / 2
            + renderedHeight * (1 - PdfTextCanvas.DescentFraction(bold));
        return new TextPlacement(x, baseline, fontSize, horizontalScale);
    }

    private static void ValidateRegion(double left, double top, double width, double height)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top) || left < 0 || top < 0)
            throw new ArgumentOutOfRangeException(nameof(left), "Region origin must be nonnegative and finite.");
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Region size must be positive and finite.");
    }
}
