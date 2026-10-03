using System.Text.Json.Serialization;

namespace mdview.Infrastructure.Mermaid;

[JsonSerializable(typeof(string))]
internal partial class MermaidJsonContext : JsonSerializerContext;
