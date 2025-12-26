using TiledImage.Core.IO;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;
using SkiaSharp;

namespace TiledImage.Formats.SkiaSharp.IO;

/// <summary>
/// Image loader using SkiaSharp for decoding.
/// Supports common formats: JPEG, PNG, BMP, GIF, WebP, etc.
/// </summary>
public class SkiaSharpImageLoader : IImageLoader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".ico", ".wbmp", ".pkm", ".ktx", ".astc", ".dng", ".heif"
    };

    /// <inheritdoc/>
    public BufferType BufferType { get; set; } = BufferType.Unmanaged;

    /// <inheritdoc/>
    public bool CanLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var extension = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(extension);
    }

    /// <inheritdoc/>
    public Task<TiledImageSource> LoadAsync(string filePath, int tileSize = 512, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(filePath, tileSize, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Synchronously load an image file.
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <param name="tileSize">Tile size (default 512)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TiledImageSource ready for use with control</returns>
    public TiledImageSource Load(string filePath, int tileSize = 512, CancellationToken cancellationToken = default)
    {
        using var codec = SKCodec.Create(filePath);
        if (codec == null)
            throw new InvalidOperationException($"Cannot decode image: {filePath}");

        cancellationToken.ThrowIfCancellationRequested();

        var info = codec.Info;
        int width = info.Width;
        int height = info.Height;

        // Always decode to BGRA32 for consistency (standard format for rendering)
        const PixelFormat pixelFormat = PixelFormat.Bgra32;
        int bytesPerPixel = pixelFormat.GetBytesPerPixel();
        long totalBytes = (long)width * height * bytesPerPixel;

        var bitmapInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        // Create buffer using configured buffer type
        var buffer = PixelBuffer.CreateTemp(totalBytes, BufferType, isWritable: true);

        try
        {
            unsafe
            {
                byte* ptr = buffer.AcquirePointer();
                try
                {
                    var result = codec.GetPixels(bitmapInfo, (IntPtr)ptr);
                    if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                    {
                        throw new InvalidOperationException($"Failed to decode image: {result}");
                    }
                }
                finally
                {
                    buffer.ReleasePointer();
                }
            }

            var provider = new RawTileProvider(width, height, 1, pixelFormat, buffer);
            return new TiledImageSource(provider, tileSize);
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }
}
