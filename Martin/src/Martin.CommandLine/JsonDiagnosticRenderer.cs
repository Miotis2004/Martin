using System.Text.Json;
using System.Text.Json.Serialization;

namespace Martin.CommandLine;

public sealed class JsonDiagnosticRenderer : DiagnosticRenderer
{
    static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Render(IEnumerable<CommandDiagnostic> diagnostics, TextWriter writer)
    {
        var payload = new DiagnosticJsonEnvelope(DiagnosticOrdering.Sort(diagnostics).Select(DiagnosticJsonModel.FromDiagnostic).ToArray());
        writer.WriteLine(JsonSerializer.Serialize(payload, Options));
    }
}
