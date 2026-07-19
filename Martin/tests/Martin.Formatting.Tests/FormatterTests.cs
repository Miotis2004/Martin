using Martin.LanguageServices;
using Xunit;

namespace Martin.Formatting.Tests;

public sealed class FormatterTests
{
    static readonly MartinLanguageService Service = new();
    static string Format(string source) => new MartinLanguageService().FormatDocument(source);
    [Fact] public void FormattingIsIdempotent(){var once=Format("func main(){let value=1 print(value)}");var twice=Format(once);Assert.Equal(once,twice);}    
    [Fact] public void IndentsNestedBlocks(){var formatted=Format("func main(){if true{print(1)}}");Assert.Contains("    if",formatted);Assert.Contains("        print",formatted);}    
    [Fact] public void KeepsBracePlacementStable(){var formatted=Format("func main(){print(1)}");Assert.Contains("{",formatted);Assert.Contains("}",formatted);}    
    [Fact] public void AddsSpacesAroundStatements(){var formatted=Format("func main(){let value=1}");Assert.Contains("let value",formatted);}    
    [Fact] public void FormatsParameterSeparators(){var formatted=Format("func add(_ left:Int,_ right:Int)->Int{return left+right}");Assert.Contains(", ",formatted);}    
    [Fact] public void FormatsEmptyBlockWithFinalNewline(){var formatted=Format("func main(){}");Assert.EndsWith(Environment.NewLine,formatted);Assert.Contains("}",formatted);}    
    [Fact] public void PreservesStringContent(){var formatted=Format("func main(){print(\"a b c\")}");Assert.Contains("\"a b c\"",formatted);}    
    [Fact] public void PreservesComments(){var formatted=Format("// hello\nfunc main(){}");Assert.Contains("// hello",formatted);}    
    [Fact] public void MalformedSourceDoesNotCrash(){var ex=Record.Exception(()=>Format("func main( {"));Assert.Null(ex);}    

    [Fact]
    public void ReturnsBoundedApplicableSortedEdits()
    {
        const string source = "func main(){let value=1}";
        var result = Service.GetFormattingEdits(source);
        Assert.False(result.WasRefused);
        Assert.NotEmpty(result.Edits);
        Assert.Equal(Format(source), TextEditApplicator.Apply(source, result.Edits));
        Assert.True(result.Edits.Zip(result.Edits.Skip(1), (left, right) => left.Span.End <= right.Span.Start).All(value => value));
        Assert.DoesNotContain(result.Edits, edit => edit.Span.Start == 0 && edit.Span.Length == source.Length);
    }

    [Fact]
    public void PreservesCrLfDocumentationCommentsAndEscapedStrings()
    {
        const string source = "/// docs\r\nfunc main(){print(\"a\\n\\\"b\")}\r\n";
        var formatted = Format(source);
        Assert.Contains("/// docs", formatted);
        Assert.Contains("\"a\\n\\\"b\"", formatted);
        Assert.DoesNotContain("\n", formatted.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void UnsafeMalformedSourceIsRefusedWithoutEdits()
    {
        const string source = "func main(){ @ lost }";
        var result = Service.GetFormattingEdits(source);
        Assert.True(result.WasRefused);
        Assert.Equal("MRT6451", result.DiagnosticCode);
        Assert.Empty(result.Edits);
        Assert.Equal(source, Format(source));
    }

    [Fact]
    public void SupportsTabsAndNoFinalNewline()
    {
        const string source = "func main(){if true{print(1)}}";
        var result = Service.GetFormattingEdits(source, new FormattingOptions { UseTabs = true, InsertFinalNewLine = false });
        var formatted = TextEditApplicator.Apply(source, result.Edits);
        Assert.Contains("\n\tif", formatted);
        Assert.False(formatted.EndsWith('\n'));
    }

    [Fact]
    public void FormatsPhase13PatternsAndSwitchCasesIdempotently()
    {
        const string source = "enum Result{case ok(value:Int) case empty}func inspect(_ result:Result?){switch result{case .some(let value):print(value) case nil:print(0)}}";
        var formatted = Format(source);
        Assert.Contains("case ok(value: Int)\n", formatted);
        Assert.Contains("case .some(let value):\n            print(value)", formatted);
        Assert.Contains("case nil:\n            print(0)", formatted);
        Assert.Equal(formatted, Format(formatted));
    }

    [Fact]
    public void FormatsPhase13ProtocolsAndConformancesIdempotently()
    {
        const string source = "protocol Resettable{var name:String mutating func reset() throws Failure}struct Item:Resettable{var name:String mutating func reset() throws Failure{print(name)}}";
        var formatted = Format(source);
        Assert.Contains("protocol Resettable {\n    var name: String\n    mutating func reset() throws Failure\n}", formatted);
        Assert.Contains("struct Item: Resettable", formatted);
        Assert.Equal(formatted, Format(formatted));
    }

    [Fact]
    public void FormatsGenericDeclarationsConstraintsAndCallsIdempotently()
    {
        const string source = "protocol Printable{func text()->String}struct Box<T:Printable>{let value:T func map<U>(_ other:U)->Box<U>{return Box<U>(value:other)}}func identity<T>(_ value:T)->T{return value}func main(){let box=Box<Int>(value:identity<Int>(42))}";
        var formatted = Format(source);
        Assert.Contains("struct Box<T: Printable> {", formatted);
        Assert.Contains("func map<U>(_ other: U) -> Box<U> {", formatted);
        Assert.Contains("Box<U>(value: other)", formatted);
        Assert.Contains("identity<Int>(42)", formatted);
        Assert.DoesNotContain("Box <", formatted);
        Assert.Equal(formatted, Format(formatted));
    }

    [Fact]
    public void FormatsNestedOptionalGenericTypesWithoutChangingComparisons()
    {
        const string source = "struct Pair<A,B>{let first:A let second:B}func choose<T>(_ value:Pair<T,Pair<String,T?>>)->T?{if 1<2{return value.first}return nil}";
        var formatted = Format(source);
        Assert.Contains("Pair<T, Pair<String, T?>>", formatted);
        Assert.Contains("if 1 < 2", formatted);
        Assert.Equal(formatted, Format(formatted));
    }
    [Fact]
    public void FormatsTypedErrorSyntaxIdempotently()
    {
        const string source = "enum FileError:Error{case notFound(path:String) case denied}func read(_ path:String)throws FileError{return try open(path)}func main(){do{let text=try read(\"a\") print(text)}catch .notFound(let path){print(path)}catch .denied{print(0)}}";
        var formatted = Format(source);
        Assert.Contains("func read(_ path: String) throws FileError {", formatted);
        Assert.Contains("return try open(path)", formatted);
        Assert.Contains("catch .notFound(let path) {", formatted);
        Assert.Contains("catch .denied {", formatted);
        Assert.Equal(formatted, Format(formatted));
    }

    [Fact]
    public void PreservesTypedErrorCommentsAndTrivia()
    {
        const string source = "func read() throws/* preserved */FileError{do{/* body */throw FileError.denied}catch/* c */.denied{/* handled */print(0)}}";
        var formatted = Format(source);
        Assert.Contains("throws /* preserved */ FileError", formatted);
        Assert.Contains("/* body */", formatted);
        Assert.Contains("catch /* c */ .denied", formatted);
        Assert.Contains("/* handled */", formatted);
        Assert.Equal(formatted, Format(formatted));
    }

}
