using Microsoft.Win32;
using mdview.Application.Abstractions;

namespace mdview.Infrastructure.FileSystem;

public sealed class FileAssociationService : IFileAssociationService
{
  public bool IsSupported => OperatingSystem.IsWindows();

  public Task<bool> RegisterMarkdownAssociationAsync(string executablePath, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
    cancellationToken.ThrowIfCancellationRequested();

    if (!OperatingSystem.IsWindows())
    {
      return Task.FromResult(false);
    }

    var fullPath = Path.GetFullPath(executablePath);
    const string applicationId = "mdview.Markdown";
    var extensions = new[] { ".md", ".markdown", ".mdown" };

    foreach (var extension in extensions)
    {
      using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
      extensionKey?.SetValue(string.Empty, applicationId);
    }

    using var applicationKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{applicationId}\shell\open\command");
    applicationKey?.SetValue(string.Empty, $"\"{fullPath}\" \"%1\"");

    return Task.FromResult(true);
  }
}
