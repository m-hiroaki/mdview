using System.Text;
using mdview.Application.Abstractions;
using mdview.Application.Models;

namespace mdview.Application.UseCases;

/// <summary>One instance per window: fixed engine/settings, bounded SVG cache and shared requests.</summary>
public sealed class MermaidDiagramService(IMermaidSvgGenerator generator, int maxEntries = 64, int maxBytes = 16 * 1024 * 1024) : IDisposable
{
  private readonly object _sync = new();
  private readonly Dictionary<string, MermaidSvgDocument> _cache = new(StringComparer.Ordinal);
  private readonly Queue<string> _order = new();
  private readonly Dictionary<string, Request> _pending = new(StringComparer.Ordinal);
  private readonly CancellationTokenSource _lifetime = new();
  private int _bytes;
  private bool _disposed;

  public async Task<MermaidGenerationResult> GetAsync(string source, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    Request request;
    lock (_sync)
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
      if (_cache.TryGetValue(source, out var document)) return new(document, null);
      if (!_pending.TryGetValue(source, out request!))
      {
        // Start asynchronously so insertion happens before completion and removal.
        request = new(CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token));
        _pending.Add(source, request);
        request.Task = GenerateAsync(source, request);
      }
      request.Subscribers++;
    }
    try { return await request.Task.WaitAsync(cancellationToken); }
    finally
    {
      lock (_sync)
      {
        if (--request.Subscribers == 0)
        {
          if (!request.Finished)
          {
            if (_pending.TryGetValue(source, out var current) && ReferenceEquals(current, request)) _pending.Remove(source);
            request.Cancellation.Cancel();
          }
          else request.Cancellation.Dispose();
        }
      }
    }
  }

  private async Task<MermaidGenerationResult> GenerateAsync(string source, Request request)
  {
    await Task.Yield();
    try
    {
      var result = await generator.GenerateAsync(source, request.Cancellation.Token);
      lock (_sync)
      {
        if (result.Document is { } document && !request.Cancellation.IsCancellationRequested)
        {
          var size = Encoding.UTF8.GetByteCount(source) + Encoding.UTF8.GetByteCount(document.Svg);
          if (maxEntries > 0 && size <= maxBytes)
          {
            while (_cache.Count >= maxEntries || _bytes + size > maxBytes)
            {
              var key = _order.Dequeue();
              var old = _cache[key];
              _bytes -= Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(old.Svg);
              _cache.Remove(key);
            }
            _cache.Add(source, document);
            _order.Enqueue(source);
            _bytes += size;
          }
        }
      }
      return result;
    }
    finally
    {
      lock (_sync)
      {
        if (_pending.TryGetValue(source, out var current) && ReferenceEquals(current, request)) _pending.Remove(source);
        request.Finished = true;
        if (request.Subscribers == 0) request.Cancellation.Dispose();
      }
    }
  }

  public void Dispose()
  {
    lock (_sync)
    {
      if (_disposed) return;
      _disposed = true;
      _cache.Clear(); _order.Clear(); _bytes = 0;
    }
    _lifetime.Cancel();
    _lifetime.Dispose();
  }

  private sealed class Request(CancellationTokenSource cancellation)
  {
    public CancellationTokenSource Cancellation { get; } = cancellation;
    public Task<MermaidGenerationResult> Task { get; set; } = null!;
    public int Subscribers { get; set; }
    public bool Finished { get; set; }
  }
}
