using mdview.Application.Abstractions;
using mdview.Application.Models;
using mdview.Application.UseCases;

namespace mdview.Application.Tests;

public class MermaidDiagramServiceTests
{
  [Fact]
  public async Task SharesConcurrentRequestsAndCachesSuccessfulResults()
  {
    var generator = new Generator();
    using var service = new MermaidDiagramService(generator);
    var token = TestContext.Current.CancellationToken;
    var first = service.GetAsync("A", token);
    var second = service.GetAsync("A", token);
    await generator.Started.Task.WaitAsync(token);
    generator.Completion.SetResult(Success());
    Assert.Equal(await first, await second);
    await service.GetAsync("A", token);
    Assert.Equal(1, generator.Count);
  }

  [Fact]
  public async Task CancelledSubscriberDoesNotCancelOtherSubscriber()
  {
    var generator = new Generator();
    using var service = new MermaidDiagramService(generator);
    using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var first = service.GetAsync("A", cancel.Token);
    var second = service.GetAsync("A", TestContext.Current.CancellationToken);
    await generator.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
    cancel.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    generator.Completion.SetResult(Success());
    Assert.NotNull((await second).Document);
  }

  [Fact]
  public async Task EvictsByCountAndDoesNotCacheFailures()
  {
    var generator = new Generator();
    generator.Completion.SetResult(Success());
    using var service = new MermaidDiagramService(generator, maxEntries: 1);
    var token = TestContext.Current.CancellationToken;
    await service.GetAsync("A", token);
    await service.GetAsync("B", token);
    await service.GetAsync("A", token);
    Assert.Equal(3, generator.Count);
    generator.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    generator.Completion.SetResult(MermaidGenerationResult.Failure("error"));
    await service.GetAsync("C", token);
    await service.GetAsync("C", token);
    Assert.Equal(5, generator.Count);
  }

  private static MermaidGenerationResult Success() => new(new("<svg/>", 10, 10), null);

  [Fact]
  public async Task CancelsGenerationWhenLastSubscriberLeavesAndAllowsRetry()
  {
    var generator = new Generator();
    using var service = new MermaidDiagramService(generator);
    using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var first = service.GetAsync("A", cancel.Token);
    await generator.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
    cancel.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    generator.Completion.TrySetResult(Success());
    Assert.NotNull((await service.GetAsync("A", TestContext.Current.CancellationToken)).Document);
    Assert.Equal(2, generator.Count);
  }

  [Fact]
  public async Task ByteLimitPreventsOversizedResultsBeingCached()
  {
    var generator = new Generator();
    generator.Completion.SetResult(Success());
    using var service = new MermaidDiagramService(generator, maxBytes: 1);
    await service.GetAsync("A", TestContext.Current.CancellationToken);
    await service.GetAsync("A", TestContext.Current.CancellationToken);
    Assert.Equal(2, generator.Count);
  }

  [Fact]
  public async Task DisposalCancelsPendingWork()
  {
    var generator = new Generator();
    var service = new MermaidDiagramService(generator);
    var task = service.GetAsync("A", TestContext.Current.CancellationToken);
    await generator.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
    service.Dispose();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    await Assert.ThrowsAsync<ObjectDisposedException>(() => service.GetAsync("B", TestContext.Current.CancellationToken));
  }

  private sealed class Generator : IMermaidSvgGenerator
  {
    public int Count;
    public TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<MermaidGenerationResult> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<MermaidGenerationResult> GenerateAsync(string source, CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref Count);
      Started.TrySetResult(true);
      return await Completion.Task.WaitAsync(cancellationToken);
    }
  }
}
