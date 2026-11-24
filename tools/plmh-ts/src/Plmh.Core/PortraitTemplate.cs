using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plmh.Core;

public sealed record PortraitTemplate(
    int SchemaVersion,
    string Template,
    string Title,
    CanvasSizeTemplate Size,
    BackgroundTemplate Background,
    FrameTemplate Frame,
    RegionsTemplate Regions,
    PageTemplate Page)
{
    [JsonPropertyName("scale")]
    public ScaleTemplate Scale { get; init; } = new();

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
        if (Background is null || string.IsNullOrWhiteSpace(Background.Image))
            throw new ArgumentException("Background image is required.");
        if (Size is null || Size.Width <= 0 || Size.Height <= 0)
            throw new ArgumentException("Template size must define positive width and height.");
        if (Frame is null || string.IsNullOrWhiteSpace(Frame.Image))
            throw new ArgumentException("Frame image is required.");
        if (Regions is null || Page is null)
            throw new ArgumentException("Template regions and page settings are required.");
        if (Scale is null || !double.IsFinite(Scale.MinHeight) || Scale.MinHeight < 0 || Scale.MinHeight > 1)
            throw new ArgumentException("Template scale.min_ht must be between 0 and 1.");
        if (Scale.Heights.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
            || !double.IsFinite(pair.Value) || pair.Value <= 0))
            throw new ArgumentException("Template size scales must have nonempty names and positive finite heights.");
        if (Regions.TokenId is null || Regions.Portrait is null || Regions.Name is null || Regions.SizeType is null)
            throw new ArgumentException("Template must define token_id, portrait, name, and size_type regions.");
        Regions.TokenId.Validate(nameof(Regions.TokenId));
        Regions.Portrait.Validate(nameof(Regions.Portrait));
        Regions.Name.Validate(nameof(Regions.Name));
        Regions.SizeType.Validate(nameof(Regions.SizeType));
        foreach (var region in new[] { Regions.TokenId, Regions.Portrait, Regions.Name, Regions.SizeType })
        {
            if (region.L + region.W > Size.Width || region.T + region.H > Size.Height)
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

public sealed record BackgroundTemplate(string Image);

public sealed record CanvasSizeTemplate(int Width, int Height);

public sealed record FrameTemplate(string Image);

public sealed record ScaleTemplate
{
    [JsonPropertyName("min_ht")]
    public double MinHeight { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; init; } = new();

    [JsonIgnore]
    public Dictionary<string, double> Heights => Extra.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.GetDouble(),
        StringComparer.OrdinalIgnoreCase);
}

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
