using System.IO.MemoryMappedFiles;
using TiledImage.Core.IO;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;
using SkiaSharp;

namespace TiledImage.Formats.SkiaSharp.IO;

/// <summary>
/// Image exporter implementation using SkiaSharp.
/// Encodes and saves TiledImageSource to image files.
/// Uses memory-mapped files for large images to avoid managed heap allocation issues.
/// </summary>
public class SkiaSharpImageExporter : IImageExporter
{
    // Threshold for using memory-mapped approach (100MB)
    private const long LargeImageThreshold = 100 * 1024 * 1024;

    /// <inheritdoc/>
    public async Task ExportAsync(
        TiledImageSource source,
        string filePath,
        ImageFormat format,
        int quality = 95)
    {
        if (source == null || !source.IsLoaded)
            throw new InvalidOperationException("No image loaded");

        await Task.Run(() =>
        {
            int width = (int)source.ImageWidth;
            int height = (int)source.ImageHeight;
            long totalBytes = (long)width * height * 4;

            if (totalBytes > LargeImageThreshold)
            {
                ExportLargeImage(source, filePath, format, quality, width, height, totalBytes);
            }
            else
            {
                ExportSmallImage(source, filePath, format, quality, width, height);
            }
        });
    }

    private static void ExportSmallImage(
        TiledImageSource source,
        string filePath,
        ImageFormat format,
        int quality,
        int width,
        int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        RenderAllTiles(canvas, source);

        using var image = SKImage.FromBitmap(bitmap);
        if (image == null)
            throw new InvalidOperationException("Failed to create image from bitmap");

        using var data = image.Encode(ToSkiaFormat(format), quality);
        if (data == null)
            throw new InvalidOperationException($"Failed to encode image to {format}. Image may be too large.");

        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    private static void ExportLargeImage(
        TiledImageSource source,
        string filePath,
        ImageFormat format,
        int quality,
        int width,
        int height,
        long totalBytes)
    {
        // Use temp file for memory-mapped pixel storage
        string tempFilePath = Path.GetTempFileName();

        try
        {
            // Create memory-mapped file for raw pixels
            using var mmf = MemoryMappedFile.CreateFromFile(
                tempFilePath,
                FileMode.Create,
                null,
                totalBytes,
                MemoryMappedFileAccess.ReadWrite);

            using var accessor = mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

            // Render tiles directly to memory-mapped file
            RenderTilesToMemoryMap(source, accessor, width, height);

            // Encode directly from memory-mapped file using SKPixmap
            unsafe
            {
                byte* ptr = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                try
                {
                    var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var pixmap = new SKPixmap(info, (IntPtr)ptr, width * 4);

                    using var fileStream = File.OpenWrite(filePath);
                    using var skStream = new SKManagedWStream(fileStream);

                    bool success = pixmap.Encode(skStream, ToSkiaFormat(format), quality);
                    if (!success)
                        throw new InvalidOperationException($"Failed to encode image to {format}. Image may be too large.");
                }
                finally
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }
        finally
        {
            // Clean up temp file
            try { File.Delete(tempFilePath); } catch { }
        }
    }

    private static void RenderTilesToMemoryMap(
        TiledImageSource source,
        MemoryMappedViewAccessor accessor,
        int width,
        int height)
    {
        int tileSize = source.TileSize;
        int bytesPerPixel = source.BytesPerPixel;

        for (long tileY = 0; tileY < source.TilesY; tileY++)
        {
            for (long tileX = 0; tileX < source.TilesX; tileX++)
            {
                long pixelX = tileX * tileSize;
                long pixelY = tileY * tileSize;

                int tileWidth = (int)Math.Min(tileSize, source.ImageWidth - pixelX);
                int tileHeight = (int)Math.Min(tileSize, source.ImageHeight - pixelY);

                var tileData = source.GetTileData(tileX, tileY);
                if (tileData == null) continue;

                // Write tile data row by row to memory-mapped file
                for (int row = 0; row < tileHeight; row++)
                {
                    long destOffset = ((pixelY + row) * width + pixelX) * bytesPerPixel;
                    int srcOffset = row * tileWidth * bytesPerPixel;
                    int rowBytes = tileWidth * bytesPerPixel;

                    accessor.WriteArray(destOffset, tileData, srcOffset, rowBytes);
                }
            }
        }
    }

    private static void RenderAllTiles(SKCanvas canvas, TiledImageSource source)
    {
        int tileSize = source.TileSize;
        int bytesPerPixel = source.BytesPerPixel;

        for (long tileY = 0; tileY < source.TilesY; tileY++)
        {
            for (long tileX = 0; tileX < source.TilesX; tileX++)
            {
                long pixelX = tileX * tileSize;
                long pixelY = tileY * tileSize;

                int tileWidth = (int)Math.Min(tileSize, source.ImageWidth - pixelX);
                int tileHeight = (int)Math.Min(tileSize, source.ImageHeight - pixelY);

                var tileData = source.GetTileData(tileX, tileY);
                if (tileData == null) continue;

                var info = new SKImageInfo(tileWidth, tileHeight, SKColorType.Bgra8888, SKAlphaType.Premul);

                unsafe
                {
                    fixed (byte* ptr = tileData)
                    {
                        using var tileBitmap = new SKBitmap();
                        tileBitmap.InstallPixels(info, (IntPtr)ptr, tileWidth * bytesPerPixel);
                        canvas.DrawBitmap(tileBitmap, (float)pixelX, (float)pixelY);
                    }
                }
            }
        }
    }

    private static SKEncodedImageFormat ToSkiaFormat(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
        ImageFormat.Webp => SKEncodedImageFormat.Webp,
        ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
        ImageFormat.Gif => SKEncodedImageFormat.Gif,
        _ => SKEncodedImageFormat.Png
    };
}
