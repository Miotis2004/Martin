using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Parser.Tests;

public sealed class Phase13PatternSyntaxTests
{
    [Fact]
    public void EverySupportedPatternHasDedicatedSyntax()
    {
        var patterns = ParseCasePatterns("""
            case _:
            case value:
            case 42:
            case nil:
            case let payload:
            case .ready:
            case Result.success(let result):
            case .some(let wrapped):
            """);

        Assert.Collection(patterns,
            pattern => Assert.IsType<WildcardPatternSyntax>(pattern),
            pattern => Assert.IsType<IdentifierPatternSyntax>(pattern),
            pattern => Assert.IsType<LiteralPatternSyntax>(pattern),
            pattern => Assert.IsType<NilPatternSyntax>(pattern),
            pattern => Assert.IsType<ValueBindingPatternSyntax>(pattern),
            pattern => Assert.IsType<EnumCasePatternSyntax>(pattern),
            pattern => Assert.IsType<EnumCasePatternSyntax>(pattern),
            pattern => Assert.IsType<OptionalSomePatternSyntax>(pattern));
    }

    [Fact]
    public void NestedArgumentsPreserveOrderTriviaSpansAndParents()
    {
        var source = "switch value { case Result.failure( let code, .some(let message) ): }";
        var tree = SyntaxTree.Parse(source);
        var pattern = Assert.IsType<EnumCasePatternSyntax>(Assert.Single(ParseCasePatterns(tree)));
        var arguments = Assert.IsType<PatternArgumentListSyntax>(pattern.Arguments);

        Assert.Equal(2, arguments.Arguments.Count);
        Assert.Same(pattern, arguments.Parent);
        Assert.All(arguments.GetChildren(), child => Assert.Same(arguments, child.Parent));
        Assert.Equal("Result.failure( let code, .some(let message) )", pattern.ToFullString());
        Assert.Equal(source.IndexOf("Result", StringComparison.Ordinal), pattern.Span.Start);
        Assert.Equal(pattern.ToFullString().Length, pattern.FullSpan.Length);
    }

    [Fact]
    public void MissingPatternTokensRemainVisibleInTree()
    {
        var tree = SyntaxTree.Parse("switch value { case .: } ");
        var pattern = Assert.IsType<EnumCasePatternSyntax>(Assert.Single(ParseCasePatterns(tree)));

        Assert.True(pattern.CaseName.IsMissing);
        Assert.Contains("IdentifierToken <missing>", SyntaxTreePrinter.ToDisplayString(pattern));
    }

    [Fact]
    public void VisitorsAndRewritersTraverseNestedPatterns()
    {
        var pattern = Assert.Single(ParseCasePatterns("case .some(.success(let value)):"));
        var visitor = new PatternCountingVisitor();

        visitor.Visit(pattern);
        var rewritten = Assert.IsType<OptionalSomePatternSyntax>(new SyntaxRewriter().Visit(pattern));

        Assert.Equal(3, visitor.Count);
        Assert.NotSame(pattern, rewritten);
        Assert.Equal(pattern.ToFullString(), rewritten.ToFullString());
    }

    private static IReadOnlyList<PatternSyntax> ParseCasePatterns(string cases) =>
        ParseCasePatterns(SyntaxTree.Parse($"switch value {{ {cases} }}"));

    private static IReadOnlyList<PatternSyntax> ParseCasePatterns(SyntaxTree tree) =>
        tree.Root.GetChildren().SelectMany(DescendantsAndSelf).OfType<PatternSyntax>()
            .Where(pattern => pattern.Parent?.Kind == SyntaxKind.SwitchCase).ToArray();

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }

    private sealed class PatternCountingVisitor : SyntaxVisitor
    {
        public int Count { get; private set; }
        public override void DefaultVisit(SyntaxNode node)
        {
            if (node is PatternSyntax) Count++;
            base.DefaultVisit(node);
        }
    }
}
