namespace TiledImage.Core.Memory;

/// <summary>
/// Specifies the type of pixel buffer to use for loading images.
/// </summary>
public enum BufferType
{
    /// <summary>
    /// Memory-mapped file buffer. Uses virtual memory mapping for efficient large file handling.
    /// Recommended for very large files where random access patterns are common.
    /// </summary>
    MemoryMapped,

    /// <summary>
    /// Unmanaged memory buffer. Loads entire file into unmanaged heap memory.
    /// Supports files larger than 2GB. Good for files that fit in available RAM.
    /// </summary>
    Unmanaged
}
