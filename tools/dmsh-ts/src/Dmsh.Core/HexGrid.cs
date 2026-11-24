using PdfSharp.Drawing;

namespace Dmsh.Core;

internal static class HexGrid
{
    internal readonly record struct Edge(XPoint Start, XPoint End);

    private static readonly (int X, int Y)[] Corners = [(2, 0), (1, 1), (-1, 1), (-2, 0)];

    public static IReadOnlyList<Edge> Build(GridTemplate grid, SheetPlan plan)
    {
        var tilt = Math.Sin((grid.Iso ? grid.IsoAngleDegrees!.Value : 90) * Math.PI / 180);
        var unitX = plan.GridSpacing / (2 * Math.Sqrt(3) * tilt);
        var unitY = plan.GridSpacing / 2;
        var angle = (grid.RotationDegrees % 360) * Math.PI / 180;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var offsetX = (grid.OffsetX % 1) * 3 * unitX;
        var offsetY = ((grid.OffsetX % 1) - 2 * (grid.OffsetY % 1)) * unitY;
        var originX = plan.CanvasWidth / 2 + offsetX * cos - offsetY * sin;
        var originY = -plan.CanvasHeight / 2 + offsetX * sin + offsetY * cos;
        var bounds = new[]
        {
            Project(0, 0), Project(plan.CanvasWidth, 0),
            Project(0, -plan.CanvasHeight), Project(plan.CanvasWidth, -plan.CanvasHeight)
        };
        var minCol = (int)Math.Floor(bounds.Min(point => point.Column)) - 2;
        var maxCol = (int)Math.Ceiling(bounds.Max(point => point.Column)) + 2;
        var minRow = (int)Math.Floor(bounds.Min(point => point.Row)) - 2;
        var maxRow = (int)Math.Ceiling(bounds.Max(point => point.Row)) + 2;
        var cellCount = (long)(maxCol - minCol + 1) * (maxRow - minRow + 1);
        if (cellCount > 50_000)
            throw new ArgumentException("This hex grid is too dense for the selected canvas. Increase grid spacing or use fewer sheets.");

        var vertices = new Dictionary<(int X, int Y), XPoint>();
        var edges = new List<Edge>((int)cellCount * 3);
        for (var col = minCol; col <= maxCol; col++)
        {
            for (var row = minRow; row <= maxRow; row++)
            {
                var x = 3 * col;
                var y = 2 * row + col;
                // Each shared edge is emitted once, with a stable dash direction.
                for (var i = 0; i < 3; i++)
                    edges.Add(new Edge(Vertex(x + Corners[i].X, y + Corners[i].Y),
                        Vertex(x + Corners[i + 1].X, y + Corners[i + 1].Y)));
            }
        }
        return edges;

        (double Column, double Row) Project(double x, double y)
        {
            var dx = x - originX;
            var dy = y - originY;
            var column = (dx * cos + dy * sin) / (3 * unitX);
            return (column, ((-dx * sin + dy * cos) / unitY - column) / 2);
        }

        XPoint Vertex(int x, int y)
        {
            if (!vertices.TryGetValue((x, y), out var point))
            {
                point = new XPoint(originX + x * unitX * cos - y * unitY * sin,
                    originY + x * unitX * sin + y * unitY * cos);
                vertices.Add((x, y), point);
            }
            return point;
        }
    }
}
