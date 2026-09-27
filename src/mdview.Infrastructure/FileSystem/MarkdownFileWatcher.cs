using mdview.Application.Abstractions;

namespace mdview.Infrastructure.FileSystem;

public sealed class MarkdownFileWatcher : IMarkdownFileWatcher
{
  private readonly FileSystemWatcher _watcher;
  private readonly TimeSpan _debounce;
  private Timer? _timer;
  private bool _disposed;

  public MarkdownFileWatcher(string path, TimeSpan? debounce = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    var fullPath = Path.GetFullPath(path);
    var directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("The path has no directory.", nameof(path));

    _watcher = new FileSystemWatcher(directory, Path.GetFileName(fullPath))
    {
      NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
      IncludeSubdirectories = false,
      EnableRaisingEvents = false
    };
    _watcher.Changed += OnFileSystemEvent;
    _watcher.Created += OnFileSystemEvent;
    _watcher.Deleted += OnFileSystemEvent;
    _watcher.Renamed += OnRenamed;
    _debounce = debounce ?? TimeSpan.FromMilliseconds(250);
  }

  public event EventHandler<MarkdownFileChangedEventArgs>? Changed;

  public void Start()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    _watcher.EnableRaisingEvents = true;
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    _disposed = true;
    _watcher.EnableRaisingEvents = false;
    _watcher.Changed -= OnFileSystemEvent;
    _watcher.Created -= OnFileSystemEvent;
    _watcher.Deleted -= OnFileSystemEvent;
    _watcher.Renamed -= OnRenamed;
    _watcher.Dispose();
    _timer?.Dispose();
  }

  private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
  {
    ScheduleNotification(e.FullPath);
  }

  private void OnRenamed(object sender, RenamedEventArgs e)
  {
    ScheduleNotification(e.FullPath);
  }

  private void ScheduleNotification(string path)
  {
    if (_disposed)
    {
      return;
    }

    _timer?.Dispose();
    _timer = new Timer(_ => Notify(path), null, _debounce, Timeout.InfiniteTimeSpan);
  }

  private void Notify(string path)
  {
    if (_disposed)
    {
      return;
    }

    Changed?.Invoke(this, new MarkdownFileChangedEventArgs(path, File.Exists(path)));
  }
}
