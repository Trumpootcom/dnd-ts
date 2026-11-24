using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;

namespace Plmh.Core;

public static class PortraitPdfExporter
{
    private const byte MinimumCropAlpha = 4;
    private const double CreatureDownShiftFeet = 0.125;
    private const double CreatureNameRatio = 0.9;

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
        if (EmbeddedFontResolver.IsEmbedded(template.Font))
            EmbeddedFontResolver.Register();
        var measureSelectedFont = EmbeddedFontResolver.IsLibreBaskerville(template.Font)
            || template.Font == EmbeddedFontResolver.GeorgiaBold;
        var labelColor = XColor.FromArgb(255, 244, 214);

        var templateDirectory = Path.GetDirectoryName(Path.GetFullPath(templatePath))!;
        var backgroundPath = Path.GetFullPath(template.Background.Image, templateDirectory);
        var framePath = Path.GetFullPath(template.Frame.Image, templateDirectory);
        using var backgroundData = ReadImage(backgroundPath, template.Size.Width, template.Size.Height,
            resizeToExpected: true);
        using var frameData = ReadImage(framePath, template.Size.Width, template.Size.Height,
            resizeToExpected: true);
        using var background = XImage.FromStream(backgroundData);
        using var frame = XImage.FromStream(frameData);
        using var badgeData = template.TokenBadge is { } badgePath
            ? ReadImage(Path.GetFullPath(badgePath, templateDirectory))
            : null;
        using var badge = badgeData is null ? null : XImage.FromStream(badgeData);
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
        var lineup = new List<(string Path, string Name, string TokenId, double WidthFeet, double HeightFeet, int BadgeScale)>(
            portraits.Count);
        var lineupPage = document.AddPage();

        for (var pageStart = 0; pageStart < portraits.Count; pageStart += perPage)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(pageWidth);
            page.Height = XUnit.FromPoint(pageHeight);
            var pageCount = Math.Min(perPage, portraits.Count - pageStart);
            var gridTop = margin;
            using (var graphics = XGraphics.FromPdfPage(page))
            {
                var text = new PdfTextCanvas(page, template.Font);
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
                    var lineupHeightFeet = portrait.HeightInches is { } explicitHeightInches
                        ? explicitHeightInches / 12
                        : template.GetDefaultHeightFeet(portrait.Size);
                    var badgeScale = portrait.Size.ToLowerInvariant() switch
                    {
                        "large" => 1,
                        "huge" => 1,
                        "gargantuan" => 1,
                        _ => 1
                    };
                    lineup.Add((portrait.Path, portrait.Name, portrait.TokenId,
                        lineupHeightFeet * image.PixelWidth / image.PixelHeight, lineupHeightFeet, badgeScale));
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
                        var label = PdfTextCanvas.Normalize(value);
                        if (measureSelectedFont)
                        {
                            var width = region.W * scale;
                            var measuredWidth = text.MeasureForFont(graphics, label, placement.FontSize, bold);
                            var widthScale = measuredWidth > width ? width / measuredWidth : 1;
                            var x = cardX + region.L * scale + (width - measuredWidth * widthScale) / 2;
                            placement = placement with { X = x, HorizontalScale = widthScale };
                        }
                        text.Draw(graphics, label, placement.FontSize, bold, labelColor,
                            placement.X, placement.Baseline, widthScale: placement.HorizontalScale);
                    }

                    void DrawSmallCaps(string value, TemplateRegion region, bool bold)
                    {
                        var placement = TextFieldLayout.Fit(value, region.ToPixelRegion(), cardX, cardY, scale, bold);
                        text.DrawSmallCaps(graphics, PdfTextCanvas.Normalize(value), placement.FontSize, bold,
                            labelColor, cardX + region.L * scale, placement.Baseline,
                            region.W * scale);
                    }

                }
                graphics.Dispose();
                text.Finish();
            }
        }

        DrawLineup(lineupPage, background, badge, lineup, pageWidth, pageHeight, margin,
            template.LineupFrame, templateDirectory, portraits[0].EncounterId, labelColor);
        document.Save(destination, false);
    }

    private static void DrawLineup(PdfPage page, XImage background, XImage? badge,
        IReadOnlyList<(string Path, string Name, string TokenId, double WidthFeet, double HeightFeet, int BadgeScale)> lineup,
        double pageWidth, double pageHeight, double pageMargin,
        LineupFrameTemplate? frame, string templateDirectory, string encounterName, XColor titleColor)
    {
        const double rightMarginFeet = 1;
        const double axisLabelGapFeet = 0.2;
        const double axisLabelOuterPaddingFeet = 0.25;
        const double labelSize = 20;
        const double topMarginFeet = 1.5;
        const double nameTopPaddingFeet = 0.15;
        const double nameLineSpacing = 1.2;
        const double baseBadgeSizeFeet = 1.5;
        const double gapFeet = 1;
        const double framePointsPerPixel = 72d / 300;

        using var topLeftData = ReadFrameTile(frame?.TopLeft);
        using var topData = ReadFrameTile(frame?.Top);
        using var leftData = ReadFrameTile(frame?.Left);
        using var bottomLeftData = ReadFrameTile(frame?.BottomLeft);
        using var bottomData = ReadFrameTile(frame?.Bottom);
        using var topLeftTile = topLeftData is null ? null : XImage.FromStream(topLeftData);
        using var topTile = topData is null ? null : XImage.FromStream(topData);
        using var leftTile = leftData is null ? null : XImage.FromStream(leftData);
        using var bottomLeftTile = bottomLeftData is null ? null : XImage.FromStream(bottomLeftData);
        using var bottomTile = bottomData is null ? null : XImage.FromStream(bottomData);

        var tallestBadgeFeet = baseBadgeSizeFeet * lineup.Max(creature => creature.BadgeScale);
        var axisWidthFeet = lineup.Sum(creature => creature.WidthFeet)
            + gapFeet * (lineup.Count - 1);
        var tallestFeet = lineup.Max(creature => creature.HeightFeet);
        var minimumLeftMarginFeet = tallestFeet > 10 ? 2d : 1.5;
        var labelIntervalFeet = tallestFeet < 10 ? 1 : 5;
        var widestLabelWidth = 0d;
        for (long foot = labelIntervalFeet; foot <= tallestFeet; foot += labelIntervalFeet)
        {
            var label = FormattableString.Invariant($"{foot} ft");
            widestLabelWidth = Math.Max(widestLabelWidth,
                PdfTextCanvas.Measure(label, labelSize, bold: false));
        }
        var labelPaddingFeet = axisLabelGapFeet + axisLabelOuterPaddingFeet;
        var printableWidth = pageWidth - 2 * pageMargin;
        var printableHeight = pageHeight - 2 * pageMargin;
        var widthWithoutLeftMarginFeet = axisWidthFeet + rightMarginFeet;
        if (widestLabelWidth >= printableWidth)
            throw new ArgumentException("The Y-axis labels leave no printable width for the lineup.");
        // The width must fit with either the minimum margin or the fixed-size labels.
        var widthLimitedScale = Math.Min(
            printableWidth / (widthWithoutLeftMarginFeet + minimumLeftMarginFeet),
            (printableWidth - widestLabelWidth) / (widthWithoutLeftMarginFeet + labelPaddingFeet));
        var nameTopFeet = CreatureDownShiftFeet + nameTopPaddingFeet;
        var preliminaryScale = Math.Min(widthLimitedScale,
            printableHeight / (topMarginFeet + tallestFeet + nameTopFeet + tallestBadgeFeet));
        var nameFontFeet = Math.Min(0.8, 20 / preliminaryScale);
        var nameLineFeet = nameFontFeet * nameLineSpacing;
        var wrappedNames = lineup.Select(creature =>
            WrapLineupName(creature.Name, creature.WidthFeet * CreatureNameRatio, nameFontFeet)).ToArray();
        var namesHeightFeet = wrappedNames.Max(lines => lines.Count) * nameLineFeet;
        var halfLineFeet = nameLineFeet / 2;
        var badgeAreaTopFeet = nameTopFeet + namesHeightFeet + halfLineFeet;
        var bottomMarginFeet = badgeAreaTopFeet + tallestBadgeFeet + halfLineFeet;
        var heightFeet = topMarginFeet + tallestFeet + bottomMarginFeet;
        var pointsPerFoot = Math.Min(widthLimitedScale, printableHeight / heightFeet);
        var frameScale = 0d;
        var bottomExtension = 0d;
        var frameWidthPixels = 0d;
        var extensionPixels = 0;
        var sideOverhangPixels = frame?.SideOverhangPixels ?? 0;
        var topOverhangPixels = frame?.TopOverhangPixels ?? 0;
        if (frame is not null && topLeftTile is not null && topTile is not null && leftTile is not null
            && bottomLeftTile is not null && bottomTile is not null)
        {
            if (sideOverhangPixels >= leftTile.PixelWidth || topOverhangPixels >= topLeftTile.PixelHeight)
                throw new InvalidDataException("Lineup frame overhangs must be smaller than their tiles.");
            frameWidthPixels = 2d * (leftTile.PixelWidth - sideOverhangPixels)
                + Math.Max(topTile.PixelWidth, bottomTile.PixelWidth);
            extensionPixels = frame.BottomExtensionPixels;
            if (extensionPixels >= bottomLeftTile.PixelHeight)
                throw new InvalidDataException("Lineup frame bottom extension must be shorter than the bottom row.");
            var nativeVisibleHeight = (double)topLeftTile.PixelHeight + leftTile.PixelHeight
                + bottomLeftTile.PixelHeight - topOverhangPixels - extensionPixels;
            frameScale = Math.Min(framePointsPerPixel, Math.Min(
                (widthWithoutLeftMarginFeet * pointsPerFoot + LeftMarginPoints(pointsPerFoot)) / frameWidthPixels,
                heightFeet * pointsPerFoot / nativeVisibleHeight));
            var heightFit = printableHeight /
                (heightFeet * pointsPerFoot + (topOverhangPixels + (double)extensionPixels) * frameScale);
            var sideOverhang = 2d * sideOverhangPixels * frameScale;
            var widthFit = sideOverhangPixels == 0 ? 1 : Math.Min(
                printableWidth / ((widthWithoutLeftMarginFeet + minimumLeftMarginFeet) * pointsPerFoot + sideOverhang),
                (printableWidth - widestLabelWidth) /
                ((widthWithoutLeftMarginFeet + labelPaddingFeet) * pointsPerFoot + sideOverhang));
            var fit = Math.Min(1, Math.Min(heightFit, widthFit));
            pointsPerFoot *= fit;
            frameScale *= fit;
        }
        var leftMargin = LeftMarginPoints(pointsPerFoot);
        var width = widthWithoutLeftMarginFeet * pointsPerFoot + leftMargin;
        if (frameWidthPixels > 0)
        {
            frameScale = Math.Min(frameScale, width / frameWidthPixels);
            bottomExtension = extensionPixels * frameScale;
        }
        var height = heightFeet * pointsPerFoot;
        var left = (pageWidth - width) / 2;
        var topOverhang = topOverhangPixels * frameScale;
        var sideExtension = sideOverhangPixels * frameScale;
        var top = (pageHeight - height - bottomExtension + topOverhang) / 2;
        var baseline = top + height - bottomMarginFeet * pointsPerFoot;

        page.Width = XUnit.FromPoint(pageWidth);
        page.Height = XUnit.FromPoint(pageHeight);
        using var graphics = XGraphics.FromPdfPage(page);
        var canvasHeight = height + bottomExtension;
        var sideBackgroundBleed = (frame?.SideBackgroundBleedPixels ?? 0) * frameScale;
        var backgroundBounds = new XRect(left - sideBackgroundBleed, top,
            width + 2 * sideBackgroundBleed, canvasHeight);
        var backgroundScale = Math.Max(backgroundBounds.Width / background.PixelWidth,
            backgroundBounds.Height / background.PixelHeight);
        var backgroundWidth = background.PixelWidth * backgroundScale;
        var backgroundHeight = background.PixelHeight * backgroundScale;
        var state = graphics.Save();
        graphics.IntersectClip(backgroundBounds);
        graphics.DrawImage(background, left + (width - backgroundWidth) / 2,
            top + (canvasHeight - backgroundHeight) / 2, backgroundWidth, backgroundHeight);
        graphics.Restore(state);

        var x = left + leftMargin;
        var right = x + axisWidthFeet * pointsPerFoot;
        DrawLineupScale(graphics, x, right, baseline, tallestFeet, pointsPerFoot);
        var creatureBaseline = baseline + pointsPerFoot * CreatureDownShiftFeet;
        var labels = new PdfTextCanvas(page, "Helvetica");
        var nameTop = baseline + nameTopFeet * pointsPerFoot;
        var badgeCenterY = baseline + (badgeAreaTopFeet + tallestBadgeFeet / 2) * pointsPerFoot;
        var heightLabelRight = x - pointsPerFoot * axisLabelGapFeet;
        for (long foot = labelIntervalFeet; foot <= tallestFeet; foot += labelIntervalFeet)
        {
            var label = FormattableString.Invariant($"{foot} ft");
            var labelWidth = PdfTextCanvas.Measure(label, labelSize, bold: false);
            var heightLabelY = baseline - foot * pointsPerFoot + labelSize * 0.35;
            labels.Draw(graphics, label, labelSize, bold: false, XColors.Black,
                heightLabelRight - labelWidth, heightLabelY);
        }

        for (var index = 0; index < lineup.Count; index++)
        {
            var creature = lineup[index];
            using var imageData = ReadImage(creature.Path, cropTransparentPadding: true);
            using var image = XImage.FromStream(imageData);
            var creatureWidth = creature.WidthFeet * pointsPerFoot;
            var creatureHeight = creature.HeightFeet * pointsPerFoot;
            graphics.DrawImage(image, x, creatureBaseline - creatureHeight, creatureWidth, creatureHeight);
            var centerX = x + creatureWidth / 2;
            DrawLineupName(graphics, labels, wrappedNames[index], centerX, nameTop,
                creatureWidth * CreatureNameRatio, nameFontFeet * pointsPerFoot, nameLineFeet * pointsPerFoot);
            var badgeSize = baseBadgeSizeFeet * creature.BadgeScale * pointsPerFoot;
            var badgeTop = badgeCenterY - badgeSize / 2;
            if (badge is not null)
                graphics.DrawImage(badge, centerX - badgeSize / 2, badgeTop, badgeSize, badgeSize);
            var tokenId = PdfTextCanvas.Normalize(creature.TokenId.ToUpperInvariant());
            var tokenSize = badgeSize * 0.5;
            var tokenWidth = PdfTextCanvas.Measure(tokenId, tokenSize, bold: true);
            var widthScale = tokenWidth > badgeSize * 0.7 ? badgeSize * 0.7 / tokenWidth : 1;
            labels.Draw(graphics, tokenId, tokenSize, bold: true, badge is null ? XColors.Black : XColors.White,
                centerX - tokenWidth * widthScale / 2, badgeTop + (badgeSize + tokenSize * 0.52) / 2,
                widthScale: widthScale);
            x += creatureWidth + gapFeet * pointsPerFoot;
        }
        if (topLeftTile is not null && topTile is not null && leftTile is not null
            && bottomLeftTile is not null && bottomTile is not null)
        {
            var frameBounds = new XRect(left - sideExtension, top - topOverhang,
                width + 2 * sideExtension, canvasHeight + topOverhang);
            DrawLineupFrame(graphics, frameBounds, frameScale,
                topLeftTile, topTile, leftTile, bottomLeftTile, bottomTile);
            if (frame?.TitleBand is { } titleBand)
            {
                if ((long)titleBand.Top + titleBand.Height > bottomTile.PixelHeight)
                    throw new InvalidDataException("Lineup frame title band must fit within the bottom tile.");
                var title = PdfTextCanvas.Normalize($"Encounter {encounterName}");
                var rowHeight = bottomTile.PixelHeight * frameScale;
                var rowTop = frameBounds.Bottom - rowHeight;
                var titleWidthPixels = width / frameScale;
                var titleSidePaddingPixels = Math.Min(24, titleWidthPixels / 4);
                var region = new PixelRegion(titleSidePaddingPixels, titleBand.Top,
                    titleWidthPixels - 2 * titleSidePaddingPixels, titleBand.Height);
                var placement = TextFieldLayout.Fit(title, region, left, rowTop, frameScale, bold: true);
                labels.Draw(graphics, title, placement.FontSize, bold: true, titleColor,
                    placement.X, placement.Baseline, widthScale: placement.HorizontalScale);
            }
        }
        graphics.Dispose();
        labels.Finish();

        double LeftMarginPoints(double scale) =>
            Math.Max(minimumLeftMarginFeet * scale, widestLabelWidth + labelPaddingFeet * scale);

        MemoryStream? ReadFrameTile(string? path) =>
            path is null ? null : ReadImage(Path.GetFullPath(path, templateDirectory));
    }

    private static void DrawLineupFrame(XGraphics graphics, XRect bounds, double scale,
        XImage topLeft, XImage top, XImage left, XImage bottomLeft, XImage bottom)
    {
        if (topLeft.PixelWidth != left.PixelWidth || bottomLeft.PixelWidth != left.PixelWidth
            || topLeft.PixelHeight != top.PixelHeight || bottomLeft.PixelHeight != bottom.PixelHeight)
            throw new InvalidDataException("Lineup frame tiles must share a side column width and match their row heights.");

        var sideWidth = left.PixelWidth * scale;
        var topHeight = topLeft.PixelHeight * scale;
        var bottomHeight = bottomLeft.PixelHeight * scale;
        var middleWidth = bounds.Width - 2 * sideWidth;
        var middleHeight = bounds.Height - topHeight - bottomHeight;
        var bottomY = bounds.Bottom - bottomHeight;

        DrawSides(topLeft, bounds.Top, topHeight);
        DrawHorizontalEdge(top, bounds.Top, topHeight);
        var sideCount = TileCount(middleHeight, left.PixelHeight);
        var sideHeight = middleHeight / sideCount;
        for (var index = 0; index < sideCount; index++)
            DrawSides(left, bounds.Top + topHeight + index * sideHeight, sideHeight);
        DrawSides(bottomLeft, bottomY, bottomHeight);
        DrawHorizontalEdge(bottom, bottomY, bottomHeight);

        int TileCount(double span, int tilePixels) =>
            Math.Max(1, (int)Math.Round(span / (tilePixels * scale), MidpointRounding.AwayFromZero));

        void DrawHorizontalEdge(XImage image, double y, double height)
        {
            var count = TileCount(middleWidth, image.PixelWidth);
            var tileWidth = middleWidth / count;
            for (var index = 0; index < count; index++)
                graphics.DrawImage(image, bounds.Left + sideWidth + index * tileWidth, y, tileWidth, height);
        }

        void DrawSides(XImage image, double y, double height)
        {
            graphics.DrawImage(image, bounds.Left, y, sideWidth, height);
            var state = graphics.Save();
            graphics.TranslateTransform(bounds.Right, y);
            graphics.ScaleTransform(-1, 1);
            graphics.DrawImage(image, 0, 0, sideWidth, height);
            graphics.Restore(state);
        }
    }

    private static List<string> WrapLineupName(string name, double maxWidth, double fontSize)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in PdfTextCanvas.Normalize(name).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && PdfTextCanvas.Measure(candidate, fontSize, bold: false) > maxWidth)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    private static void DrawLineupName(XGraphics graphics, PdfTextCanvas labels, IReadOnlyList<string> lines,
        double centerX, double top, double maxWidth, double fontSize, double lineHeight)
    {
        var baseline = top + fontSize;
        foreach (var wrappedLine in lines)
        {
            var width = PdfTextCanvas.Measure(wrappedLine, fontSize, bold: false);
            var widthScale = width > maxWidth ? maxWidth / width : 1;
            labels.Draw(graphics, wrappedLine, fontSize, bold: false, XColors.Black,
                centerX - width * widthScale / 2, baseline, widthScale: widthScale);
            baseline += lineHeight;
        }
    }

    private static void DrawLineupScale(XGraphics graphics, double left, double right,
        double baseline, double tallestFeet, double pointsPerFoot)
    {
        const double lineWidth = 1;
        var color = XColor.FromArgb(153, 0, 0, 0);
        var normal = new XPen(color, lineWidth);
        var fiveFeet = new XPen(color, lineWidth * 1.5);
        var tenFeet = new XPen(color, lineWidth * 2);

        graphics.DrawLine(normal, left, baseline - tallestFeet * pointsPerFoot, left, baseline);
        graphics.DrawLine(normal, left, baseline, right, baseline);
        for (long foot = 1; foot <= tallestFeet; foot++)
        {
            var pen = foot % 10 == 0 ? tenFeet : foot % 5 == 0 ? fiveFeet : normal;
            var y = baseline - foot * pointsPerFoot;
            graphics.DrawLine(pen, left, y, right, y);
        }
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

        // An empty center row or column can make the two edges cross.
        if (left > right || top > bottom)
            return null;

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
                if (source.GetPixel(x, y).Alpha < MinimumCropAlpha) continue;
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
                if (source.GetPixel(x, y).Alpha < MinimumCropAlpha) continue;
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
