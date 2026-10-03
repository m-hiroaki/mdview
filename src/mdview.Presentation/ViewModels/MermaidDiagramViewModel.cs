using System.ComponentModel;
using mdview.Application.Models;
using mdview.Application.UseCases;

namespace mdview.Presentation.ViewModels;

public sealed class MermaidDiagramViewModel(MermaidDiagramService service, string source) : INotifyPropertyChanged
{
  public MermaidSvgDocument? Document { get; private set; }
  public string Message { get; private set; } = "図を生成しています…";
  public bool HasError { get; private set; }

  public async Task LoadAsync(CancellationToken cancellationToken)
  {
    try
    {
      var result = await service.GetAsync(source, cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      Document = result.Document;
      Message = result.Error ?? string.Empty;
      HasError = Document is null;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    catch (OperationCanceledException) { }
    catch (ObjectDisposedException) { }
    catch (Exception)
    {
      if (cancellationToken.IsCancellationRequested) return;
      Message = "図を表示できませんでした。";
      HasError = true;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;
}
