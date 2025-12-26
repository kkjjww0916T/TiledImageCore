using System.Windows;
using TiledImage.Core.IO;
using TiledImage.Formats.SkiaSharp.IO;
using TiledImage.Demo.ViewModels;

namespace TiledImage.Demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Manual DI - create services and inject them
        IImageExporter exporter = new SkiaSharpImageExporter();
        var viewModel = new MainViewModel(exporter);
        var mainWindow = new MainWindow(viewModel);

        mainWindow.Show();
    }
}
