namespace mdview.Application.UseCases;

public sealed class MarkdownZoomState
{
  public const double Minimum = 0.75;
  public const double Maximum = 2.0;
  public const double Step = 0.1;

  public double Scale { get; private set; } = 1.0;

  public void Increase() => Scale = Math.Min(Maximum, Scale + Step);

  public void Decrease() => Scale = Math.Max(Minimum, Scale - Step);

  public void Reset() => Scale = 1.0;
}
