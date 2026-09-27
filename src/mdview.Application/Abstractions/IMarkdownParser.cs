using mdview.Application.Models;

namespace mdview.Application.Abstractions;

public interface IMarkdownParser
{
  MarkdownDocumentModel Parse(string markdown);
}
