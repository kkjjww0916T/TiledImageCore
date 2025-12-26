using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Geometry.Primitives;
using Geometry.Shapes.Shapes;
using Geometry.Shapes.Types;
using Microsoft.Win32;
using TiledImage.Core.Tiling;
using TiledImage.Formats.SkiaSharp.IO;
using TiledImage.Wpf.Drawing;

namespace TiledImage.Demo.ViewModels;

public partial class DrawingViewerViewModel : ObservableObject, IDisposable
{
    private readonly SkiaSharpImageLoader _loader = new();
    private readonly Random _random = new();

    // Clipboard for copy/paste
    private List<IShape>? _clipboard;

    [ObservableProperty]
    private TiledImageSource? _imageSource;

    [ObservableProperty]
    private long _currentZ;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private string _statusText = "Ready - Load an image file to start";

    [ObservableProperty]
    private string _imageInfoText = "";

    [ObservableProperty]
    private bool _isZSliderEnabled;

    [ObservableProperty]
    private long _maxZ;

    [ObservableProperty]
    private bool _isShapeEditEnabled = true;

    [ObservableProperty]
    private ShapeCreationMode _creationMode = ShapeCreationMode.None;

    // LUT display range
    [ObservableProperty]
    private double _displayMin;

    [ObservableProperty]
    private double _displayMax = 255;

    [ObservableProperty]
    private double _displayRangeMax = 255;

    public ObservableCollection<IShape> Shapes { get; } = new();

    public ObservableCollection<IShape> SelectedShapes { get; } = new();

    // Mode properties for RadioButton binding
    public bool IsSelectMode
    {
        get => CreationMode == ShapeCreationMode.None;
        set { if (value) CreationMode = ShapeCreationMode.None; }
    }

    public bool IsCircleMode
    {
        get => CreationMode == ShapeCreationMode.Circle;
        set { if (value) CreationMode = ShapeCreationMode.Circle; }
    }

    public bool IsEllipseMode
    {
        get => CreationMode == ShapeCreationMode.Ellipse;
        set { if (value) CreationMode = ShapeCreationMode.Ellipse; }
    }

    public bool IsRectangleMode
    {
        get => CreationMode == ShapeCreationMode.Rectangle;
        set { if (value) CreationMode = ShapeCreationMode.Rectangle; }
    }

    public bool IsOblongMode
    {
        get => CreationMode == ShapeCreationMode.Oblong;
        set { if (value) CreationMode = ShapeCreationMode.Oblong; }
    }

    public bool IsPolygonMode
    {
        get => CreationMode == ShapeCreationMode.Polygon;
        set { if (value) CreationMode = ShapeCreationMode.Polygon; }
    }

    public ShapeStyle NewShapeStyle => GetRandomStyle();

    public string ZoomPercentage => $"{Zoom * 100:F0}%";

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercentage));
    }

    [RelayCommand]
    private void ShapeCreationRequested(ShapeCreationRequestedEventArgs? args)
    {
        if (args == null) return;

        // Create shape based on the request
        IShape? shape = args.CreationMode switch
        {
            ShapeCreationMode.Circle => new EllipseShape(args.CenterX, args.CenterY, args.Width / 2, args.Width / 2),
            ShapeCreationMode.Ellipse => new EllipseShape(args.CenterX, args.CenterY, args.Width / 2, args.Height / 2),
            ShapeCreationMode.Rectangle => new RectangleShape(args.CenterX, args.CenterY, args.Width, args.Height),
            ShapeCreationMode.Oblong => new OblongShape(args.CenterX, args.CenterY, args.Width, args.Height),
            ShapeCreationMode.Polygon when args.RelativePoints != null => CreatePolygonFromPoints(args.CenterX, args.CenterY, args.RelativePoints),
            _ => null
        };

        if (shape == null) return;

        shape.Style = args.Style;
        Shapes.Add(shape);
        SelectShape(shape);
        StatusText = $"Created {args.CreationMode} at ({args.CenterX:F0}, {args.CenterY:F0})";
    }

    private PolygonShape CreatePolygonFromPoints(double centerX, double centerY, IReadOnlyList<Point2D> relativePoints)
    {
        var polygon = new PolygonShape(centerX, centerY);
        foreach (var point in relativePoints)
        {
            polygon.AddPoint(point.X, point.Y);
        }
        return polygon;
    }

    [RelayCommand]
    private void ShapeDeleteRequested(ShapeDeleteRequestedEventArgs? args)
    {
        if (SelectedShapes.Count == 0) return;

        var count = SelectedShapes.Count;
        foreach (var shape in SelectedShapes.ToList())
        {
            Shapes.Remove(shape);
        }
        SelectedShapes.Clear();
        StatusText = count == 1 ? "Shape deleted" : $"{count} shapes deleted";
    }

    [RelayCommand]
    private void ShapeCopyRequested(ShapeCopyRequestedEventArgs? args)
    {
        if (SelectedShapes.Count == 0) return;

        // Clone shapes to clipboard
        _clipboard = SelectedShapes.Select(CloneShape).ToList();
        StatusText = _clipboard.Count == 1 ? "Shape copied" : $"{_clipboard.Count} shapes copied";
    }

    [RelayCommand]
    private void ShapePasteRequested(ShapePasteRequestedEventArgs? args)
    {
        if (_clipboard == null || _clipboard.Count == 0)
        {
            StatusText = "Nothing to paste";
            return;
        }

        // Calculate offset for paste position
        double offsetX = 20, offsetY = 20;
        if (args != null && args.ImageX != 0 && args.ImageY != 0)
        {
            // Use mouse position as paste target
            var firstShape = _clipboard[0];
            offsetX = args.ImageX - firstShape.CenterX;
            offsetY = args.ImageY - firstShape.CenterY;
        }

        SelectedShapes.Clear();
        foreach (var original in _clipboard)
        {
            var clone = CloneShape(original);
            clone.CenterX += offsetX;
            clone.CenterY += offsetY;
            Shapes.Add(clone);
            SelectedShapes.Add(clone);
        }

        StatusText = _clipboard.Count == 1 ? "Shape pasted" : $"{_clipboard.Count} shapes pasted";
    }

    private IShape CloneShape(IShape original)
    {
        IShape clone = original switch
        {
            EllipseShape e => new EllipseShape(e.CenterX, e.CenterY, e.RadiusX, e.RadiusY) { Rotation = e.Rotation },
            RectangleShape r => new RectangleShape(r.CenterX, r.CenterY, r.Width, r.Height) { Rotation = r.Rotation },
            OblongShape o => new OblongShape(o.CenterX, o.CenterY, o.Width, o.Height) { Rotation = o.Rotation },
            PolygonShape p => ClonePolygon(p),
            _ => throw new NotSupportedException($"Unknown shape type: {original.GetType()}")
        };
        clone.Style = original.Style;
        return clone;
    }

    private PolygonShape ClonePolygon(PolygonShape original)
    {
        var clone = new PolygonShape(original.CenterX, original.CenterY) { Rotation = original.Rotation };
        foreach (var point in original.RelativePoints)
        {
            clone.AddPoint(point.X, point.Y);
        }
        return clone;
    }

    partial void OnCreationModeChanged(ShapeCreationMode value)
    {
        OnPropertyChanged(nameof(IsSelectMode));
        OnPropertyChanged(nameof(IsCircleMode));
        OnPropertyChanged(nameof(IsEllipseMode));
        OnPropertyChanged(nameof(IsRectangleMode));
        OnPropertyChanged(nameof(IsOblongMode));
        OnPropertyChanged(nameof(IsPolygonMode));
        StatusText = value == ShapeCreationMode.None
            ? "Select mode - click to select shapes"
            : $"Draw mode: {value} - drag to create";
    }

    partial void OnImageSourceChanged(TiledImageSource? value)
    {
        if (value != null)
        {
            MaxZ = value.ImageDepth > 0 ? value.ImageDepth - 1 : 0;
            IsZSliderEnabled = value.ImageDepth > 1;
            CurrentZ = 0;
            ImageInfoText = $"{value.ImageWidth} x {value.ImageHeight} x {value.ImageDepth} | {value.PixelFormat}";
            UpdateLutRange(value);
        }
        else
        {
            MaxZ = 0;
            IsZSliderEnabled = false;
            CurrentZ = 0;
            ImageInfoText = "";
            DisplayRangeMax = 255;
            DisplayMin = 0;
            DisplayMax = 255;
        }
    }

    private void UpdateLutRange(TiledImageSource source)
    {
        var is16Bit = source.PixelFormat == Core.Types.PixelFormat.Gray16;
        DisplayRangeMax = is16Bit ? 65535 : 255;
        DisplayMin = 0;
        DisplayMax = DisplayRangeMax;
    }

    [RelayCommand]
    private void ResetLut()
    {
        DisplayMin = 0;
        DisplayMax = DisplayRangeMax;
    }

    [RelayCommand]
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
            Title = "Open Image"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadImageFile(dialog.FileName);
        }
    }

    private void LoadImageFile(string filePath)
    {
        try
        {
            StatusText = "Loading...";
            ImageSource?.Dispose();
            ImageSource = _loader.Load(filePath);
            StatusText = $"Loaded: {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Load failed";
        }
    }

    [RelayCommand]
    private void AddCircle()
    {
        var (cx, cy) = GetRandomCenter();
        var radius = _random.Next(30, 100);
        var shape = EllipseShape.CreateCircle(cx, cy, radius);
        shape.Style = GetRandomStyle();
        Shapes.Add(shape);
        SelectShape(shape);
        StatusText = $"Added circle at ({cx:F0}, {cy:F0})";
    }

    [RelayCommand]
    private void AddEllipse()
    {
        var (cx, cy) = GetRandomCenter();
        var rx = _random.Next(40, 120);
        var ry = _random.Next(20, 80);
        var shape = new EllipseShape(cx, cy, rx, ry)
        {
            Rotation = _random.Next(0, 360),
            Style = GetRandomStyle()
        };
        Shapes.Add(shape);
        SelectShape(shape);
        StatusText = $"Added ellipse at ({cx:F0}, {cy:F0})";
    }

    [RelayCommand]
    private void AddRectangle()
    {
        var (cx, cy) = GetRandomCenter();
        var width = _random.Next(50, 150);
        var height = _random.Next(30, 100);
        var shape = new RectangleShape(cx, cy, width, height)
        {
            Rotation = _random.Next(0, 360),
            Style = GetRandomStyle()
        };
        Shapes.Add(shape);
        SelectShape(shape);
        StatusText = $"Added rectangle at ({cx:F0}, {cy:F0})";
    }

    [RelayCommand]
    private void AddOblong()
    {
        var (cx, cy) = GetRandomCenter();
        var width = _random.Next(80, 200);
        var height = _random.Next(30, 60);
        var shape = new OblongShape(cx, cy, width, height)
        {
            Rotation = _random.Next(0, 360),
            Style = GetRandomStyle()
        };
        Shapes.Add(shape);
        SelectShape(shape);
        StatusText = $"Added oblong at ({cx:F0}, {cy:F0})";
    }

    [RelayCommand]
    private void AddPolygon()
    {
        var (cx, cy) = GetRandomCenter();
        var sides = _random.Next(3, 8);
        var radius = _random.Next(40, 100);
        var shape = PolygonShape.CreateRegular(cx, cy, sides, radius);
        shape.Rotation = _random.Next(0, 360);
        shape.Style = GetRandomStyle();
        Shapes.Add(shape);
        SelectShape(shape);
        StatusText = $"Added {sides}-sided polygon at ({cx:F0}, {cy:F0})";
    }

    private void SelectShape(IShape shape)
    {
        SelectedShapes.Clear();
        SelectedShapes.Add(shape);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedShapes.Count == 0) return;

        var count = SelectedShapes.Count;
        foreach (var shape in SelectedShapes.ToList())
        {
            Shapes.Remove(shape);
        }
        SelectedShapes.Clear();
        StatusText = count == 1 ? "Shape deleted" : $"{count} shapes deleted";
    }

    [RelayCommand]
    private void ClearAllShapes()
    {
        Shapes.Clear();
        SelectedShapes.Clear();
        StatusText = "All shapes cleared";
    }

    [RelayCommand]
    private void RotateSelected()
    {
        if (SelectedShapes.Count == 0) return;

        foreach (var shape in SelectedShapes)
        {
            shape.Rotation = (shape.Rotation + 15) % 360;
        }
        StatusText = SelectedShapes.Count == 1
            ? $"Rotation: {SelectedShapes[0].Rotation:F0}°"
            : $"Rotated {SelectedShapes.Count} shapes (+15°)";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = Math.Min(Zoom * 1.2, 100.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = Math.Max(Zoom / 1.2, 0.01);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        Zoom = 1.0;
    }

    private (double cx, double cy) GetRandomCenter()
    {
        if (ImageSource != null)
        {
            return (
                _random.Next(100, (int)Math.Min(ImageSource.ImageWidth - 100, 1000)),
                _random.Next(100, (int)Math.Min(ImageSource.ImageHeight - 100, 1000))
            );
        }
        return (_random.Next(100, 500), _random.Next(100, 500));
    }

    private ShapeStyle GetRandomStyle()
    {
        var colors = new uint[]
        {
            0xFFFF0000, // Red
            0xFF00FF00, // Green
            0xFF0000FF, // Blue
            0xFFFFFF00, // Yellow
            0xFFFF00FF, // Magenta
            0xFF00FFFF, // Cyan
            0xFFFF8000, // Orange
            0xFF8000FF, // Purple
        };

        var strokeColor = colors[_random.Next(colors.Length)];
        var fillColor = (strokeColor & 0x00FFFFFF) | 0x40000000; // Same color, 25% alpha
        var thickness = _random.Next(1, 4);

        return new ShapeStyle(strokeColor, fillColor, thickness);
    }

    public void Dispose()
    {
        ImageSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
