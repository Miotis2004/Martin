using Martin.Compiler.Syntax;
using Xunit;
namespace Martin.Parser.Tests;
public class Phase1ParserTests
{
 [Fact] public void HandlesPrecedenceAndAssignment(){var tree=SyntaxTree.Parse("a = b = 1 + 2 * 3");Assert.Empty(tree.Diagnostics);var display=SyntaxTreePrinter.ToDisplayString(tree.Root);Assert.Contains("AssignmentExpression",display);Assert.Contains("BinaryExpression",display);} 
 [Fact] public void ParsesStatementsAndDeclarations(){var tree=SyntaxTree.Parse("func main(){ var x: Int = 0; while x < 3 { x = x + 1 } if x == 3 { return } else { return x } }");Assert.Empty(tree.Diagnostics);} 
 [Fact] public void MissingTokensAreRepresented(){var tree=SyntaxTree.Parse("func main( { return }");Assert.NotEmpty(tree.Diagnostics);Assert.Contains("<missing>",SyntaxTreePrinter.ToDisplayString(tree.Root));}
 [Fact] public void InvalidInputStillProducesTree(){var tree=SyntaxTree.Parse("func { let = @ return (1 + )");Assert.NotNull(tree.Root);Assert.True(tree.Diagnostics.Length>=2);} 
}
