using System.Runtime.InteropServices;

namespace TiledImage.Core.Memory;

/// <summary>
/// Pixel buffer backed by an unmanaged memory pointer.
/// Can optionally own the memory (freeing it on dispose).
/// </summary>
public sealed class UnmanagedPixelBuffer : IPixelBuffer
{
    private readonly IntPtr _pointer;
    private readonly bool _ownsMemory;
    private readonly bool _isWritable;
    private bool _disposed;

    /// <inheritdoc/>
    public long Length { get; }

    /// <inheritdoc/>
    public bool IsWritable => _isWritable;

    /// <summary>
    /// Creates a buffer from an existing unmanaged memory pointer.
    /// </summary>
    /// <param name="pointer">Pointer to unmanaged memory</param>
    /// <param name="length">Total length in bytes</param>
    /// <param name="isWritable">Whether write operations are allowed</param>
    /// <param name="ownsMemory">If true, the memory will be freed on dispose using Marshal.FreeHGlobal</param>
    public UnmanagedPixelBuffer(IntPtr pointer, long length, bool isWritable = false, bool ownsMemory = false)
    {
        if (pointer == IntPtr.Zero)
            throw new ArgumentNullException(nameof(pointer));
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _pointer = pointer;
        Length = length;
        _isWritable = isWritable;
        _ownsMemory = ownsMemory;
    }

    /// <summary>
    /// Creates a buffer from an unsafe pointer.
    /// </summary>
    /// <param name="pointer">Pointer to memory</param>
    /// <param name="length">Total length in bytes</param>
    /// <param name="isWritable">Whether write operations are allowed</param>
    /// <param name="ownsMemory">If true, the memory will be freed on dispose</param>
    public unsafe UnmanagedPixelBuffer(byte* pointer, long length, bool isWritable = false, bool ownsMemory = false)
        : this((IntPtr)pointer, length, isWritable, ownsMemory)
    {
    }

    /// <summary>
    /// Allocates a new unmanaged memory buffer.
    /// The buffer owns the allocated memory.
    /// Supports allocations larger than 2GB.
    /// </summary>
    /// <param name="length">Total length in bytes</param>
    /// <param name="zeroFill">If true, the memory will be zero-filled</param>
    /// <returns>A new UnmanagedPixelBuffer</returns>
    public static UnmanagedPixelBuffer Allocate(long length, bool zeroFill = false)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        IntPtr ptr = Marshal.AllocHGlobal((IntPtr)length);

        if (zeroFill)
        {
            NativeMemset(ptr, 0, length);
        }

        return new UnmanagedPixelBuffer(ptr, length, isWritable: true, ownsMemory: true);
    }

    [System.Runtime.InteropServices.DllImport("msvcrt.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern IntPtr memset(IntPtr dest, int c, IntPtr count);

    private static void NativeMemset(IntPtr dest, int value, long count)
    {
        memset(dest, value, (IntPtr)count);
    }

    /// <summary>
    /// Creates a buffer by loading an entire file into unmanaged memory.
    /// Supports files larger than 2GB using chunked reading.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="isWritable">Whether write operations are allowed</param>
    /// <returns>A new UnmanagedPixelBuffer containing the file data</returns>
    public static UnmanagedPixelBuffer CreateFromFile(string filePath, bool isWritable = false)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("File not found", filePath);

        long length = fileInfo.Length;
        IntPtr ptr = Marshal.AllocHGlobal((IntPtr)length);

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ReadStreamToPointer(stream, ptr, length);
            return new UnmanagedPixelBuffer(ptr, length, isWritable, ownsMemory: true);
        }
        catch
        {
            Marshal.FreeHGlobal(ptr);
            throw;
        }
    }

    private static unsafe void ReadStreamToPointer(Stream stream, IntPtr pointer, long length)
    {
        const int bufferSize = 4 * 1024 * 1024; // 4MB buffer
        byte[] buffer = new byte[bufferSize];

        long remaining = length;
        byte* dst = (byte*)pointer;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(bufferSize, remaining);
            int read = stream.Read(buffer, 0, toRead);
            if (read == 0)
                throw new EndOfStreamException("Unexpected end of stream");

            fixed (byte* src = buffer)
            {
                Buffer.MemoryCopy(src, dst, remaining, read);
            }

            dst += read;
            remaining -= read;
        }
    }

    /// <summary>
    /// Creates a buffer by loading a portion of a file into unmanaged memory.
    /// Supports files larger than 2GB using chunked reading.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="offset">Byte offset in the file</param>
    /// <param name="length">Number of bytes to read</param>
    /// <param name="isWritable">Whether write operations are allowed</param>
    /// <returns>A new UnmanagedPixelBuffer containing the file data</returns>
    public static UnmanagedPixelBuffer CreateFromFile(string filePath, long offset, long length, bool isWritable = false)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        IntPtr ptr = Marshal.AllocHGlobal((IntPtr)length);

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.Seek(offset, SeekOrigin.Begin);
            ReadStreamToPointer(stream, ptr, length);
            return new UnmanagedPixelBuffer(ptr, length, isWritable, ownsMemory: true);
        }
        catch
        {
            Marshal.FreeHGlobal(ptr);
            throw;
        }
    }

    /// <inheritdoc/>
    public unsafe byte* AcquirePointer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return (byte*)_pointer;
    }

    /// <inheritdoc/>
    public unsafe void ReleasePointer()
    {
        // No-op for unmanaged memory - pointer is always valid until dispose
    }

    /// <inheritdoc/>
    public unsafe void Read(long offset, byte[] destination, int destinationOffset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (offset < 0 || offset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        byte* src = (byte*)_pointer + offset;
        fixed (byte* dst = &destination[destinationOffset])
        {
            Buffer.MemoryCopy(src, dst, count, count);
        }
    }

    /// <inheritdoc/>
    public unsafe void Write(long offset, byte[] source, int sourceOffset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isWritable)
            throw new InvalidOperationException("Buffer is read-only");

        if (offset < 0 || offset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        byte* dst = (byte*)_pointer + offset;
        fixed (byte* src = &source[sourceOffset])
        {
            Buffer.MemoryCopy(src, dst, count, count);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~UnmanagedPixelBuffer()
    {
        Dispose(disposing: false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsMemory && _pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_pointer);
        }
    }
}
