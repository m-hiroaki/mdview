using mdview.Application.Models;
using mdview.Infrastructure.Markdown;
using mdview.Infrastructure.Mermaid;

namespace mdview.Infrastructure.Tests;

public class MermaidTests
{
  [Fact]
  public void RenderScript_SerializesSafelyWithJsonReflectionDisabled()
  {
    Assert.False(System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault);
    const string source = "flowchart TD\nA[\"日本語 \\\\ </script>\"] --> B";
    var script = MacOsMermaidSvgGenerator.CreateRenderScript(42, source);
    const string prefix = "mdviewRender(42, ";
    const string suffix = "); void 0;";
    Assert.StartsWith(prefix, script);
    Assert.EndsWith(suffix, script);
    Assert.DoesNotContain("</script>", script);
    using var json = System.Text.Json.JsonDocument.Parse(script[prefix.Length..^suffix.Length]);
    Assert.Equal(source, json.RootElement.GetString());
  }

  [Fact]
  public void Parser_RecognizesNestedDiagramsAndExcludesOnlyTheirSourceFromSearch()
  {
    var document = new MarkdigMarkdownParser().Parse("""
      visible

      > ```MERMAID
      > flowchart TD
      > A[hidden] --> B
      > ```

      - item

        ```mermaid
        sequenceDiagram
        A->>B: hidden
        ```

      ```text
      visibleCode
      ```
      """);
    var quote = Assert.Single(document.Blocks.OfType<MarkdownQuoteBlock>());
    Assert.Contains("hidden", Assert.IsType<MarkdownMermaidBlock>(Assert.Single(quote.Blocks)).Source);
    var list = Assert.Single(document.Blocks.OfType<MarkdownListBlock>());
    Assert.Single(list.Items[0].Blocks.OfType<MarkdownMermaidBlock>());
    Assert.Single(document.Blocks.OfType<MarkdownCodeBlock>());
    Assert.DoesNotContain("hidden", document.SearchText);
    Assert.Contains("visibleCode", document.SearchText);
    Assert.Contains("visible", document.SearchText);
  }

  [Fact]
  public void Validator_PreservesVectorTextAndInternalMarkerReferences()
  {
    var result = MermaidSvgValidator.Validate("""
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="-2 -3 100 50" width="100%">
        <style>#diagram .edge {marker-end:url(#arrow)}</style>
        <defs><marker id="arrow"><path d="M0 0L2 2"/></marker></defs>
        <text x="1" y="20">日本語</text><path marker-end="url(#arrow)" d="M0 0L5 5"/>
      </svg>
      """);
    Assert.Null(result.Error);
    Assert.Equal(100, result.Document!.Width);
    Assert.Contains("日本語", result.Document.Svg);
    Assert.Contains("width=\"100\"", result.Document.Svg);
  }

  [Theory]
  [InlineData("<foreignObject/>")]
  [InlineData("<script>alert(1)</script>")]
  [InlineData("<image href='https://example.com/a.png'/>")]
  [InlineData("<use href='file:///tmp/a.svg#x'/>")]
  [InlineData("<rect onclick='alert(1)'/>")]
  [InlineData("<style>@import 'https://example.com/a.css';</style>")]
  [InlineData("<style>rect{fill:url(https://example.com/a.svg)}</style>")]
  [InlineData("<animate attributeName='x'/>")]
  public void Validator_RejectsActiveOrExternalContent(string content)
  {
    var result = MermaidSvgValidator.Validate($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'>{content}</svg>");
    Assert.Null(result.Document);
    Assert.NotNull(result.Error);
  }

  [Theory]
  [InlineData("0 0 NaN 10")]
  [InlineData("0 0 -1 10")]
  [InlineData("0 0 100001 10")]
  [InlineData("0 0 1")]
  public void Validator_RejectsInvalidDimensions(string box) =>
    Assert.Null(MermaidSvgValidator.Validate($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='{box}'/>").Document);

  [Fact]
  public void Validator_RejectsDtd() => Assert.Null(MermaidSvgValidator.Validate("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///tmp/x'>]><svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'>&x;</svg>").Document);

  [Fact]
  public void Validator_RejectsExcessiveNesting()
  {
    var svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'>" + string.Concat(Enumerable.Repeat("<g>", 66))
      + string.Concat(Enumerable.Repeat("</g>", 66)) + "</svg>";
    Assert.Null(MermaidSvgValidator.Validate(svg).Document);
  }
}
