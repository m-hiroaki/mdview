namespace mdview.Application.Models;

public sealed record MarkdownTab(string FilePath, string Title, MarkdownDocumentModel Document)
{
  public string NormalizedPath => NormalizePath(FilePath);

  public static string NormalizePath(string path)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    var fullPath = Path.GetFullPath(path);
    return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
  }
}

public sealed class MarkdownTabManager
{
  private readonly List<MarkdownTab> _tabs = [];

  public IReadOnlyList<MarkdownTab> Tabs => _tabs;

  public MarkdownTab? ActiveTab { get; private set; }

  public MarkdownTab OpenOrActivate(string filePath, string? title, MarkdownDocumentModel document)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
    ArgumentNullException.ThrowIfNull(document);

    var normalizedPath = MarkdownTab.NormalizePath(filePath);
    var existing = _tabs.FirstOrDefault(tab => string.Equals(tab.NormalizedPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
    if (existing is not null)
    {
      ActiveTab = existing;
      return existing;
    }

    var tab = new MarkdownTab(filePath, string.IsNullOrWhiteSpace(title) ? Path.GetFileName(filePath) : title, document);
    _tabs.Add(tab);
    ActiveTab = tab;
    return tab;
  }

  public bool CloseTab(MarkdownTab tab)
  {
    ArgumentNullException.ThrowIfNull(tab);

    var index = _tabs.IndexOf(tab);
    if (index < 0)
    {
      return false;
    }

    _tabs.RemoveAt(index);

    if (_tabs.Count == 0)
    {
      ActiveTab = null;
      return true;
    }

    ActiveTab = index >= _tabs.Count ? _tabs[^1] : _tabs[index];
    return true;
  }
}
