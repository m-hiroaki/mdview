using System.Collections.ObjectModel;
using System.ComponentModel;
using mdview.Application.Models;

namespace mdview.Presentation.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
  private MarkdownDocumentModel _activeDocument = new(Array.Empty<MarkdownBlock>());

  public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];

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

  public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DocumentTabViewModel(string title)
{
  public string Title { get; } = title;
}
