using System.Windows;
using Geometry.Primitives;
using TiledImage.Core.Types;

namespace TiledImage.Wpf.Viewer;

/// <summary>
/// Manages the viewport state for the large image viewer.
/// Handles zoom, pan, and coordinate transformations with DPI awareness.
/// </summary>
public class ViewPortManager
{
    private double _zoom = 1.0;
    private double _offsetX;
    private double _offsetY;
    private double _viewportWidth;
    private double _viewportHeight;
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;
    private long _imageWidth;
    private long _imageHeight;

    public const double MinZoom = 0.01;
    public const double MaxZoom = 100.0;
    public const double ZoomStep = 1.2;

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, MinZoom, MaxZoom);
            ClampOffset();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double OffsetX
    {
        get => _offsetX;
        set
        {
            _offsetX = value;
            ClampOffset();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double OffsetY
    {
        get => _offsetY;
        set
        {
            _offsetY = value;
            ClampOffset();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double ViewportWidth => _viewportWidth;
    public double ViewportHeight => _viewportHeight;
    public double DpiScaleX => _dpiScaleX;
    public double DpiScaleY => _dpiScaleY;
    public long ImageWidth => _imageWidth;
    public long ImageHeight => _imageHeight;

    /// <summary>
    /// Gets the current viewport rectangle in image pixel coordinates.
    /// </summary>
    public Rect2L VisibleRect
    {
        get
        {
            double widthInPixels = _viewportWidth / _zoom;
            double heightInPixels = _viewportHeight / _zoom;

            return new Rect2L(
                (long)_offsetX,
                (long)_offsetY,
                (long)Math.Ceiling(widthInPixels),
                (long)Math.Ceiling(heightInPixels));
        }
    }

    public event EventHandler? ViewportChanged;

    public void SetImageSize(long width, long height)
    {
        _imageWidth = width;
        _imageHeight = height;
        ClampOffset();
    }

    public void SetViewportSize(double width, double height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        ClampOffset();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetDpiScale(double scaleX, double scaleY)
    {
        _dpiScaleX = scaleX;
        _dpiScaleY = scaleY;
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Zoom in/out centered on a specific screen point.
    /// </summary>
    public void ZoomAtPoint(double factor, Point screenPoint)
    {
        // Convert screen point to image coordinates before zoom
        double imageX = ScreenToImageX(screenPoint.X);
        double imageY = ScreenToImageY(screenPoint.Y);

        // Apply zoom
        double oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        if (Math.Abs(_zoom - oldZoom) < 0.0001) return;

        // Adjust offset to keep the same image point under the cursor
        _offsetX = imageX - (screenPoint.X / _zoom);
        _offsetY = imageY - (screenPoint.Y / _zoom);

        ClampOffset();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pan the viewport by screen delta.
    /// </summary>
    public void Pan(double deltaX, double deltaY)
    {
        _offsetX -= deltaX / _zoom;
        _offsetY -= deltaY / _zoom;
        ClampOffset();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Fit the entire image in the viewport.
    /// </summary>
    public void FitToView()
    {
        if (_imageWidth <= 0 || _imageHeight <= 0) return;
        if (_viewportWidth <= 0 || _viewportHeight <= 0) return;

        double zoomX = _viewportWidth / _imageWidth;
        double zoomY = _viewportHeight / _imageHeight;
        _zoom = Math.Min(zoomX, zoomY);
        _zoom = Math.Clamp(_zoom, MinZoom, MaxZoom);

        // Center the image
        double scaledWidth = _imageWidth * _zoom;
        double scaledHeight = _imageHeight * _zoom;
        _offsetX = -(_viewportWidth - scaledWidth) / (2 * _zoom);
        _offsetY = -(_viewportHeight - scaledHeight) / (2 * _zoom);

        ClampOffset();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reset to 100% zoom and center the image.
    /// </summary>
    public void ResetView()
    {
        _zoom = 1.0;

        // Center the image
        double scaledWidth = _imageWidth * _zoom;
        double scaledHeight = _imageHeight * _zoom;
        _offsetX = Math.Max(0, (_imageWidth - _viewportWidth) / 2);
        _offsetY = Math.Max(0, (_imageHeight - _viewportHeight) / 2);

        ClampOffset();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Convert screen X coordinate to image pixel X coordinate.
    /// </summary>
    public double ScreenToImageX(double screenX)
    {
        return _offsetX + (screenX / _zoom);
    }

    /// <summary>
    /// Convert screen Y coordinate to image pixel Y coordinate.
    /// </summary>
    public double ScreenToImageY(double screenY)
    {
        return _offsetY + (screenY / _zoom);
    }

    /// <summary>
    /// Convert image pixel X coordinate to screen X coordinate.
    /// </summary>
    public double ImageToScreenX(double imageX)
    {
        return (imageX - _offsetX) * _zoom;
    }

    /// <summary>
    /// Convert image pixel Y coordinate to screen Y coordinate.
    /// </summary>
    public double ImageToScreenY(double imageY)
    {
        return (imageY - _offsetY) * _zoom;
    }

    /// <summary>
    /// Convert screen point to image pixel coordinates.
    /// </summary>
    public Point2L ScreenToImage(Point screenPoint)
    {
        return new Point2L(
            (long)ScreenToImageX(screenPoint.X),
            (long)ScreenToImageY(screenPoint.Y));
    }

    /// <summary>
    /// Convert image pixel coordinates to screen point.
    /// </summary>
    public Point ImageToScreen(long imageX, long imageY)
    {
        return new Point(
            ImageToScreenX(imageX),
            ImageToScreenY(imageY));
    }

    private void ClampOffset()
    {
        if (_imageWidth <= 0 || _imageHeight <= 0) return;

        // Calculate the visible area in image pixels
        double visibleWidth = _viewportWidth / _zoom;
        double visibleHeight = _viewportHeight / _zoom;

        // Allow some margin for panning beyond image bounds
        double marginX = visibleWidth * 0.1;
        double marginY = visibleHeight * 0.1;

        // Clamp offset to keep at least some of the image visible
        _offsetX = Math.Clamp(_offsetX, -visibleWidth + marginX, _imageWidth - marginX);
        _offsetY = Math.Clamp(_offsetY, -visibleHeight + marginY, _imageHeight - marginY);
    }
}
