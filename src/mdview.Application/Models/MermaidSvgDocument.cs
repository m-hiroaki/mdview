namespace mdview.Application.Models;

public sealed record MermaidSvgDocument(string Svg, double Width, double Height);

public sealed record MermaidGenerationResult(MermaidSvgDocument? Document, string? Error)
{
  public static MermaidGenerationResult Failure(string message) => new(null, message);
}
