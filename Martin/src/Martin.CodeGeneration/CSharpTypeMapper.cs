using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

public static class CSharpTypeMapper
{
    public static string GetTypeName(TypeSymbol type) => GetTypeName(type, named => CSharpNameMangler.GetIdentifier(named.Name));

    internal static string GetTypeName(TypeSymbol type, Func<NamedTypeSymbol, string> getNamedTypeName) => type switch
    {
        _ when type == TypeSymbol.Int => "long",
        _ when type == TypeSymbol.Double => "double",
        _ when type == TypeSymbol.Bool => "bool",
        _ when type == TypeSymbol.String => "string",
        _ when type == TypeSymbol.Void => "void",
        ConstructedTypeSymbol constructed => $"{getNamedTypeName(constructed.GenericDefinition)}<{string.Join(", ", constructed.TypeArguments.Select(argument => GetTypeName(argument, getNamedTypeName)))}>",
        TypeParameterSymbol parameter => CSharpNameMangler.GetIdentifier(parameter.Name),
        NamedTypeSymbol named when named.TypeParameters.Length == 0 => getNamedTypeName(named),
        NamedTypeSymbol named => $"{getNamedTypeName(named)}<{string.Join(", ", named.TypeParameters.Select(parameter => CSharpNameMangler.GetIdentifier(parameter.Name)))}>",
        OptionalTypeSymbol optional => $"Martin.Runtime.Optional<{GetTypeName(optional.ElementType, getNamedTypeName)}>",
        _ => throw new CodeGenerationException($"Unsupported Martin type '{type.Name}' during C# generation.")
    };
}
