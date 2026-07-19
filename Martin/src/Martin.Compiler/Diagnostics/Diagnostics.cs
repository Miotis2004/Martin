using System.Collections.Immutable;
using Martin.Compiler.Text;
using Martin.Compiler.Syntax;
namespace Martin.Compiler.Diagnostics;
public enum DiagnosticSeverity{Info,Warning,Error}
public sealed record Diagnostic(string Code,DiagnosticSeverity Severity,string Message,TextLocation Location);
public sealed class DiagnosticBag:List<Diagnostic>
{
 public ImmutableArray<Diagnostic> ToImmutableArray()=>this.ToImmutableArray<Diagnostic>();
 public void Report(Diagnostic diagnostic)=>Add(diagnostic);
 public void ReportInvalidCharacter(TextLocation l,char c)=>Add(new("MRT1001",DiagnosticSeverity.Error,$"Invalid character '{c}'.",l));
 public void ReportUnterminatedString(TextLocation l)=>Add(new("MRT1002",DiagnosticSeverity.Error,"Unterminated string literal.",l));
 public void ReportInvalidEscape(TextLocation l,char c)=>Add(new("MRT1003",DiagnosticSeverity.Error,$"Invalid escape sequence '\\{c}'.",l));
 public void ReportInvalidNumber(TextLocation l)=>Add(new("MRT1004",DiagnosticSeverity.Error,"Invalid numeric literal.",l));
 public void ReportUnterminatedComment(TextLocation l)=>Add(new("MRT1005",DiagnosticSeverity.Error,"Unterminated multi-line comment.",l));
 public void ReportUnexpectedToken(TextLocation l,SyntaxKind a,SyntaxKind e)=>Add(new("MRT1101",DiagnosticSeverity.Error,$"Unexpected token '{a}'; expected '{e}'.",l));
 public void ReportExpectedExpression(TextLocation l)=>Add(new("MRT1102",DiagnosticSeverity.Error,"Expected expression.",l));
 public void ReportInvalidAssignmentTarget(TextLocation l)=>Add(new("MRT1107",DiagnosticSeverity.Error,"Invalid assignment target.",l));
}
