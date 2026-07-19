using Martin.Compiler.Symbols;

namespace Martin.CodeGeneration;

internal static class DeterministicOrder
{
    public static IOrderedEnumerable<NamedTypeSymbol> NamedTypes(IEnumerable<NamedTypeSymbol> symbols) =>
        symbols.OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => SymbolLocationKey(s), StringComparer.Ordinal);

    public static IOrderedEnumerable<FunctionSymbol> Functions(IEnumerable<FunctionSymbol> symbols) =>
        symbols.OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => SignatureKey(s), StringComparer.Ordinal).ThenBy(s => SymbolLocationKey(s), StringComparer.Ordinal);

    public static IOrderedEnumerable<PropertySymbol> Properties(IEnumerable<PropertySymbol> symbols) =>
        symbols.OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => SymbolLocationKey(s), StringComparer.Ordinal);

    public static IOrderedEnumerable<InitializerSymbol> Initializers(IEnumerable<InitializerSymbol> symbols) =>
        symbols.OrderBy(s => SignatureKey(s), StringComparer.Ordinal).ThenBy(s => SymbolLocationKey(s), StringComparer.Ordinal);

    public static IOrderedEnumerable<MethodSymbol> Methods(IEnumerable<MethodSymbol> symbols) =>
        symbols.OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => SignatureKey(s), StringComparer.Ordinal).ThenBy(s => SymbolLocationKey(s), StringComparer.Ordinal);

    public static IOrderedEnumerable<EnumCaseSymbol> EnumCases(IEnumerable<EnumCaseSymbol> symbols) =>
        symbols.OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => SymbolLocationKey(s), StringComparer.Ordinal);

    public static IOrderedEnumerable<ProtocolRequirementSymbol> ProtocolRequirements(IEnumerable<ProtocolRequirementSymbol> symbols) =>
        symbols.OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => s.Kind.ToString(), StringComparer.Ordinal).ThenBy(s => SymbolLocationKey(s), StringComparer.Ordinal);

    public static IOrderedEnumerable<ProtocolConformance> Conformances(IEnumerable<ProtocolConformance> conformances) =>
        conformances.OrderBy(c => c.Type.Name, StringComparer.Ordinal).ThenBy(c => c.Protocol.Name, StringComparer.Ordinal).ThenBy(c => SymbolLocationKey(c.Type), StringComparer.Ordinal);

    public static IOrderedEnumerable<KeyValuePair<ProtocolRequirementSymbol, MemberSymbol>> Witnesses(IEnumerable<KeyValuePair<ProtocolRequirementSymbol, MemberSymbol>> witnesses) =>
        witnesses.OrderBy(w => w.Key.Name, StringComparer.Ordinal).ThenBy(w => w.Key.Kind.ToString(), StringComparer.Ordinal).ThenBy(w => SymbolLocationKey(w.Key), StringComparer.Ordinal);

    static string SignatureKey(FunctionSymbol symbol) => string.Join("|", symbol.Parameters.Select(p => p.Type.Name)) + "->" + symbol.ReturnType.Name;
    static string SignatureKey(MethodSymbol symbol) => string.Join("|", symbol.Parameters.Select(p => p.Type.Name)) + "->" + symbol.ReturnType.Name;
    static string SignatureKey(InitializerSymbol symbol) => string.Join("|", symbol.Parameters.Select(p => p.Type.Name));

    static string SymbolLocationKey(Symbol symbol)
    {
        if (symbol.Locations.IsDefaultOrEmpty)
            return string.Empty;

        var location = symbol.Locations.FirstOrDefault();
        if (location.Text is null)
            return string.Empty;

        return $"{NormalizePath(location.FilePath)}:{location.Span.Start:D10}:{location.Span.Length:D10}";
    }

    static string NormalizePath(string? path) => string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').ToUpperInvariant();
}
