using Geometry.Primitives;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;

namespace TiledImage.Transforms;

/// <summary>
/// Interface for a single transformation step in a pipeline.
/// Each step transforms pixels and reports its output dimensions.
/// </summary>
public interface ITransformStep
{
    /// <summary>
    /// Calculate the output size after this transformation.
    /// </summary>
    /// <param name="inputWidth">Input image width.</param>
    /// <param name="inputHeight">Input image height.</param>
    /// <returns>Output dimensions after transformation.</returns>
    Size2L CalculateOutputSize(long inputWidth, long inputHeight);

    /// <summary>
    /// Execute the transformation on a single Z slice.
    /// </summary>
    /// <param name="provider">Source data provider.</param>
    /// <param name="z">Z slice index.</param>
    /// <param name="bufferType">Output buffer type.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transformed pixel buffer.</returns>
    IPixelBuffer Execute(
        ITileDataProvider provider,
        long z = 0,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute the transformation on all Z slices.
    /// </summary>
    IPixelBuffer ExecuteAll(
        ITileDataProvider provider,
        BufferType bufferType = BufferType.Unmanaged,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
