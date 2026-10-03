using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using mdview.Application.Models;

namespace mdview.Infrastructure.Mermaid;

public static partial class MermaidSvgValidator
{
  private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
  {
    "svg", "g", "defs", "style", "title", "desc", "metadata", "path", "rect", "circle", "ellipse",
    "line", "polyline", "polygon", "text", "tspan", "textPath", "marker", "clipPath", "mask", "use",
    "linearGradient", "radialGradient", "stop", "pattern", "symbol"
  };

  public static MermaidGenerationResult Validate(string svg)
  {
    if (Encoding.UTF8.GetByteCount(svg) > 5 * 1024 * 1024) return MermaidGenerationResult.Failure("図が大きすぎます。");
    try
    {
      using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
      {
        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5 * 1024 * 1024
      });
      var document = XDocument.Load(reader);
      var root = document.Root;
      XNamespace ns = "http://www.w3.org/2000/svg";
      if (root?.Name != ns + "svg") return Invalid();
      var count = 0;
      foreach (var element in root.DescendantsAndSelf())
      {
        if (++count > 25000 || element.Ancestors().Take(65).Count() > 64 || element.Name.Namespace != ns || !Elements.Contains(element.Name.LocalName)) return Invalid();
        if (element.Name.LocalName == "style" && !SafeCss(element.Value)) return Invalid();
        foreach (var attribute in element.Attributes())
        {
          var name = attribute.Name.LocalName;
          if (attribute.IsNamespaceDeclaration) continue;
          if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || name == "base") return Invalid();
          if (name is "href" or "src" && !attribute.Value.StartsWith('#')) return Invalid();
          if (!SafeCss(attribute.Value)) return Invalid();
        }
      }
      var values = root.Attribute("viewBox")?.Value.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
      if (values is not { Length: 4 }) return Invalid();
      var box = values.Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
      if (box.Any(value => !double.IsFinite(value)) || box[2] <= 0 || box[3] <= 0 || box[2] > 100000 || box[3] > 100000) return Invalid();
      // Explicit intrinsic dimensions avoid percentage width being interpreted differently by SVG loaders.
      root.SetAttributeValue("width", box[2].ToString(CultureInfo.InvariantCulture));
      root.SetAttributeValue("height", box[3].ToString(CultureInfo.InvariantCulture));
      return new(new MermaidSvgDocument(root.ToString(SaveOptions.DisableFormatting), box[2], box[3]), null);
    }
    catch (Exception e) when (e is XmlException or FormatException or OverflowException or InvalidOperationException)
    { return Invalid(); }
  }

  private static MermaidGenerationResult Invalid() => MermaidGenerationResult.Failure("対応していない SVG または不正な図です。");

  private static bool SafeCss(string value)
  {
    // Reject CSS escapes/comments that could conceal external resource syntax.
    if (value.Contains('\\') || value.Contains("/*") || value.Contains('@')) return false;
    foreach (Match match in Url().Matches(value))
    {
      var target = match.Groups[1].Value.Trim().Trim('\'', '"');
      if (!target.StartsWith('#')) return false;
    }
    return !value.Contains("javascript:", StringComparison.OrdinalIgnoreCase);
  }

  [GeneratedRegex(@"url\s*\(([^)]*)\)", RegexOptions.IgnoreCase)]
  private static partial Regex Url();
}
