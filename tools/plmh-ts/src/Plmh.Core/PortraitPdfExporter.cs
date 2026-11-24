using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;

namespace Plmh.Core;

public static class PortraitPdfExporter
{
    public static void Export(PortraitTemplate template, string templatePath,
        IReadOnlyList<PortraitFileName> portraits, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentNullException.ThrowIfNull(portraits);
        if (portraits.Count == 0)
            throw new ArgumentException("At least one portrait is required.", nameof(portraits));
        ArgumentNullException.ThrowIfNull(destination);
        template.Validate();

        var backgroundPath = Path.GetFullPath(template.Background.Image,
            Path.GetDirectoryName(Path.GetFullPath(templatePath))!);
        var framePath = Path.GetFullPath(template.Frame.Image,
            Path.GetDirectoryName(Path.GetFullPath(templatePath))!);
        using var backgroundData = ReadImage(backgroundPath, template.Size.Width, template.Size.Height,
            resizeToExpected: true);
        using var frameData = ReadImage(framePath, template.Size.Width, template.Size.Height,
            resizeToExpected: true);
        using var background = XImage.FromStream(backgroundData);
        using var frame = XImage.FromStream(frameData);
        using var document = new PdfDocument();
        document.Options.CompressContentStreams = false;
        document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;
        document.Info.Title = $"{template.Title} - Encounter {portraits[0].EncounterId}";

        var (pageWidth, pageHeight) = GetPageSize(template.Page);
        var margin = template.Page.MarginInches * 72;
        if (pageWidth <= 2 * margin || pageHeight <= 2 * margin)
            throw new ArgumentException("Page margins leave no printable area.");
        var printableWidth = pageWidth - 2 * margin;
        var printableHeight = pageHeight - 2 * margin;
        var gapX = template.Page.ColumnGapInches * 72;
        var gapY = template.Page.RowGapInches * 72;
        var cellWidth = (printableWidth - gapX * (template.Page.Columns - 1)) / template.Page.Columns;
        var cellHeight = (printableHeight - gapY * (template.Page.Rows - 1)) / template.Page.Rows;
        if (cellWidth <= 0 || cellHeight <= 0)
            throw new ArgumentException("Page margins and gaps leave no room for the configured portrait grid.");
        var perPage = template.Page.Columns * template.Page.Rows;

        for (var pageStart = 0; pageStart < portraits.Count; pageStart += perPage)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(pageWidth);
            page.Height = XUnit.FromPoint(pageHeight);
            var pageCount = Math.Min(perPage, portraits.Count - pageStart);
            var gridTop = margin;
            using (var graphics = XGraphics.FromPdfPage(page))
            {
                var text = new PdfTextCanvas(page);
                for (var localIndex = 0; localIndex < pageCount; localIndex++)
                {
                    var portrait = portraits[pageStart + localIndex];
                    var row = localIndex / template.Page.Columns;
                    var column = localIndex % template.Page.Columns;
                    var rowCount = Math.Min(template.Page.Columns, pageCount - row * template.Page.Columns);
                    var rowStart = margin;
                    var cellX = rowStart + column * (cellWidth + gapX);
                    var cellY = gridTop + row * (cellHeight + gapY);
                    var scale = Math.Min(cellWidth / template.Size.Width,
                        cellHeight / template.Size.Height);
                    var cardWidth = template.Size.Width * scale;
                    var cardHeight = template.Size.Height * scale;
                    var cardX = cellX;
                    var cardY = cellY;

                    using var portraitData = ReadImage(portrait.Path, cropTransparentPadding: true);
                    using var image = XImage.FromStream(portraitData);
                    var portraitScale = portrait.HeightInches is { } heightInches
                        ? template.Scale.MinHeight + (1 - template.Scale.MinHeight)
                            * (heightInches / GetReferenceHeight(template, portrait.Size))
                        : 1d;
                    var portraitRegion = template.Regions.Portrait;
                    var imageBounds = GetFittedImageBounds(image, cardX, cardY, portraitRegion, scale, portraitScale);
                    graphics.DrawImage(background, cardX, cardY, cardWidth, cardHeight);
                    DrawFittedImage(graphics, image, imageBounds, cardX, cardY, portraitRegion, scale);
                    graphics.DrawImage(frame, cardX, cardY, cardWidth, cardHeight);
                    DrawText(portrait.TokenId, template.Regions.TokenId, bold: true);
                    DrawSmallCaps(portrait.Name, template.Regions.Name, bold: true);
                    DrawSmallCaps($"{portrait.Size} {portrait.Type}", template.Regions.SizeType, bold: false);

                    void DrawText(string value, TemplateRegion region, bool bold)
                    {
                        var placement = TextFieldLayout.Fit(value, region.ToPixelRegion(), cardX, cardY, scale, bold);
                        text.Draw(graphics, PdfTextCanvas.Normalize(value), placement.FontSize, bold,
                            XColor.FromArgb(255, 244, 214), placement.X, placement.Baseline,
                            widthScale: placement.HorizontalScale);
                    }

                    void DrawSmallCaps(string value, TemplateRegion region, bool bold)
                    {
                        var placement = TextFieldLayout.Fit(value, region.ToPixelRegion(), cardX, cardY, scale, bold);
                        text.DrawSmallCaps(graphics, PdfTextCanvas.Normalize(value), placement.FontSize, bold,
                            XColor.FromArgb(255, 244, 214), cardX + region.L * scale, placement.Baseline,
                            region.W * scale);
                    }

                }
                graphics.Dispose();
                text.Finish();
            }
        }

        document.Save(destination, false);
    }

    private static double GetReferenceHeight(PortraitTemplate template, string size)
    {
        foreach (var pair in template.Scale.Heights)
            if (string.Equals(pair.Key, size, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        throw new InvalidDataException($"Template has no reference height configured for size '{size}'.");
    }

    private static XRect GetFittedImageBounds(XImage image, double cardX, double cardY,
        TemplateRegion region, double scale, double portraitScale)
    {
        var x = cardX + region.L * scale;
        var y = cardY + region.T * scale;
        var width = region.W * scale;
        var height = region.H * scale;
        var imageScale = height / image.PixelHeight * portraitScale;
        var drawWidth = image.PixelWidth * imageScale;
        var drawHeight = image.PixelHeight * imageScale;
        var drawX = x + (width - drawWidth) / 2;
        var drawY = y + height - drawHeight;
        return new XRect(drawX, drawY, drawWidth, drawHeight);
    }

    private static void DrawFittedImage(XGraphics graphics, XImage image, XRect imageBounds, double cardX, double cardY,
        TemplateRegion region, double scale)
    {
        var x = cardX + region.L * scale;
        var y = cardY + region.T * scale;
        var width = region.W * scale;
        var height = region.H * scale;
        var state = graphics.Save();
        graphics.IntersectClip(new XRect(x, y, width, height));
        graphics.DrawImage(image, imageBounds.X, imageBounds.Y, imageBounds.Width, imageBounds.Height);
        graphics.Restore(state);
    }

    private static MemoryStream ReadImage(string path, int? expectedWidth = null, int? expectedHeight = null,
        bool cropTransparentPadding = false, bool resizeToExpected = false)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Image file not found.", path);
        using var input = File.OpenRead(path);
        using var codec = SKCodec.Create(input)
            ?? throw new InvalidDataException($"Image is unreadable: {path}");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png
            or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Bmp))
            throw new InvalidDataException("Expected a JPEG, PNG, WebP, or BMP image.");
        if (codec.FrameCount > 1)
            throw new InvalidDataException("Animated images are not supported.");
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0
            || (long)codec.Info.Width * codec.Info.Height > 24_000_000)
            throw new InvalidDataException("Image dimensions are invalid or exceed 24 megapixels.");
        if (expectedWidth is { } width && !resizeToExpected
            && (codec.Info.Width != width || codec.Info.Height != expectedHeight))
            throw new InvalidDataException($"Image dimensions are {codec.Info.Width}x{codec.Info.Height}; template specifies {width}x{expectedHeight}.");
        using var bitmap = SKBitmap.Decode(codec)
            ?? throw new InvalidDataException($"Image pixels could not be decoded: {path}");
        using var resized = resizeToExpected && expectedWidth is { } targetWidth
            && expectedHeight is { } targetHeight
            && (bitmap.Width != targetWidth || bitmap.Height != targetHeight)
                ? Resize(bitmap, targetWidth, targetHeight)
                : null;
        var decodedImage = resized ?? bitmap;
        using var cropped = cropTransparentPadding ? CropTransparentPadding(decodedImage) : null;
        var imageToEncode = cropped ?? decodedImage;
        using var encoded = imageToEncode.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException("Image could not be converted for PDF output.");
        return new MemoryStream(encoded.ToArray(), writable: false);
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        var result = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height));
        canvas.Flush();
        return result;
    }

    private static SKBitmap? CropTransparentPadding(SKBitmap source)
    {
        // Probe inward from the image center. Generated portraits sometimes have
        // an opaque one-pixel frame around an otherwise transparent canvas, so
        // the outermost nontransparent pixel is not a useful crop boundary.
        var left = FindTransparentColumn(source, source.Width / 2, -1);
        var right = FindTransparentColumn(source, source.Width / 2, 1);
        var top = FindTransparentRow(source, source.Height / 2, -1);
        var bottom = FindTransparentRow(source, source.Height / 2, 1);

        // If no transparent boundary was found on an axis, preserve that axis.
        if (left < 0) left = 0;
        if (right < 0) right = source.Width - 1;
        if (top < 0) top = 0;
        if (bottom < 0) bottom = source.Height - 1;

        if (left == 0 && top == 0 && right == source.Width - 1 && bottom == source.Height - 1)
            return null;

        var width = right - left + 1;
        var height = bottom - top + 1;
        var result = new SKBitmap(width, height);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, new SKRect(-left, -top, source.Width - left, source.Height - top));
        canvas.Flush();
        return result;
    }

    private static int FindTransparentColumn(SKBitmap source, int start, int direction)
    {
        for (var x = start; x >= 0 && x < source.Width; x += direction)
        {
            var transparent = true;
            for (var y = 0; y < source.Height; y++)
            {
                if (source.GetPixel(x, y).Alpha == 0) continue;
                transparent = false;
                break;
            }
            if (transparent) return direction < 0 ? x + 1 : x - 1;
        }
        return -1;
    }

    private static int FindTransparentRow(SKBitmap source, int start, int direction)
    {
        for (var y = start; y >= 0 && y < source.Height; y += direction)
        {
            var transparent = true;
            for (var x = 0; x < source.Width; x++)
            {
                if (source.GetPixel(x, y).Alpha == 0) continue;
                transparent = false;
                break;
            }
            if (transparent) return direction < 0 ? y + 1 : y - 1;
        }
        return -1;
    }

    private static (double Width, double Height) GetPageSize(PageTemplate page)
    {
        var dimensions = page.Size.ToLowerInvariant() switch
        {
            "letter" => (612d, 792d),
            "legal" => (612d, 1008d),
            "ledger" => (1224d, 792d),
            _ => throw new ArgumentException($"Unsupported paper size '{page.Size}'.")
        };
        return page.Orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase)
            ? (Math.Max(dimensions.Item1, dimensions.Item2), Math.Min(dimensions.Item1, dimensions.Item2))
            : (Math.Min(dimensions.Item1, dimensions.Item2), Math.Max(dimensions.Item1, dimensions.Item2));
    }
}
