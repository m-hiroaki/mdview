using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using mdview.Application.Abstractions;
using mdview.Application.Models;

namespace mdview.Infrastructure.Mermaid;

/// <summary>WKWebView produces SVG only; the caller supplies its offscreen UI host.</summary>
public sealed class MacOsMermaidSvgGenerator(Action<NativeWebView?> setHost) : IMermaidSvgGenerator, IDisposable
{
  private readonly SemaphoreSlim _gate = new(1);
  private NativeWebView? _webView;
  private TaskCompletionSource<bool>? _ready;
  private TaskCompletionSource<MermaidGenerationResult>? _response;
  private long _requestId;
  private bool _disposed;

  public async Task<MermaidGenerationResult> GenerateAsync(string source, CancellationToken cancellationToken)
  {
    if (!OperatingSystem.IsMacOS()) return MermaidGenerationResult.Failure("Mermaid は現在 macOS のみ対応しています。");
    if (source.Length > 50000) return MermaidGenerationResult.Failure("Mermaid のソースが長すぎます。");
    // Document-provided configuration is intentionally unsupported; it must not weaken fixed settings.
    if (source.Contains("%%{", StringComparison.Ordinal) || source.TrimStart().StartsWith("---", StringComparison.Ordinal))
      return MermaidGenerationResult.Failure("Mermaid の設定指定は対応していません。");

    await _gate.WaitAsync(cancellationToken);
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(10));
    try
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
      await Dispatcher.UIThread.InvokeAsync(EnsureWebView);
      await _ready!.Task.WaitAsync(timeout.Token);
      var id = Interlocked.Increment(ref _requestId);
      var response = new TaskCompletionSource<MermaidGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
      await Dispatcher.UIThread.InvokeAsync(() => _response = response);
      var script = CreateRenderScript(id, source);
      // InvokeScript itself may hang if the WebKit process is unresponsive.
      Task<string?>? invocation = null;
      await Dispatcher.UIThread.InvokeAsync(() => { invocation = _webView!.InvokeScript(script); });
      await invocation!.WaitAsync(timeout.Token);
      return await response.Task.WaitAsync(timeout.Token);
    }
    catch (OperationCanceledException)
    {
      await Dispatcher.UIThread.InvokeAsync(Reset);
      cancellationToken.ThrowIfCancellationRequested();
      return MermaidGenerationResult.Failure("図の生成が時間内に完了しませんでした。");
    }
    catch (Exception)
    {
      await Dispatcher.UIThread.InvokeAsync(Reset);
      return MermaidGenerationResult.Failure("図の生成環境を初期化できませんでした。");
    }
    finally { _gate.Release(); }
  }

  internal static string CreateRenderScript(long id, string source) =>
    $"mdviewRender({id}, {JsonSerializer.Serialize(source, MermaidJsonContext.Default.String)}); void 0;";

  private void EnsureWebView()
  {
    if (_webView is not null) return;
    _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    _webView = new NativeWebView { Width = 1024, Height = 768, IsHitTestVisible = false, Focusable = false };
    _webView.EnvironmentRequested += OnEnvironmentRequested;
    _webView.WebMessageReceived += OnMessage;
    _webView.NavigationStarted += OnNavigation;
    _webView.NewWindowRequested += OnNewWindow;
    _webView.AdapterDestroyed += OnAdapterDestroyed;
    _webView.AdapterCreated += OnAdapterCreated;
    _webView.NavigationCompleted += OnNavigationCompleted;
    setHost(_webView);
  }

  private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
  {
    e.EnableDevTools = false;
    if (e is AppleWKWebViewEnvironmentRequestedEventArgs apple) apple.NonPersistentDataStore = true;
  }

  private async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
  {
    if (!ReferenceEquals(sender, _webView)) return;
    var ready = _ready;
    try
    {
      if (!e.IsSuccess) throw new InvalidOperationException();
      await _webView!.InvokeScript("if(typeof mdviewRender === 'function') invokeCSharpAction(JSON.stringify({ready:true}));void 0;");
    }
    catch (Exception) { ready?.TrySetException(new InvalidOperationException("Mermaid initialization failed.")); }
  }

  private void OnAdapterCreated(object? sender, WebViewAdapterEventArgs e)
  {
    if (!ReferenceEquals(sender, _webView)) return;
    try
    {
    // Inline fixed assets: no file access, CDN or HTTP server is needed.
    var nonce = Guid.NewGuid().ToString("N");
    var html = "<!doctype html><html><head><meta charset='utf-8'>" +
      $"<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'unsafe-inline'; img-src 'none'; font-src 'none'; connect-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'\">" +
      "<style>body{margin:0;font-family:Arial,'Hiragino Sans',sans-serif}</style></head><body>" +
      $"<script nonce='{nonce}'>" + Resource("mermaid-11.12.1.min.js").Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase) + "</script>" +
      $"<script nonce='{nonce}'>" + Resource("bridge.js") + "</script></body></html>";
    _webView!.NavigateToString(html, new Uri("about:blank"));
    }
    catch (Exception) { _ready?.TrySetException(new InvalidOperationException("Mermaid assets could not be loaded.")); }
  }

  private void OnMessage(object? sender, WebMessageReceivedEventArgs e)
  {
    if (!ReferenceEquals(sender, _webView) || e.Body is null || e.Body.Length > 6 * 1024 * 1024) return;
    try
    {
      using var message = JsonDocument.Parse(e.Body);
      var root = message.RootElement;
      if (root.ValueKind != JsonValueKind.Object) return;
      if (root.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True)
      { _ready?.TrySetResult(true); return; }
      if (!root.TryGetProperty("id", out var id) || !id.TryGetInt64(out var value) || value != _requestId) return;
      if (root.TryGetProperty("svg", out var svg) && svg.ValueKind == JsonValueKind.String)
      {
        var response = _response;
        var content = svg.GetString()!;
        // Parsing a large SVG must not block the window's UI thread.
        if (response is not null) _ = Task.Run(() => response.TrySetResult(MermaidSvgValidator.Validate(content)));
      }
      else _response?.TrySetResult(MermaidGenerationResult.Failure("図を生成できません。Mermaid の構文を確認してください。"));
    }
    catch (JsonException) { /* Ignore unsolicited or malformed messages. */ }
  }

  private void OnNavigation(object? sender, WebViewNavigationStartingEventArgs e) => e.Cancel = e.Request?.ToString() != "about:blank";
  private void OnNewWindow(object? sender, WebViewNewWindowRequestedEventArgs e) => e.Handled = true;
  private void OnAdapterDestroyed(object? sender, WebViewAdapterEventArgs e)
  {
    _ready?.TrySetException(new InvalidOperationException("WebView stopped."));
    _response?.TrySetException(new InvalidOperationException("WebView stopped."));
  }

  private static string Resource(string name)
  {
    using var stream = typeof(MacOsMermaidSvgGenerator).Assembly.GetManifestResourceStream("mdview.Infrastructure.Mermaid.Assets." + name)
      ?? throw new InvalidOperationException("Mermaid asset missing.");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }

  private void Reset()
  {
    ++_requestId;
    if (_webView is { } webView)
    {
      webView.EnvironmentRequested -= OnEnvironmentRequested;
      webView.WebMessageReceived -= OnMessage;
      webView.NavigationStarted -= OnNavigation;
      webView.NewWindowRequested -= OnNewWindow;
      webView.AdapterDestroyed -= OnAdapterDestroyed;
      webView.AdapterCreated -= OnAdapterCreated;
      webView.NavigationCompleted -= OnNavigationCompleted;
      webView.Stop();
      setHost(null);
      _webView = null;
    }
    _ready = null;
    _response = null;
  }

  public void Dispose()
  {
    _disposed = true;
    Reset();
  }
}
