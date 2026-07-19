using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

/// <summary>Emits the private C# representation used for Martin enums.</summary>
internal sealed class CSharpEnumEmitter(
    GeneratedSourceWriter writer,
    CSharpNameMangler names,
    bool includeSourceDirectives,
    Func<System.Collections.Immutable.ImmutableArray<TypeParameterSymbol>, string> genericConstraints)
{
    private readonly HashSet<TypeSymbol> _equalityStack = new(ReferenceEqualityComparer.Instance);

    public void Write(NamedTypeSymbol type)
    {
        var hasSemanticEquality = SupportsEquality(type);
        WriteLineDirective(type.DeclarationLocation);
        using (writer.MapTo(type.DeclarationLocation))
        {
            writer.WriteLine($"internal abstract {(hasSemanticEquality ? "record" : "class")} {names.GetName(type)}{TypeParameterList(type)}{genericConstraints(type.TypeParameters)}");
            writer.WriteLine("{");
            using (writer.Indent())
            {
                writer.WriteLine($"private {names.GetName(type)}() {{ }}");
                foreach (var @case in DeterministicOrder.EnumCases(type.Cases))
                    WriteCase(type, @case, hasSemanticEquality);
            }
            writer.WriteLine("}");
        }
        if (includeSourceDirectives)
            writer.WriteLine("#line hidden");
        writer.WriteLine();
    }

    private void WriteCase(NamedTypeSymbol type, EnumCaseSymbol @case, bool hasSemanticEquality)
    {
        WriteLineDirective(@case.DeclarationLocation);
        using (writer.MapTo(@case.DeclarationLocation))
        {
            if (hasSemanticEquality)
            {
                var parameters = string.Join(", ", @case.AssociatedValues.Select((value, index) =>
                                                                                     $"{TypeName(value.Type)} {PayloadMember(index)}"));
                writer.WriteLine($"internal sealed record {names.GetName(@case)}({parameters}) : {OpenTypeName(type)};");
                return;
            }

            writer.WriteLine($"internal sealed class {names.GetName(@case)} : {OpenTypeName(type)}");
            writer.WriteLine("{");
            using (writer.Indent())
            {
                foreach (var (value, index) in @case.AssociatedValues.Select((value, index) => (value, index)))
                    writer.WriteLine($"internal {TypeName(value.Type)} {PayloadMember(index)} {{ get; }}");

                writer.WriteLine($"internal {names.GetName(@case)}({ConstructorParameters(@case)})");
                writer.WriteLine("{");
                using (writer.Indent())
                {
                    foreach (var (value, index) in @case.AssociatedValues.Select((value, index) => (value, index)))
                        writer.WriteLine($"{PayloadMember(index)} = {ConstructorParameter(index)};");
                }
                writer.WriteLine("}");
            }
            writer.WriteLine("}");
        }
    }

    private string ConstructorParameters(EnumCaseSymbol @case) => string.Join(", ",
                                                                              @case.AssociatedValues.Select((value, index) =>
                                                                                                                $"{TypeName(value.Type)} {ConstructorParameter(index)}"));

    private static string ConstructorParameter(int index) => $"value{index}";

    private static string PayloadMember(int index) => $"Value{index}";

    private bool SupportsEquality(TypeSymbol type)
    {
        if (type == TypeSymbol.Bool || type == TypeSymbol.Int || type == TypeSymbol.Double || type == TypeSymbol.String)
            return true;
        if (type is OptionalTypeSymbol optional)
            return SupportsEquality(optional.ElementType);
        if (type is not EnumTypeSymbol enumType || !_equalityStack.Add(type))
            return false;

        var result = enumType.Cases.SelectMany(item => item.AssociatedValues)
                         .All(value => SupportsEquality(value.Type));
        _equalityStack.Remove(type);
        return result;
    }

    private void WriteLineDirective(TextLocation? location)
    {
        if (!includeSourceDirectives || location is not {} value)
            return;
        var path = (value.FilePath ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        writer.WriteLine($"#line {value.StartLinePosition.Line} \"{path}\"");
    }

    private static string TypeParameterList(NamedTypeSymbol type) => type.TypeParameters.Length == 0
                                                                         ? string.Empty
                                                                         : "<" + string.Join(", ", type.TypeParameters.Select(parameter => CSharpNameMangler.GetIdentifier(parameter.Name))) + ">";

    private string TypeName(TypeSymbol type) => CSharpTypeMapper.GetTypeName(type, names.GetName);

    private string OpenTypeName(NamedTypeSymbol type) => names.GetName(type) + TypeParameterList(type);
}
