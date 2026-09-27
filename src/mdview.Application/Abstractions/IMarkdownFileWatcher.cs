namespace mdview.Application.Abstractions;

public interface IMarkdownFileWatcher : IDisposable
{
  event EventHandler<MarkdownFileChangedEventArgs>? Changed;

  void Start();
}

public sealed class MarkdownFileChangedEventArgs(string path, bool exists) : EventArgs
{
  public string Path { get; } = path;

  public bool Exists { get; } = exists;
}
