namespace Dmsh.Core;

public sealed record GridTemplate(
    string Style, bool Iso, double? IsoAngleDegrees, double RotationDegrees,
    double OffsetX, double OffsetY, double InchesPerFiveFeet, string LineStyle,
    string Color, double LineWidthPoints, double Opacity, string Extent);

public sealed record PageTemplate(
    string Size, string Orientation, int Rows, int Columns, double MarginInches, double OverlapInches);

public sealed record SheetWindow(int Row, int Column, double Left, double Top);

public sealed record SheetPlan(
    double PageWidth, double PageHeight, double Margin, double Overlap,
    double PrintableWidth, double PrintableHeight, double CanvasWidth, double CanvasHeight,
    double GridSpacing, double MapLeft, double MapTop, double MapWidth, double MapHeight,
    double ImageDrawWidth, double ImageDrawHeight,
    bool HasCoordinateBorder, bool HasOverview, IReadOnlyList<SheetWindow> Sheets)
{
    public int PageCount => Sheets.Count + (HasOverview ? 1 : 0);
    public double CoordinateBarThickness { get; private init; }

    internal double GridEdgeOffsetX => EdgeOffset(MapWidth);
    internal double GridEdgeOffsetY => EdgeOffset(MapHeight);

    // A centered map with an odd number of cells has half-cell edges.
    private double EdgeOffset(double size) => Math.Round(size / GridSpacing) % 2 * 0.5;

    // All geometry uses PDF points, with canvas coordinates measured from the top left.
    public static SheetPlan Create(MapTemplate template, int imageWidth, int imageHeight)
    {
        var (width, height, margin, overlap, printableWidth, printableHeight, canvasWidth, canvasHeight,
            spacing, barThickness, coordinates) = CalculateCanvas(template);
        var page = template.Page;
        var stepX = printableWidth - overlap;
        var stepY = printableHeight - overlap;
        var reserve = 2 * barThickness;
        var requestedWidth = template.WorldWidthFeet / 5 * spacing;
        if (!double.IsFinite(requestedWidth) || requestedWidth <= 0 || imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentException("Map scale or image dimensions are invalid.");
        var aspect = (double)imageHeight / imageWidth;
        var fittedWidth = Math.Min(requestedWidth, Math.Min(canvasWidth - reserve, (canvasHeight - reserve) / aspect));
        if (fittedWidth <= 0)
            throw new ArgumentException("The fitted map has no room on the printable canvas.");
        var fittedHeight = fittedWidth * aspect;

        // One grid spacing represents five feet. Anchor one axis to a
        // whole cell, preserve the aspect ratio, then crop the other axis.
        var widthCells = (long)Math.Floor(fittedWidth / spacing);
        var heightCells = (long)Math.Floor(fittedHeight / spacing);
        var heightAfterWidthFit = widthCells * imageHeight / imageWidth;
        var widthAfterHeightFit = heightCells * imageWidth / imageHeight;
        if (heightAfterWidthFit < 1 && widthAfterHeightFit < 1)
            throw new ArgumentException("The fitted map is too small to retain a 5-foot cell in both dimensions.");

        var widthFitRetention = heightAfterWidthFit == 0 ? 0
            : (double)heightAfterWidthFit * imageWidth / (widthCells * imageHeight);
        var heightFitRetention = widthAfterHeightFit == 0 ? 0
            : (double)widthAfterHeightFit * imageHeight / (heightCells * imageWidth);
        var useWidthFit = widthFitRetention >= heightFitRetention;
        var mapWidth = (useWidthFit ? widthCells : widthAfterHeightFit) * spacing;
        var mapHeight = (useWidthFit ? heightAfterWidthFit : heightCells) * spacing;
        var imageDrawWidth = useWidthFit ? mapWidth : mapHeight / aspect;
        var imageDrawHeight = useWidthFit ? mapWidth * aspect : mapHeight;

        var sheets = new List<SheetWindow>();
        for (var row = 0; row < page.Rows; row++)
            for (var column = 0; column < page.Columns; column++)
                sheets.Add(new SheetWindow(row, column, column * stepX, row * stepY));

        return new SheetPlan(width, height, margin, overlap, printableWidth, printableHeight,
            canvasWidth, canvasHeight, spacing, (canvasWidth - mapWidth) / 2,
            (canvasHeight - mapHeight) / 2, mapWidth, mapHeight, imageDrawWidth, imageDrawHeight,
            coordinates, template.Overview,
            sheets.AsReadOnly()) { CoordinateBarThickness = barThickness };
    }

    public static (double WidthFeet, double HeightFeet) GetUsableWorldSize(MapTemplate template)
    {
        var canvas = CalculateCanvas(template);
        return (Math.Floor((canvas.CanvasWidth - 2 * canvas.BarThickness) / canvas.Spacing) * 5,
            Math.Floor((canvas.CanvasHeight - 2 * canvas.BarThickness) / canvas.Spacing) * 5);
    }

    private static (double Width, double Height, double Margin, double Overlap,
        double PrintableWidth, double PrintableHeight, double CanvasWidth, double CanvasHeight,
        double Spacing, double BarThickness, bool Coordinates) CalculateCanvas(MapTemplate template)
    {
        if (template.SchemaVersion != 1 || template.ImagePlacement != "center"
            || template.Grid is not { } grid || template.Page is not { } page)
            throw new ArgumentException("Expected a schema version 1 template with centered placement, grid and page settings.");

        var (shortEdge, longEdge) = page.Size switch
        {
            "letter" => (612d, 792d),
            "ledger" => (792d, 1224d),
            _ => throw new ArgumentException("Paper size must be letter or ledger.")
        };
        var (width, height) = page.Orientation switch
        {
            "portrait" => (shortEdge, longEdge),
            "landscape" => (longEdge, shortEdge),
            _ => throw new ArgumentException("Orientation must be portrait or landscape.")
        };
        if (page.Rows < 1 || page.Columns < 1 || (long)page.Rows * page.Columns > 100)
            throw new ArgumentException("The page array must contain between 1 and 100 sheets.");

        const double pointsPerInch = 72;
        var margin = page.MarginInches * pointsPerInch;
        var overlap = page.OverlapInches * pointsPerInch;
        if (!double.IsFinite(margin) || margin < 0 || 2 * margin >= shortEdge)
            throw new ArgumentException("Margin must be nonnegative and leave printable area.");
        if (!double.IsFinite(overlap) || overlap < 0 || overlap >= shortEdge - 2 * margin)
            throw new ArgumentException("Overlap must be nonnegative and smaller than the printable sheet dimensions.");

        var printableWidth = width - 2 * margin;
        var printableHeight = height - 2 * margin;
        var stepX = printableWidth - overlap;
        var stepY = printableHeight - overlap;
        var canvasWidth = printableWidth + (page.Columns - 1) * stepX;
        var canvasHeight = printableHeight + (page.Rows - 1) * stepY;
        var spacing = grid.InchesPerFiveFeet * pointsPerInch;
        if (!double.IsFinite(spacing) || spacing < 0.25)
            throw new ArgumentException("Grid spacing must be at least 0.25 PDF points.");

        if (!double.IsFinite(grid.RotationDegrees) || !double.IsFinite(grid.OffsetX) || !double.IsFinite(grid.OffsetY))
            throw new ArgumentException("Grid rotation and offsets must be finite numbers.");
        if (grid.Iso && (grid.IsoAngleDegrees is not double angle || !double.IsFinite(angle) || angle < 15 || angle > 90))
            throw new ArgumentException("Isometric grid angle must be between 15 and 90 degrees.");
        if (!grid.Iso && grid.IsoAngleDegrees is not null)
            throw new ArgumentException("The isometric grid angle must be null when grid.iso is false.");

        var coordinates = template.Coordinates && grid.Style != "hex" && !grid.Iso && grid.RotationDegrees == 0;
        var barRatio = template.CoordinateBarHeightRatio;
        if (!double.IsFinite(barRatio) || barRatio <= 0)
            throw new ArgumentException("coordinateBarHeightRatio must be a positive, finite number.");
        var barThickness = coordinates ? spacing * barRatio : 0;
        var reserve = 2 * barThickness;
        if (reserve >= Math.Min(canvasWidth, canvasHeight))
            throw new ArgumentException("The coordinate bars leave no room on the printable canvas.");
        return (width, height, margin, overlap, printableWidth, printableHeight, canvasWidth, canvasHeight,
            spacing, barThickness, coordinates);
    }
}
