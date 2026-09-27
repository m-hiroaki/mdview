using mdview.Infrastructure.FileSystem;

namespace mdview.Infrastructure.Tests;

public class MarkdownFileWatcherTests
{
  [Fact]
  public async Task Changed_IsDebouncedAndReportsExistingFile()
  {
    var directory = Directory.CreateTempSubdirectory();
    var path = Path.Combine(directory.FullName, "document.md");
    await File.WriteAllTextAsync(path, "before");
    using var watcher = new MarkdownFileWatcher(path, TimeSpan.FromMilliseconds(50));
    var notifications = new List<bool>();
    var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    watcher.Changed += (_, args) =>
    {
      notifications.Add(args.Exists);
      signal.TrySetResult();
    };

    watcher.Start();
    await File.WriteAllTextAsync(path, "after");
    await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));

    Assert.Single(notifications);
    Assert.True(notifications[0]);
    directory.Delete(true);
  }

  [Fact]
  public async Task Deleted_ReportsMissingFile()
  {
    var directory = Directory.CreateTempSubdirectory();
    var path = Path.Combine(directory.FullName, "document.md");
    await File.WriteAllTextAsync(path, "content");
    using var watcher = new MarkdownFileWatcher(path, TimeSpan.FromMilliseconds(50));
    var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    watcher.Changed += (_, args) => signal.TrySetResult(args.Exists);

    watcher.Start();
    File.Delete(path);
    var exists = await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));

    Assert.False(exists);
    directory.Delete(true);
  }
}
