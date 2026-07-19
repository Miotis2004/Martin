using System.Collections.Immutable;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.Compiler.Binding;

/// <summary>A backend-neutral decision program for one bound switch.</summary>
public sealed record PatternDecisionGraph(
    BoundExpression InputExpression,
    CompilerGeneratedLocalVariableSymbol InputTemporary,
    PatternDecisionNode Entry,
    ImmutableArray<PatternCaseTarget> CaseTargets,
    FailureDecision FailureContinuation)
{
    /// <summary>Checks structural invariants required by subsequent lowering passes.</summary>
    public ImmutableArray<string> Validate(CancellationToken cancellationToken = default) =>
        PatternDecisionGraphValidator.Validate(this, cancellationToken);
}

public sealed record PatternCaseTarget(int CaseIndex, string Label, BoundSwitchCase Case, TextLocation Location);

/// <summary>Base class for decision nodes. Labels are stable within a graph.</summary>
public abstract record PatternDecisionNode(string Label, TextLocation Location)
{
    public abstract ImmutableArray<PatternDecisionNode> Successors { get; }
}

public sealed record TestEnumCaseDecision(string Label, TextLocation Location,
    CompilerGeneratedLocalVariableSymbol Input, EnumCaseSymbol Case,
    PatternDecisionNode WhenMatched, PatternDecisionNode WhenNotMatched)
    : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [WhenMatched, WhenNotMatched];
}

public sealed record TestLiteralDecision(string Label, TextLocation Location,
    CompilerGeneratedLocalVariableSymbol Input, object? Value,
    PatternDecisionNode WhenMatched, PatternDecisionNode WhenNotMatched)
    : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [WhenMatched, WhenNotMatched];
}

public sealed record TestOptionalHasValueDecision(string Label, TextLocation Location,
    CompilerGeneratedLocalVariableSymbol Input, PatternDecisionNode WhenHasValue,
    PatternDecisionNode WhenNil)
    : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [WhenHasValue, WhenNil];
}

public sealed record ExtractEnumPayloadDecision(string Label, TextLocation Location,
    CompilerGeneratedLocalVariableSymbol Input, EnumCaseSymbol Case, int PayloadIndex,
    CompilerGeneratedLocalVariableSymbol Destination, PatternDecisionNode Next)
    : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [Next];
}

public sealed record ExtractOptionalValueDecision(string Label, TextLocation Location,
    CompilerGeneratedLocalVariableSymbol Input, CompilerGeneratedLocalVariableSymbol Destination,
    PatternDecisionNode Next)
    : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [Next];
}

public sealed record BindPatternValueDecision(string Label, TextLocation Location,
    CompilerGeneratedLocalVariableSymbol Value, LocalVariableSymbol Variable, PatternDecisionNode Next)
    : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [Next];
}

public sealed record GotoCaseDecision(string Label, TextLocation Location, PatternCaseTarget Target)
    : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [];
}

public sealed record FailureDecision(string Label, TextLocation Location) : PatternDecisionNode(Label, Location)
{
    public override ImmutableArray<PatternDecisionNode> Successors => [];
}

/// <summary>Builds ordered decision paths. Only constructor tests are shared, and only inside one pattern path.</summary>
public sealed class PatternDecisionGraphBuilder
{
    private int _label;
    private int _temporary;

    public PatternDecisionGraph Build(BoundExpression input, ImmutableArray<BoundSwitchCase> cases,
        TextLocation location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (cases.IsDefault) cases = [];
        if (input.Type == TypeSymbol.Error || cases.Any(@case => @case.Pattern.HasErrors))
            throw new ArgumentException("A decision graph can only be built for a valid switch.", nameof(cases));

        _label = 0;
        _temporary = 0;
        var inputTemporary = Temporary(input.Type, location, "input");
        var targets = cases.Select((@case, index) =>
            new PatternCaseTarget(index, $"case_{index}", @case, @case.Location)).ToImmutableArray();
        var failure = new FailureDecision(Label("failure"), location);
        PatternDecisionNode entry = failure;

        // Building backwards makes every failed pattern continue at the next source case.
        for (var i = cases.Length - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = new GotoCaseDecision(Label("goto"), cases[i].Location, targets[i]);
            entry = BuildPattern(cases[i].Pattern, inputTemporary, success, entry, cancellationToken);
        }

        return new(input, inputTemporary, entry, targets, failure);
    }

    public PatternDecisionGraph BuildCatch(CompilerGeneratedLocalVariableSymbol errorValueInput,
        ImmutableArray<BoundCatchClause> clauses, TextLocation location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(errorValueInput);
        if (clauses.IsDefault) clauses = [];

        var cases = clauses.Select(clause => new BoundSwitchCase(clause.Pattern, clause.Body, clause.Location))
            .ToImmutableArray();
        if (errorValueInput.Type == TypeSymbol.Error || cases.Any(@case => @case.Pattern.HasErrors))
            throw new ArgumentException("A catch decision graph can only be built for valid catch clauses.", nameof(clauses));

        _label = 0;
        _temporary = 0;
        var targets = cases.Select((@case, index) =>
            new PatternCaseTarget(index, $"case_{index}", @case, @case.Location)).ToImmutableArray();
        var failure = new FailureDecision(Label("failure"), location);
        PatternDecisionNode entry = failure;

        // Catch clauses are source ordered: a failed pattern continues at the next catch.
        for (var i = cases.Length - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = new GotoCaseDecision(Label("goto"), cases[i].Location, targets[i]);
            entry = BuildPattern(cases[i].Pattern, errorValueInput, success, entry, cancellationToken);
        }

        return new(new BoundVariableExpression(errorValueInput), errorValueInput, entry, targets, failure);
    }

    private PatternDecisionNode BuildPattern(BoundPattern pattern, CompilerGeneratedLocalVariableSymbol input,
        PatternDecisionNode success, PatternDecisionNode failure, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return pattern switch
        {
            BoundWildcardPattern => success,
            BoundValueBindingPattern binding => new BindPatternValueDecision(Label("bind"), pattern.Location,
                input, binding.Variable, success),
            BoundLiteralPattern literal => new TestLiteralDecision(Label("literal"), pattern.Location,
                input, literal.Value, success, failure),
            BoundNilPattern => new TestOptionalHasValueDecision(Label("optional"), pattern.Location,
                input, failure, success),
            BoundOptionalSomePattern some => BuildSome(some, input, success, failure, cancellationToken),
            BoundEnumCasePattern enumCase => BuildEnum(enumCase, input, success, failure, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
    }

    private PatternDecisionNode BuildSome(BoundOptionalSomePattern pattern,
        CompilerGeneratedLocalVariableSymbol input, PatternDecisionNode success,
        PatternDecisionNode failure, CancellationToken cancellationToken)
    {
        var value = Temporary(pattern.OptionalType.ElementType, pattern.Location, "optional");
        var child = BuildPattern(pattern.ValuePattern, value, success, failure, cancellationToken);
        var extract = new ExtractOptionalValueDecision(Label("extract_optional"), pattern.Location, input, value, child);
        return new TestOptionalHasValueDecision(Label("optional"), pattern.Location, input, extract, failure);
    }

    private PatternDecisionNode BuildEnum(BoundEnumCasePattern pattern,
        CompilerGeneratedLocalVariableSymbol input, PatternDecisionNode success,
        PatternDecisionNode failure, CancellationToken cancellationToken)
    {
        var matched = success;
        for (var i = pattern.AssociatedPatterns.Length - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = Temporary(pattern.Case.AssociatedValues[i].Type, pattern.AssociatedPatterns[i].Location, "payload");
            var child = BuildPattern(pattern.AssociatedPatterns[i], payload, matched, failure, cancellationToken);
            matched = new ExtractEnumPayloadDecision(Label("extract_enum"), pattern.AssociatedPatterns[i].Location,
                input, pattern.Case, i, payload, child);
        }
        return new TestEnumCaseDecision(Label("enum"), pattern.Location, input, pattern.Case, matched, failure);
    }

    private CompilerGeneratedLocalVariableSymbol Temporary(TypeSymbol type, TextLocation location, string role) =>
        new($"$pattern_{role}_{_temporary++}", type, location);

    private string Label(string role) => $"pattern_{role}_{_label++}";
}

internal static class PatternDecisionGraphValidator
{
    public static ImmutableArray<string> Validate(PatternDecisionGraph graph, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var errors = ImmutableArray.CreateBuilder<string>();
        var labels = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<PatternDecisionNode>(ReferenceEqualityComparer.Instance);
        var active = new HashSet<PatternDecisionNode>(ReferenceEqualityComparer.Instance);

        void Visit(PatternDecisionNode node)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (active.Contains(node)) { errors.Add($"Decision graph contains a cycle at '{node.Label}'."); return; }
            if (!visited.Add(node)) return;
            if (!labels.Add(node.Label)) errors.Add($"Decision label '{node.Label}' is not unique.");
            active.Add(node);
            foreach (var next in node.Successors) Visit(next);
            active.Remove(node);
        }

        Visit(graph.Entry);
        Visit(graph.FailureContinuation);
        foreach (var target in graph.CaseTargets)
            if (!visited.Any(node => node is GotoCaseDecision branch && ReferenceEquals(branch.Target, target)))
                errors.Add($"Case target '{target.Label}' is unreachable from the graph entry.");
        return errors.ToImmutable();
    }
}
