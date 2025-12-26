using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;
using TiledImage.Transforms.Operations;

namespace TiledImage.Transforms;

/// <summary>
/// Pipeline for chaining multiple image transformations.
/// Executes transforms sequentially, passing output of each step to the next.
/// </summary>
public class TransformPipeline
{
    private readonly List<ITransformStep> _steps = new();

    /// <summary>
    /// Gets the number of steps in the pipeline.
    /// </summary>
    public int StepCount => _steps.Count;

    /// <summary>
    /// Gets whether the pipeline has any steps.
    /// </summary>
    public bool HasSteps => _steps.Count > 0;

    #region Fluent API - Add Steps

    /// <summary>
    /// Add a custom transform step.
    /// </summary>
    public TransformPipeline Add(ITransformStep step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Add a crop transformation.
    /// </summary>
    public TransformPipeline Crop(Rect2L region)
    {
        _steps.Add(new CropTransform(region));
        return this;
    }

    /// <summary>
    /// Add a crop transformation with explicit coordinates.
    /// </summary>
    public TransformPipeline Crop(long x, long y, long width, long height)
    {
        return Crop(new Rect2L(x, y, width, height));
    }

    /// <summary>
    /// Add a horizontal flip transformation.
    /// </summary>
    public TransformPipeline FlipHorizontal()
    {
        _steps.Add(FlipTransform.Horizontal());
        return this;
    }

    /// <summary>
    /// Add a vertical flip transformation.
    /// </summary>
    public TransformPipeline FlipVertical()
    {
        _steps.Add(FlipTransform.Vertical());
        return this;
    }

    /// <summary>
    /// Add a 90-degree clockwise rotation.
    /// </summary>
    public TransformPipeline Rotate90()
    {
        _steps.Add(RotateTransform.Rotate90());
        return this;
    }

    /// <summary>
    /// Add a 180-degree rotation.
    /// </summary>
    public TransformPipeline Rotate180()
    {
        _steps.Add(RotateTransform.Rotate180());
        return this;
    }

    /// <summary>
    /// Add a 270-degree clockwise rotation.
    /// </summary>
    public TransformPipeline Rotate270()
    {
        _steps.Add(RotateTransform.Rotate270());
        return this;
    }

    /// <summary>
    /// Add an arbitrary rotation.
    /// </summary>
    public TransformPipeline Rotate(double degrees, InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        _steps.Add(new RotateTransform(degrees, interpolation));
        return this;
    }

    /// <summary>
    /// Add a scale transformation.
    /// </summary>
    public TransformPipeline Scale(double factor, InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        _steps.Add(new ScaleTransform(factor, interpolation));
        return this;
    }

    /// <summary>
    /// Add an affine transformation using a matrix.
    /// </summary>
    public TransformPipeline Affine(Matrix3x3 matrix, long outputWidth, long outputHeight,
        InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        _steps.Add(new AffineTransform(matrix, outputWidth, outputHeight, interpolation));
        return this;
    }

    /// <summary>
    /// Add a perspective transformation.
    /// </summary>
    public TransformPipeline Perspective(Point2F[] destinationCorners,
        InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        _steps.Add(new PerspectiveTransform(destinationCorners, interpolation));
        return this;
    }

    #endregion

    #region Execution

    /// <summary>
    /// Calculate the final output size after all transformations.
    /// </summary>
    public Size2L CalculateFinalSize(long inputWidth, long inputHeight)
    {
        long width = inputWidth;
        long height = inputHeight;

        foreach (var step in _steps)
        {
            var size = step.CalculateOutputSize(width, height);
            width = size.Width;
            height = size.Height;
        }

        return new Size2L(width, height);
    }

    /// <summary>
    /// Execute all transformations on a single Z slice.
    /// </summary>
    public IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Pipeline has no transformation steps");

        // Execute first step directly on provider
        var currentBuffer = _steps[0].Execute(provider, z, bufferType,
            CreateStepProgress(progress, 0), cancellationToken);

        // Execute remaining steps on intermediate buffers
        for (int i = 1; i < _steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prevSize = _steps[i - 1].CalculateOutputSize(
                i == 1 ? provider.ImageWidth : GetPreviousWidth(i - 1, provider.ImageWidth),
                i == 1 ? provider.ImageHeight : GetPreviousHeight(i - 1, provider.ImageHeight));

            using var tempProvider = CreateProviderFromBuffer(currentBuffer, prevSize, provider);
            var newBuffer = _steps[i].Execute(tempProvider, 0, bufferType,
                CreateStepProgress(progress, i), cancellationToken);

            currentBuffer.Dispose();
            currentBuffer = newBuffer;
        }

        return currentBuffer;
    }

    /// <summary>
    /// Execute all transformations on all Z slices.
    /// </summary>
    public IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Pipeline has no transformation steps");

        long depth = provider.ImageDepth;
        var finalSize = CalculateFinalSize(provider.ImageWidth, provider.ImageHeight);
        int bytesPerPixel = provider.BytesPerPixel;
        long sliceBytes = finalSize.Width * finalSize.Height * bytesPerPixel;
        long totalBytes = sliceBytes * depth;

        var outputBuffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            for (long z = 0; z < depth; z++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var sliceBuffer = Execute(provider, z, bufferType, null, cancellationToken);
                CopySliceToBuffer(sliceBuffer, outputBuffer, z * sliceBytes, sliceBytes);

                progress?.Report((double)(z + 1) / depth);
            }

            return outputBuffer;
        }
        catch
        {
            outputBuffer.Dispose();
            throw;
        }
    }

    #endregion

    #region Helpers

    private IProgress<double>? CreateStepProgress(IProgress<double>? progress, int stepIndex)
    {
        if (progress == null) return null;

        double stepStart = (double)stepIndex / _steps.Count;
        double stepWeight = 1.0 / _steps.Count;

        return new Progress<double>(p => progress.Report(stepStart + p * stepWeight));
    }

    private long GetPreviousWidth(int stepIndex, long originalWidth)
    {
        long width = originalWidth;
        for (int i = 0; i < stepIndex; i++)
        {
            width = _steps[i].CalculateOutputSize(width, 1).Width;
        }
        return width;
    }

    private long GetPreviousHeight(int stepIndex, long originalHeight)
    {
        long height = originalHeight;
        for (int i = 0; i < stepIndex; i++)
        {
            height = _steps[i].CalculateOutputSize(1, height).Height;
        }
        return height;
    }

    private static ITileDataProvider CreateProviderFromBuffer(
        IPixelBuffer buffer, Size2L size, ITileDataProvider originalProvider)
    {
        return new BufferTileProvider(buffer, size.Width, size.Height, 1,
            originalProvider.PixelFormat, originalProvider.BytesPerPixel);
    }

    private static void CopySliceToBuffer(IPixelBuffer source, IPixelBuffer dest, long destOffset, long length)
    {
        const int chunkSize = 1024 * 1024; // 1MB chunks
        byte[] tempBuffer = new byte[Math.Min(chunkSize, length)];

        long remaining = length;
        long srcOffset = 0;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(remaining, tempBuffer.Length);
            source.Read(srcOffset, tempBuffer, 0, toRead);
            dest.Write(destOffset + srcOffset, tempBuffer, 0, toRead);

            srcOffset += toRead;
            remaining -= toRead;
        }
    }

    #endregion
}

/// <summary>
/// Simple tile provider that wraps a pixel buffer for pipeline intermediate steps.
/// </summary>
internal sealed class BufferTileProvider : ITileDataProvider
{
    private readonly IPixelBuffer _buffer;
    private readonly long _width;
    private readonly long _height;
    private readonly long _depth;
    private readonly PixelFormat _pixelFormat;
    private readonly int _bytesPerPixel;

    public BufferTileProvider(IPixelBuffer buffer, long width, long height, long depth,
        PixelFormat pixelFormat, int bytesPerPixel)
    {
        _buffer = buffer;
        _width = width;
        _height = height;
        _depth = depth;
        _pixelFormat = pixelFormat;
        _bytesPerPixel = bytesPerPixel;
    }

    public long ImageWidth => _width;
    public long ImageHeight => _height;
    public long ImageDepth => _depth;
    public PixelFormat PixelFormat => _pixelFormat;
    public int BytesPerPixel => _bytesPerPixel;
    public bool IsReady => true;

    public void ReadTileData(long pixelX, long pixelY, long z, int tileWidth, int tileHeight, byte[] buffer)
    {
        ReadPixels(pixelX, pixelY, z, tileWidth, tileHeight, buffer);
    }

    public void ReadPixels(long x, long y, long z, int width, int height, byte[] buffer)
    {
        long sliceOffset = z * _width * _height * _bytesPerPixel;
        int stride = width * _bytesPerPixel;

        for (int row = 0; row < height; row++)
        {
            long srcOffset = sliceOffset + ((y + row) * _width + x) * _bytesPerPixel;
            int destOffset = row * stride;
            _buffer.Read(srcOffset, buffer, destOffset, stride);
        }
    }

    public void Dispose()
    {
        // Don't dispose the buffer - it's owned by the caller
    }
}
