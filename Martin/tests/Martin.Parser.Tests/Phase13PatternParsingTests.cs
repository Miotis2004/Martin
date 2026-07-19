using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Parser.Tests;

public sealed class Phase13PatternParsingTests
{
    [Fact]
    public void ParsesNestedQualifiedBareAndOptionalPatterns()
    {
        var cases = Cases("""
            switch value {
              case Result.success(.some(let payload)):
              case failure(let reason):
              case ready:
            }
            """);

        var qualified = Assert.IsType<EnumCasePatternSyntax>(Pattern(cases[0]));
        Assert.Equal("Result", qualified.Qualifier?.Text);
        var some = Assert.IsType<OptionalSomePatternSyntax>(Assert.Single(qualified.Arguments!.Arguments));
        Assert.IsType<ValueBindingPatternSyntax>(some.ValuePattern);

        var bareAssociated = Assert.IsType<EnumCasePatternSyntax>(Pattern(cases[1]));
        Assert.Null(bareAssociated.Qualifier);
        Assert.Null(bareAssociated.DotToken);
        Assert.Equal("failure", bareAssociated.CaseName.Text);
        Assert.IsType<ValueBindingPatternSyntax>(Assert.Single(bareAssociated.Arguments!.Arguments));

        Assert.IsType<IdentifierPatternSyntax>(Pattern(cases[2]));
    }

    [Fact]
    public void MissingPatternColonAndClosingDelimiterPreserveLaterCases()
    {
        var tree = SyntaxTree.Parse("""
            switch value {
              case :
              case .success(let payload:
              case .failure:
              default:
            }
            """);
        var cases = Cases(tree);

        Assert.Equal(4, cases.Count);
        Assert.True(Assert.IsType<IdentifierPatternSyntax>(Pattern(cases[0])).Identifier.IsMissing);
        var malformed = Assert.IsType<EnumCasePatternSyntax>(Pattern(cases[1]));
        Assert.True(malformed.Arguments!.CloseParenToken.IsMissing);
        Assert.Equal("failure", Assert.IsType<EnumCasePatternSyntax>(Pattern(cases[2])).CaseName.Text);
        Assert.DoesNotContain(cases[3].GetChildren(), child => child is PatternSyntax);
        Assert.NotEmpty(tree.Diagnostics);
    }

    [Fact]
    public void MissingCaseNameAndSwitchBracesAreRepresentedWithoutLosingCases()
    {
        var tree = SyntaxTree.Parse("switch value case .: case .ready:");
        var statement = Assert.IsType<GenericStatementSyntax>(Assert.IsType<GlobalStatementSyntax>(tree.Root.Members[0]).Statement);
        var children = statement.GetChildren().ToArray();
        var cases = children.OfType<GenericSyntaxNode>().Where(node => node.Kind == SyntaxKind.SwitchCase).ToArray();

        Assert.True(children.OfType<SyntaxToken>().Single(token => token.Kind == SyntaxKind.OpenBraceToken).IsMissing);
        Assert.True(children.OfType<SyntaxToken>().Single(token => token.Kind == SyntaxKind.CloseBraceToken).IsMissing);
        Assert.Equal(2, cases.Length);
        Assert.True(Assert.IsType<EnumCasePatternSyntax>(Pattern(cases[0])).CaseName.IsMissing);
        Assert.Equal("ready", Assert.IsType<EnumCasePatternSyntax>(Pattern(cases[1])).CaseName.Text);
    }

    private static PatternSyntax Pattern(GenericSyntaxNode switchCase) =>
        Assert.Single(switchCase.GetChildren().OfType<PatternSyntax>());

    private static IReadOnlyList<GenericSyntaxNode> Cases(string source) => Cases(SyntaxTree.Parse(source));

    private static IReadOnlyList<GenericSyntaxNode> Cases(SyntaxTree tree) =>
        tree.Root.GetChildren().SelectMany(DescendantsAndSelf).OfType<GenericSyntaxNode>()
            .Where(node => node.Kind == SyntaxKind.SwitchCase).ToArray();

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in DescendantsAndSelf(child))
                yield return descendant;
    }
}
