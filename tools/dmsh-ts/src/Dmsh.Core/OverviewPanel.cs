using System.Globalization;
using PdfSharp.Drawing;

namespace Dmsh.Core;

internal static class OverviewPanel
{
    internal static readonly XColor Ink = XColor.FromArgb(26, 36, 46);
    // Parchment at 88% opacity over the white overview page.
    private static readonly XColor Paper = XColor.FromArgb(246, 237, 217);

    public static void Draw(XGraphics graphics, PdfLabelText text, MapTemplate template, SheetPlan plan,
        double pageHeight, double panelWidth, double scale, string imageName)
    {
        var margin = plan.Margin;
        var height = pageHeight - 2 * margin;
        var section = height / 3;
        var titleColumn = 30 * scale;
        var contentX = margin + titleColumn;
        var bottom = pageHeight - margin;
        graphics.DrawRectangle(new XPen(Ink, 0.5 * scale), new XSolidBrush(Paper),
            margin, margin, panelWidth, height);
        var divider = new XPen(XColor.FromArgb(84, Ink.R, Ink.G, Ink.B), 0.5 * scale);
        graphics.DrawLine(divider, contentX, margin, contentX, bottom);
        for (var i = 1; i < 3; i++)
            graphics.DrawLine(divider, contentX, margin + section * i, margin + panelWidth, margin + section * i);

        var title = PdfLabelText.Normalize(imageName.Trim());
        var titleSize = 17 * scale;
        var limit = height - 16 * scale;
        var heading = title;
        for (var length = title.Length; length > 0 && PdfLabelText.Measure(heading, titleSize, bold: true) > limit;)
            heading = title[..--length] + "...";
        titleSize *= Math.Min(1, limit / Math.Max(1, PdfLabelText.Measure(heading, titleSize, bold: true)));
        if (titleSize > 0)
            Centered(heading, true, titleSize, margin + (titleColumn + PdfLabelText.Height(titleSize, bold: true)) / 2, bottom, height);

        var headingSize = 14 * scale;
        var headingHeight = PdfLabelText.Height(headingSize, bold: true);
        var legendBottom = margin + section;
        Centered("MAP AND SHEET LEGEND", true, headingSize, contentX + headingHeight + 3 * scale, legendBottom, section);
        var feetUnit = plan.GridSpacing / 5;
        var grid = template.Grid;
        var legend = new[]
        {
            $"{template.Page.Size.ToUpperInvariant()} {template.Page.Orientation} | {template.Page.Rows} rows x {template.Page.Columns} columns",
            $"Paper: {Dims(plan.CanvasWidth, plan.CanvasHeight, 72)} in | {Dims(plan.CanvasWidth, plan.CanvasHeight, feetUnit)} ft",
            $"Image: {Dims(plan.MapWidth, plan.MapHeight, 72)} in | {Dims(plan.MapWidth, plan.MapHeight, feetUnit)} ft",
            $"Grid: {grid.Style}, {grid.Color}, {grid.LineStyle} | {Format(grid.LineWidthPoints)} pt | opacity {Format(grid.Opacity)}"
                + (grid.Iso ? $" | ISO {Format(grid.IsoAngleDegrees ?? 90)} deg" : ""),
            $"Rotation: {Format(grid.RotationDegrees)} deg | Offsets: {Format(grid.OffsetX)}, {Format(grid.OffsetY)} cells"
        };
        var dataX = contentX + 2 * headingHeight;
        var legendSize = 12 * scale;
        var legendHeight = PdfLabelText.Height(legendSize, bold: false);
        foreach (var line in legend)
        {
            text.Draw(graphics, line, legendSize, false, Ink, dataX + legendHeight, legendBottom - 5 * scale, rotated: true);
            dataX += legendHeight * 1.5;
        }

        Centered("SCALING", true, headingSize, contentX + headingHeight + 3 * scale, bottom, section);
        dataX = contentX + headingHeight * 2.5;
        ScalingRow($"5 feet = {Fixed(plan.GridSpacing / 72)} inches", 18 * scale);
        ScalingRow($"{Fixed(plan.MapWidth / feetUnit)} feet x {Fixed(plan.MapHeight / feetUnit)} feet", 22 * scale);

        void ScalingRow(string line, double size)
        {
            var fontHeight = PdfLabelText.Height(size, bold: false);
            Centered(line, false, size, dataX + fontHeight, bottom, section);
            dataX += fontHeight * 2;
        }

        void Centered(string label, bool bold, double size, double x, double sectionBottom, double sectionHeight) =>
            text.Draw(graphics, label, size, bold, Ink, x,
                sectionBottom - (sectionHeight - PdfLabelText.Measure(label, size, bold)) / 2, rotated: true);
    }

    private static string Fixed(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Dims(double width, double height, double unit) => $"{Fixed(width / unit)} x {Fixed(height / unit)}";
}
