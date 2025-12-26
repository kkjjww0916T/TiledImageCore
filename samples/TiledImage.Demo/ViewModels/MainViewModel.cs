using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiledImage.Core.IO;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;
using TiledImage.Formats.SkiaSharp.IO;
using TiledImage.Wpf.Mapping;
using Microsoft.Win32;

namespace TiledImage.Demo.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IImageExporter _exporter;
    private readonly IImageLoader _imageDecoder;

    [ObservableProperty]
    private TiledImageSource? _imageSource;

    [ObservableProperty]
    private PerspectiveEditor? _transform;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private long _imageWidth;

    [ObservableProperty]
    private long _imageHeight;

    [ObservableProperty]
    private Point _mousePosition;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isPerspectiveMode;

    [ObservableProperty]
    private bool _hasPerspectiveTransform;

    [ObservableProperty]
    private string? _overlayImagePath;

    [ObservableProperty]
    private double _overlayOpacity = 0.5;

    [ObservableProperty]
    private bool _isOverlayVisible = true;

    [ObservableProperty]
    private double _overlayScale = 1.0;

    [ObservableProperty]
    private bool _isImageLoaded;

    public string ZoomPercentage => $"{Zoom * 100:F0}%";

    public MainViewModel(IImageExporter exporter)
        : this(exporter, new SkiaSharpImageLoader())
    {
    }

    public MainViewModel(IImageExporter exporter, IImageLoader imageDecoder)
    {
        _exporter = exporter;
        _imageDecoder = imageDecoder;
        _transform = new PerspectiveEditor();
        _transform.TransformChanged += OnTransformChanged;
    }

    private void OnTransformChanged(object? sender, EventArgs e)
    {
        HasPerspectiveTransform = Transform?.HasTransform ?? false;
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercentage));
    }

    partial void OnImageWidthChanged(long value)
    {
        UpdateStatusText();
        IsImageLoaded = value > 0 && ImageHeight > 0;
    }

    partial void OnImageHeightChanged(long value)
    {
        UpdateStatusText();
        IsImageLoaded = ImageWidth > 0 && value > 0;
    }

    partial void OnMousePositionChanged(Point value)
    {
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (ImageWidth > 0 && ImageHeight > 0)
        {
            StatusText = $"Image: {ImageWidth} x {ImageHeight} | Mouse: ({MousePosition.X:F0}, {MousePosition.Y:F0})";
        }
        else
        {
            StatusText = "Ready";
        }
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif;*.webp|All Files|*.*",
            Title = "Open Image"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadImageAsync(dialog.FileName);
        }
    }

    private async Task LoadImageAsync(string filePath)
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            // Dispose previous image source
            ImageSource?.Dispose();

            // Load image using decoder - now returns TiledImageSource directly
            var newSource = await _imageDecoder.LoadAsync(filePath);

            ImageSource = newSource;
            ImageWidth = newSource.ImageWidth;
            ImageHeight = newSource.ImageHeight;

            // Initialize transform for new image
            Transform?.InitializeForImage(newSource.ImageWidth, newSource.ImageHeight);
            HasPerspectiveTransform = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
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

    [RelayCommand]
    private void TogglePerspectiveMode()
    {
        IsPerspectiveMode = !IsPerspectiveMode;
    }

    [RelayCommand]
    private async Task SaveImageAsync()
    {
        if (ImageSource == null || !ImageSource.IsLoaded)
        {
            MessageBox.Show("No image loaded.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg|WebP Image|*.webp",
            Title = "Save Image",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                StatusText = "Saving...";

                var format = GetFormatFromExtension(dialog.FileName);
                int quality = format == ImageFormat.Png ? 100 : 95;

                // Export the image (perspective transform is only for preview, not applied to save)
                StatusText = "Encoding...";
                await _exporter.ExportAsync(ImageSource, dialog.FileName, format, quality);

                MessageBox.Show("Image saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                UpdateStatusText();
            }
        }
    }

    private static ImageFormat GetFormatFromExtension(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".webp" => ImageFormat.Webp,
            ".bmp" => ImageFormat.Bmp,
            ".gif" => ImageFormat.Gif,
            _ => ImageFormat.Png
        };
    }

    [RelayCommand]
    private void ResetPerspective()
    {
        if (ImageSource != null && ImageSource.IsLoaded && Transform != null)
        {
            Transform.InitializeForImage(ImageSource.ImageWidth, ImageSource.ImageHeight);
            HasPerspectiveTransform = false;
        }
    }

    [RelayCommand]
    private void LoadOverlay()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif;*.webp|All Files|*.*",
            Title = "Open Overlay Image"
        };

        if (dialog.ShowDialog() == true)
        {
            OverlayImagePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ClearOverlay()
    {
        OverlayImagePath = null;
    }

    public void Dispose()
    {
        if (Transform != null)
        {
            Transform.TransformChanged -= OnTransformChanged;
        }
        ImageSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
