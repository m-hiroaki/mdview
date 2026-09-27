using mdview.Application.UseCases;

namespace mdview.Application.Tests;

public class MarkdownSearchAndZoomTests
{
  [Fact]
  public void Search_CalculatesMatchesAndWrapsNavigation()
  {
    var state = new MarkdownSearchState();
    state.SetText("Readme README reader");
    state.SetQuery("readme");

    Assert.Equal(2, state.MatchCount);
    Assert.Equal(0, state.CurrentMatch);

    state.MoveNext();
    Assert.Equal(1, state.CurrentMatch);
    state.MoveNext();
    Assert.Equal(0, state.CurrentMatch);
    state.MovePrevious();
    Assert.Equal(1, state.CurrentMatch);
  }

  [Fact]
  public void Search_ClearsMatchesWhenQueryIsEmpty()
  {
    var state = new MarkdownSearchState();
    state.SetText("content");
    state.SetQuery("content");
    state.SetQuery(string.Empty);

    Assert.Equal(0, state.MatchCount);
    Assert.Equal(0, state.CurrentMatch);
  }

  [Fact]
  public void Zoom_IsBoundedAndResettable()
  {
    var state = new MarkdownZoomState();

    for (var index = 0; index < 30; index++)
    {
      state.Increase();
    }

    Assert.Equal(MarkdownZoomState.Maximum, state.Scale);

    for (var index = 0; index < 30; index++)
    {
      state.Decrease();
    }

    Assert.Equal(MarkdownZoomState.Minimum, state.Scale);
    state.Reset();
    Assert.Equal(1.0, state.Scale);
  }
}
