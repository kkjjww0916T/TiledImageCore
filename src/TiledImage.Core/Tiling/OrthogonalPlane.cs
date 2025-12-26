namespace TiledImage.Core.Tiling;

/// <summary>
/// Represents orthogonal viewing planes for 3D volume data.
/// </summary>
public enum OrthogonalPlane
{
    /// <summary>
    /// XY plane (Axial view): X horizontal, Y vertical, Z depth.
    /// Standard 2D image view.
    /// </summary>
    XY = 0,

    /// <summary>
    /// XZ plane (Coronal view): X horizontal, Z vertical, Y depth.
    /// Placed below XY so X-axis aligns horizontally.
    /// </summary>
    XZ = 1,

    /// <summary>
    /// YZ plane (Sagittal view): Z horizontal, Y vertical, X depth.
    /// Placed right of XY so Y-axis aligns vertically.
    /// </summary>
    YZ = 2
}

/// <summary>
/// Extension methods for OrthogonalPlane.
/// </summary>
public static class OrthogonalPlaneExtensions
{
    /// <summary>
    /// Get plane dimensions from volume dimensions.
    /// </summary>
    /// <param name="plane">The viewing plane.</param>
    /// <param name="volumeWidth">Volume X dimension.</param>
    /// <param name="volumeHeight">Volume Y dimension.</param>
    /// <param name="volumeDepth">Volume Z dimension.</param>
    /// <returns>Tuple of (PlaneWidth, PlaneHeight, PlaneDepth).</returns>
    public static (long PlaneWidth, long PlaneHeight, long PlaneDepth) GetPlaneDimensions(
        this OrthogonalPlane plane,
        long volumeWidth,
        long volumeHeight,
        long volumeDepth) => plane switch
    {
        OrthogonalPlane.XY => (volumeWidth, volumeHeight, volumeDepth),
        OrthogonalPlane.XZ => (volumeWidth, volumeDepth, volumeHeight),   // X horizontal, Z vertical
        OrthogonalPlane.YZ => (volumeDepth, volumeHeight, volumeWidth),   // Z horizontal, Y vertical
        _ => (volumeWidth, volumeHeight, volumeDepth)
    };

    /// <summary>
    /// Convert plane-local coordinates (u, v, slice) to volume coordinates (x, y, z).
    /// </summary>
    /// <param name="plane">The viewing plane.</param>
    /// <param name="u">Horizontal coordinate in plane.</param>
    /// <param name="v">Vertical coordinate in plane.</param>
    /// <param name="slice">Depth (slice index) in plane.</param>
    /// <returns>Volume coordinates (X, Y, Z).</returns>
    public static (long X, long Y, long Z) ToVolumeCoordinates(
        this OrthogonalPlane plane,
        long u,
        long v,
        long slice) => plane switch
    {
        OrthogonalPlane.XY => (u, v, slice),
        OrthogonalPlane.XZ => (u, slice, v),  // U=X, V=Z, Slice=Y
        OrthogonalPlane.YZ => (slice, v, u),  // U=Z, V=Y, Slice=X
        _ => (u, v, slice)
    };

    /// <summary>
    /// Convert volume coordinates (x, y, z) to plane-local coordinates (u, v, slice).
    /// </summary>
    /// <param name="plane">The viewing plane.</param>
    /// <param name="x">Volume X coordinate.</param>
    /// <param name="y">Volume Y coordinate.</param>
    /// <param name="z">Volume Z coordinate.</param>
    /// <returns>Plane coordinates (U, V, Slice).</returns>
    public static (long U, long V, long Slice) ToPlaneCoordinates(
        this OrthogonalPlane plane,
        long x,
        long y,
        long z) => plane switch
    {
        OrthogonalPlane.XY => (x, y, z),
        OrthogonalPlane.XZ => (x, z, y),  // U=X, V=Z, Slice=Y
        OrthogonalPlane.YZ => (z, y, x),  // U=Z, V=Y, Slice=X
        _ => (x, y, z)
    };

    /// <summary>
    /// Get the display name for the plane.
    /// </summary>
    public static string GetDisplayName(this OrthogonalPlane plane) => plane switch
    {
        OrthogonalPlane.XY => "Axial (XY)",
        OrthogonalPlane.XZ => "Coronal (XZ)",
        OrthogonalPlane.YZ => "Sagittal (YZ)",
        _ => plane.ToString()
    };

    /// <summary>
    /// Get the axis labels for the plane.
    /// </summary>
    /// <returns>Tuple of (HorizontalAxis, VerticalAxis, DepthAxis).</returns>
    public static (string Horizontal, string Vertical, string Depth) GetAxisLabels(
        this OrthogonalPlane plane) => plane switch
    {
        OrthogonalPlane.XY => ("X", "Y", "Z"),
        OrthogonalPlane.XZ => ("X", "Z", "Y"),  // X horizontal, Z vertical
        OrthogonalPlane.YZ => ("Z", "Y", "X"),  // Z horizontal, Y vertical
        _ => ("X", "Y", "Z")
    };
}
