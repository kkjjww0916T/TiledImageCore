using System.Windows;
using TiledImage.Core.Types;
using TiledImage.Demo.ViewModels;
using Microsoft.Win32;

namespace TiledImage.Demo;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void FitButton_Click(object sender, RoutedEventArgs e)
    {
        MappingControl.FitToView();
    }

    private void OpenDrawingButton_Click(object sender, RoutedEventArgs e)
    {
        var drawingWindow = new DrawingWindow();
        drawingWindow.Show();
    }

    private void OpenTransformButton_Click(object sender, RoutedEventArgs e)
    {
        var transformWindow = new TransformDemoWindow();
        transformWindow.Show();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm?.ImageSource == null || !vm.ImageSource.IsLoaded)
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
                // If overlay exists, use ExportMappedImageAsync
                if (MappingControl.FrameWidth > 0 && MappingControl.FrameHeight > 0)
                {
                    var format = GetFormatFromExtension(dialog.FileName);
                    var success = await MappingControl.ExportMappedImageAsync(dialog.FileName, format);
                    if (success)
                    {
                        MessageBox.Show($"Saved to overlay size: {MappingControl.FrameWidth} x {MappingControl.FrameHeight}",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Export failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // No overlay, use original save command
                    vm.SaveImageCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
