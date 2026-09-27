using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using mdview.Presentation.ViewModels;

namespace mdview.Presentation;

public partial class MainWindow : Window
{
    public MainWindow(IEnumerable<string>? startupPaths = null)
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        if (startupPaths is not null)
        {
            foreach (var path in startupPaths)
            {
                _ = OpenFileAsync(path);
            }
        }
    }

    public void ScrollToAnchor(string anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return;
        }

        var scrollViewer = this.FindControl<ScrollViewer>("DocumentScrollViewer");
        if (scrollViewer is null)
        {
            return;
        }

        var target = scrollViewer.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => string.Equals(control.Tag?.ToString(), anchor, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return;
        }

        target.BringIntoView();
    }

    private async Task OpenFileAsync(string path)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.OpenFileAsync(path);
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        await OpenFileDialogAsync();
    }

    private async Task OpenFileDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown")
                {
                    Patterns = ["*.md", "*.markdown", "*.mdown"]
                }
            ]
        });

        if (files.Count > 0)
        {
            var localPath = files[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                await OpenFileAsync(localPath);
            }
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Key == Key.O && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            || (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Meta))
        {
            e.Handled = true;
            await OpenFileDialogAsync();
            return;
        }
    }

    private void OnCloseTabClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (sender is Button { Tag: DocumentTabViewModel tab })
        {
            viewModel.CloseTab(tab);
        }
    }
}