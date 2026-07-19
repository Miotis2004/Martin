using Martin.Compiler.Syntax;
using Xunit;
namespace Martin.Lexer.Tests;

public class Phase1LexerTests
{
    [Fact] public void RoundTripsTriviaAndComments() { var text = "/// doc\nfunc main() { /* c */ let answer = 42\n}"; var tree = SyntaxTree.Parse(text); Assert.Equal(text, tree.Root.ToFullString()); Assert.Empty(tree.Diagnostics); }
    [Theory][InlineData("@", "MRT1001")][InlineData("\"unterminated", "MRT1002")][InlineData("\"\\q\"", "MRT1003")][InlineData("/* nope", "MRT1005")] public void ReportsLexicalDiagnostics(string text, string code) { var tree = SyntaxTree.Parse(text); Assert.Contains(tree.Diagnostics, d => d.Code == code); }
    [Fact] public void ParsesAllLineEndings() { var source = Martin.Compiler.Text.SourceText.From("a\nb\rc\r\nd"); Assert.Equal(4, source.Lines.Count); Assert.Equal(2, source.GetLinePosition(2).Line); }
    [Fact] public void RepresentativeProgramHasNoDiagnostics() { var text = """
func add(_ left: Int, _ right: Int) -> Int {
    return left + right
}

func main() {
    let answer = add(20, 22)

    if answer == 42 {
        print("Martin works!")
    }
}
"""; Assert.Empty(SyntaxTree.Parse(text).Diagnostics); }
}
