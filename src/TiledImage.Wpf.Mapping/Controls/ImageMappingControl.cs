using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TiledImage.Core.IO;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;
using Geometry.Primitives;
using TiledImage.Wpf.Viewer;
using TiledImage.Wpf.Viewer.Caching;
using SkiaSharp;

namespace TiledImage.Wpf.Mapping;

/// <summary>
/// A WPF control for mapping source images onto a frame image using perspective transformation.
/// The frame (overlay) image defines the output size and acts as a reference.
/// The source image is transformed to fit within the frame boundaries.
/// Inherits common tile viewing functionality from ImageViewControl.
/// </summary>
public class ImageMappingControl : ImageViewControl
{
    private Canvas? _perspectiveCanvas;

    // Perspective transform fields
    private readonly Ellipse[] _cornerHandles = new Ellipse[4];
    private readonly Polygon _perspectiveOutline;
    private int _draggingCorner = -1;
    private const double CornerHandleSize = 16;
    private const double CornerHitTestRadius = 20;

    // Cached bitmaps for perspective rendering performance
    private WriteableBitmap? _perspectiveRenderTarget;
    private SKBitmap? _perspectiveSkBitmap;
    private int _cachedPixelWidth;
    private int _cachedPixelHeight;

    // Overlay (frame) image fields - independent from source image
    private SKBitmap? _overlayBitmap;
    private long _overlayWidth;
    private long _overlayHeight;

    // Overlay is loaded and ready
    private bool _hasOverlay => _overlayBitmap != null && _overlayWidth > 0 && _overlayHeight > 0;

    #region Dependency Properties

    // Note: ImageSource, Zoom, ImageWidth, ImageHeight, ImageDepth, CurrentZ, IsLoading,
    // CanZoomIn, CanZoomOut, MouseImagePosition are inherited from ImageViewControl

    public static readonly DependencyProperty TransformProperty =
        DependencyProperty.Register(nameof(Transform), typeof(PerspectiveEditor), typeof(ImageMappingControl),
            new PropertyMetadata(null, OnTransformChanged));

    public static readonly DependencyProperty IsPerspectiveModeProperty =
        DependencyProperty.Register(nameof(IsPerspectiveMode), typeof(bool), typeof(ImageMappingControl),
            new PropertyMetadata(false, OnIsPerspectiveModeChanged));

    public static readonly DependencyProperty HasPerspectiveTransformProperty =
        DependencyProperty.Register(nameof(HasPerspectiveTransform), typeof(bool), typeof(ImageMappingControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty OverlayImagePathProperty =
        DependencyProperty.Register(nameof(OverlayImagePath), typeof(string), typeof(ImageMappingControl),
            new PropertyMetadata(null, OnOverlayImagePathChanged));

    public static readonly DependencyProperty OverlayOpacityProperty =
        DependencyProperty.Register(nameof(OverlayOpacity), typeof(double), typeof(ImageMappingControl),
            new PropertyMetadata(0.5, OnOverlayPropertyChanged, CoerceOverlayOpacity));

    public static readonly DependencyProperty IsOverlayVisibleProperty =
        DependencyProperty.Register(nameof(IsOverlayVisible), typeof(bool), typeof(ImageMappingControl),
            new PropertyMetadata(true, OnOverlayPropertyChanged));

    public static readonly DependencyProperty OverlayScaleProperty =
        DependencyProperty.Register(nameof(OverlayScale), typeof(double), typeof(ImageMappingControl),
            new PropertyMetadata(1.0, OnOverlayPropertyChanged, CoerceOverlayScale));

    // Note: ImageSource, Zoom, ImageWidth, ImageHeight, IsLoading, CanZoomIn, CanZoomOut,
    // MouseImagePosition properties are inherited from ImageViewControl

    public PerspectiveEditor? Transform
    {
        get => (PerspectiveEditor?)GetValue(TransformProperty);
        set => SetValue(TransformProperty, value);
    }

    public bool IsPerspectiveMode
    {
        get => (bool)GetValue(IsPerspectiveModeProperty);
        set => SetValue(IsPerspectiveModeProperty, value);
    }

    public bool HasPerspectiveTransform
    {
        get => (bool)GetValue(HasPerspectiveTransformProperty);
        private set => SetValue(HasPerspectiveTransformProperty, value);
    }

    public string? OverlayImagePath
    {
        get => (string?)GetValue(OverlayImagePathProperty);
        set => SetValue(OverlayImagePathProperty, value);
    }

    public double OverlayOpacity
    {
        get => (double)GetValue(OverlayOpacityProperty);
        set => SetValue(OverlayOpacityProperty, value);
    }

    public bool IsOverlayVisible
    {
        get => (bool)GetValue(IsOverlayVisibleProperty);
        set => SetValue(IsOverlayVisibleProperty, value);
    }

    /// <summary>
    /// Scale factor for overlay image itself (0.1 ~ 10.0).
    /// 1.0 = overlay original pixel size (1:1)
    /// 0.5 = overlay displayed at half its original size
    /// 2.0 = overlay displayed at double its original size
    /// This scales the overlay image independently from the source image.
    /// </summary>
    public double OverlayScale
    {
        get => (double)GetValue(OverlayScaleProperty);
        set => SetValue(OverlayScaleProperty, value);
    }

    /// <summary>
    /// Gets the overlay image width (original pixels).
    /// </summary>
    public long FrameWidth => _overlayWidth;

    /// <summary>
    /// Gets the overlay image height (original pixels).
    /// </summary>
    public long FrameHeight => _overlayHeight;

    /// <summary>
    /// Gets the scaled overlay width in source image coordinates.
    /// </summary>
    public double ScaledOverlayWidth => _overlayWidth * OverlayScale;

    /// <summary>
    /// Gets the scaled overlay height in source image coordinates.
    /// </summary>
    public double ScaledOverlayHeight => _overlayHeight * OverlayScale;

    #endregion

    // Note: ImageLoaded and ImageLoadError events are inherited from ImageViewControl

    static ImageMappingControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageMappingControl),
            new FrameworkPropertyMetadata(typeof(ImageMappingControl)));
    }

    public ImageMappingControl()
    {
        // Base class handles ViewPortManager initialization

        // Create perspective outline polygon
        _perspectiveOutline = new Polygon
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255))
        };

        // Create corner handles
        for (int i = 0; i < 4; i++)
        {
            _cornerHandles[i] = new Ellipse
            {
                Width = CornerHandleSize,
                Height = CornerHandleSize,
                Fill = Brushes.Yellow,
                Stroke = Brushes.DarkOrange,
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Tag = i
            };
        }
    }

    protected override void OnApplyTemplateCore()
    {
        base.OnApplyTemplateCore();

        // Base class handles _imageElement
        _perspectiveCanvas = GetTemplateChild("PART_PerspectiveCanvas") as Canvas;

        // Setup perspective canvas with handles
        if (_perspectiveCanvas != null)
        {
            _perspectiveCanvas.Children.Add(_perspectiveOutline);
            foreach (var handle in _cornerHandles)
            {
                _perspectiveCanvas.Children.Add(handle);
            }
        }
    }

    // Note: OnRenderSizeChanged is handled by base class, which calls RequestRender

    #region Mouse Handling Overrides

    protected override bool OnMouseLeftButtonDownCore(MouseButtonEventArgs e, Point position)
    {
        // Check if clicking on a corner handle in perspective mode
        if (IsPerspectiveMode)
        {
            int cornerIndex = GetCornerAtScreenPosition(position);
            if (cornerIndex >= 0)
            {
                _draggingCorner = cornerIndex;
                _lastMousePosition = position;
                CaptureMouse();
                Cursor = Cursors.Hand;
                Focus();
                e.Handled = true;
                return true;
            }
        }
        return false; // Let base class handle panning
    }

    protected override bool OnMouseLeftButtonUpCore(MouseButtonEventArgs e)
    {
        if (_draggingCorner >= 0)
        {
            _draggingCorner = -1;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            return true;
        }
        return false;
    }

    protected override bool OnMouseMoveCore(MouseEventArgs e, Point position)
    {
        // Handle corner dragging in perspective mode
        if (_draggingCorner >= 0)
        {
            double screenDeltaX = position.X - _lastMousePosition.X;
            double screenDeltaY = position.Y - _lastMousePosition.Y;

            float imageDeltaX = (float)(screenDeltaX / _viewPortManager.Zoom);
            float imageDeltaY = (float)(screenDeltaY / _viewPortManager.Zoom);

            Transform?.MoveCornerBy(_draggingCorner, imageDeltaX, imageDeltaY);
            _lastMousePosition = position;
            UpdatePerspectiveHandles();
            RequestRender();
            return true;
        }
        return false; // Let base class handle panning
    }

    #endregion

    // Note: Base class handles OnMouseWheel, we don't need to override

    #region Public Methods

    /// <summary>
    /// Reset the perspective transform to identity.
    /// </summary>
    public void ResetPerspectiveTransform()
    {
        if (ImageSource != null && ImageSource.IsLoaded && Transform != null)
        {
            Transform.InitializeForImage(ImageSource.ImageWidth, ImageSource.ImageHeight);
            HasPerspectiveTransform = false;
            UpdatePerspectiveHandles();
            RequestRender();
        }
    }

    /// <summary>
    /// Clear the tile cache. Useful when memory pressure is high.
    /// </summary>
    public void ClearCache()
    {
        _tileCache?.Clear();
    }

    #endregion

    #region Private Methods

    private static void OnTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageMappingControl viewer)
        {
            // Unsubscribe from old transform events
            if (e.OldValue is PerspectiveEditor oldTransform)
            {
                oldTransform.TransformChanged -= viewer.OnPerspectiveTransformChanged;
            }

            // Subscribe to new transform events
            if (e.NewValue is PerspectiveEditor newTransform)
            {
                newTransform.TransformChanged += viewer.OnPerspectiveTransformChanged;
                viewer.HasPerspectiveTransform = newTransform.HasTransform;
            }
            else
            {
                viewer.HasPerspectiveTransform = false;
            }

            viewer.UpdatePerspectiveHandles();
            viewer.RequestRender();
        }
    }

    protected override void OnImageSourceChangedCore(TiledImageSource? oldSource, TiledImageSource? newSource)
    {
        base.OnImageSourceChangedCore(oldSource, newSource);
        if (newSource != null && newSource.IsLoaded)
        {
            UpdatePerspectiveHandles();
        }
    }

    protected override bool ShouldRenderCustom()
    {
        // Use custom rendering when perspective transform is active or overlay is visible
        return (Transform?.HasTransform ?? false) || (_overlayBitmap != null && IsOverlayVisible);
    }

    protected override void RenderCustom()
    {
        RenderWithPerspective();
    }

    private static void OnIsPerspectiveModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageMappingControl viewer)
        {
            viewer.UpdatePerspectiveHandles();
            viewer.RequestRender();
        }
    }

    private static void OnOverlayImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageMappingControl viewer)
        {
            viewer.LoadOverlayImage(e.NewValue as string);
        }
    }

    private static void OnOverlayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageMappingControl viewer)
        {
            viewer.RequestRender();
        }
    }

    private static object CoerceOverlayOpacity(DependencyObject d, object baseValue)
    {
        var value = (double)baseValue;
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static object CoerceOverlayScale(DependencyObject d, object baseValue)
    {
        var value = (double)baseValue;
        return Math.Clamp(value, 0.1, 20.0);
    }

    private void LoadOverlayImage(string? path)
    {
        // Dispose previous overlay
        _overlayBitmap?.Dispose();
        _overlayBitmap = null;
        _overlayWidth = 0;
        _overlayHeight = 0;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            RequestRender();
            return;
        }

        try
        {
            _overlayBitmap = SKBitmap.Decode(path);
            if (_overlayBitmap != null)
            {
                _overlayWidth = _overlayBitmap.Width;
                _overlayHeight = _overlayBitmap.Height;
            }
            RequestRender();
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"Failed to load overlay image: {ex.Message}");
#endif
        }
    }

    private void OnPerspectiveTransformChanged(object? sender, EventArgs e)
    {
        HasPerspectiveTransform = Transform?.HasTransform ?? false;
    }

    /// <summary>
    /// Update the position of perspective corner handles on screen.
    /// </summary>
    private void UpdatePerspectiveHandles()
    {
        if (_perspectiveCanvas == null || ImageSource == null || !ImageSource.IsLoaded || Transform == null)
            return;

        var corners = Transform.DestinationCorners;

        // Update outline polygon
        var points = new PointCollection();
        for (int i = 0; i < 4; i++)
        {
            var screenPos = ImageToScreen(corners[i]);
            points.Add(screenPos);

            // Update handle position
            Canvas.SetLeft(_cornerHandles[i], screenPos.X - CornerHandleSize / 2);
            Canvas.SetTop(_cornerHandles[i], screenPos.Y - CornerHandleSize / 2);
        }
        _perspectiveOutline.Points = points;
    }

    /// <summary>
    /// Get the corner index at the given screen position.
    /// First checks if near a corner handle, then uses quadrant-based selection.
    /// </summary>
    private int GetCornerAtScreenPosition(Point screenPosition)
    {
        if (Transform == null) return -1;
        var corners = Transform.DestinationCorners;

        // First, check if clicking directly on a corner handle
        for (int i = 0; i < 4; i++)
        {
            var cornerScreen = ImageToScreen(corners[i]);
            var dx = screenPosition.X - cornerScreen.X;
            var dy = screenPosition.Y - cornerScreen.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance <= CornerHitTestRadius)
            {
                return i;
            }
        }

        // If not on a handle, use quadrant-based selection
        // Divide the image into 4 quadrants and select the corner based on click position
        return GetCornerByQuadrant(screenPosition);
    }

    /// <summary>
    /// Get corner index based on which quadrant of the image was clicked.
    /// Quadrant layout:
    ///   0 (TL) | 1 (TR)
    ///   -------+-------
    ///   3 (BL) | 2 (BR)
    /// </summary>
    private int GetCornerByQuadrant(Point screenPosition)
    {
        if (ImageSource == null || !ImageSource.IsLoaded)
            return -1;

        // Convert screen position to image coordinates
        var imagePos = _viewPortManager.ScreenToImage(screenPosition);

        // Check if within image bounds
        if (imagePos.X < 0 || imagePos.X >= ImageSource.ImageWidth ||
            imagePos.Y < 0 || imagePos.Y >= ImageSource.ImageHeight)
        {
            return -1;
        }

        // Calculate image center
        double centerX = ImageSource.ImageWidth / 2.0;
        double centerY = ImageSource.ImageHeight / 2.0;

        // Determine quadrant based on position relative to center
        // Corner indices: 0=TopLeft, 1=TopRight, 2=BottomRight, 3=BottomLeft
        bool isLeft = imagePos.X < centerX;
        bool isTop = imagePos.Y < centerY;

        if (isTop && isLeft) return 0;      // TopLeft
        if (isTop && !isLeft) return 1;     // TopRight
        if (!isTop && !isLeft) return 2;    // BottomRight
        return 3;                            // BottomLeft
    }

    /// <summary>
    /// Convert image coordinates to screen coordinates.
    /// </summary>
    private Point ImageToScreen(Point2F imagePoint)
    {
        double screenX = _viewPortManager.ImageToScreenX(imagePoint.X);
        double screenY = _viewPortManager.ImageToScreenY(imagePoint.Y);
        return new Point(screenX, screenY);
    }

    /// <summary>
    /// Render viewport with perspective transform preview using SkiaSharp.
    /// Optimized with cached bitmaps and direct pixel copy.
    /// </summary>
    private void RenderWithPerspective()
    {
        if (ImageSource == null || !ImageSource.IsLoaded || _imageElement == null)
            return;

        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        lock (_renderLock)
        {
            int pixelWidth = (int)Math.Ceiling(ActualWidth * _viewPortManager.DpiScaleX);
            int pixelHeight = (int)Math.Ceiling(ActualHeight * _viewPortManager.DpiScaleY);

            if (pixelWidth <= 0 || pixelHeight <= 0) return;

            // Get cached bitmaps (reused across frames)
            var skBitmap = GetOrCreateSkBitmap(pixelWidth, pixelHeight);
            var renderTarget = GetOrCreateRenderTarget(pixelWidth, pixelHeight);

            using var canvas = new SKCanvas(skBitmap);

            // Clear background
            canvas.Clear(new SKColor(40, 40, 40));

            double zoom = _viewPortManager.Zoom;
            double dpiX = 96 * _viewPortManager.DpiScaleX;
            double dpiY = 96 * _viewPortManager.DpiScaleY;

            // Calculate effective zoom considering perspective scale factor
            // When perspective area is smaller, scale factor < 1, so we use lower resolution tiles
            double perspectiveScale = Transform?.GetScaleFactor() ?? 1.0;
            double effectiveZoom = zoom * perspectiveScale;

            // Calculate the source viewport by inverse-transforming the screen corners
            var sourceViewport = CalculateSourceViewportForPerspective();

            // Get transform matrix once (reused for all tiles)
            var matrix = Transform != null ? Transform.GetTransformMatrix().ToSKMatrix() : SKMatrix.Identity;

            // Reusable paint object
            using var paint = new SKPaint
            {
                IsAntialias = false,  // Faster without anti-aliasing
                FilterQuality = SKFilterQuality.Medium  // Balance quality/speed
            };

            // Reusable arrays for tile corners
            var tileCorners = new SKPoint[4];
            var transformedCorners = new SKPoint[4];
            var screenCorners = new SKPoint[4];
            var srcCorners = new SKPoint[4];

            foreach (var tile in GetTilesInViewport(sourceViewport, effectiveZoom))
            {
                if (tile.PixelData == null) continue;

                int mipScale = 1 << tile.MipLevel;
                double tileRenderZoom = zoom * mipScale;

                // Convert to BGRA32 for SkiaSharp (handles Gray16, etc.)
                var displayData = ConvertToDisplayFormat(tile.PixelData, tile.Width, tile.Height, tile.Format);
                using var tileBitmap = new SKBitmap();
                var info = new SKImageInfo(tile.Width, tile.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                unsafe
                {
                    fixed (byte* ptr = displayData)
                    {
                        tileBitmap.InstallPixels(info, (IntPtr)ptr, tile.Width * 4);

                        if (Transform?.HasTransform ?? false)
                        {
                            // Set up tile corners in original image space
                            tileCorners[0] = new SKPoint(tile.X, tile.Y);
                            tileCorners[1] = new SKPoint(tile.X + tile.Width * mipScale, tile.Y);
                            tileCorners[2] = new SKPoint(tile.X + tile.Width * mipScale, tile.Y + tile.Height * mipScale);
                            tileCorners[3] = new SKPoint(tile.X, tile.Y + tile.Height * mipScale);

                            // Transform to perspective space then to screen
                            for (int i = 0; i < 4; i++)
                            {
                                transformedCorners[i] = matrix.MapPoint(tileCorners[i]);
                                screenCorners[i] = new SKPoint(
                                    (float)_viewPortManager.ImageToScreenX(transformedCorners[i].X),
                                    (float)_viewPortManager.ImageToScreenY(transformedCorners[i].Y)
                                );
                            }

                            // Skip tiles completely outside screen
                            if (!IsTileVisibleOnScreen(screenCorners, pixelWidth, pixelHeight))
                                continue;

                            // Set up source corners
                            srcCorners[0] = new SKPoint(0, 0);
                            srcCorners[1] = new SKPoint(tile.Width, 0);
                            srcCorners[2] = new SKPoint(tile.Width, tile.Height);
                            srcCorners[3] = new SKPoint(0, tile.Height);

                            var tileMatrix = SkiaExtensions.ComputeHomography(srcCorners, screenCorners);

                            canvas.Save();
                            canvas.SetMatrix(tileMatrix);
                            canvas.DrawBitmap(tileBitmap, 0, 0, paint);
                            canvas.Restore();
                        }
                        else
                        {
                            float screenX = (float)_viewPortManager.ImageToScreenX(tile.X);
                            float screenY = (float)_viewPortManager.ImageToScreenY(tile.Y);
                            float screenWidth = (float)(tile.Width * tileRenderZoom);
                            float screenHeight = (float)(tile.Height * tileRenderZoom);

                            var srcRect = new SKRect(0, 0, tile.Width, tile.Height);
                            var destRect = new SKRect(screenX, screenY, screenX + screenWidth, screenY + screenHeight);
                            canvas.DrawBitmap(tileBitmap, srcRect, destRect, paint);
                        }
                    }
                }
            }

            // Render overlay image on top (semi-transparent)
            RenderOverlay(canvas, pixelWidth, pixelHeight);

            // Direct pixel copy to WriteableBitmap (no PNG encoding!)
            CopySkBitmapToWriteableBitmap(skBitmap, renderTarget);
            _imageElement.Source = renderTarget;

            // Update perspective handles overlay
            if (IsPerspectiveMode)
            {
                UpdatePerspectiveHandles();
            }
        }
    }

    /// <summary>
    /// Render the overlay (frame) image onto the canvas.
    /// Overlay is centered on the source image and scaled by OverlayScale.
    /// </summary>
    private void RenderOverlay(SKCanvas canvas, int pixelWidth, int pixelHeight)
    {
        if (_overlayBitmap == null || !IsOverlayVisible || OverlayOpacity <= 0 || ImageSource == null)
            return;

        // Calculate overlay position: centered on source image
        double scaledWidth = _overlayWidth * OverlayScale;
        double scaledHeight = _overlayHeight * OverlayScale;
        double centerX = ImageSource.ImageWidth / 2.0;
        double centerY = ImageSource.ImageHeight / 2.0;

        // Overlay bounds in source image coordinates
        double left = centerX - scaledWidth / 2.0;
        double top = centerY - scaledHeight / 2.0;
        double right = centerX + scaledWidth / 2.0;
        double bottom = centerY + scaledHeight / 2.0;

        // Convert to screen coordinates
        var screenCorners = new SKPoint[]
        {
            new SKPoint(
                (float)_viewPortManager.ImageToScreenX(left),
                (float)_viewPortManager.ImageToScreenY(top)),
            new SKPoint(
                (float)_viewPortManager.ImageToScreenX(right),
                (float)_viewPortManager.ImageToScreenY(top)),
            new SKPoint(
                (float)_viewPortManager.ImageToScreenX(right),
                (float)_viewPortManager.ImageToScreenY(bottom)),
            new SKPoint(
                (float)_viewPortManager.ImageToScreenX(left),
                (float)_viewPortManager.ImageToScreenY(bottom))
        };

        // Overlay image corners (original pixels)
        var overlayCorners = new SKPoint[]
        {
            new SKPoint(0, 0),
            new SKPoint(_overlayBitmap.Width, 0),
            new SKPoint(_overlayBitmap.Width, _overlayBitmap.Height),
            new SKPoint(0, _overlayBitmap.Height)
        };

        // Map overlay to centered region
        var overlayMatrix = SkiaExtensions.ComputeHomography(overlayCorners, screenCorners);

        // Use SaveLayer to apply alpha only to the overlay
        byte alpha = (byte)(OverlayOpacity * 255);
        using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, alpha) };

        canvas.SaveLayer(layerPaint);
        canvas.SetMatrix(overlayMatrix);

        using var drawPaint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        canvas.DrawBitmap(_overlayBitmap, 0, 0, drawPaint);
        canvas.Restore();
    }

    /// <summary>
    /// Get the overlay bounds in source image coordinates (centered, scaled).
    /// </summary>
    public (double Left, double Top, double Right, double Bottom) GetOverlayBounds()
    {
        if (!_hasOverlay || ImageSource == null) return (0, 0, 0, 0);

        double scaledWidth = _overlayWidth * OverlayScale;
        double scaledHeight = _overlayHeight * OverlayScale;
        double centerX = ImageSource.ImageWidth / 2.0;
        double centerY = ImageSource.ImageHeight / 2.0;

        return (
            centerX - scaledWidth / 2.0,
            centerY - scaledHeight / 2.0,
            centerX + scaledWidth / 2.0,
            centerY + scaledHeight / 2.0
        );
    }

    /// <summary>
    /// Calculate the source viewport in original image coordinates that covers
    /// all pixels visible on screen after perspective transformation.
    /// </summary>
    private Rect2L CalculateSourceViewportForPerspective()
    {
        var visibleViewport = _viewPortManager.VisibleRect;

        if (Transform == null || !Transform.HasTransform)
        {
            return visibleViewport;
        }

        // Get the inverse transform matrix (Matrix3x3)
        var inverseMatrix = Transform.GetInverseMatrix();

        // Get the four corners of the current viewport in transformed image space
        var viewportCorners = new Point2F[]
        {
            new Point2F(visibleViewport.X, visibleViewport.Y),
            new Point2F(visibleViewport.Right, visibleViewport.Y),
            new Point2F(visibleViewport.Right, visibleViewport.Bottom),
            new Point2F(visibleViewport.X, visibleViewport.Bottom)
        };

        // Also add center and edge midpoints for better coverage with extreme transforms
        var additionalPoints = new Point2F[]
        {
            new Point2F((visibleViewport.X + visibleViewport.Right) / 2, visibleViewport.Y),
            new Point2F(visibleViewport.Right, (visibleViewport.Y + visibleViewport.Bottom) / 2),
            new Point2F((visibleViewport.X + visibleViewport.Right) / 2, visibleViewport.Bottom),
            new Point2F(visibleViewport.X, (visibleViewport.Y + visibleViewport.Bottom) / 2),
            new Point2F((visibleViewport.X + visibleViewport.Right) / 2, (visibleViewport.Y + visibleViewport.Bottom) / 2)
        };

        // Transform viewport corners back to source image coordinates
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var corner in viewportCorners)
        {
            var sourcePoint = inverseMatrix.MapPoint(corner);  // Returns Point2F
            minX = Math.Min(minX, sourcePoint.X);
            minY = Math.Min(minY, sourcePoint.Y);
            maxX = Math.Max(maxX, sourcePoint.X);
            maxY = Math.Max(maxY, sourcePoint.Y);
        }

        foreach (var point in additionalPoints)
        {
            var sourcePoint = inverseMatrix.MapPoint(point);  // Returns Point2F
            minX = Math.Min(minX, sourcePoint.X);
            minY = Math.Min(minY, sourcePoint.Y);
            maxX = Math.Max(maxX, sourcePoint.X);
            maxY = Math.Max(maxY, sourcePoint.Y);
        }

        // Add some margin to ensure we don't miss edge tiles
        float margin = ImageSource!.TileSize * 2;
        minX -= margin;
        minY -= margin;
        maxX += margin;
        maxY += margin;

        // Clamp to image bounds
        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(ImageSource.ImageWidth, maxX);
        maxY = Math.Min(ImageSource.ImageHeight, maxY);

        return new Rect2L(
            (long)minX,
            (long)minY,
            (long)(maxX - minX),
            (long)(maxY - minY)
        );
    }

    /// <summary>
    /// Check if a tile (defined by its screen corners) is at least partially visible on screen.
    /// </summary>
    private static bool IsTileVisibleOnScreen(SKPoint[] screenCorners, int screenWidth, int screenHeight)
    {
        // Calculate bounding box of the transformed tile
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var corner in screenCorners)
        {
            minX = Math.Min(minX, corner.X);
            minY = Math.Min(minY, corner.Y);
            maxX = Math.Max(maxX, corner.X);
            maxY = Math.Max(maxY, corner.Y);
        }

        // Check if bounding box intersects with screen
        return maxX >= 0 && minX <= screenWidth && maxY >= 0 && minY <= screenHeight;
    }

    /// <summary>
    /// Get or create a cached WriteableBitmap for perspective rendering.
    /// </summary>
    private WriteableBitmap GetOrCreateRenderTarget(int pixelWidth, int pixelHeight)
    {
        if (_perspectiveRenderTarget == null ||
            _cachedPixelWidth != pixelWidth ||
            _cachedPixelHeight != pixelHeight)
        {
            _perspectiveRenderTarget = new WriteableBitmap(
                pixelWidth, pixelHeight,
                96 * _viewPortManager.DpiScaleX,
                96 * _viewPortManager.DpiScaleY,
                PixelFormats.Bgra32, null);
            _cachedPixelWidth = pixelWidth;
            _cachedPixelHeight = pixelHeight;
        }
        return _perspectiveRenderTarget;
    }

    /// <summary>
    /// Get or create a cached SKBitmap for perspective rendering.
    /// </summary>
    private SKBitmap GetOrCreateSkBitmap(int pixelWidth, int pixelHeight)
    {
        if (_perspectiveSkBitmap == null ||
            _perspectiveSkBitmap.Width != pixelWidth ||
            _perspectiveSkBitmap.Height != pixelHeight)
        {
            _perspectiveSkBitmap?.Dispose();
            _perspectiveSkBitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        }
        return _perspectiveSkBitmap;
    }

    /// <summary>
    /// Copy SKBitmap pixels directly to WriteableBitmap (no encoding).
    /// </summary>
    private void CopySkBitmapToWriteableBitmap(SKBitmap skBitmap, WriteableBitmap writeableBitmap)
    {
        writeableBitmap.Lock();
        try
        {
            var srcPtr = skBitmap.GetPixels();
            var dstPtr = writeableBitmap.BackBuffer;
            int stride = skBitmap.RowBytes;
            int height = skBitmap.Height;

            unsafe
            {
                Buffer.MemoryCopy(
                    srcPtr.ToPointer(),
                    dstPtr.ToPointer(),
                    stride * height,
                    stride * height);
            }

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, skBitmap.Width, skBitmap.Height));
        }
        finally
        {
            writeableBitmap.Unlock();
        }
    }

    #endregion

    #region Export Methods

    /// <summary>
    /// Get tiles for export without accessing UI-bound fields.
    /// Uses mip level 0 (full resolution) for export quality.
    /// </summary>
    private static IEnumerable<ImageTile> GetTilesForExport(TiledImageSource imageSource, Rect2L viewport, int tileSize)
    {
        // Calculate tile range
        long startTileX = Math.Max(0, viewport.X / tileSize);
        long startTileY = Math.Max(0, viewport.Y / tileSize);
        long endTileX = Math.Min(imageSource.TilesX - 1, (viewport.Right - 1) / tileSize);
        long endTileY = Math.Min(imageSource.TilesY - 1, (viewport.Bottom - 1) / tileSize);

        for (long ty = startTileY; ty <= endTileY; ty++)
        {
            for (long tx = startTileX; tx <= endTileX; tx++)
            {
                long pixelX = tx * tileSize;
                long pixelY = ty * tileSize;

                int tileWidth = (int)Math.Min(tileSize, imageSource.ImageWidth - pixelX);
                int tileHeight = (int)Math.Min(tileSize, imageSource.ImageHeight - pixelY);

                // Get raw tile data from source
                var tileData = imageSource.GetTileData(tx, ty);
                if (tileData != null)
                {
                    // Create tile with correct position and size
                    var tile = new ImageTile(pixelX, pixelY, tileWidth, tileHeight, 0, TiledImage.Core.Types.PixelFormat.Bgra32);
                    tile.SetPixelData(tileData);
                    yield return tile;
                }
            }
        }
    }

    /// <summary>
    /// Export the overlay region to a file. The output size matches the overlay image original size.
    /// The source image within the overlay bounds (centered, scaled) is rendered with perspective transform.
    /// Areas where the source image doesn't cover will be filled with black.
    /// </summary>
    /// <param name="filePath">Output file path</param>
    /// <param name="format">Image format (PNG, JPEG, etc.)</param>
    /// <param name="quality">Quality for lossy formats (0-100)</param>
    /// <returns>True if export succeeded</returns>
    public async Task<bool> ExportMappedImageAsync(string filePath, ImageFormat format = ImageFormat.Png, int quality = 95)
    {
        if (!_hasOverlay || ImageSource == null || !ImageSource.IsLoaded)
        {
            return false;
        }

        // Capture all needed data on UI thread
        var exportData = new ExportData
        {
            FilePath = filePath,
            Format = format,
            Quality = quality,
            OutputWidth = (int)_overlayWidth,
            OutputHeight = (int)_overlayHeight,
            Bounds = GetOverlayBounds(),
            PerspectiveMatrix = Transform?.HasTransform == true ? Transform.GetTransformMatrix() : Matrix3x3.Identity,
            ImageSource = ImageSource,
            TileSize = ImageSource.TileSize,
            ImageWidth = ImageSource.ImageWidth,
            ImageHeight = ImageSource.ImageHeight
        };

        return await Task.Run(() => ExportMappedImageInternal(exportData));
    }

    private class ExportData
    {
        public string FilePath { get; set; } = "";
        public ImageFormat Format { get; set; }
        public int Quality { get; set; }
        public int OutputWidth { get; set; }
        public int OutputHeight { get; set; }
        public (double Left, double Top, double Right, double Bottom) Bounds { get; set; }
        public Matrix3x3 PerspectiveMatrix { get; set; }
        public TiledImageSource? ImageSource { get; set; }
        public int TileSize { get; set; }
        public long ImageWidth { get; set; }
        public long ImageHeight { get; set; }
    }

    /// <summary>
    /// Internal export implementation that renders the overlay region to overlay image size.
    /// For perspective: uses inverse sampling to correctly map output pixels to source pixels.
    /// </summary>
    private bool ExportMappedImageInternal(ExportData data)
    {
        if (data.ImageSource == null || data.OutputWidth <= 0 || data.OutputHeight <= 0)
            return false;

        try
        {
            // Get overlay bounds in source image coordinates (centered, scaled)
            double overlayLeft = data.Bounds.Left;
            double overlayTop = data.Bounds.Top;
            double overlayRight = data.Bounds.Right;
            double overlayBottom = data.Bounds.Bottom;
            double overlayWidthInSource = overlayRight - overlayLeft;
            double overlayHeightInSource = overlayBottom - overlayTop;

            // Scale from output to source overlay region
            double scaleX = overlayWidthInSource / data.OutputWidth;
            double scaleY = overlayHeightInSource / data.OutputHeight;

            // Check if we have perspective transform
            bool hasPerspective = !data.PerspectiveMatrix.IsIdentity;

            // For perspective, we need to use inverse transform sampling
            // For each output pixel, find the corresponding source pixel
            if (hasPerspective)
            {
                return ExportWithPerspectiveSampling(data, overlayLeft, overlayTop, scaleX, scaleY);
            }

            // No perspective: simple tile composition and crop
            // Calculate the region we need from source
            long srcLeft = Math.Max(0, (long)Math.Floor(overlayLeft));
            long srcTop = Math.Max(0, (long)Math.Floor(overlayTop));
            long srcRight = Math.Min(data.ImageWidth, (long)Math.Ceiling(overlayRight));
            long srcBottom = Math.Min(data.ImageHeight, (long)Math.Ceiling(overlayBottom));
            int srcWidth = (int)(srcRight - srcLeft);
            int srcHeight = (int)(srcBottom - srcTop);

            // Compose all tiles into a source region bitmap
            using var sourceBitmap = new SKBitmap(srcWidth, srcHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var sourceCanvas = new SKCanvas(sourceBitmap);
            sourceCanvas.Clear(SKColors.Black);

            var sourceViewport = new Rect2L(srcLeft, srcTop, srcWidth, srcHeight);

            foreach (var tile in GetTilesForExport(data.ImageSource, sourceViewport, data.TileSize))
            {
                if (tile.PixelData == null) continue;

                // Convert to BGRA32 for SkiaSharp
                var displayData = ConvertToDisplayFormat(tile.PixelData, tile.Width, tile.Height, tile.Format);
                using var tileBitmap = new SKBitmap();
                var info = new SKImageInfo(tile.Width, tile.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                unsafe
                {
                    fixed (byte* ptr = displayData)
                    {
                        tileBitmap.InstallPixels(info, (IntPtr)ptr, tile.Width * 4);
                        float destX = tile.X - srcLeft;
                        float destY = tile.Y - srcTop;
                        sourceCanvas.DrawBitmap(tileBitmap, destX, destY);
                    }
                }
            }

            // Create output bitmap and scale/crop
            using var outputBitmap = new SKBitmap(data.OutputWidth, data.OutputHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(outputBitmap);
            canvas.Clear(SKColors.Black);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            // Map source region to output (accounting for overlay offset)
            float destX2 = (float)((srcLeft - overlayLeft) / scaleX);
            float destY2 = (float)((srcTop - overlayTop) / scaleY);
            float destWidth = (float)(srcWidth / scaleX);
            float destHeight = (float)(srcHeight / scaleY);

            var srcRect = new SKRect(0, 0, srcWidth, srcHeight);
            var destRect = new SKRect(destX2, destY2, destX2 + destWidth, destY2 + destHeight);
            canvas.DrawBitmap(sourceBitmap, srcRect, destRect, paint);

            // Encode and save
            return SaveBitmapToFile(outputBitmap, data.FilePath, data.Format, data.Quality);
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"Export failed: {ex.Message}");
#endif
            return false;
        }
    }

    /// <summary>
    /// Export with perspective using inverse sampling.
    /// For each output pixel, find the corresponding source pixel through inverse transform.
    /// </summary>
    private bool ExportWithPerspectiveSampling(ExportData data, double overlayLeft, double overlayTop, double scaleX, double scaleY)
    {
        // Get inverse perspective matrix
        if (!data.PerspectiveMatrix.TryInvert(out var inverseMatrix))
        {
            return false;
        }

        // Calculate the source region needed (inverse transform overlay bounds)
        var overlayCorners = new Point2F[]
        {
            new Point2F((float)data.Bounds.Left, (float)data.Bounds.Top),
            new Point2F((float)data.Bounds.Right, (float)data.Bounds.Top),
            new Point2F((float)data.Bounds.Right, (float)data.Bounds.Bottom),
            new Point2F((float)data.Bounds.Left, (float)data.Bounds.Bottom)
        };

        // Find bounding box of inverse-transformed overlay in source space
        float minSrcX = float.MaxValue, minSrcY = float.MaxValue;
        float maxSrcX = float.MinValue, maxSrcY = float.MinValue;

        foreach (var corner in overlayCorners)
        {
            var srcPt = inverseMatrix.MapPoint(corner);
            minSrcX = Math.Min(minSrcX, srcPt.X);
            minSrcY = Math.Min(minSrcY, srcPt.Y);
            maxSrcX = Math.Max(maxSrcX, srcPt.X);
            maxSrcY = Math.Max(maxSrcY, srcPt.Y);
        }

        // Add margin and clamp to image bounds
        int margin = data.TileSize;
        long srcLeft = Math.Max(0, (long)Math.Floor(minSrcX) - margin);
        long srcTop = Math.Max(0, (long)Math.Floor(minSrcY) - margin);
        long srcRight = Math.Min(data.ImageWidth, (long)Math.Ceiling(maxSrcX) + margin);
        long srcBottom = Math.Min(data.ImageHeight, (long)Math.Ceiling(maxSrcY) + margin);
        int srcWidth = (int)(srcRight - srcLeft);
        int srcHeight = (int)(srcBottom - srcTop);

        if (srcWidth <= 0 || srcHeight <= 0) return false;

        // Compose source tiles into a bitmap
        using var sourceBitmap = new SKBitmap(srcWidth, srcHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var sourceCanvas = new SKCanvas(sourceBitmap);
        sourceCanvas.Clear(SKColors.Black);

        var sourceViewport = new Rect2L(srcLeft, srcTop, srcWidth, srcHeight);

        foreach (var tile in GetTilesForExport(data.ImageSource, sourceViewport, data.TileSize))
        {
            if (tile.PixelData == null) continue;

            // Convert to BGRA32 for SkiaSharp
            var displayData = ConvertToDisplayFormat(tile.PixelData, tile.Width, tile.Height, tile.Format);
            using var tileBitmap = new SKBitmap();
            var info = new SKImageInfo(tile.Width, tile.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            unsafe
            {
                fixed (byte* ptr = displayData)
                {
                    tileBitmap.InstallPixels(info, (IntPtr)ptr, tile.Width * 4);
                    float destX = tile.X - srcLeft;
                    float destY = tile.Y - srcTop;
                    sourceCanvas.DrawBitmap(tileBitmap, destX, destY);
                }
            }
        }

        // Create output bitmap
        using var outputBitmap = new SKBitmap(data.OutputWidth, data.OutputHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(outputBitmap);
        canvas.Clear(SKColors.Black);

        // Create shader-based sampling using matrix
        // Output pixel (ox, oy) -> overlay coords -> inverse perspective -> source coords -> sample

        // Build the combined transform matrix:
        // 1. Output pixel to overlay coordinates: translate + scale
        // 2. Overlay coordinates through inverse perspective to source coordinates
        // 3. Source coordinates to source bitmap coordinates: translate by -srcLeft, -srcTop

        // Step 1: Output to overlay
        var outputToOverlay = SKMatrix.CreateScale((float)scaleX, (float)scaleY);
        outputToOverlay = outputToOverlay.PostConcat(SKMatrix.CreateTranslation((float)overlayLeft, (float)overlayTop));

        // Step 2: Overlay to source (inverse perspective)
        var skInverse = inverseMatrix.ToSKMatrix();

        // Step 3: Source to source bitmap
        var sourceTobitmap = SKMatrix.CreateTranslation(-srcLeft, -srcTop);

        // Combined: output -> overlay -> source -> bitmap
        var combinedMatrix = outputToOverlay;
        combinedMatrix = combinedMatrix.PostConcat(skInverse);
        combinedMatrix = combinedMatrix.PostConcat(sourceTobitmap);

        // Use the inverse of combined matrix to draw source bitmap to output
        if (combinedMatrix.TryInvert(out var drawMatrix))
        {
            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            canvas.SetMatrix(drawMatrix);
            canvas.DrawBitmap(sourceBitmap, 0, 0, paint);
        }

        return SaveBitmapToFile(outputBitmap, data.FilePath, data.Format, data.Quality);
    }

    /// <summary>
    /// Save SKBitmap to file with specified format.
    /// </summary>
    private static bool SaveBitmapToFile(SKBitmap bitmap, string filePath, ImageFormat format, int quality)
    {
        var skFormat = format switch
        {
            ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Webp => SKEncodedImageFormat.Webp,
            ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
            ImageFormat.Gif => SKEncodedImageFormat.Gif,
            _ => SKEncodedImageFormat.Png
        };

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(skFormat, quality);
        using var stream = File.OpenWrite(filePath);
        encoded.SaveTo(stream);
        return true;
    }

    #endregion
}
