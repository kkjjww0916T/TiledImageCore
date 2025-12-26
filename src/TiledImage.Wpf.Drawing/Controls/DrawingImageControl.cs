using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Geometry.Primitives;
using Geometry.Shapes.Geometry;
using Geometry.Shapes.Shapes;
using Geometry.Shapes.Types;
using TiledImage.Core.Tiling;
using TiledImage.Wpf.Viewer;

namespace TiledImage.Wpf.Drawing;

/// <summary>
/// Simple relay command implementation for control commands.
/// </summary>
internal class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Shape creation mode for DrawingImageControl.
/// </summary>
public enum ShapeCreationMode
{
    /// <summary>No creation mode - select/move shapes.</summary>
    None,
    /// <summary>Create circle by dragging from center.</summary>
    Circle,
    /// <summary>Create ellipse by dragging diagonal.</summary>
    Ellipse,
    /// <summary>Create rectangle by dragging diagonal.</summary>
    Rectangle,
    /// <summary>Create oblong by dragging diagonal.</summary>
    Oblong,
    /// <summary>Create polygon by clicking points.</summary>
    Polygon
}

/// <summary>
/// Resize handle position on a shape.
/// </summary>
public enum ResizeHandlePosition
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

/// <summary>
/// A control for viewing large images with shape overlay support.
/// Control raises events for shape operations; ViewModel handles data changes.
/// </summary>
public class DrawingImageControl : ImageViewControl
{
    private ShapeRenderer? _shapeRenderer;
    private bool _isDraggingShape;
    private Point _dragStartImagePosition;

    // Shape creation preview (not added to Shapes collection)
    private bool _isCreatingShape;
    private IShape? _previewShape;
    private Point _creationStartPoint;
    private PolygonShape? _previewPolygon;

    // Marquee selection state
    private bool _isMarqueeSelecting;
    private Point _marqueeStartPoint;
    private Point _marqueeCurrentPoint;

    // Multi-shape dragging state
    private Dictionary<IShape, (double X, double Y)>? _multiDragStartPositions;

    // Current mouse position in image coordinates
    private Point _currentMouseImagePosition;

    // Resize handle state
    private bool _isResizing;
    private IShape? _resizingShape;
    private ResizeHandlePosition _activeHandle;
    private double _resizeStartCenterX;
    private double _resizeStartCenterY;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private Point _resizeMouseStart;

    // Resize handle rendering settings
    private const double ResizeHandleSize = 8.0;
    private const double ResizeHandleHitRadius = 10.0;

    #region Dependency Properties

    public static readonly DependencyProperty ShapesProperty =
        DependencyProperty.Register(
            nameof(Shapes),
            typeof(IEnumerable),
            typeof(DrawingImageControl),
            new PropertyMetadata(null, OnShapesChanged));

    public static readonly DependencyProperty SelectedShapesProperty =
        DependencyProperty.Register(
            nameof(SelectedShapes),
            typeof(ObservableCollection<IShape>),
            typeof(DrawingImageControl),
            new PropertyMetadata(null, OnSelectedShapesChanged));

    public static readonly DependencyProperty IsShapeEditEnabledProperty =
        DependencyProperty.Register(
            nameof(IsShapeEditEnabled),
            typeof(bool),
            typeof(DrawingImageControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty CreationModeProperty =
        DependencyProperty.Register(
            nameof(CreationMode),
            typeof(ShapeCreationMode),
            typeof(DrawingImageControl),
            new FrameworkPropertyMetadata(ShapeCreationMode.None,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCreationModeChanged));

    public static readonly DependencyProperty NewShapeStyleProperty =
        DependencyProperty.Register(
            nameof(NewShapeStyle),
            typeof(ShapeStyle),
            typeof(DrawingImageControl),
            new PropertyMetadata(ShapeStyle.Default));

    public static readonly DependencyProperty SelectionChangedCommandProperty =
        DependencyProperty.Register(
            nameof(SelectionChangedCommand),
            typeof(ICommand),
            typeof(DrawingImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShapeCreationRequestedCommandProperty =
        DependencyProperty.Register(
            nameof(ShapeCreationRequestedCommand),
            typeof(ICommand),
            typeof(DrawingImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShapeDeleteRequestedCommandProperty =
        DependencyProperty.Register(
            nameof(ShapeDeleteRequestedCommand),
            typeof(ICommand),
            typeof(DrawingImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShapeCopyRequestedCommandProperty =
        DependencyProperty.Register(
            nameof(ShapeCopyRequestedCommand),
            typeof(ICommand),
            typeof(DrawingImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShapePasteRequestedCommandProperty =
        DependencyProperty.Register(
            nameof(ShapePasteRequestedCommand),
            typeof(ICommand),
            typeof(DrawingImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShapeMovedCommandProperty =
        DependencyProperty.Register(
            nameof(ShapeMovedCommand),
            typeof(ICommand),
            typeof(DrawingImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShapeResizeRequestedCommandProperty =
        DependencyProperty.Register(
            nameof(ShapeResizeRequestedCommand),
            typeof(ICommand),
            typeof(DrawingImageControl),
            new PropertyMetadata(null));

    /// <summary>
    /// The collection of shapes to display.
    /// </summary>
    public IEnumerable? Shapes
    {
        get => (IEnumerable?)GetValue(ShapesProperty);
        set => SetValue(ShapesProperty, value);
    }

    /// <summary>
    /// The collection of selected shapes (multi-selection).
    /// </summary>
    public ObservableCollection<IShape>? SelectedShapes
    {
        get => (ObservableCollection<IShape>?)GetValue(SelectedShapesProperty);
        set => SetValue(SelectedShapesProperty, value);
    }

    /// <summary>
    /// Whether shape editing (selection, movement) is enabled.
    /// </summary>
    public bool IsShapeEditEnabled
    {
        get => (bool)GetValue(IsShapeEditEnabledProperty);
        set => SetValue(IsShapeEditEnabledProperty, value);
    }

    /// <summary>
    /// The current shape creation mode.
    /// </summary>
    public ShapeCreationMode CreationMode
    {
        get => (ShapeCreationMode)GetValue(CreationModeProperty);
        set => SetValue(CreationModeProperty, value);
    }

    /// <summary>
    /// The style to apply to newly created shapes.
    /// </summary>
    public ShapeStyle NewShapeStyle
    {
        get => (ShapeStyle)GetValue(NewShapeStyleProperty);
        set => SetValue(NewShapeStyleProperty, value);
    }

    /// <summary>
    /// Command to execute when selection changes.
    /// </summary>
    public ICommand? SelectionChangedCommand
    {
        get => (ICommand?)GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when shape creation is requested.
    /// </summary>
    public ICommand? ShapeCreationRequestedCommand
    {
        get => (ICommand?)GetValue(ShapeCreationRequestedCommandProperty);
        set => SetValue(ShapeCreationRequestedCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when shape deletion is requested.
    /// </summary>
    public ICommand? ShapeDeleteRequestedCommand
    {
        get => (ICommand?)GetValue(ShapeDeleteRequestedCommandProperty);
        set => SetValue(ShapeDeleteRequestedCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when shape copy is requested.
    /// </summary>
    public ICommand? ShapeCopyRequestedCommand
    {
        get => (ICommand?)GetValue(ShapeCopyRequestedCommandProperty);
        set => SetValue(ShapeCopyRequestedCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when shape paste is requested.
    /// </summary>
    public ICommand? ShapePasteRequestedCommand
    {
        get => (ICommand?)GetValue(ShapePasteRequestedCommandProperty);
        set => SetValue(ShapePasteRequestedCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when shapes are moved.
    /// </summary>
    public ICommand? ShapeMovedCommand
    {
        get => (ICommand?)GetValue(ShapeMovedCommandProperty);
        set => SetValue(ShapeMovedCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when shape resize is requested.
    /// </summary>
    public ICommand? ShapeResizeRequestedCommand
    {
        get => (ICommand?)GetValue(ShapeResizeRequestedCommandProperty);
        set => SetValue(ShapeResizeRequestedCommandProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent ShapeSelectedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ShapeSelected),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(DrawingImageControl));

    public static readonly RoutedEvent ShapeMovedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ShapeMoved),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(DrawingImageControl));

    public static readonly RoutedEvent SelectionChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectionChanged),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ShapeSelectionChangedEventArgs>),
            typeof(DrawingImageControl));

    public static readonly RoutedEvent ShapeCreationRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ShapeCreationRequested),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ShapeCreationRequestedEventArgs>),
            typeof(DrawingImageControl));

    public static readonly RoutedEvent ShapeDeleteRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ShapeDeleteRequested),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ShapeDeleteRequestedEventArgs>),
            typeof(DrawingImageControl));

    public static readonly RoutedEvent ShapeCopyRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ShapeCopyRequested),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ShapeCopyRequestedEventArgs>),
            typeof(DrawingImageControl));

    public static readonly RoutedEvent ShapePasteRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ShapePasteRequested),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ShapePasteRequestedEventArgs>),
            typeof(DrawingImageControl));

    public static readonly RoutedEvent ShapeResizeRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ShapeResizeRequested),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ShapeResizeRequestedEventArgs>),
            typeof(DrawingImageControl));

    public event RoutedEventHandler ShapeSelected
    {
        add => AddHandler(ShapeSelectedEvent, value);
        remove => RemoveHandler(ShapeSelectedEvent, value);
    }

    public event RoutedEventHandler ShapeMoved
    {
        add => AddHandler(ShapeMovedEvent, value);
        remove => RemoveHandler(ShapeMovedEvent, value);
    }

    public event EventHandler<ShapeSelectionChangedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public event EventHandler<ShapeCreationRequestedEventArgs> ShapeCreationRequested
    {
        add => AddHandler(ShapeCreationRequestedEvent, value);
        remove => RemoveHandler(ShapeCreationRequestedEvent, value);
    }

    public event EventHandler<ShapeDeleteRequestedEventArgs> ShapeDeleteRequested
    {
        add => AddHandler(ShapeDeleteRequestedEvent, value);
        remove => RemoveHandler(ShapeDeleteRequestedEvent, value);
    }

    public event EventHandler<ShapeCopyRequestedEventArgs> ShapeCopyRequested
    {
        add => AddHandler(ShapeCopyRequestedEvent, value);
        remove => RemoveHandler(ShapeCopyRequestedEvent, value);
    }

    public event EventHandler<ShapePasteRequestedEventArgs> ShapePasteRequested
    {
        add => AddHandler(ShapePasteRequestedEvent, value);
        remove => RemoveHandler(ShapePasteRequestedEvent, value);
    }

    public event EventHandler<ShapeResizeRequestedEventArgs> ShapeResizeRequested
    {
        add => AddHandler(ShapeResizeRequestedEvent, value);
        remove => RemoveHandler(ShapeResizeRequestedEvent, value);
    }

    #endregion

    static DrawingImageControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DrawingImageControl),
            new FrameworkPropertyMetadata(typeof(DrawingImageControl)));
    }

    public DrawingImageControl()
    {
        SelectedShapes = new ObservableCollection<IShape>();
        Focusable = true;
        InitializeCommands();
    }

    #region Commands

    private ICommand? _fitToViewCommand;

    public ICommand FitToViewCommand => _fitToViewCommand!;

    private void InitializeCommands()
    {
        _fitToViewCommand = new RelayCommand(FitToView, () => ImageSource != null);
    }

    #endregion

    protected override void OnApplyTemplateCore()
    {
        base.OnApplyTemplateCore();
        _shapeRenderer = new ShapeRenderer(_viewPortManager);
    }

    protected override void OnImageSourceChangedCore(TiledImageSource? oldSource, TiledImageSource? newSource)
    {
        base.OnImageSourceChangedCore(oldSource, newSource);
        if (newSource != null && newSource.IsLoaded)
        {
            FitToView();
        }
    }

    #region Property Changed Handlers

    private static void OnShapesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DrawingImageControl control) return;

        if (e.OldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnShapesCollectionChanged;

        if (e.OldValue is IEnumerable oldShapes)
        {
            foreach (var shape in oldShapes.OfType<INotifyPropertyChanged>())
                shape.PropertyChanged -= control.OnShapePropertyChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += control.OnShapesCollectionChanged;

        if (e.NewValue is IEnumerable newShapes)
        {
            foreach (var shape in newShapes.OfType<INotifyPropertyChanged>())
                shape.PropertyChanged += control.OnShapePropertyChanged;
        }

        control.RequestRender();
    }

    private void OnShapesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var shape in e.OldItems.OfType<INotifyPropertyChanged>())
                shape.PropertyChanged -= OnShapePropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var shape in e.NewItems.OfType<INotifyPropertyChanged>())
                shape.PropertyChanged += OnShapePropertyChanged;
        }

        RequestRender();
    }

    private void OnShapePropertyChanged(object? sender, PropertyChangedEventArgs e) => RequestRender();

    private static void OnCreationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DrawingImageControl control) return;
        control.CancelShapeCreation();
        control.Cursor = control.CreationMode != ShapeCreationMode.None ? Cursors.Cross : Cursors.Arrow;
    }

    private static void OnSelectedShapesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DrawingImageControl control) return;

        if (e.OldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnSelectedShapesCollectionChanged;

        if (e.NewValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += control.OnSelectedShapesCollectionChanged;

        control.RequestRender();
    }

    private void OnSelectedShapesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RequestRender();
    }

    private void RaiseSelectionChanged(IEnumerable<IShape> added, IEnumerable<IShape> removed)
    {
        var args = new ShapeSelectionChangedEventArgs(
            SelectionChangedEvent, added, removed,
            SelectedShapes ?? Enumerable.Empty<IShape>());
        RaiseEvent(args);

        if (SelectionChangedCommand?.CanExecute(args) == true)
            SelectionChangedCommand.Execute(args);
    }

    private void CancelShapeCreation()
    {
        _isCreatingShape = false;
        _previewShape = null;
        _previewPolygon = null;
        ReleaseMouseCapture();
        RequestRender();
    }

    #endregion

    #region Keyboard Handling

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        var isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (e.Key)
        {
            case Key.Delete:
                RaiseShapeDeleteRequested();
                e.Handled = true;
                break;

            case Key.C when isCtrl:
                RaiseShapeCopyRequested();
                e.Handled = true;
                break;

            case Key.V when isCtrl:
                RaiseShapePasteRequested();
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isCreatingShape)
                {
                    CancelShapeCreation();
                    e.Handled = true;
                }
                break;
        }
    }

    private void RaiseShapeDeleteRequested()
    {
        if (SelectedShapes == null || SelectedShapes.Count == 0) return;

        var args = new ShapeDeleteRequestedEventArgs(ShapeDeleteRequestedEvent);
        RaiseEvent(args);

        if (ShapeDeleteRequestedCommand?.CanExecute(args) == true)
            ShapeDeleteRequestedCommand.Execute(args);
    }

    private void RaiseShapeCopyRequested()
    {
        if (SelectedShapes == null || SelectedShapes.Count == 0) return;

        var args = new ShapeCopyRequestedEventArgs(ShapeCopyRequestedEvent);
        RaiseEvent(args);

        if (ShapeCopyRequestedCommand?.CanExecute(args) == true)
            ShapeCopyRequestedCommand.Execute(args);
    }

    private void RaiseShapePasteRequested()
    {
        var args = new ShapePasteRequestedEventArgs(
            ShapePasteRequestedEvent,
            _currentMouseImagePosition.X,
            _currentMouseImagePosition.Y);
        RaiseEvent(args);

        if (ShapePasteRequestedCommand?.CanExecute(args) == true)
            ShapePasteRequestedCommand.Execute(args);
    }

    private void RaiseShapeResizeRequested(IShape shape, double newCenterX, double newCenterY, double newWidth, double newHeight)
    {
        var args = new ShapeResizeRequestedEventArgs(
            ShapeResizeRequestedEvent,
            shape,
            newCenterX,
            newCenterY,
            newWidth,
            newHeight);
        RaiseEvent(args);

        if (ShapeResizeRequestedCommand?.CanExecute(args) == true)
            ShapeResizeRequestedCommand.Execute(args);
    }

    #endregion

    #region Mouse Handling

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus(); // Enable keyboard events
    }

    protected override bool OnMouseLeftButtonDownCore(MouseButtonEventArgs e, Point position)
    {
        if (!IsShapeEditEnabled)
            return base.OnMouseLeftButtonDownCore(e, position);

        var imagePos = _viewPortManager.ScreenToImage(position);
        var isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        // Shape creation mode
        if (CreationMode != ShapeCreationMode.None)
        {
            return HandleCreationMouseDown(imagePos.X, imagePos.Y, e);
        }

        if (Shapes == null)
            return base.OnMouseLeftButtonDownCore(e, position);

        // Check for resize handle hit (only when single shape selected)
        var (resizeShape, handlePos) = HitTestResizeHandles(imagePos.X, imagePos.Y);
        if (resizeShape != null && handlePos != ResizeHandlePosition.None)
        {
            _isResizing = true;
            _resizingShape = resizeShape;
            _activeHandle = handlePos;
            var (w, h) = GetShapeDimensions(resizeShape);
            _resizeStartCenterX = resizeShape.CenterX;
            _resizeStartCenterY = resizeShape.CenterY;
            _resizeStartWidth = w;
            _resizeStartHeight = h;
            _resizeMouseStart = new Point(imagePos.X, imagePos.Y);
            CaptureMouse();
            Cursor = GetResizeCursor(handlePos);
            e.Handled = true;
            return true;
        }

        // Selection/dragging mode
        var hitShape = ShapeHitTester.HitTestShapes(
            Shapes.OfType<IShape>(), imagePos.X, imagePos.Y);

        if (hitShape != null)
        {
            if (isCtrlPressed)
            {
                // Ctrl+Click: Toggle selection
                if (SelectedShapes!.Contains(hitShape))
                {
                    SelectedShapes.Remove(hitShape);
                    RaiseSelectionChanged(Array.Empty<IShape>(), new[] { hitShape });
                }
                else
                {
                    SelectedShapes.Add(hitShape);
                    RaiseSelectionChanged(new[] { hitShape }, Array.Empty<IShape>());
                }
            }
            else
            {
                if (!SelectedShapes!.Contains(hitShape))
                {
                    var removed = SelectedShapes.ToList();
                    SelectedShapes.Clear();
                    SelectedShapes.Add(hitShape);
                    RaiseSelectionChanged(new[] { hitShape }, removed);
                }
            }

            // Start dragging
            _isDraggingShape = true;
            _dragStartImagePosition = new Point(imagePos.X, imagePos.Y);
            _multiDragStartPositions = new Dictionary<IShape, (double X, double Y)>();
            foreach (var shape in SelectedShapes)
                _multiDragStartPositions[shape] = (shape.CenterX, shape.CenterY);

            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
            return true;
        }
        else
        {
            // Click on empty area
            if (!isCtrlPressed && SelectedShapes!.Count > 0)
            {
                var removed = SelectedShapes.ToList();
                SelectedShapes.Clear();
                RaiseSelectionChanged(Array.Empty<IShape>(), removed);
            }

            // Start marquee selection
            _isMarqueeSelecting = true;
            _marqueeStartPoint = new Point(imagePos.X, imagePos.Y);
            _marqueeCurrentPoint = _marqueeStartPoint;
            CaptureMouse();
            e.Handled = true;
            return true;
        }
    }

    private bool HandleCreationMouseDown(double imageX, double imageY, MouseButtonEventArgs e)
    {
        if (CreationMode == ShapeCreationMode.Polygon)
            return HandlePolygonClick(imageX, imageY, e);

        // Start drag creation - preview shape only (not added to Shapes)
        _isCreatingShape = true;
        _creationStartPoint = new Point(imageX, imageY);
        _previewShape = CreatePreviewShape(CreationMode, imageX, imageY, 1, 1);

        CaptureMouse();
        e.Handled = true;
        return true;
    }

    private bool HandlePolygonClick(double imageX, double imageY, MouseButtonEventArgs e)
    {
        if (_previewPolygon == null)
        {
            _previewPolygon = new PolygonShape(imageX, imageY) { Style = NewShapeStyle };
            _previewPolygon.AddPoint(0, 0);
            _previewShape = _previewPolygon;
            _isCreatingShape = true;
        }
        else
        {
            var relX = imageX - _previewPolygon.CenterX;
            var relY = imageY - _previewPolygon.CenterY;
            _previewPolygon.AddPoint(relX, relY);
        }

        RequestRender();
        e.Handled = true;
        return true;
    }

    private IShape? CreatePreviewShape(ShapeCreationMode mode, double centerX, double centerY, double width, double height)
    {
        IShape? shape = mode switch
        {
            ShapeCreationMode.Circle => new EllipseShape(centerX, centerY, Math.Max(1, width / 2), Math.Max(1, width / 2)),
            ShapeCreationMode.Ellipse => new EllipseShape(centerX, centerY, Math.Max(1, width / 2), Math.Max(1, height / 2)),
            ShapeCreationMode.Rectangle => new RectangleShape(centerX, centerY, Math.Max(1, width), Math.Max(1, height)),
            ShapeCreationMode.Oblong => new OblongShape(centerX, centerY, Math.Max(1, width), Math.Max(1, height)),
            _ => null
        };

        if (shape != null)
            shape.Style = NewShapeStyle;

        return shape;
    }

    protected override bool OnMouseLeftButtonUpCore(MouseButtonEventArgs e)
    {
        if (_isCreatingShape && _previewShape != null && CreationMode != ShapeCreationMode.Polygon)
        {
            FinalizeShapeCreation();
            e.Handled = true;
            return true;
        }

        if (_isMarqueeSelecting)
        {
            FinalizeMarqueeSelection();
            e.Handled = true;
            return true;
        }

        if (_isResizing && _resizingShape != null)
        {
            // Get current dimensions from the shape
            var (newWidth, newHeight) = GetShapeDimensions(_resizingShape);
            var newCenterX = _resizingShape.CenterX;
            var newCenterY = _resizingShape.CenterY;

            // Restore original dimensions (event handler will update)
            ApplyDimensionsToShape(_resizingShape, _resizeStartCenterX, _resizeStartCenterY, _resizeStartWidth, _resizeStartHeight);

            // Fire the resize event
            RaiseShapeResizeRequested(_resizingShape, newCenterX, newCenterY, newWidth, newHeight);

            // Reset resize state
            _isResizing = false;
            _resizingShape = null;
            _activeHandle = ResizeHandlePosition.None;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            e.Handled = true;
            return true;
        }

        if (_isDraggingShape)
        {
            _isDraggingShape = false;
            _multiDragStartPositions = null;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            RaiseEvent(new RoutedEventArgs(ShapeMovedEvent));
            ShapeMovedCommand?.Execute(null);
            e.Handled = true;
            return true;
        }

        return base.OnMouseLeftButtonUpCore(e);
    }

    private void FinalizeMarqueeSelection()
    {
        _isMarqueeSelecting = false;
        ReleaseMouseCapture();

        if (Shapes == null) return;

        var minX = Math.Min(_marqueeStartPoint.X, _marqueeCurrentPoint.X);
        var minY = Math.Min(_marqueeStartPoint.Y, _marqueeCurrentPoint.Y);
        var maxX = Math.Max(_marqueeStartPoint.X, _marqueeCurrentPoint.X);
        var maxY = Math.Max(_marqueeStartPoint.Y, _marqueeCurrentPoint.Y);

        if (Math.Abs(maxX - minX) < 5 && Math.Abs(maxY - minY) < 5)
        {
            RequestRender();
            return;
        }

        var isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var previousSelection = SelectedShapes!.ToList();
        var addedShapes = new List<IShape>();

        if (!isCtrlPressed)
            SelectedShapes.Clear();

        foreach (var shape in Shapes.OfType<IShape>())
        {
            var bounds = shape.GetWorldBounds();
            if (bounds.X + bounds.Width >= minX && bounds.X <= maxX &&
                bounds.Y + bounds.Height >= minY && bounds.Y <= maxY)
            {
                if (!SelectedShapes.Contains(shape))
                {
                    SelectedShapes.Add(shape);
                    addedShapes.Add(shape);
                }
            }
        }

        var removedShapes = isCtrlPressed
            ? Array.Empty<IShape>()
            : previousSelection.Except(SelectedShapes).ToArray();

        if (addedShapes.Count > 0 || removedShapes.Length > 0)
            RaiseSelectionChanged(addedShapes, removedShapes);

        RaiseEvent(new RoutedEventArgs(ShapeSelectedEvent));
        RequestRender();
    }

    private void FinalizeShapeCreation()
    {
        if (_previewShape == null) return;

        var mode = CreationMode;
        double width, height;
        IReadOnlyList<Point2D>? relativePoints = null;

        // Extract shape dimensions
        switch (_previewShape)
        {
            case EllipseShape ellipse:
                width = ellipse.RadiusX * 2;
                height = ellipse.RadiusY * 2;
                break;
            case RectangleShape rect:
                width = rect.Width;
                height = rect.Height;
                break;
            case OblongShape oblong:
                width = oblong.Width;
                height = oblong.Height;
                break;
            case PolygonShape polygon:
                width = 0;
                height = 0;
                relativePoints = polygon.RelativePoints.ToList().AsReadOnly();
                break;
            default:
                width = 0;
                height = 0;
                break;
        }

        var args = new ShapeCreationRequestedEventArgs(
            ShapeCreationRequestedEvent,
            mode,
            _previewShape.CenterX,
            _previewShape.CenterY,
            width,
            height,
            NewShapeStyle,
            relativePoints);

        // Reset state
        _isCreatingShape = false;
        _previewShape = null;
        _previewPolygon = null;
        ReleaseMouseCapture();
        Cursor = CreationMode != ShapeCreationMode.None ? Cursors.Cross : Cursors.Arrow;

        // Raise event
        RaiseEvent(args);

        if (ShapeCreationRequestedCommand?.CanExecute(args) == true)
            ShapeCreationRequestedCommand.Execute(args);

        RequestRender();
    }

    protected override bool OnMouseMoveCore(MouseEventArgs e, Point position)
    {
        var imagePos = _viewPortManager.ScreenToImage(position);
        _currentMouseImagePosition = new Point(imagePos.X, imagePos.Y);

        if (_isCreatingShape && _previewShape != null)
        {
            UpdatePreviewShape(imagePos.X, imagePos.Y);
            RequestRender();
            return true;
        }

        if (CreationMode == ShapeCreationMode.Polygon && _previewPolygon != null)
        {
            RequestRender();
            return true;
        }

        if (_isMarqueeSelecting)
        {
            _marqueeCurrentPoint = new Point(imagePos.X, imagePos.Y);
            RequestRender();
            return true;
        }

        if (_isResizing && _resizingShape != null)
        {
            UpdateResizePreview(imagePos.X, imagePos.Y);
            RequestRender();
            return true;
        }

        if (_isDraggingShape && _multiDragStartPositions != null && _multiDragStartPositions.Count > 0)
        {
            var deltaX = imagePos.X - _dragStartImagePosition.X;
            var deltaY = imagePos.Y - _dragStartImagePosition.Y;

            foreach (var kvp in _multiDragStartPositions)
            {
                kvp.Key.CenterX = kvp.Value.X + deltaX;
                kvp.Key.CenterY = kvp.Value.Y + deltaY;
            }

            RequestRender();
            return true;
        }

        if (CreationMode == ShapeCreationMode.None && IsShapeEditEnabled && Shapes != null && !_isPanning)
        {
            // Check for resize handle hover first
            var (_, handlePos) = HitTestResizeHandles(imagePos.X, imagePos.Y);
            if (handlePos != ResizeHandlePosition.None)
            {
                Cursor = GetResizeCursor(handlePos);
            }
            else
            {
                var hitShape = ShapeHitTester.HitTestShapes(
                    Shapes.OfType<IShape>(), imagePos.X, imagePos.Y);
                Cursor = hitShape != null ? Cursors.Hand : Cursors.Arrow;
            }
        }

        return base.OnMouseMoveCore(e, position);
    }

    protected override bool OnMouseRightButtonDownCore(MouseButtonEventArgs e, Point position)
    {
        if (CreationMode == ShapeCreationMode.Polygon && _previewPolygon != null && _previewPolygon.RelativePoints.Count >= 3)
        {
            FinalizeShapeCreation();
            e.Handled = true;
            return true;
        }

        if (_isCreatingShape)
        {
            CancelShapeCreation();
            e.Handled = true;
            return true;
        }

        // Let base class handle right-click panning
        return false;
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        if (CreationMode == ShapeCreationMode.Polygon && _previewPolygon != null && _previewPolygon.RelativePoints.Count >= 3)
        {
            FinalizeShapeCreation();
            e.Handled = true;
            return;
        }

        base.OnMouseDoubleClick(e);
    }

    private void UpdatePreviewShape(double currentX, double currentY)
    {
        if (_previewShape == null) return;

        var startX = _creationStartPoint.X;
        var startY = _creationStartPoint.Y;

        switch (CreationMode)
        {
            case ShapeCreationMode.Circle when _previewShape is EllipseShape circle:
                var radius = Math.Sqrt(Math.Pow(currentX - startX, 2) + Math.Pow(currentY - startY, 2));
                circle.CenterX = startX;
                circle.CenterY = startY;
                circle.RadiusX = circle.RadiusY = Math.Max(1, radius);
                break;

            case ShapeCreationMode.Ellipse when _previewShape is EllipseShape ellipse:
                ellipse.CenterX = (startX + currentX) / 2;
                ellipse.CenterY = (startY + currentY) / 2;
                ellipse.RadiusX = Math.Max(1, Math.Abs(currentX - startX) / 2);
                ellipse.RadiusY = Math.Max(1, Math.Abs(currentY - startY) / 2);
                break;

            case ShapeCreationMode.Rectangle when _previewShape is RectangleShape rect:
                rect.CenterX = (startX + currentX) / 2;
                rect.CenterY = (startY + currentY) / 2;
                rect.Width = Math.Max(1, Math.Abs(currentX - startX));
                rect.Height = Math.Max(1, Math.Abs(currentY - startY));
                break;

            case ShapeCreationMode.Oblong when _previewShape is OblongShape oblong:
                oblong.CenterX = (startX + currentX) / 2;
                oblong.CenterY = (startY + currentY) / 2;
                oblong.Width = Math.Max(1, Math.Abs(currentX - startX));
                oblong.Height = Math.Max(1, Math.Abs(currentY - startY));
                break;
        }
    }

    #endregion

    #region Resize Handles

    private static (double Width, double Height) GetShapeDimensions(IShape shape)
    {
        return shape switch
        {
            RectangleShape rect => (rect.Width, rect.Height),
            OblongShape oblong => (oblong.Width, oblong.Height),
            EllipseShape ellipse => (ellipse.RadiusX * 2, ellipse.RadiusY * 2),
            _ => (0, 0) // Polygons don't support resize handles
        };
    }

    private static bool SupportsResize(IShape shape)
    {
        return shape is RectangleShape or OblongShape or EllipseShape;
    }

    private IEnumerable<(ResizeHandlePosition Position, Point2D ImagePoint)> GetResizeHandlePositions(IShape shape)
    {
        if (!SupportsResize(shape)) yield break;

        var (width, height) = GetShapeDimensions(shape);
        var halfW = width / 2;
        var halfH = height / 2;
        var cx = shape.CenterX;
        var cy = shape.CenterY;
        var center = new Point2D(cx, cy);
        var rotation = (shape as ShapeBase)?.Rotation ?? 0;

        // 8 handles: 4 corners + 4 edges (in local coordinates)
        var handles = new (ResizeHandlePosition Position, Point2D LocalPoint)[]
        {
            (ResizeHandlePosition.TopLeft, new Point2D(cx - halfW, cy - halfH)),
            (ResizeHandlePosition.Top, new Point2D(cx, cy - halfH)),
            (ResizeHandlePosition.TopRight, new Point2D(cx + halfW, cy - halfH)),
            (ResizeHandlePosition.Right, new Point2D(cx + halfW, cy)),
            (ResizeHandlePosition.BottomRight, new Point2D(cx + halfW, cy + halfH)),
            (ResizeHandlePosition.Bottom, new Point2D(cx, cy + halfH)),
            (ResizeHandlePosition.BottomLeft, new Point2D(cx - halfW, cy + halfH)),
            (ResizeHandlePosition.Left, new Point2D(cx - halfW, cy))
        };

        foreach (var (position, localPoint) in handles)
        {
            var worldPoint = rotation == 0
                ? localPoint
                : localPoint.RotateAroundDegrees(center, rotation);
            yield return (position, worldPoint);
        }
    }

    private (IShape? Shape, ResizeHandlePosition Handle) HitTestResizeHandles(double imageX, double imageY)
    {
        if (SelectedShapes == null || SelectedShapes.Count != 1) return (null, ResizeHandlePosition.None);

        var shape = SelectedShapes[0];
        if (!SupportsResize(shape)) return (null, ResizeHandlePosition.None);

        // Convert hit radius from screen to image coordinates
        var hitRadiusImage = ResizeHandleHitRadius / Zoom;

        foreach (var (position, point) in GetResizeHandlePositions(shape))
        {
            var dx = imageX - point.X;
            var dy = imageY - point.Y;
            if (dx * dx + dy * dy <= hitRadiusImage * hitRadiusImage)
            {
                return (shape, position);
            }
        }

        return (null, ResizeHandlePosition.None);
    }

    private Cursor GetResizeCursor(ResizeHandlePosition position)
    {
        return position switch
        {
            ResizeHandlePosition.TopLeft or ResizeHandlePosition.BottomRight => Cursors.SizeNWSE,
            ResizeHandlePosition.TopRight or ResizeHandlePosition.BottomLeft => Cursors.SizeNESW,
            ResizeHandlePosition.Top or ResizeHandlePosition.Bottom => Cursors.SizeNS,
            ResizeHandlePosition.Left or ResizeHandlePosition.Right => Cursors.SizeWE,
            _ => Cursors.Arrow
        };
    }

    private void UpdateResizePreview(double currentX, double currentY)
    {
        if (_resizingShape == null) return;

        var deltaX = currentX - _resizeMouseStart.X;
        var deltaY = currentY - _resizeMouseStart.Y;

        // Get rotation angle and transform delta to local coordinates
        var rotation = (_resizingShape as ShapeBase)?.Rotation ?? 0;
        double localDeltaX, localDeltaY;

        if (rotation != 0)
        {
            var radians = -rotation * Math.PI / 180; // Inverse rotation
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            localDeltaX = deltaX * cos - deltaY * sin;
            localDeltaY = deltaX * sin + deltaY * cos;
        }
        else
        {
            localDeltaX = deltaX;
            localDeltaY = deltaY;
        }

        double newWidth = _resizeStartWidth;
        double newHeight = _resizeStartHeight;
        double localOffsetX = 0;
        double localOffsetY = 0;

        // Calculate new dimensions based on which handle is being dragged (in local coordinates)
        switch (_activeHandle)
        {
            case ResizeHandlePosition.TopLeft:
                newWidth = Math.Max(10, _resizeStartWidth - localDeltaX);
                newHeight = Math.Max(10, _resizeStartHeight - localDeltaY);
                localOffsetX = (_resizeStartWidth - newWidth) / 2;
                localOffsetY = (_resizeStartHeight - newHeight) / 2;
                break;

            case ResizeHandlePosition.Top:
                newHeight = Math.Max(10, _resizeStartHeight - localDeltaY);
                localOffsetY = (_resizeStartHeight - newHeight) / 2;
                break;

            case ResizeHandlePosition.TopRight:
                newWidth = Math.Max(10, _resizeStartWidth + localDeltaX);
                newHeight = Math.Max(10, _resizeStartHeight - localDeltaY);
                localOffsetX = (newWidth - _resizeStartWidth) / 2;
                localOffsetY = (_resizeStartHeight - newHeight) / 2;
                break;

            case ResizeHandlePosition.Right:
                newWidth = Math.Max(10, _resizeStartWidth + localDeltaX);
                localOffsetX = (newWidth - _resizeStartWidth) / 2;
                break;

            case ResizeHandlePosition.BottomRight:
                newWidth = Math.Max(10, _resizeStartWidth + localDeltaX);
                newHeight = Math.Max(10, _resizeStartHeight + localDeltaY);
                localOffsetX = (newWidth - _resizeStartWidth) / 2;
                localOffsetY = (newHeight - _resizeStartHeight) / 2;
                break;

            case ResizeHandlePosition.Bottom:
                newHeight = Math.Max(10, _resizeStartHeight + localDeltaY);
                localOffsetY = (newHeight - _resizeStartHeight) / 2;
                break;

            case ResizeHandlePosition.BottomLeft:
                newWidth = Math.Max(10, _resizeStartWidth - localDeltaX);
                newHeight = Math.Max(10, _resizeStartHeight + localDeltaY);
                localOffsetX = (_resizeStartWidth - newWidth) / 2;
                localOffsetY = (newHeight - _resizeStartHeight) / 2;
                break;

            case ResizeHandlePosition.Left:
                newWidth = Math.Max(10, _resizeStartWidth - localDeltaX);
                localOffsetX = (_resizeStartWidth - newWidth) / 2;
                break;
        }

        // Transform center offset back to world coordinates (forward rotation)
        double worldOffsetX, worldOffsetY;
        if (rotation != 0)
        {
            var radians = rotation * Math.PI / 180; // Forward rotation
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            worldOffsetX = localOffsetX * cos - localOffsetY * sin;
            worldOffsetY = localOffsetX * sin + localOffsetY * cos;
        }
        else
        {
            worldOffsetX = localOffsetX;
            worldOffsetY = localOffsetY;
        }

        double newCenterX = _resizeStartCenterX + worldOffsetX;
        double newCenterY = _resizeStartCenterY + worldOffsetY;

        // Apply dimensions to shape for visual preview (will be overwritten by event handler)
        ApplyDimensionsToShape(_resizingShape, newCenterX, newCenterY, newWidth, newHeight);
    }

    private static void ApplyDimensionsToShape(IShape shape, double centerX, double centerY, double width, double height)
    {
        shape.CenterX = centerX;
        shape.CenterY = centerY;

        switch (shape)
        {
            case RectangleShape rect:
                rect.Width = width;
                rect.Height = height;
                break;
            case OblongShape oblong:
                oblong.Width = width;
                oblong.Height = height;
                break;
            case EllipseShape ellipse:
                ellipse.RadiusX = width / 2;
                ellipse.RadiusY = height / 2;
                break;
        }
    }

    #endregion

    #region Rendering

    protected override void OnRenderOverlay(DrawingContext dc)
    {
        base.OnRenderOverlay(dc);

        if (_shapeRenderer == null) return;

        // Render all shapes from collection
        if (Shapes != null)
        {
            foreach (var shape in Shapes.OfType<IShape>())
            {
                var isSelected = SelectedShapes?.Contains(shape) ?? false;
                _shapeRenderer.Render(dc, shape, isSelected);
            }
        }

        // Render preview shape (being created, not in Shapes collection)
        if (_isCreatingShape && _previewShape != null)
        {
            _shapeRenderer.Render(dc, _previewShape, false);
        }

        RenderResizeHandles(dc);
        RenderPolygonPreview(dc);
        RenderMarqueeSelection(dc);
    }

    private void RenderResizeHandles(DrawingContext dc)
    {
        // Only show handles when a single resizable shape is selected
        if (SelectedShapes == null || SelectedShapes.Count != 1) return;
        if (CreationMode != ShapeCreationMode.None) return;

        var shape = SelectedShapes[0];
        if (!SupportsResize(shape)) return;

        var handleBrush = Brushes.White;
        var handlePen = new Pen(Brushes.Black, 1);
        var halfSize = ResizeHandleSize / 2;

        foreach (var (position, point) in GetResizeHandlePositions(shape))
        {
            var screenX = _viewPortManager.ImageToScreenX(point.X);
            var screenY = _viewPortManager.ImageToScreenY(point.Y);
            var rect = new Rect(
                screenX - halfSize,
                screenY - halfSize,
                ResizeHandleSize,
                ResizeHandleSize);
            dc.DrawRectangle(handleBrush, handlePen, rect);
        }
    }

    private void RenderPolygonPreview(DrawingContext dc)
    {
        if (CreationMode != ShapeCreationMode.Polygon || _previewPolygon == null) return;

        var points = _previewPolygon.GetAbsolutePoints().ToList();
        if (points.Count == 0) return;

        var lastPoint = points[^1];
        var lastScreenPos = _viewPortManager.ImageToScreen((long)lastPoint.X, (long)lastPoint.Y);
        var currentScreenPos = _viewPortManager.ImageToScreen(
            (long)_currentMouseImagePosition.X, (long)_currentMouseImagePosition.Y);

        var previewPen = new Pen(Brushes.DodgerBlue, 2) { DashStyle = DashStyles.Dash };
        dc.DrawLine(previewPen,
            new Point(lastScreenPos.X, lastScreenPos.Y),
            new Point(currentScreenPos.X, currentScreenPos.Y));

        if (points.Count >= 2)
        {
            var firstPoint = points[0];
            var firstScreenPos = _viewPortManager.ImageToScreen((long)firstPoint.X, (long)firstPoint.Y);
            var closingPen = new Pen(Brushes.DodgerBlue, 1) { DashStyle = DashStyles.Dot };
            dc.DrawLine(closingPen,
                new Point(currentScreenPos.X, currentScreenPos.Y),
                new Point(firstScreenPos.X, firstScreenPos.Y));
        }

        var markerBrush = new SolidColorBrush(Color.FromArgb(200, 30, 144, 255));
        foreach (var point in points)
        {
            var screenPos = _viewPortManager.ImageToScreen((long)point.X, (long)point.Y);
            dc.DrawEllipse(markerBrush, null, new Point(screenPos.X, screenPos.Y), 4, 4);
        }
    }

    private void RenderMarqueeSelection(DrawingContext dc)
    {
        if (!_isMarqueeSelecting) return;

        var startScreen = _viewPortManager.ImageToScreen((long)_marqueeStartPoint.X, (long)_marqueeStartPoint.Y);
        var endScreen = _viewPortManager.ImageToScreen((long)_marqueeCurrentPoint.X, (long)_marqueeCurrentPoint.Y);

        var rect = new Rect(
            Math.Min(startScreen.X, endScreen.X),
            Math.Min(startScreen.Y, endScreen.Y),
            Math.Abs(endScreen.X - startScreen.X),
            Math.Abs(endScreen.Y - startScreen.Y));

        var fillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
        var strokePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 1) { DashStyle = DashStyles.Dash };
        dc.DrawRectangle(fillBrush, strokePen, rect);
    }

    #endregion
}
