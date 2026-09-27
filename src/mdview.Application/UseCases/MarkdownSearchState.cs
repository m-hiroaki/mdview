namespace mdview.Application.UseCases;

public sealed class MarkdownSearchState
{
  private string _text = string.Empty;
  private string _query = string.Empty;
  private IReadOnlyList<int> _matches = Array.Empty<int>();

  public string Query => _query;

  public int CurrentMatch { get; private set; }

  public int MatchCount => _matches.Count;

  public void SetText(string? text)
  {
    _text = text ?? string.Empty;
    Recalculate();
  }

  public void SetQuery(string? query)
  {
    _query = query ?? string.Empty;
    Recalculate();
  }

  public void MoveNext()
  {
    if (_matches.Count > 0)
    {
      CurrentMatch = (CurrentMatch + 1) % _matches.Count;
    }
  }

  public void MovePrevious()
  {
    if (_matches.Count > 0)
    {
      CurrentMatch = (CurrentMatch - 1 + _matches.Count) % _matches.Count;
    }
  }

  public bool IsMatch(int index, int length) =>
    length > 0 && _matches.Any(match => index < match + _query.Length && index + length > match);

  private void Recalculate()
  {
    if (string.IsNullOrEmpty(_query))
    {
      _matches = Array.Empty<int>();
      CurrentMatch = 0;
      return;
    }

    var matches = new List<int>();
    var offset = 0;
    while (offset <= _text.Length - _query.Length)
    {
      var match = _text.IndexOf(_query, offset, StringComparison.CurrentCultureIgnoreCase);
      if (match < 0)
      {
        break;
      }

      matches.Add(match);
      offset = match + Math.Max(1, _query.Length);
    }

    _matches = matches;
    CurrentMatch = _matches.Count == 0 ? 0 : Math.Min(CurrentMatch, _matches.Count - 1);
  }
}
