using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

public sealed class CSharpEmitter
{
    readonly CSharpNameMangler _names = new(); GeneratedSourceWriter _w = null!; LoweredProgram _program = null!; CodeGenerationRequest _request = null!; int _catchTemporary;
    public CodeGenerationResult Emit(CodeGenerationRequest request)
    {
        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        if (request.Program.Diagnostics.Any(d=>d.Severity==DiagnosticSeverity.Error)){diags.Add(new("MRT3012",DiagnosticSeverity.Error,"Internal code-generation failure: semantic diagnostics are present.", request.Program.Diagnostics[0].Location)); return new(){Diagnostics=diags.ToImmutable()};}
        if (request.OutputKind != OutputKind.ConsoleApplication){diags.Add(NoLoc("MRT3014",$"Unsupported output kind '{request.OutputKind}'.")); return new(){Diagnostics=diags.ToImmutable()};}
        var entry=request.Program.EntryPoint;
        if(entry is null){diags.Add(NoLoc("MRT3001","Executable target requires a valid main function.")); return new(){Diagnostics=diags.ToImmutable()};}
        try{_request=request; _program=request.Program; _w=new(); _catchTemporary=0; WriteProgram(entry); return new(){Success=true,GeneratedSource=_w.ToString(),Diagnostics=[],SourceMap=_w.SourceMap};} catch(Exception ex){return new(){Diagnostics=[NoLoc("MRT3012",$"Internal code-generation failure: {ex.Message}")]};}
    }
    static Diagnostic NoLoc(string code,string msg)=>new(code,DiagnosticSeverity.Error,msg,new TextLocation(SourceText.From(""),new TextSpan(0,0)));
    void WriteProgram(FunctionSymbol entry){_w.WriteLine("#nullable enable");if(_request.IncludeSourceDirectives)_w.WriteLine("#line hidden");_w.WriteLine();_w.WriteLine("namespace Martin.Generated;");_w.WriteLine();foreach(var t in DeterministicOrder.NamedTypes(_program.NamedTypes)) WriteNamedType(t);_w.WriteLine("internal static class MartinProgram");_w.WriteLine("{");using(_w.Indent()){foreach(var f in _program.LoweredFunctions) WriteFunction(f); WriteRuntimeBoundaryHelpers();}_w.WriteLine("}");_w.WriteLine();_w.WriteLine("internal static class Program");_w.WriteLine("{");using(_w.Indent()){_w.WriteLine("public static int Main(string[] args)");_w.WriteLine("{");using(_w.Indent()){_w.WriteLine("Martin.Runtime.MartinConsole.SetArguments(args);");_w.WriteLine($"MartinProgram.{_names.GetName(entry)}();");_w.WriteLine("return 0;");}_w.WriteLine("}");}_w.WriteLine("}");}
    void WriteNamedType(NamedTypeSymbol t){if(t is ProtocolTypeSymbol protocol){WriteProtocol(protocol);return;}if(t.IsEnum){WriteEnum(t);return;}_w.WriteLine($"internal {(t.IsStruct?"struct":"sealed class")} {_names.GetName(t)}{TypeParameterList(t)}{InterfaceList(t)}{GenericConstraints(t.TypeParameters)}");_w.WriteLine("{");using(_w.Indent()){foreach(var property in DeterministicOrder.Properties(t.Properties))_w.WriteLine($"internal {(property.IsReadOnly?"readonly ":"")}{TypeName(property.Type)} {_names.GetName(property)};");foreach(var init in DeterministicOrder.Initializers(t.Initializers))WriteInitializer(init);foreach(var m in DeterministicOrder.Methods(t.Methods))WriteMethod(m);foreach(var conformance in ConformancesFor(t))WriteConformanceBridges(conformance);}_w.WriteLine("}");_w.WriteLine();}

    string TypeParameterList(NamedTypeSymbol t)=>TypeParameterList(t.TypeParameters);
    string TypeParameterList(FunctionSymbol f)=>TypeParameterList(f.TypeParameters);
    static string TypeParameterList(ImmutableArray<TypeParameterSymbol> parameters)=>parameters.Length==0?string.Empty:"<"+string.Join(", ",parameters.Select(tp=>CSharpNameMangler.GetIdentifier(tp.Name)))+">";
    string Parameters(ImmutableArray<ParameterSymbol> parameters)=>string.Join(", ",parameters.OrderBy(p=>p.Ordinal).Select(p=>$"{TypeName(p.Type)} {_names.GetName(p)}"));
    string GenericConstraints(ImmutableArray<TypeParameterSymbol> parameters)=>string.Concat(parameters.Select(parameter=>
    {
        var constraints=parameter.Constraints.OfType<ProtocolConstraint>().Where(constraint=>!ReferenceEquals(constraint.Protocol,BuiltIns.ErrorProtocol)).Select(constraint=>_names.GetName(constraint.Protocol)).ToArray();
        return constraints.Length==0?string.Empty:$" where {CSharpNameMangler.GetIdentifier(parameter.Name)} : {string.Join(", ",constraints)}";
    }));
    static bool IsEmittedConformance(ProtocolConformance conformance)=>!ReferenceEquals(conformance.Protocol,BuiltIns.ErrorProtocol);
    string InterfaceList(NamedTypeSymbol t){var ps=ConformancesFor(t).Select(c=>_names.GetName(c.Protocol)).ToArray();return ps.Length==0?string.Empty:" : "+string.Join(", ",ps);}
    IOrderedEnumerable<ProtocolConformance> ConformancesFor(NamedTypeSymbol type)=>DeterministicOrder.Conformances(_program.Conformances.Where(c=>ReferenceEquals(c.Type,type)&&IsEmittedConformance(c)));
    void WriteProtocol(ProtocolTypeSymbol p){_w.WriteLine($"internal interface {_names.GetName(p)}");_w.WriteLine("{");using(_w.Indent()){foreach(var r in DeterministicOrder.ProtocolRequirements(p.Requirements))WriteProtocolRequirement(r);}_w.WriteLine("}");_w.WriteLine();}
    void WriteProtocolRequirement(ProtocolRequirementSymbol requirement)
    {
        WriteSourceMapped(requirement.Locations.FirstOrDefault(), () =>
        {
            if(requirement is ProtocolPropertyRequirementSymbol property)
                _w.WriteLine($"{TypeName(property.Type)} {_names.GetName(property)} {{ get; {(property.RequiresSetter?"set; ":string.Empty)}}}");
            else if(requirement is ProtocolMethodRequirementSymbol method)
            {
                _w.WriteLine($"{TypeName(method.ReturnType)} {_names.GetName(method)}{TypeParameterList(method.TypeParameters)}({Parameters(method.Parameters)}){GenericConstraints(method.TypeParameters)};");
            }
        });
    }
    void WriteConformanceBridges(ProtocolConformance conformance)
    {
        foreach(var pair in DeterministicOrder.Witnesses(conformance.Witnesses))
        {
            if(pair.Key is ProtocolPropertyRequirementSymbol property && pair.Value is PropertySymbol propertyWitness)
                WritePropertyBridge(conformance.Protocol,property,propertyWitness);
            else if(pair.Key is ProtocolMethodRequirementSymbol method && pair.Value is MethodSymbol methodWitness)
                WriteMethodBridge(conformance.Protocol,method,methodWitness);
            else
                throw new CodeGenerationException($"Invalid witness for protocol requirement '{pair.Key.Name}'.");
        }
    }
    void WritePropertyBridge(ProtocolTypeSymbol protocol,ProtocolPropertyRequirementSymbol requirement,PropertySymbol witness)
    {
        WriteSourceMapped(witness.Locations.FirstOrDefault(),()=>
        {
            _w.WriteLine($"{TypeName(requirement.Type)} {_names.GetName(protocol)}.{_names.GetName(requirement)}");
            _w.WriteLine("{");
            using(_w.Indent())
            {
                if(requirement.RequiresGetter)_w.WriteLine($"get => this.{_names.GetName(witness)};");
                if(requirement.RequiresSetter)_w.WriteLine($"set => this.{_names.GetName(witness)} = value;");
            }
            _w.WriteLine("}");
        });
    }
    void WriteMethodBridge(ProtocolTypeSymbol protocol,ProtocolMethodRequirementSymbol requirement,MethodSymbol witness)
    {
        WriteSourceMapped(witness.Locations.FirstOrDefault(),()=>
        {
            var typeArguments=requirement.TypeParameters.Length==0?string.Empty:"<"+string.Join(", ",requirement.TypeParameters.Select(p=>p.Name))+">";
            var arguments=string.Join(", ",requirement.Parameters.OrderBy(p=>p.Ordinal).Select(p=>_names.GetName(p)));
            _w.WriteLine($"{TypeName(requirement.ReturnType)} {_names.GetName(protocol)}.{_names.GetName(requirement)}{TypeParameterList(requirement.TypeParameters)}({Parameters(requirement.Parameters)})");
            _w.WriteLine("{");
            using(_w.Indent())_w.WriteLine($"{(requirement.ReturnType==TypeSymbol.Void?string.Empty:"return ")}{_names.GetName(witness)}{typeArguments}({arguments});");
            _w.WriteLine("}");
        });
    }
    void WriteSourceMapped(TextLocation? location,Action write)
    {
        if(location is not { } mappedLocation){write();return;}
        if(_request.IncludeSourceDirectives)WriteLineDirective(mappedLocation);
        using(_w.MapTo(mappedLocation))write();
        if(_request.IncludeSourceDirectives)_w.WriteLine("#line hidden");
    }
    void WriteEnum(NamedTypeSymbol t) => new CSharpEnumEmitter(_w, _names, _request.IncludeSourceDirectives, GenericConstraints).Write(t);
    void WriteInitializer(InitializerSymbol init){_w.Write($"internal {_names.GetName(init.ContainingType)}(");_w.Write(Parameters(init.Parameters));_w.WriteLine(")");_w.WriteLine("{");using(_w.Indent()){if(init.IsSynthesized){foreach(var p in init.Parameters){var prop=((NamedTypeSymbol)init.ContainingType).Properties.First(x=>x.Name==p.Name);_w.WriteLine($"{_names.GetName(prop)} = {_names.GetName(p)};");}}else foreach(var s in _program.InitializerBodies[init].Statements)WriteStatement(s);}_w.WriteLine("}");}
    void WriteMethod(MethodSymbol m){_w.Write($"internal {TypeName(m.ReturnType)} {_names.GetName(m)}{TypeParameterList(m.TypeParameters)}(");_w.Write(Parameters(m.Parameters));_w.WriteLine($"){GenericConstraints(m.TypeParameters)}");_w.WriteLine("{");using(_w.Indent()){foreach(var s in _program.MethodBodies[m].Statements)WriteStatement(s);}_w.WriteLine("}");}
    string InterfaceMethodName(MethodSymbol m)=>_names.GetName(m);
    void WriteFunction(LoweredFunction f){_w.Write($"internal static {TypeName(f.Symbol.ReturnType)} {_names.GetName(f.Symbol)}{TypeParameterList(f.Symbol)}(");_w.Write(Parameters(f.Symbol.Parameters));_w.WriteLine($"){GenericConstraints(f.Symbol.TypeParameters)}");_w.WriteLine("{");using(_w.Indent()){foreach(var s in f.Body.Statements) WriteStatement(s);}_w.WriteLine("}");_w.WriteLine();}
    void WriteStatement(BoundStatement s){if(s is BoundBlockStatement){WriteMappedStatement(s);return;}if(s.Location is { } loc){if(_request.IncludeSourceDirectives)WriteLineDirective(loc);using(_w.MapTo(loc))WriteMappedStatement(s);if(_request.IncludeSourceDirectives)_w.WriteLine("#line hidden");return;}WriteMappedStatement(s);} void WriteMappedStatement(BoundStatement s){switch(s){case BoundBlockStatement b:_w.WriteLine("{");using(_w.Indent()){foreach(var x in b.Statements)WriteStatement(x);} _w.WriteLine("}");break; case BoundVariableDeclaration v:_w.WriteLine($"{TypeName(v.Variable.Type)} {_names.GetName(v.Variable)} = {Expr(v.Initializer)};");break; case BoundExpressionStatement e:_w.WriteLine($"{Expr(e.Expression)};");break; case BoundIfStatement i:_w.WriteLine($"if {Condition(i.Condition)}");WriteStatement(i.ThenStatement); if(i.ElseStatement!=null){_w.WriteLine("else");WriteStatement(i.ElseStatement);}break; case BoundLabelStatement l:_w.WriteLine($"{l.Label}: ;");break; case BoundGotoStatement g:_w.WriteLine($"goto {g.Label};");break; case BoundConditionalGotoStatement g:_w.WriteLine($"if {Condition(g.Condition)} goto {g.Label};");break; case BoundIfLetStatement il:throw new CodeGenerationException("IfLet should have been lowered"); case BoundSwitchStatement sw:WriteSwitch(sw);break; case BoundWhileStatement w:_w.WriteLine($"while ({Expr(w.Condition)})");WriteStatement(w.Body);break; case BoundReturnStatement r:_w.WriteLine(r.Expression==null?"return;":$"return {Expr(r.Expression)};");break; case BoundThrowStatement t:WriteThrow(t);break; case BoundDoCatchStatement dc:WriteDoCatch(dc);break; default:throw new CodeGenerationException($"Unsupported bound node '{s.Kind}'",s);}}

    void WriteThrow(BoundThrowStatement statement)
    {
        _w.WriteLine($"throw new Martin.Runtime.MartinThrownErrorValue({Expr(statement.Expression)}, typeof({TypeName(statement.ErrorType)}));");
    }

    void WriteDoCatch(BoundDoCatchStatement statement)
    {
        if (statement.DecisionGraph is not { } graph)
            throw new CodeGenerationException("Exhaustive catch lowering requires a pattern decision graph.", statement);

        var validationErrors = graph.Validate();
        if (!validationErrors.IsEmpty)
            throw new CodeGenerationException($"Invalid catch pattern decision graph: {string.Join("; ", validationErrors)}", statement);

        var errorName = $"__martin_error_{_catchTemporary++}";
        var endLabel = $"__martin_catch_{_catchTemporary++}_end";
        var prefix = $"__martin_catch_{_catchTemporary++}_";
        string NodeLabel(PatternDecisionNode node) => prefix + node.Label;
        string TargetLabel(PatternCaseTarget target) => prefix + target.Label;

        _w.WriteLine("try");
        WriteStatement(statement.Body);
        _w.WriteLine($"catch (Martin.Runtime.MartinThrownError {errorName})");
        _w.WriteLine("{");
        using (_w.Indent())
        {
            _w.WriteLine($"if ({errorName}.DeclaredErrorType != typeof({TypeName(statement.ErrorType)})) throw;");
            _w.WriteLine($"{TypeName(statement.ErrorType)} {_names.GetName(graph.InputTemporary)} = ({TypeName(statement.ErrorType)}){errorName}.ErrorValue;");

            var pending = new Stack<PatternDecisionNode>();
            var visited = new HashSet<PatternDecisionNode>(ReferenceEqualityComparer.Instance);
            pending.Push(graph.Entry);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!visited.Add(node)) continue;
                _w.WriteLine($"{NodeLabel(node)}: ;");
                WriteCatchDecisionNode(node, NodeLabel, TargetLabel);
                for (var i = node.Successors.Length - 1; i >= 0; i--)
                    pending.Push(node.Successors[i]);
            }

            foreach (var target in graph.CaseTargets)
            {
                if ((uint)target.CaseIndex >= (uint)statement.CatchClauses.Length)
                    throw new CodeGenerationException($"Catch decision graph target '{target.Label}' references missing clause {target.CaseIndex}.", statement);

                _w.WriteLine($"{TargetLabel(target)}: ;");
                var body = statement.CatchClauses[target.CaseIndex].Body;
                if (body is BoundBlockStatement block)
                {
                    foreach (var nested in block.Statements) WriteStatement(nested);
                }
                else
                {
                    WriteStatement(body);
                }
                _w.WriteLine($"goto {endLabel};");
            }
            _w.WriteLine($"{endLabel}: ;");
        }
        _w.WriteLine("}");
    }

    void WriteCatchDecisionNode(PatternDecisionNode node, Func<PatternDecisionNode, string> nodeLabel,
        Func<PatternCaseTarget, string> targetLabel)
    {
        switch (node)
        {
            case TestEnumCaseDecision test:
                Branch(new BoundEnumCaseTestExpression(new BoundVariableExpression(test.Input), test.Case), test.WhenMatched, test.WhenNotMatched);
                break;
            case TestLiteralDecision test:
                _w.WriteLine($"if (object.Equals({_names.GetName(test.Input)}, {CSharpLiteralWriter.WriteLiteral(new BoundLiteralExpression(test.Value, test.Input.Type))})) goto {nodeLabel(test.WhenMatched)};");
                _w.WriteLine($"goto {nodeLabel(test.WhenNotMatched)};");
                break;
            case TestOptionalHasValueDecision test:
                Branch(new BoundHasValueExpression(new BoundVariableExpression(test.Input)), test.WhenHasValue, test.WhenNil);
                break;
            case ExtractEnumPayloadDecision extract:
                _w.WriteLine($"{TypeName(extract.Destination.Type)} {_names.GetName(extract.Destination)} = {Expr(new BoundEnumPayloadAccessExpression(new BoundVariableExpression(extract.Input), extract.Case, extract.PayloadIndex))};");
                _w.WriteLine($"goto {nodeLabel(extract.Next)};");
                break;
            case ExtractOptionalValueDecision extract:
                _w.WriteLine($"{TypeName(extract.Destination.Type)} {_names.GetName(extract.Destination)} = {Expr(new BoundGetValueExpression(new BoundVariableExpression(extract.Input), extract.Destination.Type))};");
                _w.WriteLine($"goto {nodeLabel(extract.Next)};");
                break;
            case BindPatternValueDecision bind:
                _w.WriteLine($"{TypeName(bind.Variable.Type)} {_names.GetName(bind.Variable)} = {_names.GetName(bind.Value)};");
                _w.WriteLine($"goto {nodeLabel(bind.Next)};");
                break;
            case GotoCaseDecision branch:
                _w.WriteLine($"goto {targetLabel(branch.Target)};");
                break;
            case FailureDecision:
                _w.WriteLine("throw;");
                break;
            default:
                throw new CodeGenerationException($"Unsupported catch pattern decision node '{node.GetType().Name}'.");
        }

        void Branch(BoundExpression condition, PatternDecisionNode matched, PatternDecisionNode notMatched)
        {
            _w.WriteLine($"if {Condition(condition)} goto {nodeLabel(matched)};");
            _w.WriteLine($"goto {nodeLabel(notMatched)};");
        }
    }

    void WriteLineDirective(TextLocation location){var path=location.FilePath??string.Empty;path=path.Replace("\\","\\\\").Replace("\"","\\\"");_w.WriteLine($"#line {location.StartLinePosition.Line} \"{path}\"");}
    void WriteSwitch(BoundSwitchStatement sw){_w.WriteLine($"switch ({Expr(sw.Expression)})");_w.WriteLine("{");using(_w.Indent()){foreach(var c in sw.Cases){if(c.Case==null)_w.WriteLine("default:");else{if(c.PatternVariable is null)throw new CodeGenerationException("Lowered switch case is missing its pattern variable.");_w.WriteLine($"case {TypeName(c.Case.ContainingType)}.{_names.GetName(c.Case)} {_names.GetName(c.PatternVariable)}:");}using(_w.Indent()){if(c.Body is BoundBlockStatement cb){foreach(var x in cb.Statements)WriteStatement(x);}else WriteStatement(c.Body);_w.WriteLine("break;");}}}_w.WriteLine("}");}
    string Condition(BoundExpression e) => e is BoundBinaryExpression b ? $"({Bin(b)})" : $"({Expr(e)})";
    string Expr(BoundExpression e)=>e switch{BoundRuntimeCallExpression rc=>$"{rc.MethodName}({string.Join(", ", rc.Arguments.Select(Expr))})", BoundHasValueExpression hv=>$"{Expr(hv.Receiver)}.HasValue", BoundGetValueExpression gv=>$"{Expr(gv.Receiver)}.Value", BoundEnumCaseTestExpression et=>$"{Expr(et.Receiver)} is {TypeName(et.Case.ContainingType)}.{_names.GetName(et.Case)}", BoundEnumPayloadAccessExpression ep=>$"(({TypeName(ep.Case.ContainingType)}.{_names.GetName(ep.Case)}){Expr(ep.Receiver)}).{EnumPayloadMember(ep.Case, ep.PayloadIndex)}", BoundLiteralExpression l=>Lit(l),BoundVariableExpression v=>v.Variable is SelfParameterSymbol?"this":_names.GetName(v.Variable),BoundAssignmentExpression a=>$"({_names.GetName(a.Variable)} = {Expr(a.Expression)})",BoundMemberAccessExpression m=>$"{Expr(m.Receiver)}.{_names.GetName(m.Property)}",BoundAssociatedValueAccessExpression av=>$"{Expr(av.Receiver)}.{AssociatedValueMember(av.AssociatedValue)}",BoundPropertyAssignmentExpression pa=>$"({Expr(pa.Receiver)}.{_names.GetName(pa.Property)} = {Expr(pa.Value)})",BoundObjectCreationExpression o=>$"new {TypeName(o.Initializer.ContainingType)}({string.Join(", ", o.Arguments.Select(Expr))})",BoundEnumCaseCreationExpression ec=>$"new {TypeName(ec.Case.ContainingType)}.{_names.GetName(ec.Case)}({string.Join(", ", ec.Arguments.Select(Expr))})",BoundMethodCallExpression mc=>$"{Expr(mc.Receiver)}.{InterfaceMethodName(mc.OriginalDefinition)}{TypeArguments(mc.TypeArguments)}({string.Join(", ", mc.Arguments.Select(Expr))})",BoundProtocolRequirementCallExpression pc=>$"{Expr(pc.Receiver)}.{_names.GetName(pc.Requirement)}({string.Join(", ", pc.Arguments.Select(Expr))})",BoundConversionExpression c=>$"(({TypeName(c.TargetType)}){Expr(c.Expression)})",BoundOptionalInjectionExpression oi=>$"new {TypeName(oi.OptionalType)}({Expr(oi.Expression)})",BoundNilExpression n=>$"{TypeName(n.OptionalType)}.None",BoundTryExpression t=>Expr(t.Expression),BoundUnaryExpression u=>u.Operator.Kind switch{BoundUnaryOperatorKind.Identity=>$"(+{Expr(u.Operand)})",BoundUnaryOperatorKind.Negation when u.Type==TypeSymbol.Int=>$"checked(-{Expr(u.Operand)})",BoundUnaryOperatorKind.Negation=>$"(-{Expr(u.Operand)})",BoundUnaryOperatorKind.LogicalNegation=>$"(!{Expr(u.Operand)})",_=>throw new CodeGenerationException("unary",u)},BoundBinaryExpression b=>Bin(b),BoundCallExpression c=>Call(c),_=>throw new CodeGenerationException($"Unsupported bound node '{e.Kind}'",e)};
    static string EnumPayloadMember(EnumCaseSymbol @case, int index) => $"Value{index}";
    string AssociatedValueMember(ParameterSymbol value){foreach(var @case in _program.NamedTypes.SelectMany(type=>type.Cases)){var index=@case.AssociatedValues.IndexOf(value);if(index>=0)return EnumPayloadMember(@case,index);}throw new CodeGenerationException("Associated value is not part of an emitted enum case.");}
    string Bin(BoundBinaryExpression b){var op=b.Operator.Kind switch{BoundBinaryOperatorKind.Addition=>"+",BoundBinaryOperatorKind.Subtraction=>"-",BoundBinaryOperatorKind.Multiplication=>"*",BoundBinaryOperatorKind.Division=>"/",BoundBinaryOperatorKind.Modulo=>"%",BoundBinaryOperatorKind.LogicalAnd=>"&&",BoundBinaryOperatorKind.LogicalOr=>"||",BoundBinaryOperatorKind.Equals=>"==",BoundBinaryOperatorKind.NotEquals=>"!=",BoundBinaryOperatorKind.Less=>"<",BoundBinaryOperatorKind.LessOrEquals=>"<=",BoundBinaryOperatorKind.Greater=>">",BoundBinaryOperatorKind.GreaterOrEquals=>">=",_=>"?"}; var inner=$"({Expr(b.Left)} {op} {Expr(b.Right)})"; return b.Operator.ResultType==TypeSymbol.Int && b.Operator.Kind is BoundBinaryOperatorKind.Addition or BoundBinaryOperatorKind.Subtraction or BoundBinaryOperatorKind.Multiplication ? $"checked{inner}" : inner;}
    string Call(BoundCallExpression c)=>c.OriginalDefinition.IsBuiltIn&&c.OriginalDefinition.Name=="readFile"?$"__martin_readFile({string.Join(", ", c.Arguments.Select(Expr))})":$"{_names.GetName(c.OriginalDefinition)}{TypeArguments(c.TypeArguments)}({string.Join(", ", c.Arguments.Select(Expr))})";
    void WriteRuntimeBoundaryHelpers()
    {
        _w.WriteLine("internal static string __martin_readFile(string path)");
        _w.WriteLine("{");
        using (_w.Indent())
        {
            _w.WriteLine("var __martin_result = Martin.Runtime.MartinFileSystem.ReadFile(path);");
            _w.WriteLine("if (__martin_result.IsSuccess) return __martin_result.Value!;");
            _w.WriteLine("var __martin_error = __martin_result.Error!.Value;");
            _w.WriteLine($"object __martin_value = __martin_error.Kind switch");
            _w.WriteLine("{");
            using (_w.Indent())
            {
                var fileError = TypeName(Martin.Compiler.Binding.BuiltIns.FileError);
                _w.WriteLine($"Martin.Runtime.MartinFileErrorKind.NotFound => new {fileError}.{_names.GetName(Martin.Compiler.Binding.BuiltIns.FileError.Cases.First(c => c.Name == "notFound"))}(__martin_error.Payload),");
                _w.WriteLine($"Martin.Runtime.MartinFileErrorKind.AccessDenied => new {fileError}.{_names.GetName(Martin.Compiler.Binding.BuiltIns.FileError.Cases.First(c => c.Name == "accessDenied"))}(__martin_error.Payload),");
                _w.WriteLine($"Martin.Runtime.MartinFileErrorKind.InvalidPath => new {fileError}.{_names.GetName(Martin.Compiler.Binding.BuiltIns.FileError.Cases.First(c => c.Name == "invalidPath"))}(__martin_error.Payload),");
                _w.WriteLine($"_ => new {fileError}.{_names.GetName(Martin.Compiler.Binding.BuiltIns.FileError.Cases.First(c => c.Name == "io"))}(__martin_error.Payload)");
            }
            _w.WriteLine("};");
            _w.WriteLine($"throw new Martin.Runtime.MartinThrownErrorValue(__martin_value, typeof({TypeName(Martin.Compiler.Binding.BuiltIns.FileError)}));");
        }
        _w.WriteLine("}");
    }

    string TypeArguments(ImmutableArray<TypeSymbol> arguments)=>arguments.IsDefaultOrEmpty?string.Empty:$"<{string.Join(", ",arguments.Select(TypeName))}>";
    string TypeName(TypeSymbol type)=>CSharpTypeMapper.GetTypeName(type, _names.GetName);
    string Lit(BoundLiteralExpression l)=>CSharpLiteralWriter.WriteLiteral(l);

}
