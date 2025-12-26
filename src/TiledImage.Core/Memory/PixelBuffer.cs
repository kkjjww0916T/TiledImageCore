namespace TiledImage.Core.Memory;

/// <summary>
/// Static helper for creating pixel buffers based on BufferType.
/// </summary>
public static class PixelBuffer
{
    /// <summary>
    /// Creates a pixel buffer from a file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="bufferType">Type of buffer to create</param>
    /// <param name="isWritable">Whether the buffer should be writable</param>
    /// <returns>A pixel buffer containing the file data</returns>
    public static IPixelBuffer CreateFromFile(string filePath, BufferType bufferType, bool isWritable = false)
        => bufferType switch
        {
            BufferType.MemoryMapped => MemoryMappedPixelBuffer.CreateFromFile(filePath, isWritable),
            BufferType.Unmanaged => UnmanagedPixelBuffer.CreateFromFile(filePath, isWritable),
            _ => throw new ArgumentOutOfRangeException(nameof(bufferType))
        };

    /// <summary>
    /// Creates a temporary pixel buffer of the specified size.
    /// </summary>
    /// <param name="size">Size in bytes</param>
    /// <param name="bufferType">Type of buffer to create</param>
    /// <param name="isWritable">Whether the buffer should be writable</param>
    /// <returns>A pixel buffer of the specified size</returns>
    public static IPixelBuffer CreateTemp(long size, BufferType bufferType, bool isWritable = true)
        => bufferType switch
        {
            BufferType.MemoryMapped => MemoryMappedPixelBuffer.CreateTemp(size, isWritable),
            BufferType.Unmanaged => UnmanagedPixelBuffer.Allocate(size, zeroFill: false),
            _ => throw new ArgumentOutOfRangeException(nameof(bufferType))
        };
}
