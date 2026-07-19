using System.Globalization;
using System.Text;
using Martin.Compiler.Binding;

namespace Martin.CodeGeneration;

internal static class CSharpLiteralWriter
{
    public static string WriteLiteral(BoundLiteralExpression literal) => literal.Value switch {
        null => "null",
        int i => $"{i}L",
        long v => $"{v}L",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        string s => "\"" + Escape(s) + "\"",
        _ => throw new CodeGenerationException("literal")
    };

    static string Escape(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value)
            builder.Append(c switch {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\0",
                _ when char.IsControl(c) => $"\\u{(int)c:x4}",
                _ => c.ToString()
            });
        return builder.ToString();
    }
}
