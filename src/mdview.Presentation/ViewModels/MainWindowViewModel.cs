using System.Collections.ObjectModel;
using System.ComponentModel;
using mdview.Application.Models;

namespace mdview.Presentation.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
  private readonly MarkdownTabManager _tabManager = new();
  private MarkdownDocumentModel _activeDocument = new(Array.Empty<MarkdownBlock>());
  private DocumentTabViewModel? _activeTab;

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

  public void OpenOrActivateDocument(string filePath, string? title, MarkdownDocumentModel document)
  {
    var tab = _tabManager.OpenOrActivate(filePath, title, document);
    var existing = Tabs.FirstOrDefault(candidate => string.Equals(candidate.FilePath, tab.FilePath, StringComparison.OrdinalIgnoreCase));

    if (existing is null)
    {
      existing = new DocumentTabViewModel(tab.FilePath, tab.Title, tab.Document);
      Tabs.Add(existing);
    }
    else
    {
      existing.Title = tab.Title;
      existing.Document = tab.Document;
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
    Tabs.Remove(tab);

    if (Tabs.Count == 0)
    {
      ActiveTab = null;
      return;
    }

    var active = _tabManager.ActiveTab is null ? Tabs[^1] : Tabs.FirstOrDefault(candidate => string.Equals(candidate.FilePath, _tabManager.ActiveTab.FilePath, StringComparison.OrdinalIgnoreCase)) ?? Tabs[^1];
    ActiveTab = active;
  }

  public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DocumentTabViewModel : INotifyPropertyChanged
{
  private string _title;
  private MarkdownDocumentModel _document;

  public DocumentTabViewModel(string filePath, string title, MarkdownDocumentModel document)
  {
    FilePath = filePath;
    _title = title;
    _document = document;
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

  public event PropertyChangedEventHandler? PropertyChanged;
}
