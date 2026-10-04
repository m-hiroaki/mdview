using mdview.Application.Models;
using mdview.Infrastructure.Markdown;
using mdview.Infrastructure.Mermaid;

namespace mdview.Infrastructure.Tests;

public class MermaidTests
{
  [Theory]
  [InlineData(null, false)]
  [InlineData("", false)]
  [InlineData("0.0.0.0", false)]
  [InlineData("invalid", false)]
  [InlineData("154.0.4258.53", true)]
  public void RuntimeDetection_RequiresInstalledVersion(string? version, bool installed) =>
    Assert.Equal(installed, WebViewMermaidSvgGenerator.HasRuntimeVersion(version));

  [Theory]
  [InlineData("about:blank", true)]
  [InlineData("data:text/html;charset=utf-8;base64,fixed", true)]
  [InlineData("data:text/html;charset=utf-8;base64,other", false)]
  [InlineData("https://example.com", false)]
  [InlineData("file:///C:/secret.md", false)]
  [InlineData(null, false)]
  public void Navigation_AllowsOnlyExactBootstrapAndBlank(string? request, bool allowed) =>
    Assert.Equal(allowed, WebViewMermaidSvgGenerator.IsAllowedNavigation(request, "data:text/html;charset=utf-8;base64,fixed"));

  [Fact]
  public void WindowsHtml_StaysBelowWebView2LimitAndBlocksExternalResources()
  {
    var html = WebViewMermaidSvgGenerator.CreateWindowsHtml();
    Assert.True(System.Text.Encoding.UTF8.GetByteCount(html) < 2 * 1024 * 1024);
    Assert.DoesNotContain("<script", html);
    Assert.Contains("script-src 'none'", html);
    Assert.Contains("connect-src 'none'", html);
    Assert.Contains("img-src 'none'", html);
    Assert.Contains("frame-src 'none'", html);
  }

  [Fact]
  public void WindowsEnvironment_UsesPrivateProfileOutsideExecutableDirectory()
  {
    // Avalonia creates these event args through an internal constructor.
    var constructor = Assert.Single(typeof(Avalonia.Platform.WindowsWebView2EnvironmentRequestedEventArgs)
      .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
    var environment = (Avalonia.Platform.WindowsWebView2EnvironmentRequestedEventArgs)constructor.Invoke([null]);
    WebViewMermaidSvgGenerator.ConfigureWindowsEnvironment(environment);
    Assert.False(environment.EnableDevTools);
    Assert.True(environment.IsInPrivateModeEnabled);
    Assert.Equal("Mermaid", environment.ProfileName);
    Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "mdview", "MermaidWebView2"), environment.UserDataFolder);
  }

  [Theory]
  [InlineData("%%{init: {securityLevel: 'loose'}}%%\nflowchart TD\nA-->B")]
  [InlineData("---\nconfig:\n  securityLevel: loose\n---\nflowchart TD\nA-->B")]
  [InlineData("long")]
  public async Task Generator_RejectsUnsafeOrExcessiveSourceBeforeCreatingHost(string source)
  {
    if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS()) return;
    using var generator = new WebViewMermaidSvgGenerator(_ => throw new Exception("Host must not be created."));
    var result = await generator.GenerateAsync(source == "long" ? new string('a', 50001) : source, TestContext.Current.CancellationToken);
    Assert.Null(result.Document);
    Assert.NotNull(result.Error);
  }

  [Fact]
  public void RenderScript_SerializesSafelyWithJsonReflectionDisabled()
  {
    Assert.False(System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault);
    const string source = "flowchart TD\nA[\"日本語 \\\\ </script>\"] --> B";
    var script = WebViewMermaidSvgGenerator.CreateRenderScript(42, source);
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
