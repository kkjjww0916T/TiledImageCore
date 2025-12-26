using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Geometry.Primitives;
using Microsoft.Win32;
using TiledImage.Core.Memory;
using TiledImage.Core.Tiling;
using TiledImage.Core.Types;
using TiledImage.Formats.SkiaSharp.IO;
using TiledImage.Transforms;

namespace TiledImage.Demo.ViewModels;

public partial class TransformDemoViewModel : ObservableObject, IDisposable
{
    private readonly SkiaSharpImageLoader _loader = new();

    // Original image
    [ObservableProperty]
    private TiledImageSource? _originalImageSource;

    // Transformed image
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private TiledImageSource? _transformedImageSource;

    // Transform options
    [ObservableProperty]
    private double _rotationDegrees;

    [ObservableProperty]
    private double _scaleFactor = 1.0;

    [ObservableProperty]
    private bool _flipHorizontal;

    [ObservableProperty]
    private bool _flipVertical;

    [ObservableProperty]
    private int _selectedInterpolation = 1; // 0=Nearest, 1=Bilinear, 2=Bicubic

    // Crop options
    [ObservableProperty]
    private bool _enableCrop;

    // Pixel-based crop (image center offset)
    [ObservableProperty]
    private long _cropX;

    [ObservableProperty]
    private long _cropY;

    [ObservableProperty]
    private long _cropWidth = 100;

    [ObservableProperty]
    private long _cropHeight = 100;

    // Status
    [ObservableProperty]
    private string _statusText = "Ready - Open an image file to start";

    [ObservableProperty]
    private string _originalInfoText = "";

    [ObservableProperty]
    private string _transformedInfoText = "";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransformCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _isTransforming;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransformCommand))]
    private bool _canTransform;

    // Z slice navigation (for 3D images, though standard images are 2D)
    [ObservableProperty]
    private long _currentZ;

    [ObservableProperty]
    private long _maxZ;

    [ObservableProperty]
    private bool _isZSliderEnabled;

    // LUT display range
    [ObservableProperty]
    private double _displayMin;

    [ObservableProperty]
    private double _displayMax = 255;

    [ObservableProperty]
    private double _displayRangeMax = 255;

    partial void OnOriginalImageSourceChanged(TiledImageSource? value)
    {
        if (value != null)
        {
            MaxZ = value.ImageDepth > 0 ? value.ImageDepth - 1 : 0;
            IsZSliderEnabled = value.ImageDepth > 1;
            CurrentZ = 0;
            OriginalInfoText = $"{value.ImageWidth} x {value.ImageHeight} x {value.ImageDepth} | {value.PixelFormat}";
            CanTransform = true;
            UpdateLutRange(value);

            // Initialize crop: centered (offset 0,0), half image size
            CropX = 0;
            CropY = 0;
            CropWidth = value.ImageWidth / 2;
            CropHeight = value.ImageHeight / 2;
        }
        else
        {
            MaxZ = 0;
            IsZSliderEnabled = false;
            CurrentZ = 0;
            OriginalInfoText = "";
            CanTransform = false;
            DisplayRangeMax = 255;
            DisplayMin = 0;
            DisplayMax = 255;
        }
    }

    private void UpdateLutRange(TiledImageSource source)
    {
        var is16Bit = source.PixelFormat == PixelFormat.Gray16;
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

    partial void OnTransformedImageSourceChanged(TiledImageSource? value)
    {
        if (value != null)
        {
            TransformedInfoText = $"{value.ImageWidth} x {value.ImageHeight} x {value.ImageDepth} | {value.PixelFormat}";
        }
        else
        {
            TransformedInfoText = "";
        }
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

            // Dispose previous sources
            OriginalImageSource?.Dispose();
            TransformedImageSource?.Dispose();
            TransformedImageSource = null;

            OriginalImageSource = _loader.Load(filePath);

            StatusText = $"Loaded: {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Load failed";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteTransform))]
    private async Task TransformAsync()
    {
        if (OriginalImageSource == null || !OriginalImageSource.IsLoaded)
            return;

        IsTransforming = true;
        ProgressValue = 0;

        try
        {
            // Determine crop region
            Rect2L? cropRegion = null;
            if (EnableCrop)
            {
                long imageCenterX = OriginalImageSource.ImageWidth / 2;
                long imageCenterY = OriginalImageSource.ImageHeight / 2;
                long cropLeft = imageCenterX + CropX - CropWidth / 2;
                long cropTop = imageCenterY + CropY - CropHeight / 2;
                cropRegion = new Rect2L(cropLeft, cropTop, CropWidth, CropHeight);
            }

            // Count active transforms
            bool hasCrop = cropRegion.HasValue;
            bool hasFlipH = FlipHorizontal;
            bool hasFlipV = FlipVertical;
            bool hasScale = Math.Abs(ScaleFactor - 1.0) > 0.001;
            bool hasRotation = Math.Abs(RotationDegrees) > 0.001;
            int transformCount = (hasCrop ? 1 : 0) + (hasFlipH ? 1 : 0) + (hasFlipV ? 1 : 0) + (hasScale ? 1 : 0) + (hasRotation ? 1 : 0);

            if (transformCount == 0)
            {
                MessageBox.Show("No transformation specified. Please set at least one transform option.",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StatusText = "Transforming...";
            var progress = new Progress<double>(p =>
            {
                ProgressValue = p * 100;
                StatusText = $"Transforming... {p:P0}";
            });

            // Dispose previous transformed source
            TransformedImageSource?.Dispose();

            var interpolation = (InterpolationMode)SelectedInterpolation;

            // Build and execute transform pipeline
            var resultSource = await Task.Run(() =>
            {
                var pipeline = new TransformPipeline();

                // Apply transforms in order: Crop → Scale → Flip → Rotation
                if (cropRegion.HasValue)
                    pipeline.Crop(cropRegion.Value);

                if (hasScale)
                    pipeline.Scale(ScaleFactor, interpolation);

                if (hasFlipH)
                    pipeline.FlipHorizontal();

                if (hasFlipV)
                    pipeline.FlipVertical();

                if (hasRotation)
                {
                    double normalized = RotationDegrees % 360;
                    if (normalized < 0) normalized += 360;

                    if (Math.Abs(normalized - 90) < 0.001)
                        pipeline.Rotate90();
                    else if (Math.Abs(normalized - 180) < 0.001)
                        pipeline.Rotate180();
                    else if (Math.Abs(normalized - 270) < 0.001)
                        pipeline.Rotate270();
                    else
                        pipeline.Rotate(RotationDegrees, interpolation);
                }

                // Calculate output size and execute
                var outputSize = pipeline.CalculateFinalSize(OriginalImageSource.ImageWidth, OriginalImageSource.ImageHeight);
                var buffer = pipeline.Execute(OriginalImageSource.Provider, CurrentZ, BufferType.Unmanaged, progress);

                // Wrap buffer in provider and create source
                var provider = new RawTileProvider(outputSize.Width, outputSize.Height, 1, OriginalImageSource.PixelFormat, buffer);
                return new TiledImageSource(provider);
            });

            TransformedImageSource = resultSource;
            StatusText = "Transform complete!";
            ProgressValue = 100;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Transform failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Transform failed";
        }
        finally
        {
            IsTransforming = false;
        }
    }

    private bool CanExecuteTransform() => CanTransform && !IsTransforming;

    [RelayCommand(CanExecute = nameof(CanExecuteExport))]
    private async Task ExportAsync()
    {
        if (TransformedImageSource == null || !TransformedImageSource.IsLoaded)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg|WebP Image|*.webp|BMP Image|*.bmp",
            Title = "Export Transformed Image",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() == true)
        {
            IsTransforming = true;
            ProgressValue = 0;

            try
            {
                StatusText = "Exporting...";
                ProgressValue = 50; // Simple progress indication

                var format = GetFormatFromExtension(dialog.FileName);
                var exporter = new SkiaSharpImageExporter();

                await exporter.ExportAsync(
                    TransformedImageSource,
                    dialog.FileName,
                    format);

                ProgressValue = 100;
                StatusText = $"Exported to: {System.IO.Path.GetFileName(dialog.FileName)}";
                MessageBox.Show($"Successfully exported to:\n{dialog.FileName}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "Export failed";
            }
            finally
            {
                IsTransforming = false;
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

    private bool CanExecuteExport() => TransformedImageSource != null && !IsTransforming;

    [RelayCommand]
    private void ResetOptions()
    {
        RotationDegrees = 0;
        ScaleFactor = 1.0;
        FlipHorizontal = false;
        FlipVertical = false;
        SelectedInterpolation = 1;
        EnableCrop = false;

        if (OriginalImageSource != null)
        {
            // Pixel-based defaults
            CropX = 0;
            CropY = 0;
            CropWidth = OriginalImageSource.ImageWidth / 2;
            CropHeight = OriginalImageSource.ImageHeight / 2;
        }
    }

    public void Dispose()
    {
        OriginalImageSource?.Dispose();
        TransformedImageSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
