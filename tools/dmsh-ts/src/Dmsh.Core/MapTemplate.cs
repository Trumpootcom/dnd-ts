using System.Text.Json;

namespace Dmsh.Core;

public sealed record MapTemplate(
    int SchemaVersion, string Template, string Title, bool Overview, bool Coordinates,
    double WorldWidthFeet, string ImagePlacement, GridTemplate Grid, PageTemplate Page)
{
    public double CoordinateBarHeightRatio { get; init; } = 0.4;

    public static MapTemplate Read(Stream json)
    {
        return JsonSerializer.Deserialize<MapTemplate>(json, JsonSerializerOptions.Web)
            ?? throw new JsonException("Expected a template object.");
    }
}
