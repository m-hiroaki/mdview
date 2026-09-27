namespace mdview.Application.Abstractions;

public interface IMarkdownFileReader
{
    Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default);
}
