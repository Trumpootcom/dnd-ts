using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plmh.Core;

public sealed record PortraitTemplate(
    int SchemaVersion,
    string Template,
    string Title,
    BackgroundTemplate Background,
    FrameTemplate Frame,
    RegionsTemplate Regions,
    PageTemplate Page)
{
    public static PortraitTemplate Read(Stream json)
    {
        var template = JsonSerializer.Deserialize<PortraitTemplate>(json, JsonSerializerOptions.Web)
            ?? throw new JsonException("Expected a template object.");
        template.Validate();
        return template;
    }

    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new ArgumentException($"Unsupported template schema version {SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(Template) || string.IsNullOrWhiteSpace(Title))
            throw new ArgumentException("Template name and title are required.");
        if (Background is null || string.IsNullOrWhiteSpace(Background.Image)
            || Background.Width <= 0 || Background.Height <= 0)
            throw new ArgumentException("Background image and positive pixel dimensions are required.");
        if (Frame is null || string.IsNullOrWhiteSpace(Frame.Image) || Frame.Width <= 0 || Frame.Height <= 0)
            throw new ArgumentException("Frame image and positive pixel dimensions are required.");
        var canvasAspect = (double)Background.Width / Background.Height;
        var frameAspect = (double)Frame.Width / Frame.Height;
        if (Math.Abs(canvasAspect - frameAspect) > 0.001)
            throw new ArgumentException("Background and frame images must have matching aspect ratios.");
        if (Regions is null || Page is null)
            throw new ArgumentException("Template regions and page settings are required.");
        if (Regions.TokenId is null || Regions.Portrait is null || Regions.Name is null || Regions.SizeType is null)
            throw new ArgumentException("Template must define token_id, portrait, name, and size_type regions.");
        Regions.TokenId.Validate(nameof(Regions.TokenId));
        Regions.Portrait.Validate(nameof(Regions.Portrait));
        Regions.Name.Validate(nameof(Regions.Name));
        Regions.SizeType.Validate(nameof(Regions.SizeType));
        foreach (var region in new[] { Regions.TokenId, Regions.Portrait, Regions.Name, Regions.SizeType })
        {
            if (region.L + region.W > Background.Width || region.T + region.H > Background.Height)
                throw new ArgumentException("A template region falls outside the background canvas dimensions.");
        }
        if (Page.Size.ToLowerInvariant() is not ("letter" or "legal" or "ledger"))
            throw new ArgumentException($"Unsupported paper size '{Page.Size}'.");
        if (Page.Orientation.ToLowerInvariant() is not ("portrait" or "landscape"))
            throw new ArgumentException($"Unsupported page orientation '{Page.Orientation}'.");
        if (!double.IsFinite(Page.MarginInches) || Page.MarginInches < 0 || Page.MarginInches >= 5)
            throw new ArgumentException("Page margin must be nonnegative and leave printable space.");
        if (Page.Rows <= 0 || Page.Columns <= 0 || (long)Page.Rows * Page.Columns > 100)
            throw new ArgumentException("Page rows and columns must be positive and allow no more than 100 portraits per page.");
        if (!double.IsFinite(Page.ColumnGapInches) || Page.ColumnGapInches < 0
            || !double.IsFinite(Page.RowGapInches) || Page.RowGapInches < 0)
            throw new ArgumentException("Page row and column gaps must be nonnegative finite values.");
        if (!string.Equals(Page.Placement, "topLeft", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Page placement must be 'topLeft'.");
    }
}

public sealed record BackgroundTemplate(string Image, int Width, int Height);

public sealed record FrameTemplate(string Image, int Width, int Height);

public sealed record RegionsTemplate(
    [property: JsonPropertyName("token_id")] TemplateRegion TokenId,
    [property: JsonPropertyName("portrait")] TemplateRegion Portrait,
    [property: JsonPropertyName("name")] TemplateRegion Name,
    [property: JsonPropertyName("size_type")] TemplateRegion SizeType);

public sealed record TemplateRegion(double L, double T, double W, double H)
{
    public PixelRegion ToPixelRegion() => new(L, T, W, H);

    public void Validate(string name)
    {
        if (!double.IsFinite(L) || !double.IsFinite(T) || L < 0 || T < 0
            || !double.IsFinite(W) || !double.IsFinite(H) || W <= 0 || H <= 0)
            throw new ArgumentException($"Template region '{name}' must have a nonnegative origin and positive finite dimensions.");
    }
}

public sealed record PageTemplate(string Size, string Orientation, double MarginInches,
    double ColumnGapInches, double RowGapInches, int Columns, int Rows, string Placement);
