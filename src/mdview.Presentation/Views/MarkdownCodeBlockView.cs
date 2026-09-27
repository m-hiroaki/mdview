using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using mdview.Application.Models;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;

namespace mdview.Presentation.Views;

public sealed class MarkdownCodeBlockView : Border
{
  private static readonly RegistryOptions GrammarRegistry = new(ThemeName.DarkPlus);

  private static readonly IReadOnlyDictionary<string, string> LanguageExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
  {
    ["c"] = ".c",
    ["h"] = ".h",
    ["c++"] = ".cpp",
    ["cpp"] = ".cpp",
    ["cxx"] = ".cpp",
    ["c#"] = ".cs",
    ["cs"] = ".cs",
    ["csharp"] = ".cs",
    ["python"] = ".py",
    ["py"] = ".py",
    ["javascript"] = ".js",
    ["js"] = ".js",
    ["typescript"] = ".ts",
    ["ts"] = ".ts",
    ["json"] = ".json",
    ["xml"] = ".xml",
    ["html"] = ".html",
    ["css"] = ".css",
    ["bash"] = ".sh",
    ["sh"] = ".sh",
    ["shell"] = ".sh",
    ["powershell"] = ".ps1",
    ["ps1"] = ".ps1",
    ["yaml"] = ".yml",
    ["yml"] = ".yml",
    ["markdown"] = ".md",
    ["md"] = ".md"
  };

  private readonly TextEditor _editor;
  private readonly string? _grammarScope;
  private IDisposable? _textMateInstallation;

  public MarkdownCodeBlockView(MarkdownCodeBlock block)
  {
    _editor = new TextEditor
    {
      Text = block.Code,
      IsReadOnly = true,
      ShowLineNumbers = false,
      WordWrap = true,
      MinHeight = 32,
      MaxHeight = 440,
      FontFamily = new FontFamily("monospace"),
      Background = Brushes.Transparent,
      Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
    };

    Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
    BorderBrush = new SolidColorBrush(Color.Parse("#353535"));
    BorderThickness = new Thickness(1);
    CornerRadius = new CornerRadius(4);
    Padding = new Thickness(8);
    Margin = new Thickness(0, 6, 0, 12);
    Child = _editor;

    if (block.Language is not null && LanguageExtensions.TryGetValue(block.Language, out var extension))
    {
      var language = GrammarRegistry.GetLanguageByExtension(extension);
      if (language is not null)
      {
        _grammarScope = GrammarRegistry.GetScopeByLanguageId(language.Id);
      }
    }
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);

    if (_grammarScope is not null && _textMateInstallation is null)
    {
      var installation = _editor.InstallTextMate(GrammarRegistry);
      installation.SetGrammar(_grammarScope);
      _textMateInstallation = installation as IDisposable;
    }
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    _textMateInstallation?.Dispose();
    _textMateInstallation = null;
    base.OnDetachedFromVisualTree(e);
  }
}
