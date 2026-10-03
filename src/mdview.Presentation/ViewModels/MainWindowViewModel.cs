using System.Collections.ObjectModel;
using System.ComponentModel;
using mdview.Application.Abstractions;
using mdview.Application.Models;
using mdview.Application.UseCases;
using mdview.Infrastructure.FileSystem;
using mdview.Infrastructure.Markdown;
using Avalonia.Threading;

namespace mdview.Presentation.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
  public MainWindowViewModel(MermaidDiagramService? diagrams = null) => Diagrams = diagrams;
  public MermaidDiagramService? Diagrams { get; }
  private readonly MarkdownTabManager _tabManager = new();
  private readonly MarkdownFileReader _fileReader = new();
  private readonly MarkdigMarkdownParser _parser = new();
  private readonly Dictionary<string, IMarkdownFileWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
  private readonly MarkdownSearchState _searchState = new();
  private readonly MarkdownZoomState _zoomState = new();
  private MarkdownDocumentModel _activeDocument = new(Array.Empty<MarkdownBlock>());
  private DocumentTabViewModel? _activeTab;
  private bool _isSearchVisible;
  private bool _disposed;

  public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];

  public DocumentTabViewModel? ActiveTab
  {
    get => _activeTab;
    set
    {
      if (ReferenceEquals(_activeTab, value))
      {
        return;
      }

      _activeTab = value;
      ActiveDocument = value?.Document ?? new MarkdownDocumentModel(Array.Empty<MarkdownBlock>());
      _searchState.SetText(value?.Document.SearchText ?? value?.SourceContent);
      OnPropertyChanged(nameof(SearchStatus));
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveTab)));
    }
  }

  public MarkdownDocumentModel ActiveDocument
  {
    get => _activeDocument;
    set
    {
      if (ReferenceEquals(_activeDocument, value))
      {
        return;
      }

      _activeDocument = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveDocument)));
    }
  }

  public bool IsSearchVisible
  {
    get => _isSearchVisible;
    set
    {
      if (_isSearchVisible == value)
      {
        return;
      }

      _isSearchVisible = value;
      OnPropertyChanged(nameof(IsSearchVisible));
    }
  }

  public string SearchQuery
  {
    get => _searchState.Query;
    set
    {
      _searchState.SetQuery(value);
      OnPropertyChanged(nameof(SearchQuery));
      OnPropertyChanged(nameof(SearchStatus));
    }
  }

  public string SearchStatus => _searchState.MatchCount == 0
    ? "0 / 0"
    : $"{_searchState.CurrentMatch + 1} / {_searchState.MatchCount}";

  public double ZoomScale => _zoomState.Scale;

  public void MoveSearchNext()
  {
    _searchState.MoveNext();
    OnPropertyChanged(nameof(SearchStatus));
  }

  public void MoveSearchPrevious()
  {
    _searchState.MovePrevious();
    OnPropertyChanged(nameof(SearchStatus));
  }

  public void IncreaseZoom()
  {
    _zoomState.Increase();
    OnPropertyChanged(nameof(ZoomScale));
  }

  public void DecreaseZoom()
  {
    _zoomState.Decrease();
    OnPropertyChanged(nameof(ZoomScale));
  }

  public void ResetZoom()
  {
    _zoomState.Reset();
    OnPropertyChanged(nameof(ZoomScale));
  }

  public async Task OpenFileAsync(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      return;
    }

    var fullPath = Path.GetFullPath(path);

    try
    {
      var content = await _fileReader.ReadTextAsync(fullPath);
      var document = _parser.Parse(content);
      OpenOrActivateDocument(fullPath, Path.GetFileName(fullPath), document, content, DocumentTabState.Loaded);
      StartWatching(fullPath);
    }
    catch (Exception ex)
    {
      OpenOrActivateDocument(fullPath, Path.GetFileName(fullPath), CreateErrorDocument($"File could not be opened: {ex.Message}"), null, File.Exists(fullPath) ? DocumentTabState.Error : DocumentTabState.Missing);
      StartWatching(fullPath);
    }
  }

  public async Task ReloadActiveFileAsync()
  {
    if (ActiveTab is not null)
    {
      await ReloadFileAsync(ActiveTab.FilePath);
    }
  }

  public void OpenOrActivateDocument(string filePath, string? title, MarkdownDocumentModel document, string? sourceContent = null, DocumentTabState state = DocumentTabState.Loaded)
  {
    var tab = _tabManager.OpenOrActivate(filePath, title, document);
    var existing = Tabs.FirstOrDefault(candidate => string.Equals(candidate.FilePath, tab.FilePath, StringComparison.OrdinalIgnoreCase));

    if (existing is null)
    {
      existing = new DocumentTabViewModel(tab.FilePath, tab.Title, tab.Document, sourceContent, state);
      Tabs.Add(existing);
    }
    else
    {
      existing.Title = tab.Title;
      existing.Document = tab.Document;
      existing.SourceContent = sourceContent;
      existing.State = state;
    }

    ActiveTab = existing;
  }

  public void CloseTab(DocumentTabViewModel tab)
  {
    var match = _tabManager.Tabs.FirstOrDefault(candidate => string.Equals(candidate.FilePath, tab.FilePath, StringComparison.OrdinalIgnoreCase));
    if (match is null)
    {
      Tabs.Remove(tab);
      if (Tabs.Count == 0)
      {
        ActiveTab = null;
      }
      else
      {
        ActiveTab = Tabs[^1];
      }
      return;
    }

    _tabManager.CloseTab(match);
    StopWatching(tab.FilePath);
    Tabs.Remove(tab);

    if (Tabs.Count == 0)
    {
      ActiveTab = null;
      return;
    }

    var active = _tabManager.ActiveTab is null ? Tabs[^1] : Tabs.FirstOrDefault(candidate => string.Equals(candidate.FilePath, _tabManager.ActiveTab.FilePath, StringComparison.OrdinalIgnoreCase)) ?? Tabs[^1];
    ActiveTab = active;
  }

  private void StartWatching(string path)
  {
    if (_watchers.ContainsKey(path))
    {
      return;
    }

    var watcher = new MarkdownFileWatcher(path);
    watcher.Changed += OnFileChanged;
    watcher.Start();
    _watchers[path] = watcher;
  }

  private void StopWatching(string path)
  {
    if (!_watchers.Remove(path, out var watcher))
    {
      return;
    }

    watcher.Changed -= OnFileChanged;
    watcher.Dispose();
  }

  private void OnFileChanged(object? sender, MarkdownFileChangedEventArgs e)
  {
    Dispatcher.UIThread.Post(() => { if (!_disposed) _ = ReloadFileAsync(e.Path); });
  }

  private async Task ReloadFileAsync(string path)
  {
    var tab = Tabs.FirstOrDefault(candidate => string.Equals(candidate.FilePath, path, StringComparison.OrdinalIgnoreCase));
    if (tab is null)
    {
      return;
    }

    if (!File.Exists(path))
    {
      tab.Document = CreateErrorDocument("File not found.");
      tab.SourceContent = null;
      tab.State = DocumentTabState.Missing;
      return;
    }

    try
    {
      var content = await _fileReader.ReadTextAsync(path);
      if (_disposed || !Tabs.Contains(tab)) return;
      if (tab.State == DocumentTabState.Loaded && string.Equals(tab.SourceContent, content, StringComparison.Ordinal))
      {
        return;
      }

      tab.Document = _parser.Parse(content);
      tab.SourceContent = content;
      tab.State = DocumentTabState.Loaded;
      if (ReferenceEquals(ActiveTab, tab))
      {
        _searchState.SetText(tab.Document.SearchText ?? content);
        OnPropertyChanged(nameof(SearchStatus));
      }
      if (ReferenceEquals(ActiveTab, tab))
      {
        ActiveDocument = tab.Document;
      }
    }
    catch (Exception ex)
    {
      tab.Document = CreateErrorDocument($"File could not be opened: {ex.Message}");
      tab.SourceContent = null;
      tab.State = DocumentTabState.Error;
    }
  }

  private static MarkdownDocumentModel CreateErrorDocument(string message) => new([
    new MarkdownParagraphBlock([
      new MarkdownTextInline(message)
    ])
  ]);

  private void OnPropertyChanged(string propertyName) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

  public event PropertyChangedEventHandler? PropertyChanged;

  public void Dispose()
  {
    _disposed = true;
    foreach (var watcher in _watchers.Values) { watcher.Changed -= OnFileChanged; watcher.Dispose(); }
    _watchers.Clear();
    Diagrams?.Dispose();
  }
}

public sealed class DocumentTabViewModel : INotifyPropertyChanged
{
  private string _title;
  private MarkdownDocumentModel _document;
  private string? _sourceContent;
  private DocumentTabState _state;

  public DocumentTabViewModel(string filePath, string title, MarkdownDocumentModel document, string? sourceContent = null, DocumentTabState state = DocumentTabState.Loaded)
  {
    FilePath = filePath;
    _title = title;
    _document = document;
    _sourceContent = sourceContent;
    _state = state;
  }

  public string FilePath { get; }

  public string Title
  {
    get => _title;
    set
    {
      if (_title == value)
      {
        return;
      }

      _title = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
    }
  }

  public MarkdownDocumentModel Document
  {
    get => _document;
    set
    {
      if (ReferenceEquals(_document, value))
      {
        return;
      }

      _document = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
    }
  }

  public string? SourceContent
  {
    get => _sourceContent;
    set => _sourceContent = value;
  }

  public DocumentTabState State
  {
    get => _state;
    set
    {
      if (_state == value)
      {
        return;
      }

      _state = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;
}

public enum DocumentTabState
{
  Loaded,
  Missing,
  Error
}
