using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Dmsh.Core;

public static class MapPdfExporter
{
    public static void Export(MapTemplate template, string imagePath, Stream destination)
    {
        using var jpeg = WorkingImage.CreateJpeg(imagePath);
        using var image = XImage.FromStream(jpeg);
        var plan = SheetPlan.Create(template, image.PixelWidth, image.PixelHeight);
        var grid = template.Grid;
        var hexEdges = grid.Style == "hex" && grid.LineStyle != "none" && grid.Opacity > 0
            && grid.Color is not ("diff" or "contrast") ? HexGrid.Build(grid, plan) : null;
        using var document = new PdfDocument();
        // PDFsharp 6.2.4 emitted Flate streams without a complete zlib
        // trailer in an Acrobat-rejected export.
        document.Options.CompressContentStreams = false;
        document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;
        document.Info.Title = template.Title;

        if (plan.HasOverview)
        {
            var pageWidth = Math.Max(plan.PageWidth, plan.PageHeight);
            var pageHeight = Math.Min(plan.PageWidth, plan.PageHeight);
            var page = AddPage(document, pageWidth, pageHeight);
            using var graphics = XGraphics.FromPdfPage(page);
            var text = new PdfLabelText(page);
            var tableScale = (pageHeight - 2 * plan.Margin) / 756;
            var panelWidth = 144 * tableScale;
            var overviewWidth = pageWidth - 3 * plan.Margin - panelWidth;
            if (overviewWidth <= 0)
                throw new ArgumentException("The page margins leave no room for the overview map.");
            var scale = overviewWidth / plan.CanvasWidth;
            var left = 2 * plan.Margin + panelWidth;
            var top = (pageHeight - plan.CanvasHeight * scale) / 2;
            DrawMapImage(graphics, image, plan, left + plan.MapLeft * scale, top + plan.MapTop * scale, scale);

            var overviewState = graphics.Save();
            graphics.TranslateTransform(left, top);
            graphics.ScaleTransform(scale, scale);
            var gridState = graphics.Save();
            var canvasClip = new XGraphicsPath();
            canvasClip.AddRectangle(0, 0, plan.CanvasWidth, plan.CanvasHeight);
            graphics.IntersectClip(canvasClip);
            SheetArtwork.DrawOverviewGrid(graphics, grid, plan, scale, hexEdges);
            graphics.Restore(gridState);

            var outline = new XPen(OverviewPanel.Ink, 0.5 / scale);
            graphics.DrawRectangle(outline, 0, 0, plan.CanvasWidth, plan.CanvasHeight);
            graphics.Restore(overviewState);
            if (plan.HasCoordinateBorder)
                SheetArtwork.DrawOverviewCoordinates(graphics, text, template.Grid, plan, left, top, scale);
            foreach (var sheet in plan.Sheets)
                SheetArtwork.DrawBadge(graphics, text, sheet,
                    new XRect(left + sheet.Left * scale, top + sheet.Top * scale,
                        plan.PrintableWidth * scale, plan.PrintableHeight * scale), Math.Min(1, scale));
            OverviewPanel.Draw(graphics, text, template, plan, pageHeight, panelWidth, tableScale,
                Path.GetFileNameWithoutExtension(imagePath));
            graphics.Dispose();
            text.Finish();
        }

        foreach (var sheet in plan.Sheets)
        {
            var page = AddPage(document, plan.PageWidth, plan.PageHeight);
            using var graphics = XGraphics.FromPdfPage(page);
            var text = new PdfLabelText(page);
            var clip = new XGraphicsPath();
            var state = graphics.Save();
            clip.AddRectangle(plan.Margin, plan.Margin, plan.PrintableWidth, plan.PrintableHeight);
            graphics.IntersectClip(clip);
            DrawMapImage(graphics, image, plan, plan.Margin + plan.MapLeft - sheet.Left,
                plan.Margin + plan.MapTop - sheet.Top);
            SheetArtwork.DrawGrid(graphics, grid, plan, sheet, hexEdges);
            graphics.Restore(state);
            if (plan.HasCoordinateBorder)
                SheetArtwork.DrawCoordinates(graphics, text, template.Grid, plan, sheet);
            SheetArtwork.DrawBadge(graphics, text, sheet,
                new XRect(plan.Margin, plan.Margin, plan.PrintableWidth, plan.PrintableHeight));
            graphics.Dispose();
            text.Finish();
        }

        document.Save(destination, false);
    }

    private static void DrawMapImage(XGraphics graphics, XImage image, SheetPlan plan, double left, double top,
        double scale = 1)
    {
        var state = graphics.Save();
        var clip = new XGraphicsPath();
        clip.AddRectangle(left, top, plan.MapWidth * scale, plan.MapHeight * scale);
        graphics.IntersectClip(clip);
        graphics.DrawImage(image,
            left - (plan.ImageDrawWidth - plan.MapWidth) * scale / 2,
            top - (plan.ImageDrawHeight - plan.MapHeight) * scale / 2,
            plan.ImageDrawWidth * scale, plan.ImageDrawHeight * scale);
        graphics.Restore(state);
    }

    private static PdfPage AddPage(PdfDocument document, double width, double height)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(width);
        page.Height = XUnit.FromPoint(height);
        return page;
    }
}
