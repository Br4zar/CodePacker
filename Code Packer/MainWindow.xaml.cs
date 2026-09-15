using System.IO;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfWindow = System.Windows.Window;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;

using CodePacker.Services;
using CodePacker.ViewModels;

namespace CodePacker;

public partial class MainWindow : WpfWindow
{
    private TrayService? _trayService;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) => _trayService = new TrayService(this);
        Closed += (_, _) => _trayService?.Dispose();
    }

    private void Window_DragOver(object sender, WpfDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(WpfDataFormats.FileDrop)
            ? WpfDragDropEffects.Copy
            : WpfDragDropEffects.None;

        e.Handled = true;
    }

    private async void Window_Drop(object sender, WpfDragEventArgs e)
    {
        if (!e.Data.GetDataPresent(WpfDataFormats.FileDrop))
            return;

        var paths = e.Data.GetData(WpfDataFormats.FileDrop) as string[];

        if (paths == null || paths.Length == 0)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        var path = paths[0];

        if (Directory.Exists(path))
            await viewModel.OpenDirectoryPathAsync(path);
    }
}