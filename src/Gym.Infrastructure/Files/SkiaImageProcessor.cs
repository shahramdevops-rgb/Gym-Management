using Gym.Application.Common.Files;
using Gym.Domain.Common;

using SkiaSharp;

namespace Gym.Infrastructure.Files;

/// <summary>
/// <see cref="IImageProcessor"/> with SkiaSharp (16.3, ADR 0010): decodes, turns upright, resizes and
/// writes WebP. Only pixels are re-encoded, so no EXIF, IPTC or XMP data reaches the new file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three formats.</b> The codec names the format from the file's own bytes, and anything but
/// JPEG, PNG or WebP is refused before a pixel is decoded.
/// </para>
/// <para>
/// <b>Size before pixels.</b> The header is read first and a photo above <see cref="MaxPixels"/> is
/// refused, so a small file that claims an enormous canvas cannot fill the server's 2 GB of memory.
/// A JPEG is then decoded at a reduced scale when it is far larger than needed.
/// </para>
/// <para>
/// <b>Native memory.</b> Skia's bitmaps live outside the .NET heap, where the garbage collector does
/// not see their size; every one is disposed as soon as it is done with.
/// </para>
/// </remarks>
public sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>About 50 megapixels: more than any phone's normal photo.</summary>
    public const long MaxPixels = 50_000_000;

    private const int LargeQuality = 82;

    private const int SmallQuality = 75;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    public async Task<Result<ProcessedImage>> ProcessAsync(Stream input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > IImageProcessor.MaxUploadBytes)
        {
            return Result.Failure<ProcessedImage>(PhotoErrors.TooLarge);
        }

        return Process(buffer.ToArray());
    }

    private static Result<ProcessedImage> Process(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null ||
            codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp) ||
            codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return Result.Failure<ProcessedImage>(PhotoErrors.Unreadable);
        }

        using var decoded = Decode(codec);
        if (decoded is null)
        {
            return Result.Failure<ProcessedImage>(PhotoErrors.Unreadable);
        }

        using var upright = Orient(decoded, codec.EncodedOrigin);
        using var large = FitWithin(upright, IImageProcessor.LargeSide);
        using var small = FitWithin(large, IImageProcessor.SmallSide);

        return new ProcessedImage(
            Encode(large, LargeQuality),
            Encode(small, SmallQuality),
            large.Width,
            large.Height);
    }

    /// <summary>
    /// Decodes the first frame, at a reduced scale when the codec can do it and the result still
    /// covers the large side; null when the bytes are broken or cut short.
    /// </summary>
    private static SKBitmap? Decode(SKCodec codec)
    {
        var size = codec.Info.Size;
        var longSide = Math.Max(size.Width, size.Height);
        if (longSide > IImageProcessor.LargeSide)
        {
            var scaled = codec.GetScaledDimensions((float)IImageProcessor.LargeSide / longSide);
            if (Math.Max(scaled.Width, scaled.Height) >= IImageProcessor.LargeSide)
            {
                size = scaled;
            }
        }

        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            return null;
        }

        return bitmap;
    }

    /// <summary>
    /// Draws the photo the way the phone meant it to be seen: a phone often saves the pixels sideways
    /// and only notes the turn in EXIF, which the new file no longer carries.
    /// </summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var turned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = turned ? source.Height : source.Width;
        var height = turned ? source.Width : source.Height;

        var target = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(target);

        // The canvas applies the last call first to what it draws.
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // mirrored
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight: // upside down
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft: // mirrored upside down
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop: // transposed
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop: // a quarter turn clockwise
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // transversed
                canvas.Translate(width, height);
                canvas.RotateDegrees(-90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftBottom: // a quarter turn counter-clockwise
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
            default: // upright already
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        return target;
    }

    /// <summary>A copy that fits a square of <paramref name="side"/>; never enlarged.</summary>
    private static SKBitmap FitWithin(SKBitmap source, int side)
    {
        var longSide = Math.Max(source.Width, source.Height);
        if (longSide <= side)
        {
            return source.Copy();
        }

        var scale = (double)side / longSide;
        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)),
            source.ColorType,
            source.AlphaType);

        return source.Resize(info, Sampling)
            ?? throw new InvalidOperationException("Skia could not resize the photo.");
    }

    private static byte[] Encode(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality)
            ?? throw new InvalidOperationException("Skia could not encode the photo as WebP.");
        return encoded.ToArray();
    }
}
