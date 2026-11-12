using Gym.Domain.Common;

namespace Gym.Application.Common.Files;

/// <summary>
/// Re-encodes an uploaded photo (BUSINESS_RULES.md §16 <i>Products</i>, ADR 0010): only JPEG, PNG
/// and WebP are read; the result is WebP without any EXIF data (a phone photo can carry where it was
/// taken), turned upright as the phone meant it, at most <see cref="LargeSide"/> pixels on its long
/// side, plus a <see cref="SmallSide"/>-pixel copy for lists.
/// </summary>
public interface IImageProcessor
{
    public const int LargeSide = 1600;

    public const int SmallSide = 480;

    /// <summary>The largest upload accepted, 10 MB (§16).</summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>
    /// <see cref="PhotoErrors.Unreadable"/> when the bytes are not a JPEG, PNG or WebP photo this
    /// can decode.
    /// </summary>
    Task<Result<ProcessedImage>> ProcessAsync(Stream input, CancellationToken cancellationToken);
}

/// <summary>The re-encoded photo: both files' bytes and the large one's size in pixels.</summary>
public sealed record ProcessedImage(byte[] Large, byte[] Small, int Width, int Height);

public static class PhotoErrors
{
    public static readonly Error Unreadable = Error.Validation(
        "Photos.Unreadable",
        "The file is not a JPEG, PNG or WebP photo that can be read.");

    public static readonly Error TooLarge = Error.Validation(
        "Photos.TooLarge",
        "A photo is at most 10 MB.");

    public static readonly Error Required = Error.Validation(
        "Photos.Required",
        "No photo was sent.");
}
