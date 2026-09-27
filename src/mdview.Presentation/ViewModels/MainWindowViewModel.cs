using System.Collections.ObjectModel;

namespace mdview.Presentation.ViewModels;

public sealed class MainWindowViewModel
{
    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];
}

public sealed class DocumentTabViewModel(string title)
{
    public string Title { get; } = title;
}
