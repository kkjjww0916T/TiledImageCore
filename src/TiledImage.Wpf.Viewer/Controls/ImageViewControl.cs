using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Geometry.Primitives;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;
using TiledImage.Wpf.Viewer.Caching;
using TiledImage.Wpf.Viewer.LUT;
using TiledImage.Wpf.Viewer.Types;
using SkiaSharp;
using CorePixelFormat = TiledImage.Core.Types.PixelFormat;

namespace TiledImage.Wpf.Viewer;

/// <summary>
/// Base control for displaying large tiled images with zoom/pan support.
/// Provides tile caching, MipMap management, and viewport handling.
/// </summary>
public class ImageViewControl : Control
{
    protected readonly ViewPortManager _viewPortManager;
    protected Image? _imageElement;
    protected Point _lastMousePosition;
    protected bool _isPanning;
    protected bool _renderPending;
    protected readonly object _renderLock = new();

    // Tile caching and MipMap management
    protected TileCache? _tileCache;
    protected MipMapManager? _mipMapManager;
    protected long _cachedZ = -1; // Track Z for cache invalidation

    // LUT for display range adjustment
    private LookupTable? _lookupTable;
    private CorePixelFormat _currentPixelFormat;

    #region Dependency Properties

    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(TiledImageSource), typeof(ImageViewControl),
            new PropertyMetadata(null, OnImageSourceChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ImageViewControl),
            new PropertyMetadata(1.0, OnZoomChanged, CoerceZoom));

    public static readonly DependencyProperty ImageWidthProperty =
        DependencyProperty.Register(nameof(ImageWidth), typeof(long), typeof(ImageViewControl),
            new PropertyMetadata(0L));

    public static readonly DependencyProperty ImageHeightProperty =
        DependencyProperty.Register(nameof(ImageHeight), typeof(long), typeof(ImageViewControl),
            new PropertyMetadata(0L));

    public static readonly DependencyProperty ImageDepthProperty =
        DependencyProperty.Register(nameof(ImageDepth), typeof(long), typeof(ImageViewControl),
            new PropertyMetadata(0L));

    public static readonly DependencyProperty CurrentZProperty =
        DependencyProperty.Register(nameof(CurrentZ), typeof(long), typeof(ImageViewControl),
            new PropertyMetadata(0L, OnCurrentZChanged, CoerceCurrentZ));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(ImageViewControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanZoomInProperty =
        DependencyProperty.Register(nameof(CanZoomIn), typeof(bool), typeof(ImageViewControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty CanZoomOutProperty =
        DependencyProperty.Register(nameof(CanZoomOut), typeof(bool), typeof(ImageViewControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty MouseImagePositionProperty =
        DependencyProperty.Register(nameof(MouseImagePosition), typeof(Point), typeof(ImageViewControl),
            new PropertyMetadata(default(Point)));

    public static readonly DependencyProperty DisplayMinProperty =
        DependencyProperty.Register(nameof(DisplayMin), typeof(double), typeof(ImageViewControl),
            new PropertyMetadata(0.0, OnDisplayRangeChanged));

    public static readonly DependencyProperty DisplayMaxProperty =
        DependencyProperty.Register(nameof(DisplayMax), typeof(double), typeof(ImageViewControl),
            new PropertyMetadata(255.0, OnDisplayRangeChanged));

    /// <summary>
    /// The tiled image source to display.
    /// </summary>
    public TiledImageSource? ImageSource
    {
        get => (TiledImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>
    /// Current zoom level (1.0 = 100%).
    /// </summary>
    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public long ImageWidth
    {
        get => (long)GetValue(ImageWidthProperty);
        protected set => SetValue(ImageWidthProperty, value);
    }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public long ImageHeight
    {
        get => (long)GetValue(ImageHeightProperty);
        protected set => SetValue(ImageHeightProperty, value);
    }

    /// <summary>
    /// Image depth (number of Z slices). 1 for 2D images.
    /// </summary>
    public long ImageDepth
    {
        get => (long)GetValue(ImageDepthProperty);
        protected set => SetValue(ImageDepthProperty, value);
    }

    /// <summary>
    /// Current Z slice index (0-based).
    /// </summary>
    public long CurrentZ
    {
        get => (long)GetValue(CurrentZProperty);
        set => SetValue(CurrentZProperty, value);
    }

    /// <summary>
    /// Whether the control is currently loading an image.
    /// </summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        protected set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// Whether zoom in is available.
    /// </summary>
    public bool CanZoomIn
    {
        get => (bool)GetValue(CanZoomInProperty);
        protected set => SetValue(CanZoomInProperty, value);
    }

    /// <summary>
    /// Whether zoom out is available.
    /// </summary>
    public bool CanZoomOut
    {
        get => (bool)GetValue(CanZoomOutProperty);
        protected set => SetValue(CanZoomOutProperty, value);
    }

    /// <summary>
    /// Current mouse position in image coordinates.
    /// </summary>
    public Point MouseImagePosition
    {
        get => (Point)GetValue(MouseImagePositionProperty);
        protected set => SetValue(MouseImagePositionProperty, value);
    }

    /// <summary>
    /// Minimum value of the display range (maps to black).
    /// For 8-bit images: 0-255, for 16-bit images: 0-65535.
    /// </summary>
    public double DisplayMin
    {
        get => (double)GetValue(DisplayMinProperty);
        set => SetValue(DisplayMinProperty, value);
    }

    /// <summary>
    /// Maximum value of the display range (maps to white).
    /// For 8-bit images: 0-255, for 16-bit images: 0-65535.
    /// </summary>
    public double DisplayMax
    {
        get => (double)GetValue(DisplayMaxProperty);
        set => SetValue(DisplayMaxProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent ImageLoadedEvent =
        EventManager.RegisterRoutedEvent(nameof(ImageLoaded), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ImageViewControl));

    public static readonly RoutedEvent ImageLoadErrorEvent =
        EventManager.RegisterRoutedEvent(nameof(ImageLoadError), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ImageViewControl));

    public event RoutedEventHandler ImageLoaded
    {
        add => AddHandler(ImageLoadedEvent, value);
        remove => RemoveHandler(ImageLoadedEvent, value);
    }

    public event RoutedEventHandler ImageLoadError
    {
        add => AddHandler(ImageLoadErrorEvent, value);
        remove => RemoveHandler(ImageLoadErrorEvent, value);
    }

    #endregion

    static ImageViewControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageViewControl),
            new FrameworkPropertyMetadata(typeof(ImageViewControl)));
    }

    public ImageViewControl()
    {
        _viewPortManager = new ViewPortManager();
        _viewPortManager.ViewportChanged += OnViewportChanged;

        Focusable = true;
        ClipToBounds = true;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _imageElement = GetTemplateChild("PART_Image") as Image;

        // Setup DPI awareness
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var dpiX = source.CompositionTarget.TransformToDevice.M11;
            var dpiY = source.CompositionTarget.TransformToDevice.M22;
            _viewPortManager.SetDpiScale(dpiX, dpiY);
        }

        OnApplyTemplateCore();

        // If image was already loaded before template was applied, render now
        if (ImageSource != null && ImageSource.IsLoaded)
        {
            RequestRender();
        }
    }

    /// <summary>
    /// Override to add additional template initialization in derived classes.
    /// </summary>
    protected virtual void OnApplyTemplateCore()
    {
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _viewPortManager.SetViewportSize(ActualWidth, ActualHeight);
        RequestRender();
    }

    #region Property Changed Handlers

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewControl viewer)
        {
            var oldSource = e.OldValue as TiledImageSource;
            var newSource = e.NewValue as TiledImageSource;

            // Dispose old cache
            viewer._tileCache?.Dispose();
            viewer._mipMapManager?.Dispose();
            viewer._tileCache = null;
            viewer._mipMapManager = null;
            viewer._cachedZ = -1;

            if (newSource != null && newSource.IsLoaded)
            {
                viewer.ImageWidth = newSource.ImageWidth;
                viewer.ImageHeight = newSource.ImageHeight;
                viewer.ImageDepth = newSource.ImageDepth;
                viewer.CurrentZ = 0;

                // Initialize viewport
                viewer._viewPortManager.SetImageSize(newSource.ImageWidth, newSource.ImageHeight);

                // Initialize cache and MipMap
                viewer._tileCache = new TileCache(100);
                viewer._mipMapManager = new MipMapManager(newSource);
                viewer._mipMapManager.Initialize();
                viewer._cachedZ = 0;

                // Initialize LUT for pixel format
                viewer._currentPixelFormat = newSource.PixelFormat;
                viewer.UpdateLookupTable();

                viewer.OnImageSourceChangedCore(oldSource, newSource);
                viewer.RaiseEvent(new RoutedEventArgs(ImageLoadedEvent));
            }
            else
            {
                viewer.ImageWidth = 0;
                viewer.ImageHeight = 0;
                viewer.ImageDepth = 0;
                viewer._lookupTable = null;
                viewer.OnImageSourceChangedCore(oldSource, newSource);
            }

            viewer.RequestRender();
        }
    }

    private static void OnCurrentZChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewControl viewer)
        {
            var newZ = (long)e.NewValue;

            // Update source's CurrentZ
            if (viewer.ImageSource != null)
            {
                viewer.ImageSource.CurrentZ = newZ;
            }

            // Invalidate cache if Z changed
            if (viewer._cachedZ != newZ)
            {
                viewer._tileCache?.Clear();
                viewer._mipMapManager?.Dispose();

                if (viewer.ImageSource != null && viewer.ImageSource.IsLoaded)
                {
                    viewer._mipMapManager = new MipMapManager(viewer.ImageSource);
                    viewer._mipMapManager.Initialize();
                }

                viewer._cachedZ = newZ;
            }

            viewer.OnCurrentZChangedCore((long)e.OldValue, newZ);
            viewer.RequestRender();
        }
    }

    private static object CoerceCurrentZ(DependencyObject d, object baseValue)
    {
        if (d is ImageViewControl viewer && baseValue is long z)
        {
            long maxZ = viewer.ImageDepth > 0 ? viewer.ImageDepth - 1 : 0;
            return Math.Clamp(z, 0, maxZ);
        }
        return 0L;
    }

    private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewControl viewer)
        {
            viewer._viewPortManager.Zoom = (double)e.NewValue;
            viewer.UpdateZoomCapabilities();
            viewer.RequestRender();
        }
    }

    private static object CoerceZoom(DependencyObject d, object baseValue)
    {
        if (baseValue is double zoom)
        {
            return Math.Clamp(zoom, ViewPortManager.MinZoom, ViewPortManager.MaxZoom);
        }
        return 1.0;
    }

    /// <summary>
    /// Override to handle image source changes in derived classes.
    /// </summary>
    protected virtual void OnImageSourceChangedCore(TiledImageSource? oldSource, TiledImageSource? newSource)
    {
    }

    /// <summary>
    /// Override to handle Z slice changes in derived classes.
    /// </summary>
    protected virtual void OnCurrentZChangedCore(long oldZ, long newZ)
    {
    }

    private static void OnDisplayRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewControl viewer)
        {
            viewer.UpdateLookupTable();
            viewer.RequestRender();
        }
    }

    #endregion

    #region Mouse Handling

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (ImageSource == null || !ImageSource.IsLoaded) return;

        var position = e.GetPosition(this);
        double factor = e.Delta > 0 ? ViewPortManager.ZoomStep : 1.0 / ViewPortManager.ZoomStep;

        _viewPortManager.ZoomAtPoint(factor, position);
        UpdateZoomProperty();
        RequestRender();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (ImageSource == null || !ImageSource.IsLoaded) return;

        var position = e.GetPosition(this);

        // Let derived classes handle first
        if (OnMouseLeftButtonDownCore(e, position))
        {
            return;
        }

        _isPanning = true;
        _lastMousePosition = position;
        CaptureMouse();
        Cursor = Cursors.Hand;
        Focus();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        // Let derived classes handle first
        if (OnMouseLeftButtonUpCore(e))
        {
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        if (ImageSource == null || !ImageSource.IsLoaded) return;

        var position = e.GetPosition(this);

        // Let derived classes handle first
        if (OnMouseRightButtonDownCore(e, position))
        {
            return;
        }

        // Right-click panning
        _isPanning = true;
        _lastMousePosition = position;
        CaptureMouse();
        Cursor = Cursors.Hand;
        Focus();
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);

        // Let derived classes handle first
        if (OnMouseRightButtonUpCore(e))
        {
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (ImageSource == null || !ImageSource.IsLoaded) return;

        var position = e.GetPosition(this);

        // Update mouse image position
        var imagePos = _viewPortManager.ScreenToImage(position);
        MouseImagePosition = new Point(imagePos.X, imagePos.Y);

        // Let derived classes handle first
        if (OnMouseMoveCore(e, position))
        {
            return;
        }

        if (_isPanning)
        {
            var delta = position - _lastMousePosition;
            _viewPortManager.Pan(delta.X, delta.Y);
            _lastMousePosition = position;
            RequestRender();
        }
    }

    /// <summary>
    /// Override to handle mouse left button down in derived classes.
    /// Return true if handled.
    /// </summary>
    protected virtual bool OnMouseLeftButtonDownCore(MouseButtonEventArgs e, Point position)
    {
        return false;
    }

    /// <summary>
    /// Override to handle mouse left button up in derived classes.
    /// Return true if handled.
    /// </summary>
    protected virtual bool OnMouseLeftButtonUpCore(MouseButtonEventArgs e)
    {
        return false;
    }

    /// <summary>
    /// Override to handle mouse right button down in derived classes.
    /// Return true if handled.
    /// </summary>
    protected virtual bool OnMouseRightButtonDownCore(MouseButtonEventArgs e, Point position)
    {
        return false;
    }

    /// <summary>
    /// Override to handle mouse right button up in derived classes.
    /// Return true if handled.
    /// </summary>
    protected virtual bool OnMouseRightButtonUpCore(MouseButtonEventArgs e)
    {
        return false;
    }

    /// <summary>
    /// Override to handle mouse move in derived classes.
    /// Return true if handled.
    /// </summary>
    protected virtual bool OnMouseMoveCore(MouseEventArgs e, Point position)
    {
        return false;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Fit the image to the viewport.
    /// </summary>
    public virtual void FitToView()
    {
        if (ImageSource == null || !ImageSource.IsLoaded) return;
        _viewPortManager.FitToView();
        UpdateZoomProperty();
        RequestRender();
    }

    /// <summary>
    /// Reset zoom to 100%.
    /// </summary>
    public virtual void ResetZoom()
    {
        _viewPortManager.ResetView();
        UpdateZoomProperty();
        RequestRender();
    }

    /// <summary>
    /// Zoom in.
    /// </summary>
    public virtual void ZoomIn()
    {
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        _viewPortManager.ZoomAtPoint(ViewPortManager.ZoomStep, center);
        UpdateZoomProperty();
        RequestRender();
    }

    /// <summary>
    /// Zoom out.
    /// </summary>
    public virtual void ZoomOut()
    {
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        _viewPortManager.ZoomAtPoint(1.0 / ViewPortManager.ZoomStep, center);
        UpdateZoomProperty();
        RequestRender();
    }

    /// <summary>
    /// Center the view on a specific image coordinate.
    /// </summary>
    public virtual void CenterOn(double imageX, double imageY)
    {
        _viewPortManager.OffsetX = imageX - (ActualWidth / 2 / _viewPortManager.Zoom);
        _viewPortManager.OffsetY = imageY - (ActualHeight / 2 / _viewPortManager.Zoom);
        RequestRender();
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Request a render on the next frame.
    /// </summary>
    public void RequestRender()
    {
        if (_renderPending) return;

        _renderPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            _renderPending = false;
            Render();
        }));
    }

    /// <summary>
    /// Main render method.
    /// </summary>
    protected virtual void Render()
    {
        if (ImageSource == null || !ImageSource.IsLoaded || _imageElement == null)
            return;

        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        // Check if derived class wants custom rendering
        if (ShouldRenderCustom())
        {
            RenderCustom();
            return;
        }

        lock (_renderLock)
        {
            // Create DrawingVisual for GPU-accelerated rendering
            var drawingVisual = new DrawingVisual();

            using (var dc = drawingVisual.RenderOpen())
            {
                // Draw background
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    null, new Rect(0, 0, ActualWidth, ActualHeight));

                // Get visible tiles with optimal mip level for current zoom
                var viewport = _viewPortManager.VisibleRect;
                double zoom = _viewPortManager.Zoom;
                double dpiX = 96 * _viewPortManager.DpiScaleX;
                double dpiY = 96 * _viewPortManager.DpiScaleY;

                foreach (var tile in GetTilesInViewport(viewport, zoom))
                {
                    if (tile.PixelData != null)
                    {
                        RenderTile(dc, tile, dpiX, dpiY);
                    }
                }

                // Let derived classes render additional content
                OnRenderOverlay(dc);
            }

            // Render to RenderTargetBitmap for display in Image element
            if (ActualWidth > 0 && ActualHeight > 0)
            {
                int pixelWidth = (int)Math.Ceiling(ActualWidth * _viewPortManager.DpiScaleX);
                int pixelHeight = (int)Math.Ceiling(ActualHeight * _viewPortManager.DpiScaleY);

                if (pixelWidth > 0 && pixelHeight > 0)
                {
                    var renderTarget = new RenderTargetBitmap(
                        pixelWidth, pixelHeight,
                        96 * _viewPortManager.DpiScaleX,
                        96 * _viewPortManager.DpiScaleY,
                        PixelFormats.Pbgra32);

                    renderTarget.Render(drawingVisual);

                    if (_imageElement != null)
                    {
                        _imageElement.Source = renderTarget;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Override to determine if custom rendering should be used.
    /// </summary>
    protected virtual bool ShouldRenderCustom()
    {
        return false;
    }

    /// <summary>
    /// Override to provide custom rendering.
    /// </summary>
    protected virtual void RenderCustom()
    {
    }

    /// <summary>
    /// Override to render additional overlay content.
    /// </summary>
    protected virtual void OnRenderOverlay(DrawingContext dc)
    {
    }

    /// <summary>
    /// Get a tile from cache or load it.
    /// </summary>
    protected ImageTile? GetCachedTile(long tileX, long tileY, int mipLevel)
    {
        if (_tileCache == null || _mipMapManager == null) return null;

        // Check cache first
        if (_tileCache.TryGet(tileX, tileY, mipLevel, out var cachedTile))
        {
            return cachedTile;
        }

        // Load tile and add to cache
        var tile = _mipMapManager.GetTile(tileX, tileY, mipLevel);
        if (tile != null)
        {
            _tileCache.Add(tileX, tileY, mipLevel, tile);
        }
        return tile;
    }

    /// <summary>
    /// Get all tiles in viewport, using cache.
    /// </summary>
    protected IEnumerable<ImageTile> GetTilesInViewport(Rect2L viewport, double zoom)
    {
        if (_mipMapManager == null) yield break;

        int mipLevel = _mipMapManager.GetOptimalMipLevel(zoom);
        var mip = _mipMapManager.GetLevel(mipLevel);
        if (mip == null) yield break;

        int scale = 1 << mipLevel;
        int tileSize = ImageSource!.TileSize;

        // Convert viewport to mip level coordinates
        long mipViewportX = viewport.X / scale;
        long mipViewportY = viewport.Y / scale;
        long mipViewportRight = (viewport.Right + scale - 1) / scale;
        long mipViewportBottom = (viewport.Bottom + scale - 1) / scale;

        // Calculate tile range at this mip level
        long startTileX = Math.Max(0, mipViewportX / tileSize);
        long startTileY = Math.Max(0, mipViewportY / tileSize);
        long endTileX = Math.Min(mip.TilesX - 1, (mipViewportRight - 1) / tileSize);
        long endTileY = Math.Min(mip.TilesY - 1, (mipViewportBottom - 1) / tileSize);

        for (long ty = startTileY; ty <= endTileY; ty++)
        {
            for (long tx = startTileX; tx <= endTileX; tx++)
            {
                var tile = GetCachedTile(tx, ty, mipLevel);
                if (tile != null)
                {
                    yield return tile;
                }
            }
        }
    }

    /// <summary>
    /// Render a single tile.
    /// </summary>
    protected virtual void RenderTile(DrawingContext dc, ImageTile tile, double dpiX, double dpiY)
    {
        if (tile.PixelData == null) return;

        // Create WriteableBitmap from tile data
        var bitmap = CreateBitmapFromTileData(tile, dpiX, dpiY);

        // Calculate screen position for this tile
        // Account for mip level scaling
        int mipScale = 1 << tile.MipLevel;
        double effectiveZoom = _viewPortManager.Zoom * mipScale;

        double screenX = _viewPortManager.ImageToScreenX(tile.X);
        double screenY = _viewPortManager.ImageToScreenY(tile.Y);
        double screenWidth = tile.Width * effectiveZoom;
        double screenHeight = tile.Height * effectiveZoom;

        // Skip if completely outside viewport
        if (screenX + screenWidth < 0 || screenX >= ActualWidth ||
            screenY + screenHeight < 0 || screenY >= ActualHeight)
            return;

        // Use pixel-snapped coordinates to eliminate gaps without affecting size/ratio
        double x1 = Math.Floor(screenX);
        double y1 = Math.Floor(screenY);
        double x2 = Math.Ceiling(screenX + screenWidth);
        double y2 = Math.Ceiling(screenY + screenHeight);

        var destRect = new Rect(x1, y1, x2 - x1, y2 - y1);
        dc.DrawImage(bitmap, destRect);
    }

    /// <summary>
    /// Create a WriteableBitmap from tile data.
    /// </summary>
    protected virtual WriteableBitmap CreateBitmapFromTileData(ImageTile tile, double dpiX, double dpiY)
    {
        var bitmap = new WriteableBitmap(tile.Width, tile.Height, dpiX, dpiY, PixelFormats.Bgra32, null);

        bitmap.Lock();
        try
        {
            // Convert pixel data if needed
            byte[] displayData = ConvertToDisplayFormat(tile.PixelData!, tile.Width, tile.Height, tile.Format);

            System.Runtime.InteropServices.Marshal.Copy(displayData, 0, bitmap.BackBuffer, displayData.Length);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, tile.Width, tile.Height));
        }
        finally
        {
            bitmap.Unlock();
        }

        return bitmap;
    }

    /// <summary>
    /// Convert pixel data to BGRA32 display format with LUT applied.
    /// </summary>
    protected byte[] ConvertToDisplayFormat(byte[] sourceData, int width, int height, CorePixelFormat format)
    {
        int pixelCount = width * height;
        byte[] result = new byte[pixelCount * 4];

        int bytesPerPixel = format.GetBytesPerPixel();
        int maxPixels = Math.Min(pixelCount, sourceData.Length / bytesPerPixel);

        // Use LUT if available, otherwise use identity mapping
        var lut = _lookupTable;
        bool hasLut = lut != null && !lut.IsIdentity;

        switch (format)
        {
            case CorePixelFormat.Bgra32:
                if (hasLut)
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        int idx = i * 4;
                        result[idx + 0] = lut!.Apply(sourceData[idx + 0]); // B
                        result[idx + 1] = lut.Apply(sourceData[idx + 1]);  // G
                        result[idx + 2] = lut.Apply(sourceData[idx + 2]);  // R
                        result[idx + 3] = sourceData[idx + 3];             // A (unchanged)
                    }
                }
                else
                {
                    Array.Copy(sourceData, result, Math.Min(sourceData.Length, result.Length));
                }
                break;

            case CorePixelFormat.Rgba32:
                if (hasLut)
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        int srcIdx = i * 4;
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = lut!.Apply(sourceData[srcIdx + 2]); // B
                        result[dstIdx + 1] = lut.Apply(sourceData[srcIdx + 1]);  // G
                        result[dstIdx + 2] = lut.Apply(sourceData[srcIdx + 0]);  // R
                        result[dstIdx + 3] = sourceData[srcIdx + 3];             // A
                    }
                }
                else
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        int srcIdx = i * 4;
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = sourceData[srcIdx + 2]; // B
                        result[dstIdx + 1] = sourceData[srcIdx + 1]; // G
                        result[dstIdx + 2] = sourceData[srcIdx + 0]; // R
                        result[dstIdx + 3] = sourceData[srcIdx + 3]; // A
                    }
                }
                break;

            case CorePixelFormat.Bgr24:
                if (hasLut)
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        int srcIdx = i * 3;
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = lut!.Apply(sourceData[srcIdx + 0]); // B
                        result[dstIdx + 1] = lut.Apply(sourceData[srcIdx + 1]);  // G
                        result[dstIdx + 2] = lut.Apply(sourceData[srcIdx + 2]);  // R
                        result[dstIdx + 3] = 255; // A
                    }
                }
                else
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        int srcIdx = i * 3;
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = sourceData[srcIdx + 0]; // B
                        result[dstIdx + 1] = sourceData[srcIdx + 1]; // G
                        result[dstIdx + 2] = sourceData[srcIdx + 2]; // R
                        result[dstIdx + 3] = 255; // A
                    }
                }
                break;

            case CorePixelFormat.Rgb24:
                if (hasLut)
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        int srcIdx = i * 3;
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = lut!.Apply(sourceData[srcIdx + 2]); // B
                        result[dstIdx + 1] = lut.Apply(sourceData[srcIdx + 1]);  // G
                        result[dstIdx + 2] = lut.Apply(sourceData[srcIdx + 0]);  // R
                        result[dstIdx + 3] = 255; // A
                    }
                }
                else
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        int srcIdx = i * 3;
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = sourceData[srcIdx + 2]; // B
                        result[dstIdx + 1] = sourceData[srcIdx + 1]; // G
                        result[dstIdx + 2] = sourceData[srcIdx + 0]; // R
                        result[dstIdx + 3] = 255; // A
                    }
                }
                break;

            case CorePixelFormat.Gray8:
                if (hasLut)
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        byte gray = lut!.Apply(sourceData[i]);
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = gray; // B
                        result[dstIdx + 1] = gray; // G
                        result[dstIdx + 2] = gray; // R
                        result[dstIdx + 3] = 255;  // A
                    }
                }
                else
                {
                    for (int i = 0; i < maxPixels; i++)
                    {
                        byte gray = sourceData[i];
                        int dstIdx = i * 4;
                        result[dstIdx + 0] = gray; // B
                        result[dstIdx + 1] = gray; // G
                        result[dstIdx + 2] = gray; // R
                        result[dstIdx + 3] = 255;  // A
                    }
                }
                break;

            case CorePixelFormat.Gray16:
                for (int i = 0; i < maxPixels; i++)
                {
                    int srcIdx = i * 2;
                    // Read 16-bit value (little-endian)
                    ushort value = (ushort)(sourceData[srcIdx] | (sourceData[srcIdx + 1] << 8));
                    byte gray = lut?.Apply(value) ?? (byte)(value >> 8);
                    int dstIdx = i * 4;
                    result[dstIdx + 0] = gray; // B
                    result[dstIdx + 1] = gray; // G
                    result[dstIdx + 2] = gray; // R
                    result[dstIdx + 3] = 255;  // A
                }
                break;
        }

        return result;
    }

    #endregion

    #region Protected Helpers

    protected void UpdateZoomProperty()
    {
        Zoom = _viewPortManager.Zoom;
        UpdateZoomCapabilities();
    }

    protected void UpdateZoomCapabilities()
    {
        CanZoomIn = _viewPortManager.Zoom < ViewPortManager.MaxZoom;
        CanZoomOut = _viewPortManager.Zoom > ViewPortManager.MinZoom;
    }

    /// <summary>
    /// Updates the lookup table based on current DisplayMin/Max and pixel format.
    /// </summary>
    protected void UpdateLookupTable()
    {
        var range = new DisplayRange(DisplayMin, DisplayMax);

        _lookupTable = _currentPixelFormat switch
        {
            CorePixelFormat.Gray16 => LookupTable.Create16Bit(range),
            _ => LookupTable.Create8Bit(range)
        };
    }

    protected void OnViewportChanged(object? sender, EventArgs e)
    {
        RequestRender();
    }

    /// <summary>
    /// Access to ViewPortManager for derived classes.
    /// </summary>
    protected ViewPortManager ViewPort => _viewPortManager;

    /// <summary>
    /// Access to MipMapManager for derived classes.
    /// </summary>
    protected MipMapManager? MipMap => _mipMapManager;

    /// <summary>
    /// Access to TileCache for derived classes.
    /// </summary>
    protected TileCache? Cache => _tileCache;

    #endregion
}
