using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using mdview.Presentation.ViewModels;

namespace mdview.Presentation;

public partial class MainWindow : Window
{
    public MainWindow(IEnumerable<string>? startupPaths = null)
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);

        if (startupPaths is not null)
        {
            foreach (var path in startupPaths)
            {
                _ = OpenFileAsync(path);
            }
        }
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
        var dialog = new OpenFileDialog
        {
            AllowMultiple = false,
            Filters =
            [
                new FileDialogFilter { Name = "Markdown", Extensions = ["md", "markdown", "mdown"] }
            ]
        };

        var result = await dialog.ShowAsync(this);
        if (result is not null && result.Length > 0)
        {
            await OpenFileAsync(result[0]);
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

        if (e.Key == Key.D && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            e.Handled = true;
            return;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        var files = e.Data.GetFiles();
        if (files is null)
        {
            return;
        }

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                await OpenFileAsync(path);
            }
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