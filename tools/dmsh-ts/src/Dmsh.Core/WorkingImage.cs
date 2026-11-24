using SkiaSharp;

namespace Dmsh.Core;

internal static class WorkingImage
{
    private const int MaxInputPixels = 12_000_000;
    private const int MaxWorkingEdge = 2400;
    private const int JpegQuality = 50;

    public static MemoryStream CreateJpeg(string path)
    {
        using var input = File.OpenRead(path);
        using var codec = SKCodec.Create(input)
            ?? throw new InvalidDataException("The image format is unreadable.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png
            or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Bmp))
            throw new InvalidDataException("Expected a JPEG, PNG, WebP or BMP image.");

        var sourceWidth = codec.Info.Width;
        var sourceHeight = codec.Info.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0 || (long)sourceWidth * sourceHeight > MaxInputPixels)
            throw new InvalidDataException("The image must have valid dimensions and be at most 12 megapixels.");
        if (codec.FrameCount > 1)
            throw new InvalidDataException("Animated images are not supported.");

        using var source = new SKBitmap(sourceWidth, sourceHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        if (codec.GetPixels(source.Info, source.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("The image pixels could not be decoded.");

        var scale = Math.Min(1d, (double)MaxWorkingEdge / Math.Max(sourceWidth, sourceHeight));
        var width = Math.Max(1, Round(sourceWidth * scale));
        var height = Math.Max(1, Round(sourceHeight * scale));
        using var working = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        for (var y = 0; y < height; y++)
        {
            var top = (int)((long)y * sourceHeight / height);
            var bottom = (int)((long)(y + 1) * sourceHeight / height);
            for (var x = 0; x < width; x++)
            {
                var left = (int)((long)x * sourceWidth / width);
                var right = (int)((long)(x + 1) * sourceWidth / width);
                var samples = (bottom - top) * (right - left);
                double red = 0, green = 0, blue = 0;
                for (var sy = top; sy < bottom; sy++)
                {
                    for (var sx = left; sx < right; sx++)
                    {
                        var pixel = source.GetPixel(sx, sy);
                        var alpha = pixel.Alpha / 255d;
                        var white = 255 * (1 - alpha);
                        red += pixel.Red * alpha + white;
                        green += pixel.Green * alpha + white;
                        blue += pixel.Blue * alpha + white;
                    }
                }
                working.SetPixel(x, y, new SKColor(
                    (byte)Round(red / samples), (byte)Round(green / samples), (byte)Round(blue / samples)));
            }
        }

        using var jpeg = working.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
            ?? throw new InvalidDataException("The working JPEG could not be encoded.");
        return new MemoryStream(jpeg.ToArray(), writable: false);
    }

    private static int Round(double value) => (int)Math.Floor(value + 0.5);
}
