namespace TiledImage.Core.Memory;

/// <summary>
/// Abstraction for pixel data memory access.
/// Supports both memory-mapped files and unmanaged memory pointers.
/// </summary>
public interface IPixelBuffer : IDisposable
{
    /// <summary>
    /// Total size of the buffer in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Whether the buffer supports write operations.
    /// </summary>
    bool IsWritable { get; }

    /// <summary>
    /// Acquires a pointer to the buffer memory.
    /// Must call ReleasePointer when done.
    /// </summary>
    /// <returns>Pointer to the buffer memory</returns>
    unsafe byte* AcquirePointer();

    /// <summary>
    /// Releases the pointer acquired by AcquirePointer.
    /// </summary>
    unsafe void ReleasePointer();

    /// <summary>
    /// Reads data from the buffer at the specified offset.
    /// </summary>
    /// <param name="offset">Byte offset in the buffer</param>
    /// <param name="destination">Destination buffer</param>
    /// <param name="destinationOffset">Offset in destination buffer</param>
    /// <param name="count">Number of bytes to read</param>
    void Read(long offset, byte[] destination, int destinationOffset, int count);

    /// <summary>
    /// Writes data to the buffer at the specified offset.
    /// Throws InvalidOperationException if IsWritable is false.
    /// </summary>
    /// <param name="offset">Byte offset in the buffer</param>
    /// <param name="source">Source buffer</param>
    /// <param name="sourceOffset">Offset in source buffer</param>
    /// <param name="count">Number of bytes to write</param>
    void Write(long offset, byte[] source, int sourceOffset, int count);
}
