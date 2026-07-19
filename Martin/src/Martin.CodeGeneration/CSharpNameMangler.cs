using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

internal sealed class CSharpNameMangler
{
    readonly Dictionary<Symbol, string> _names = new(ReferenceEqualityComparer.Instance); readonly HashSet<string> _used = []; readonly TemporaryAllocator _temporaries = new();
    static readonly HashSet<string> Keywords = ["abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while", "var", "record", "with", "init", "required"];
    public string GetName(Symbol symbol) { if (_names.TryGetValue(symbol, out var n)) return n; var prefix = symbol switch { FunctionSymbol f => f.Name == "main" ? "__martin_" : "__fn_", NamedTypeSymbol => "", EnumCaseSymbol => "", PropertySymbol => "", ProtocolRequirementSymbol => "", MethodSymbol => "__method_", _ => "__local_" }; var baseName = Sanitize(symbol.Name); n = Unique(prefix + baseName); _names[symbol] = n; return n; }
    public string CreateTemporaryName(string purpose) => Unique(_temporaries.Allocate(purpose));
    string Unique(string n) { var c = n; var i = 0; while (!_used.Add(c)) c = n + "_" + i++; return c; }
    internal static string GetIdentifier(string value) => Sanitize(value);
    static string Sanitize(string s) { if (string.IsNullOrWhiteSpace(s)) return "unnamed"; var b = new StringBuilder(); for (int i = 0; i < s.Length; i++) { var ch = s[i]; b.Append((i == 0 ? char.IsLetter(ch) || ch == '_' : char.IsLetterOrDigit(ch) || ch == '_') ? ch : '_'); } var r = b.ToString(); return Keywords.Contains(r) ? "@" + r : r; }
}
