using System.Collections.Immutable;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.Compiler.Binding;

/// <summary>Describes the types and scopes used while recursively binding one pattern.</summary>
public sealed record PatternBindingContext
{
    public required TypeSymbol InputType { get; init; }
    public required BoundScope EnclosingScope { get; init; }
    public required BoundScope CaseScope { get; init; }
    public required SourceText SourceText { get; init; }
    public required int NestingDepth { get; init; }
    public TypeSymbol? ExpectedEnum { get; init; }
    public OptionalTypeSymbol? ExpectedOptional { get; init; }
}

/// <summary>Binds pattern syntax without treating patterns as expressions.</summary>
public sealed class PatternBinder
{
    /// <summary>The semantic recursion limit, independent of parser recovery limits.</summary>
    public const int MaximumNestingDepth = 256;
    private readonly Dictionary<SyntaxNode, Symbol>? _declaredSymbols;
    private readonly Dictionary<SyntaxNode, Symbol>? _symbolInfo;
    private readonly HashSet<string> _bindings = new(StringComparer.Ordinal);

    public PatternBinder(Dictionary<SyntaxNode, Symbol>? declaredSymbols = null, Dictionary<SyntaxNode, Symbol>? symbolInfo = null)
    {
        _declaredSymbols = declaredSymbols;
        _symbolInfo = symbolInfo;
    }

    public BoundPattern Bind(PatternSyntax syntax, PatternBindingContext context, DiagnosticBag diagnostics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostics);
        _bindings.Clear();
        return BindCore(syntax, context, diagnostics, cancellationToken);
    }

    private BoundPattern BindCore(PatternSyntax syntax, PatternBindingContext context, DiagnosticBag diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var location = new TextLocation(context.SourceText, syntax.Span);
        if (context.NestingDepth >= MaximumNestingDepth)
        {
            diagnostics.Report(new("MRT2157", DiagnosticSeverity.Error,
                $"Pattern nesting exceeds the supported limit of {MaximumNestingDepth}.", location));
            return Error(context.InputType, location);
        }
        if (context.InputType == TypeSymbol.Error)
            return new BoundWildcardPattern(TypeSymbol.Error, location, hasErrors: true);

        return syntax switch
        {
            WildcardPatternSyntax => new BoundWildcardPattern(context.InputType, location),
            LiteralPatternSyntax literal => BindLiteral(literal, context.InputType, context.SourceText, diagnostics),
            ValueBindingPatternSyntax binding => BindValue(binding, context, diagnostics),
            NilPatternSyntax => BindNil(context.InputType, location, diagnostics),
            OptionalSomePatternSyntax some => BindSome(some, context, diagnostics, cancellationToken),
            EnumCasePatternSyntax enumCase => BindEnum(enumCase, context, diagnostics, cancellationToken),
            IdentifierPatternSyntax identifier => BindIdentifier(identifier, context, diagnostics, cancellationToken),
            _ => Error(context.InputType, location)
        };
    }

    private BoundPattern BindLiteral(LiteralPatternSyntax syntax, TypeSymbol inputType, SourceText text, DiagnosticBag diagnostics)
    {
        var expected = syntax.LiteralToken.Kind switch
        {
            SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword => TypeSymbol.Bool,
            SyntaxKind.IntegerLiteralToken => TypeSymbol.Int,
            SyntaxKind.FloatingPointLiteralToken => TypeSymbol.Double,
            SyntaxKind.StringLiteralToken => TypeSymbol.String,
            _ => TypeSymbol.Error
        };
        var location = syntax.LiteralToken.Location(text);
        var valid = inputType == expected;
        if (!valid)
            diagnostics.Report(new("MRT2201", DiagnosticSeverity.Error,
                $"Literal pattern of type '{expected.Name}' cannot match '{inputType.Name}'.", location));
        return new BoundLiteralPattern(syntax.LiteralToken.Value, inputType, location, !valid);
    }

    private BoundPattern BindValue(ValueBindingPatternSyntax syntax, PatternBindingContext context, DiagnosticBag diagnostics)
    {
        var location = syntax.Identifier.Location(context.SourceText);
        var patternLocation = new TextLocation(context.SourceText, syntax.Span);
        var variable = new LocalVariableSymbol(syntax.Identifier.Text, true, context.InputType, [location]);
        var duplicate = syntax.Identifier.IsMissing || !_bindings.Add(variable.Name);
        if (duplicate)
            diagnostics.Report(new("MRT2202", DiagnosticSeverity.Error,
                $"Pattern binding '{variable.Name}' is already declared in this pattern.", location));
        else
        {
            context.CaseScope.TryDeclareVariable(variable);
            if (_declaredSymbols is not null) _declaredSymbols[syntax] = variable;
            if (_symbolInfo is not null) _symbolInfo[syntax.Identifier] = variable;
        }
        return new BoundValueBindingPattern(variable, context.InputType, patternLocation, duplicate);
    }

    private static BoundPattern BindNil(TypeSymbol inputType, TextLocation location, DiagnosticBag diagnostics)
    {
        if (inputType is OptionalTypeSymbol optional) return new BoundNilPattern(optional, location);
        diagnostics.Report(new("MRT2203", DiagnosticSeverity.Error,
            $"The 'nil' pattern requires an optional input, not '{inputType.Name}'.", location));
        return Error(inputType, location);
    }

    private BoundPattern BindSome(OptionalSomePatternSyntax syntax, PatternBindingContext context, DiagnosticBag diagnostics,
        CancellationToken cancellationToken)
    {
        var location = new TextLocation(context.SourceText, syntax.Span);
        if (context.InputType is not OptionalTypeSymbol optional)
        {
            diagnostics.Report(new("MRT2204", DiagnosticSeverity.Error,
                $"The '.some' pattern requires an optional input, not '{context.InputType.Name}'.", location));
            return Error(context.InputType, location);
        }
        var child = BindCore(syntax.ValuePattern, context with
        {
            InputType = optional.ElementType,
            ExpectedOptional = null,
            NestingDepth = context.NestingDepth + 1
        }, diagnostics, cancellationToken);
        return new BoundOptionalSomePattern(optional, child, location);
    }

    private BoundPattern BindIdentifier(IdentifierPatternSyntax syntax, PatternBindingContext context,
        DiagnosticBag diagnostics, CancellationToken cancellationToken)
    {
        // A bare identifier is an enum-case lookup, never an implicit variable declaration.
        var token = new SyntaxToken(SyntaxKind.IdentifierToken, syntax.Identifier.Position,
            syntax.Identifier.Text, syntax.Identifier.Value, syntax.Identifier.IsMissing);
        var result = BindEnum(new EnumCasePatternSyntax(null, null, token, null), context, diagnostics, cancellationToken);
        if (result is BoundEnumCasePattern enumCase && _symbolInfo is not null)
            _symbolInfo[syntax.Identifier] = enumCase.Case;
        return result;
    }

    private BoundPattern BindEnum(EnumCasePatternSyntax syntax, PatternBindingContext context, DiagnosticBag diagnostics,
        CancellationToken cancellationToken)
    {
        var location = new TextLocation(context.SourceText, syntax.Span);
        var enumDefinition = context.InputType switch
        {
            EnumTypeSymbol definition => definition,
            ConstructedTypeSymbol { GenericDefinition: EnumTypeSymbol definition } => definition,
            _ => null
        };
        if (enumDefinition is null)
        {
            diagnostics.Report(new("MRT2205", DiagnosticSeverity.Error,
                $"Enum case pattern '{syntax.CaseName.Text}' cannot match '{context.InputType.Name}'.", location));
            return Error(context.InputType, location);
        }
        if (syntax.Qualifier is not null && syntax.Qualifier.Text != enumDefinition.Name)
        {
            diagnostics.Report(new("MRT2206", DiagnosticSeverity.Error,
                $"Enum qualifier '{syntax.Qualifier.Text}' does not match input type '{context.InputType.Name}'.", syntax.Qualifier.Location(context.SourceText)));
            return Error(context.InputType, location);
        }
        var cases = context.InputType is ConstructedTypeSymbol constructed ? constructed.Cases : enumDefinition.Cases;
        var enumCase = cases.FirstOrDefault(candidate => candidate.Name == syntax.CaseName.Text);
        if (enumCase is null)
        {
            diagnostics.Report(new("MRT2145", DiagnosticSeverity.Error,
                $"Enum '{context.InputType.Name}' has no case named '{syntax.CaseName.Text}'.", syntax.CaseName.Location(context.SourceText)));
            return Error(context.InputType, location);
        }
        if (_symbolInfo is not null) _symbolInfo[syntax.CaseName] = enumCase;
        var arguments = syntax.Arguments?.Arguments ?? [];
        var countValid = arguments.Count == enumCase.AssociatedValues.Length;
        if (!countValid)
            diagnostics.Report(new("MRT2142", DiagnosticSeverity.Error,
                $"Enum case '{enumCase.Name}' expects {enumCase.AssociatedValues.Length} associated values, but {arguments.Count} were provided.", location));
        var children = ImmutableArray.CreateBuilder<BoundPattern>();
        for (var i = 0; i < Math.Min(arguments.Count, enumCase.AssociatedValues.Length); i++)
            children.Add(BindCore(arguments[i], context with
            {
                InputType = enumCase.AssociatedValues[i].Type,
                ExpectedEnum = null,
                NestingDepth = context.NestingDepth + 1
            }, diagnostics, cancellationToken));
        return new BoundEnumCasePattern(context.InputType, enumCase, children.ToImmutable(), location, !countValid);
    }

    private static BoundPattern Error(TypeSymbol inputType, TextLocation location) =>
        new BoundWildcardPattern(inputType, location, hasErrors: true);
}
