using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;
using TiledImage.Transforms.Sampling;

namespace TiledImage.Transforms.Operations;

/// <summary>
/// Perspective transformation using homography matrix.
/// Maps quadrilateral to quadrilateral using Direct Linear Transform (DLT) algorithm.
/// </summary>
public sealed class PerspectiveTransform : ITransformStep
{
    private const int TileSize = 512;

    private readonly Point2F[] _destinationCorners;
    private readonly InterpolationMode _interpolation;

    public PerspectiveTransform(Point2F[] destinationCorners,
        InterpolationMode interpolation = InterpolationMode.Bilinear)
    {
        if (destinationCorners.Length != 4)
            throw new ArgumentException("Must provide exactly 4 destination corners", nameof(destinationCorners));
        _destinationCorners = destinationCorners;
        _interpolation = interpolation;
    }

    #region ITransformStep Implementation

    public Size2L CalculateOutputSize(long inputWidth, long inputHeight)
        => CalculateOutputSize(_destinationCorners);

    public IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var transform = Compute(provider.ImageWidth, provider.ImageHeight, _destinationCorners);
        var outputSize = CalculateOutputSize(_destinationCorners);
        return ExecuteInternal(provider, transform, outputSize.Width, outputSize.Height,
            z, bufferType, progress, cancellationToken);
    }

    public IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var transform = Compute(provider.ImageWidth, provider.ImageHeight, _destinationCorners);
        var outputSize = CalculateOutputSize(_destinationCorners);
        return ExecuteAllInternal(provider, transform, outputSize.Width, outputSize.Height,
            bufferType, progress, cancellationToken);
    }

    #endregion

    #region Static Compute Methods

    /// <summary>
    /// Compute perspective transform from source corners to destination corners.
    /// </summary>
    public static PerspectiveTransformResult Compute(Point2F[] sourceCorners, Point2F[] destinationCorners)
    {
        if (sourceCorners.Length != 4)
            throw new ArgumentException("Must provide exactly 4 source corners", nameof(sourceCorners));
        if (destinationCorners.Length != 4)
            throw new ArgumentException("Must provide exactly 4 destination corners", nameof(destinationCorners));

        var forward = ComputeHomography(sourceCorners, destinationCorners);

        Matrix3x3 inverse;
        if (!forward.TryInvert(out inverse))
        {
            inverse = Matrix3x3.Identity;
        }

        return new PerspectiveTransformResult(forward, inverse, sourceCorners, destinationCorners);
    }

    /// <summary>
    /// Compute perspective transform for an image rectangle to destination corners.
    /// </summary>
    public static PerspectiveTransformResult Compute(long imageWidth, long imageHeight, Point2F[] destinationCorners)
    {
        var sourceCorners = new Point2F[]
        {
            new(0, 0),
            new(imageWidth, 0),
            new(imageWidth, imageHeight),
            new(0, imageHeight)
        };

        return Compute(sourceCorners, destinationCorners);
    }

    /// <summary>
    /// Compute homography matrix that maps source points to destination points.
    /// Uses Direct Linear Transform (DLT) algorithm.
    /// </summary>
    public static Matrix3x3 ComputeHomography(Point2F[] src, Point2F[] dst)
    {
        if (src.Length != 4 || dst.Length != 4)
            throw new ArgumentException("Must provide exactly 4 points for each quadrilateral");

        double[] A = new double[64];
        double[] b = new double[8];

        for (int i = 0; i < 4; i++)
        {
            double sx = src[i].X;
            double sy = src[i].Y;
            double dx = dst[i].X;
            double dy = dst[i].Y;

            int row1 = 2 * i;
            A[row1 * 8 + 0] = sx;
            A[row1 * 8 + 1] = sy;
            A[row1 * 8 + 2] = 1;
            A[row1 * 8 + 3] = 0;
            A[row1 * 8 + 4] = 0;
            A[row1 * 8 + 5] = 0;
            A[row1 * 8 + 6] = -sx * dx;
            A[row1 * 8 + 7] = -sy * dx;
            b[row1] = dx;

            int row2 = 2 * i + 1;
            A[row2 * 8 + 0] = 0;
            A[row2 * 8 + 1] = 0;
            A[row2 * 8 + 2] = 0;
            A[row2 * 8 + 3] = sx;
            A[row2 * 8 + 4] = sy;
            A[row2 * 8 + 5] = 1;
            A[row2 * 8 + 6] = -sx * dy;
            A[row2 * 8 + 7] = -sy * dy;
            b[row2] = dy;
        }

        double[] h = SolveLinearSystem(A, b, 8);

        return new Matrix3x3(
            (float)h[0], (float)h[1], (float)h[2],
            (float)h[3], (float)h[4], (float)h[5],
            (float)h[6], (float)h[7], 1.0f
        );
    }

    #endregion

    #region Static Convenience Methods

    /// <summary>
    /// Execute perspective transformation on a single Z slice.
    /// </summary>
    public static IPixelBuffer Execute(
        ITileDataProvider provider,
        Point2F[] destinationCorners,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new PerspectiveTransform(destinationCorners, interpolation)
            .Execute(provider, z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Execute perspective transformation on a single Z slice using pre-computed transform.
    /// </summary>
    public static IPixelBuffer Execute(
        ITileDataProvider provider,
        PerspectiveTransformResult transform,
        long outputWidth,
        long outputHeight,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var perspectiveTransform = new PerspectiveTransform(transform.DestinationCorners, interpolation);
        return perspectiveTransform.ExecuteInternal(provider, transform, outputWidth, outputHeight,
            z, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Execute perspective transformation on all Z slices.
    /// </summary>
    public static IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        Point2F[] destinationCorners,
        InterpolationMode interpolation = InterpolationMode.Bilinear,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return new PerspectiveTransform(destinationCorners, interpolation)
            .ExecuteAll(provider, bufferType, progress, cancellationToken);
    }

    /// <summary>
    /// Calculate the output size (bounding box) of the destination quadrilateral.
    /// </summary>
    public static Size2L CalculateOutputSize(Point2F[] destinationCorners)
    {
        if (destinationCorners.Length != 4)
            throw new ArgumentException("Must provide exactly 4 corners", nameof(destinationCorners));

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var corner in destinationCorners)
        {
            minX = MathF.Min(minX, corner.X);
            minY = MathF.Min(minY, corner.Y);
            maxX = MathF.Max(maxX, corner.X);
            maxY = MathF.Max(maxY, corner.Y);
        }

        long width = (long)MathF.Ceiling(maxX - minX);
        long height = (long)MathF.Ceiling(maxY - minY);

        return new Size2L(width, height);
    }

    #endregion

    #region Static Utility Methods

    /// <summary>
    /// Transform a point using the homography matrix.
    /// </summary>
    public static Point2F TransformPoint(Matrix3x3 matrix, Point2F point)
    {
        return matrix.MapPoint(point);
    }

    /// <summary>
    /// Calculate the area of a quadrilateral.
    /// </summary>
    public static float CalculateQuadArea(Point2F[] corners)
    {
        if (corners.Length != 4)
            throw new ArgumentException("Must provide exactly 4 corners", nameof(corners));

        float area = 0;
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            area += corners[i].X * corners[j].Y;
            area -= corners[j].X * corners[i].Y;
        }
        return MathF.Abs(area) / 2f;
    }

    /// <summary>
    /// Calculate the scale factor between source and destination quadrilaterals.
    /// </summary>
    public static float CalculateScaleFactor(Point2F[] sourceCorners, Point2F[] destinationCorners)
    {
        float srcArea = CalculateQuadArea(sourceCorners);
        float dstArea = CalculateQuadArea(destinationCorners);

        if (srcArea <= 0) return 1f;

        return MathF.Sqrt(dstArea / srcArea);
    }

    /// <summary>
    /// Check if a point is inside a quadrilateral.
    /// </summary>
    public static bool IsPointInQuad(Point2F[] quad, Point2F point)
    {
        if (quad.Length != 4)
            throw new ArgumentException("Must provide exactly 4 corners", nameof(quad));

        bool sign = false;
        for (int i = 0; i < 4; i++)
        {
            var p1 = quad[i];
            var p2 = quad[(i + 1) % 4];

            float cross = (p2.X - p1.X) * (point.Y - p1.Y) - (p2.Y - p1.Y) * (point.X - p1.X);

            if (i == 0)
            {
                sign = cross > 0;
            }
            else if ((cross > 0) != sign)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Find the corner index closest to the given point within the threshold.
    /// </summary>
    /// <returns>Corner index (0-3) or -1 if no corner is within threshold.</returns>
    public static int GetCornerAtPoint(Point2F[] corners, Point2F point, float threshold)
    {
        if (corners.Length != 4)
            throw new ArgumentException("Must provide exactly 4 corners", nameof(corners));

        int closestCorner = -1;
        float minDistance = threshold;

        for (int i = 0; i < 4; i++)
        {
            float distance = corners[i].DistanceTo(point);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestCorner = i;
            }
        }

        return closestCorner;
    }

    #endregion

    #region Internal Implementation

    private IPixelBuffer ExecuteInternal(
        ITileDataProvider provider,
        PerspectiveTransformResult transform,
        long outputWidth,
        long outputHeight,
        long z,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        int bytesPerPixel = provider.BytesPerPixel;
        long totalBytes = outputWidth * outputHeight * bytesPerPixel;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            ResampleSlice(provider, buffer, transform, outputWidth, outputHeight,
                bytesPerPixel, z, 0, progress, cancellationToken);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private IPixelBuffer ExecuteAllInternal(
        ITileDataProvider provider,
        PerspectiveTransformResult transform,
        long outputWidth,
        long outputHeight,
        BufferType bufferType,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        long depth = provider.ImageDepth;
        int bytesPerPixel = provider.BytesPerPixel;
        long sliceBytes = outputWidth * outputHeight * bytesPerPixel;
        long totalBytes = sliceBytes * depth;

        var buffer = PixelBuffer.CreateTemp(totalBytes, bufferType, isWritable: true);

        try
        {
            for (long zSlice = 0; zSlice < depth; zSlice++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long sliceOffset = zSlice * sliceBytes;
                ResampleSlice(provider, buffer, transform, outputWidth, outputHeight,
                    bytesPerPixel, zSlice, sliceOffset);

                progress?.Report((double)(zSlice + 1) / depth);
            }

            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private void ResampleSlice(
        ITileDataProvider provider,
        IPixelBuffer outputBuffer,
        PerspectiveTransformResult transform,
        long outputWidth,
        long outputHeight,
        int bytesPerPixel,
        long z,
        long bufferOffset,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int tilesX = (int)Math.Ceiling((double)outputWidth / TileSize);
        int tilesY = (int)Math.Ceiling((double)outputHeight / TileSize);
        int totalTiles = tilesX * tilesY;
        int completedTiles = 0;

        Parallel.For(0, totalTiles, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, tileIndex =>
        {
            int tileX = tileIndex % tilesX;
            int tileY = tileIndex / tilesX;

            int outX = tileX * TileSize;
            int outY = tileY * TileSize;
            int outTileWidth = (int)Math.Min(TileSize, outputWidth - outX);
            int outTileHeight = (int)Math.Min(TileSize, outputHeight - outY);

            if (outTileWidth <= 0 || outTileHeight <= 0) return;

            int stride = outTileWidth * bytesPerPixel;
            byte[] tileBuffer = new byte[outTileWidth * outTileHeight * bytesPerPixel];

            var sampler = new TilePixelSampler(provider, z);

            PixelResampler.ResampleRegion(
                sampler,
                transform.Inverse,
                tileBuffer,
                outX,
                outY,
                outTileWidth,
                outTileHeight,
                stride,
                bytesPerPixel,
                provider.PixelFormat,
                _interpolation);

            for (int row = 0; row < outTileHeight; row++)
            {
                long destOffset = bufferOffset + ((outY + row) * outputWidth + outX) * bytesPerPixel;
                int srcOffset = row * stride;
                outputBuffer.Write(destOffset, tileBuffer, srcOffset, stride);
            }

            int completed = Interlocked.Increment(ref completedTiles);
            progress?.Report((double)completed / totalTiles);
        });
    }

    #endregion

    #region Linear Algebra

    private static double[] SolveLinearSystem(double[] A, double[] b, int n)
    {
        double[,] aug = new double[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                aug[i, j] = A[i * n + j];
            }
            aug[i, n] = b[i];
        }

        for (int col = 0; col < n; col++)
        {
            int maxRow = col;
            double maxVal = Math.Abs(aug[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                if (Math.Abs(aug[row, col]) > maxVal)
                {
                    maxVal = Math.Abs(aug[row, col]);
                    maxRow = row;
                }
            }

            if (maxRow != col)
            {
                for (int j = 0; j <= n; j++)
                {
                    (aug[col, j], aug[maxRow, j]) = (aug[maxRow, j], aug[col, j]);
                }
            }

            double pivot = aug[col, col];
            if (Math.Abs(pivot) < 1e-10)
            {
                continue;
            }

            for (int row = col + 1; row < n; row++)
            {
                double factor = aug[row, col] / pivot;
                for (int j = col; j <= n; j++)
                {
                    aug[row, j] -= factor * aug[col, j];
                }
            }
        }

        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = aug[i, n];
            for (int j = i + 1; j < n; j++)
            {
                sum -= aug[i, j] * x[j];
            }
            if (Math.Abs(aug[i, i]) > 1e-10)
            {
                x[i] = sum / aug[i, i];
            }
            else
            {
                x[i] = 0;
            }
        }

        return x;
    }

    #endregion
}
