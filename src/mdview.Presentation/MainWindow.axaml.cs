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
    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(IEnumerable<string>? startupPaths = null)
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        ApplyZoom();

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);

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

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var path = GetDroppedMarkdownPath(e);
        e.DragEffects = path is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var path = GetDroppedMarkdownPath(e);
        e.Handled = true;

        if (path is not null)
        {
            await OpenFileAsync(path);
        }
    }

    private static string? GetDroppedMarkdownPath(DragEventArgs e)
    {
        var storageItem = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        var path = storageItem?.TryGetLocalPath();

        return !string.IsNullOrWhiteSpace(path) && IsMarkdownPath(path)
            ? path
            : null;
    }

    private static bool IsMarkdownPath(string path) =>
        string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), ".markdown", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), ".mdown", StringComparison.OrdinalIgnoreCase);

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            e.Handled = true;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsSearchVisible = true;
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }

            return;
        }

        var hasCommandModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (e.Key == Key.W && hasCommandModifier)
        {
            e.Handled = true;
            if (DataContext is MainWindowViewModel closeViewModel && closeViewModel.ActiveTab is { } activeTab)
            {
                closeViewModel.CloseTab(activeTab);
                if (closeViewModel.Tabs.Count == 0)
                {
                    Close();
                }
            }

            return;
        }

        if (e.Key == Key.Add || (e.Key == Key.OemPlus && hasCommandModifier))
        {
            if (DataContext is MainWindowViewModel zoomViewModel && hasCommandModifier)
            {
                e.Handled = true;
                zoomViewModel.IncreaseZoom();
                ApplyZoom();
                return;
            }
        }

        if (e.Key == Key.Subtract || (e.Key == Key.OemMinus && hasCommandModifier))
        {
            if (DataContext is MainWindowViewModel zoomViewModel && hasCommandModifier)
            {
                e.Handled = true;
                zoomViewModel.DecreaseZoom();
                ApplyZoom();
                return;
            }
        }

        if (e.Key == Key.R && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            e.Handled = true;
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.ReloadActiveFileAsync();
            }

            return;
        }

        if ((e.Key == Key.O && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            || (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Meta))
        {
            e.Handled = true;
            await OpenFileDialogAsync();
            return;
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsSearchVisible = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && DataContext is MainWindowViewModel searchViewModel)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                searchViewModel.MoveSearchPrevious();
            }
            else
            {
                searchViewModel.MoveSearchNext();
            }

            e.Handled = true;
        }
    }

    private void OnPreviousSearchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.MoveSearchPrevious();
        }
    }

    private void OnNextSearchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.MoveSearchNext();
        }
    }

    private void OnCloseSearchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsSearchVisible = false;
        }
    }

    private void ApplyZoom()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            DocumentContent.RenderTransform = new Avalonia.Media.ScaleTransform(viewModel.ZoomScale, viewModel.ZoomScale);
            DocumentContent.RenderTransformOrigin = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative);
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