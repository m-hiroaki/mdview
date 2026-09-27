namespace mdview.Application.Abstractions;

public interface IFileAssociationService
{
  bool IsSupported { get; }

  Task<bool> RegisterMarkdownAssociationAsync(string executablePath, CancellationToken cancellationToken = default);
}
