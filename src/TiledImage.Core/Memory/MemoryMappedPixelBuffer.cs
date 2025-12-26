using System.IO.MemoryMappedFiles;

namespace TiledImage.Core.Memory;

/// <summary>
/// Pixel buffer backed by a memory-mapped file.
/// Owns the MemoryMappedFile and accessor, disposing them on cleanup.
/// </summary>
public sealed class MemoryMappedPixelBuffer : IPixelBuffer
{
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly string? _tempFilePath;
    private readonly bool _isWritable;
    private bool _disposed;

    /// <inheritdoc/>
    public long Length { get; }

    /// <inheritdoc/>
    public bool IsWritable => _isWritable;

    /// <summary>
    /// Creates a buffer from an existing memory-mapped file.
    /// Takes ownership of the resources.
    /// </summary>
    /// <param name="memoryMappedFile">The memory-mapped file</param>
    /// <param name="accessor">The view accessor</param>
    /// <param name="length">Total length in bytes</param>
    /// <param name="isWritable">Whether write operations are allowed</param>
    /// <param name="tempFilePath">Optional temp file path for cleanup</param>
    public MemoryMappedPixelBuffer(
        MemoryMappedFile memoryMappedFile,
        MemoryMappedViewAccessor accessor,
        long length,
        bool isWritable = false,
        string? tempFilePath = null)
    {
        _memoryMappedFile = memoryMappedFile ?? throw new ArgumentNullException(nameof(memoryMappedFile));
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        Length = length;
        _isWritable = isWritable;
        _tempFilePath = tempFilePath;
    }

    /// <summary>
    /// Creates a new memory-mapped buffer backed by a temporary file.
    /// </summary>
    /// <param name="length">Total length in bytes</param>
    /// <param name="isWritable">Whether write operations are allowed</param>
    /// <returns>A new MemoryMappedPixelBuffer</returns>
    public static MemoryMappedPixelBuffer CreateTemp(long length, bool isWritable = true)
    {
        string tempFilePath = Path.GetTempFileName();
        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? accessor = null;

        try
        {
            var access = isWritable ? MemoryMappedFileAccess.ReadWrite : MemoryMappedFileAccess.Read;

            mmf = MemoryMappedFile.CreateFromFile(
                tempFilePath,
                FileMode.Create,
                null,
                length,
                MemoryMappedFileAccess.ReadWrite);

            accessor = mmf.CreateViewAccessor(0, length, access);

            return new MemoryMappedPixelBuffer(mmf, accessor, length, isWritable, tempFilePath);
        }
        catch
        {
            accessor?.Dispose();
            mmf?.Dispose();
            try { File.Delete(tempFilePath); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Creates a memory-mapped buffer from an existing file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="isWritable">Whether write operations are allowed</param>
    /// <returns>A new MemoryMappedPixelBuffer</returns>
    public static MemoryMappedPixelBuffer CreateFromFile(string filePath, bool isWritable = false)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("File not found", filePath);

        long length = fileInfo.Length;
        var fileMode = isWritable ? FileMode.Open : FileMode.Open;
        var fileAccess = isWritable ? FileAccess.ReadWrite : FileAccess.Read;
        var mmfAccess = isWritable ? MemoryMappedFileAccess.ReadWrite : MemoryMappedFileAccess.Read;

        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? accessor = null;

        try
        {
            using var fileStream = new FileStream(filePath, fileMode, fileAccess, FileShare.Read);
            mmf = MemoryMappedFile.CreateFromFile(
                fileStream,
                null,
                0,
                mmfAccess,
                HandleInheritability.None,
                leaveOpen: false);

            accessor = mmf.CreateViewAccessor(0, length, mmfAccess);

            return new MemoryMappedPixelBuffer(mmf, accessor, length, isWritable, tempFilePath: null);
        }
        catch
        {
            accessor?.Dispose();
            mmf?.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public unsafe byte* AcquirePointer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        return ptr;
    }

    /// <inheritdoc/>
    public unsafe void ReleasePointer()
    {
        if (!_disposed)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    /// <inheritdoc/>
    public void Read(long offset, byte[] destination, int destinationOffset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (offset < 0 || offset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _accessor.ReadArray(offset, destination, destinationOffset, count);
    }

    /// <inheritdoc/>
    public void Write(long offset, byte[] source, int sourceOffset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isWritable)
            throw new InvalidOperationException("Buffer is read-only");

        if (offset < 0 || offset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _accessor.WriteArray(offset, source, sourceOffset, count);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~MemoryMappedPixelBuffer()
    {
        Dispose(disposing: false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            _accessor.Dispose();
            _memoryMappedFile.Dispose();
        }
        else
        {
            try { _accessor.Dispose(); } catch { }
            try { _memoryMappedFile.Dispose(); } catch { }
        }

        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
        {
            try { File.Delete(_tempFilePath); }
            catch { /* Ignore cleanup errors */ }
        }
    }
}
