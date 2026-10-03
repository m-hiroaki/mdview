using mdview.Application.Models;

namespace mdview.Application.Abstractions;

public interface IMermaidSvgGenerator
{
  Task<MermaidGenerationResult> GenerateAsync(string source, CancellationToken cancellationToken);
}
