using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

public sealed class CSharpLowerer
{
    private TemporaryAllocator _temporaries = new();
    private int _decisionGraph;
    private CancellationToken _cancellationToken;

    public LoweredProgram Lower(BoundProgram program, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        _temporaries = new TemporaryAllocator();
        _decisionGraph = 0;

        var loweredFunctions = ImmutableArray.CreateBuilder<LoweredFunction>();
        var functions = DeterministicOrder.Functions(IncludeBuiltIns(program.Functions)).ToImmutableArray();

        foreach (var function in functions)
        {
            if (function.IsBuiltIn)
                continue;
            var body = program.FunctionBodies[function];
            loweredFunctions.Add(LowerFunction(function, body));
        }

        var loweredMethods = ImmutableDictionary.CreateBuilder<MethodSymbol, BoundBlockStatement>();
        foreach (var method in DeterministicOrder.Methods(program.MethodBodies.Keys))
        {
            _cancellationToken.ThrowIfCancellationRequested();
            loweredMethods[method] = LowerMethodBody(program.MethodBodies[method], method.ReturnType);
        }

        var loweredInitializers = ImmutableDictionary.CreateBuilder<InitializerSymbol, BoundBlockStatement>();
        foreach (var init in DeterministicOrder.Initializers(program.InitializerBodies.Keys))
        {
            _cancellationToken.ThrowIfCancellationRequested();
            loweredInitializers[init] = LowerMethodBody(program.InitializerBodies[init], TypeSymbol.Void);
        }

        var loweredProgram = new LoweredProgram(
            program.Diagnostics,
            loweredFunctions.ToImmutable(),
            functions,
            program.NamedTypes,
            loweredMethods.ToImmutable(),
            loweredInitializers.ToImmutable(),
            program.Conformances,
            FindEntry(functions));

        return loweredProgram;
    }

    private LoweredFunction LowerFunction(FunctionSymbol symbol, BoundBlockStatement body)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return new LoweredFunction(symbol, LowerMethodBody(body, symbol.ReturnType), body.Location);
    }

    private BoundBlockStatement LowerMethodBody(BoundBlockStatement block, TypeSymbol returnType)
    {
        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        foreach (var statement in block.Statements)
        {
            LowerStatement(statement, statements, returnType);
        }
        var loweredBlock = new BoundBlockStatement(statements.ToImmutable());

        // Add implicit return
        if (returnType == TypeSymbol.Void)
        {
            var hasReturn = loweredBlock.Statements.Length > 0 && loweredBlock.Statements.Last().Kind == BoundNodeKind.ReturnStatement;
            var hasThrow = loweredBlock.Statements.Length > 0 && loweredBlock.Statements.Last().Kind == BoundNodeKind.ThrowStatement;
            if (!hasReturn && !hasThrow)
            {
                return new BoundBlockStatement(loweredBlock.Statements.Add(new BoundReturnStatement(null)));
            }
        }

        return loweredBlock;
    }

    private BoundBlockStatement LowerBlockStatement(BoundBlockStatement block, TypeSymbol returnType)
    {
        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        foreach (var statement in block.Statements)
        {
            LowerStatement(statement, statements, returnType);
        }
        return new BoundBlockStatement(statements.ToImmutable());
    }

    private void LowerStatement(BoundStatement statement, ImmutableArray<BoundStatement>.Builder statements, TypeSymbol returnType)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        switch (statement.Kind)
        {
        case BoundNodeKind.BlockStatement:
            statements.Add(LowerBlockStatement((BoundBlockStatement)statement, returnType));
            break;
        case BoundNodeKind.VariableDeclaration:
            var decl = (BoundVariableDeclaration)statement;
            statements.Add(new BoundVariableDeclaration(decl.Variable, LowerExpression(decl.Initializer)) { Location = statement.Location });
            break;
        case BoundNodeKind.ExpressionStatement:
            var exprStmt = (BoundExpressionStatement)statement;
            statements.Add(new BoundExpressionStatement(LowerExpression(exprStmt.Expression)) { Location = statement.Location });
            break;
        case BoundNodeKind.IfStatement:
            var ifStmt = (BoundIfStatement)statement;
            var thenStatements = ImmutableArray.CreateBuilder<BoundStatement>();
            LowerStatement(ifStmt.ThenStatement, thenStatements, returnType);
            var thenBlock = thenStatements.Count == 1 ? thenStatements[0] : new BoundBlockStatement(thenStatements.ToImmutable());

            BoundStatement? elseBlock = null;
            if (ifStmt.ElseStatement != null)
            {
                var elseStatements = ImmutableArray.CreateBuilder<BoundStatement>();
                LowerStatement(ifStmt.ElseStatement, elseStatements, returnType);
                elseBlock = elseStatements.Count == 1 ? elseStatements[0] : new BoundBlockStatement(elseStatements.ToImmutable());
            }

            statements.Add(new BoundIfStatement(LowerExpression(ifStmt.Condition), thenBlock, elseBlock) { Location = statement.Location });
            break;
        case BoundNodeKind.WhileStatement:
            var whileStmt = (BoundWhileStatement)statement;
            var bodyStatements = ImmutableArray.CreateBuilder<BoundStatement>();
            LowerStatement(whileStmt.Body, bodyStatements, returnType);
            var bodyBlock = bodyStatements.Count == 1 ? bodyStatements[0] : new BoundBlockStatement(bodyStatements.ToImmutable());
            statements.Add(new BoundWhileStatement(LowerExpression(whileStmt.Condition), bodyBlock) { Location = statement.Location });
            break;
        case BoundNodeKind.ReturnStatement:
            var retStmt = (BoundReturnStatement)statement;
            statements.Add(new BoundReturnStatement(retStmt.Expression != null ? LowerExpression(retStmt.Expression) : null) { Location = statement.Location });
            break;
        case BoundNodeKind.ThrowStatement:
            var throwStmt = (BoundThrowStatement)statement;
            statements.Add(new BoundThrowStatement(LowerExpression(throwStmt.Expression), throwStmt.ErrorType) { Location = statement.Location });
            break;
        case BoundNodeKind.DoCatchStatement:
            var doCatch = (BoundDoCatchStatement)statement;
            var tryStmts = ImmutableArray.CreateBuilder<BoundStatement>();
            LowerStatement(doCatch.Body, tryStmts, returnType);
            var tryBlock = tryStmts.Count == 1 ? tryStmts[0] : new BoundBlockStatement(tryStmts.ToImmutable());

            var clauses = ImmutableArray.CreateBuilder<BoundCatchClause>();
            foreach (var c in doCatch.CatchClauses)
            {
                var catchStmts = ImmutableArray.CreateBuilder<BoundStatement>();
                LowerStatement(c.Body, catchStmts, returnType);
                var catchBlock = catchStmts.Count == 1 ? catchStmts[0] : new BoundBlockStatement(catchStmts.ToImmutable());
                clauses.Add(new BoundCatchClause(c.Pattern, catchBlock, c.Location));
            }
            statements.Add(new BoundDoCatchStatement(tryBlock, doCatch.ErrorType, clauses.ToImmutable(), doCatch.Analysis, doCatch.DecisionGraph) { Location = statement.Location });
            break;
        case BoundNodeKind.IfLetStatement:
            LowerIfLetStatement((BoundIfLetStatement)statement, statements, returnType);
            break;
        case BoundNodeKind.SwitchStatement:
            LowerSwitchStatement((BoundSwitchStatement)statement, statements, returnType);
            break;
        default:
            throw new CodeGenerationException($"Unsupported bound node '{statement.Kind}'", statement);
        }
    }

    private void LowerIfLetStatement(BoundIfLetStatement ifLet, ImmutableArray<BoundStatement>.Builder statements, TypeSymbol returnType)
    {
        var tmpName = _temporaries.Allocate("optional");
        var tmpVar = new CompilerGeneratedLocalVariableSymbol(tmpName, ifLet.OptionalExpression.Type, ifLet.Location);
        var decl = new BoundVariableDeclaration(tmpVar, LowerExpression(ifLet.OptionalExpression)) { Location = ifLet.Location };

        var thenStatements = ImmutableArray.CreateBuilder<BoundStatement>();
        var valDecl = OptionalValueDeclaration(ifLet.BoundVariable, tmpVar, ifLet.Location);
        thenStatements.Add(valDecl);
        LowerStatement(ifLet.ThenStatement, thenStatements, returnType);
        var thenBlock = new BoundBlockStatement(thenStatements.ToImmutable());

        BoundStatement? elseBlock = null;
        if (ifLet.ElseStatement != null)
        {
            var elseStatements = ImmutableArray.CreateBuilder<BoundStatement>();
            LowerStatement(ifLet.ElseStatement, elseStatements, returnType);
            elseBlock = elseStatements.Count == 1 ? elseStatements[0] : new BoundBlockStatement(elseStatements.ToImmutable());
        }

        var cond = new BoundHasValueExpression(new BoundVariableExpression(tmpVar));
        var newIf = new BoundIfStatement(cond, thenBlock, elseBlock) { Location = ifLet.Location };

        statements.Add(new BoundBlockStatement([decl, newIf]) { Location = ifLet.Location });
    }

    private void LowerSwitchStatement(BoundSwitchStatement switchStmt, ImmutableArray<BoundStatement>.Builder statements, TypeSymbol returnType)
    {
        if (switchStmt.DecisionGraph is {} graph)
        {
            LowerDecisionGraph(graph, statements, returnType, switchStmt.Location);
            return;
        }

        var tmpName = _temporaries.Allocate("switch");
        var tmpVar = new CompilerGeneratedLocalVariableSymbol(tmpName, switchStmt.Expression.Type, switchStmt.Location);
        var decl = new BoundVariableDeclaration(tmpVar, LowerExpression(switchStmt.Expression)) { Location = switchStmt.Location };

        var loweredCases = ImmutableArray.CreateBuilder<BoundSwitchCase>();
        foreach (var c in switchStmt.Cases)
        {
            var caseStmts = ImmutableArray.CreateBuilder<BoundStatement>();
            LocalVariableSymbol? patternVariable = null;

            if (c.Case is not null)
            {
                var patternName = _temporaries.Allocate(c.Case.Name.ToLowerInvariant());
                patternVariable = new CompilerGeneratedLocalVariableSymbol(patternName, c.Case.ContainingType, switchStmt.Location);

                for (var i = 0; i < c.Variables.Length; i++)
                {
                    var value = new BoundAssociatedValueAccessExpression(new BoundVariableExpression(patternVariable), c.Case.AssociatedValues[i]);
                    caseStmts.Add(new BoundVariableDeclaration(c.Variables[i], value));
                }
            }

            LowerStatement(c.Body, caseStmts, returnType);
            var caseBlock = caseStmts.Count == 1 ? caseStmts[0] : new BoundBlockStatement(caseStmts.ToImmutable());
            loweredCases.Add(new BoundSwitchCase(c.Pattern, caseBlock, c.Location, patternVariable));
        }

        var newSwitch = new BoundSwitchStatement(new BoundVariableExpression(tmpVar), loweredCases.ToImmutable()) { Location = switchStmt.Location };
        statements.Add(new BoundBlockStatement([decl, newSwitch]) { Location = switchStmt.Location });
    }

    private void LowerDecisionGraph(PatternDecisionGraph graph, ImmutableArray<BoundStatement>.Builder statements,
                                    TypeSymbol returnType, TextLocation? switchLocation)
    {
        var validationErrors = graph.Validate(_cancellationToken);
        if (!validationErrors.IsEmpty)
            throw new CodeGenerationException($"Invalid pattern decision graph: {string.Join("; ", validationErrors)}");

        var prefix = $"__pattern_graph_{_decisionGraph++}_";
        string NodeLabel(PatternDecisionNode node) => prefix + node.Label;
        string TargetLabel(PatternCaseTarget target) => prefix + target.Label;
        var endLabel = prefix + "end";
        var flow = ImmutableArray.CreateBuilder<BoundStatement>();
        flow.Add(Generated(new BoundVariableDeclaration(graph.InputTemporary,
                                                        LowerExpression(graph.InputExpression)),
                           switchLocation));

        var pending = new Stack<PatternDecisionNode>();
        var visited = new HashSet<PatternDecisionNode>(ReferenceEqualityComparer.Instance);
        pending.Push(graph.Entry);
        while (pending.Count > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var node = pending.Pop();
            if (!visited.Add(node))
                continue;
            flow.Add(Generated(new BoundLabelStatement(NodeLabel(node)), node.Location));
            LowerDecisionNode(node, flow, NodeLabel, TargetLabel);
            for (var i = node.Successors.Length - 1; i >= 0; i--)
                pending.Push(node.Successors[i]);
        }

        foreach (var target in graph.CaseTargets)
        {
            flow.Add(Generated(new BoundLabelStatement(TargetLabel(target)), target.Location));
            LowerStatement(target.Case.Body, flow, returnType);
            flow.Add(Generated(new BoundGotoStatement(endLabel), target.Location));
        }
        flow.Add(Generated(new BoundLabelStatement(endLabel), switchLocation));
        var block = Generated(new BoundBlockStatement(flow.ToImmutable()), switchLocation);
        statements.Add(block);
    }

    private void LowerDecisionNode(PatternDecisionNode node, ImmutableArray<BoundStatement>.Builder statements,
                                   Func<PatternDecisionNode, string> nodeLabel, Func<PatternCaseTarget, string> targetLabel)
    {
        switch (node)
        {
        case TestEnumCaseDecision test:
            Branch(new BoundEnumCaseTestExpression(new BoundVariableExpression(test.Input), test.Case), test.WhenMatched, test.WhenNotMatched);
            break;
        case TestLiteralDecision test:
            Branch(LiteralEquality(test.Input, test.Value), test.WhenMatched, test.WhenNotMatched);
            break;
        case TestOptionalHasValueDecision test:
            Branch(new BoundHasValueExpression(new BoundVariableExpression(test.Input)), test.WhenHasValue, test.WhenNil);
            break;
        case ExtractEnumPayloadDecision extract:
            statements.Add(Generated(new BoundVariableDeclaration(extract.Destination,
                                                                  new BoundEnumPayloadAccessExpression(new BoundVariableExpression(extract.Input),
                                                                                                       extract.Case, extract.PayloadIndex)),
                                     extract.Location));
            GoTo(extract.Next);
            break;
        case ExtractOptionalValueDecision extract:
            statements.Add(OptionalValueDeclaration(extract.Destination, extract.Input, extract.Location));
            GoTo(extract.Next);
            break;
        case BindPatternValueDecision bind:
            statements.Add(Generated(new BoundVariableDeclaration(bind.Variable,
                                                                  new BoundVariableExpression(bind.Value)),
                                     bind.Location));
            GoTo(bind.Next);
            break;
        case GotoCaseDecision branch:
            statements.Add(Generated(new BoundGotoStatement(targetLabel(branch.Target)), branch.Location));
            break;
        case FailureDecision failure:
            statements.Add(Generated(new BoundThrowStatement(
                                         new BoundLiteralExpression("Pattern decision graph reached its defensive failure continuation.", TypeSymbol.String),
                                         TypeSymbol.Error),
                                     failure.Location));
            break;
        default:
            throw new CodeGenerationException($"Unsupported pattern decision node '{node.GetType().Name}'.");
        }

        void Branch(BoundExpression condition, PatternDecisionNode matched, PatternDecisionNode notMatched)
        {
            statements.Add(Generated(new BoundConditionalGotoStatement(nodeLabel(matched), condition), node.Location));
            GoTo(notMatched);
        }

        void GoTo(PatternDecisionNode next) =>
            statements.Add(Generated(new BoundGotoStatement(nodeLabel(next)), node.Location));
    }

    private static BoundExpression LiteralEquality(CompilerGeneratedLocalVariableSymbol input, object? value)
    {
        var left = new BoundVariableExpression(input);
        var right = new BoundLiteralExpression(value, input.Type);
        var op = BoundBinaryOperator.Bind(Martin.Compiler.Syntax.SyntaxKind.EqualEqualToken, input.Type, input.Type) ?? throw new CodeGenerationException($"Pattern literal type '{input.Type.Name}' does not support equality.");
        return new BoundBinaryExpression(left, op, right);
    }

    private static BoundVariableDeclaration OptionalValueDeclaration(VariableSymbol destination,
                                                                     CompilerGeneratedLocalVariableSymbol optional, TextLocation? location) =>
        Generated(new BoundVariableDeclaration(destination,
                                               new BoundGetValueExpression(new BoundVariableExpression(optional), destination.Type)),
                  location);

    private static T Generated<T>(T statement, TextLocation? location)
        where T : BoundStatement
    {
        statement.Location = location;
        statement.IsCompilerGenerated = true;
        return statement;
    }

    private BoundExpression LowerExpression(BoundExpression expression)
    {
        switch (expression.Kind)
        {
        case BoundNodeKind.LiteralExpression:
        case BoundNodeKind.VariableExpression:
        case BoundNodeKind.ErrorExpression:
        case BoundNodeKind.NilExpression:
            return expression;
        case BoundNodeKind.AssignmentExpression:
            var assign = (BoundAssignmentExpression)expression;
            return new BoundAssignmentExpression(assign.Variable, LowerExpression(assign.Expression));
        case BoundNodeKind.UnaryExpression:
            var unary = (BoundUnaryExpression)expression;
            return new BoundUnaryExpression(unary.Operator, LowerExpression(unary.Operand));
        case BoundNodeKind.BinaryExpression:
            var binary = (BoundBinaryExpression)expression;
            return new BoundBinaryExpression(LowerExpression(binary.Left), binary.Operator, LowerExpression(binary.Right));
        case BoundNodeKind.CallExpression:
            var call = (BoundCallExpression)expression;
            var loweredArgs = call.Arguments.Select(LowerExpression).ToImmutableArray();
            if (call.Function.IsBuiltIn)
            {
                return call.Function.Name switch {
                    "print" => new BoundRuntimeCallExpression("Martin.Runtime.MartinConsole.Print", loweredArgs, TypeSymbol.Void),
                    "argumentCount" => new BoundRuntimeCallExpression("Martin.Runtime.MartinConsole.ArgumentCount", loweredArgs, TypeSymbol.Int),
                    "argument" => new BoundRuntimeCallExpression("Martin.Runtime.MartinConsole.Argument", loweredArgs, TypeSymbol.String),
                    "readLine" => new BoundRuntimeCallExpression("Martin.Runtime.MartinConsole.ReadLine", loweredArgs, TypeSymbol.String),
                    "writeError" => new BoundRuntimeCallExpression("Martin.Runtime.MartinConsole.WriteError", loweredArgs, TypeSymbol.Void),
                    "exit" => new BoundRuntimeCallExpression("Martin.Runtime.MartinConsole.Exit", loweredArgs, TypeSymbol.Void),
                    _ => throw new CodeGenerationException($"Unsupported built-in function '{call.Function.Name}'", call)
                };
            }
            return new BoundCallExpression(call.Function, loweredArgs, call.TypeArguments);
        case BoundNodeKind.ConversionExpression:
            var conv = (BoundConversionExpression)expression;
            return new BoundConversionExpression(conv.TargetType, LowerExpression(conv.Expression));
        case BoundNodeKind.MemberAccessExpression:
            var member = (BoundMemberAccessExpression)expression;
            return new BoundMemberAccessExpression(LowerExpression(member.Receiver), member.Property);
        case BoundNodeKind.AssociatedValueAccessExpression:
            var associatedValue = (BoundAssociatedValueAccessExpression)expression;
            return new BoundAssociatedValueAccessExpression(LowerExpression(associatedValue.Receiver), associatedValue.AssociatedValue);
        case BoundNodeKind.EnumCaseTestExpression:
            var enumTest = (BoundEnumCaseTestExpression)expression;
            return new BoundEnumCaseTestExpression(LowerExpression(enumTest.Receiver), enumTest.Case);
        case BoundNodeKind.EnumPayloadAccessExpression:
            var payload = (BoundEnumPayloadAccessExpression)expression;
            return new BoundEnumPayloadAccessExpression(LowerExpression(payload.Receiver), payload.Case, payload.PayloadIndex);
        case BoundNodeKind.PropertyAssignmentExpression:
            var propAssign = (BoundPropertyAssignmentExpression)expression;
            return new BoundPropertyAssignmentExpression(LowerExpression(propAssign.Receiver), propAssign.Property, LowerExpression(propAssign.Value));
        case BoundNodeKind.MethodCallExpression:
            var methodCall = (BoundMethodCallExpression)expression;
            return new BoundMethodCallExpression(LowerExpression(methodCall.Receiver), methodCall.Method, methodCall.Arguments.Select(LowerExpression).ToImmutableArray(), methodCall.TypeArguments);
        case BoundNodeKind.ProtocolRequirementCallExpression:
            var requirementCall = (BoundProtocolRequirementCallExpression)expression;
            return new BoundProtocolRequirementCallExpression(LowerExpression(requirementCall.Receiver), requirementCall.Requirement, requirementCall.Arguments.Select(LowerExpression).ToImmutableArray());
        case BoundNodeKind.ObjectCreationExpression:
            var objCreate = (BoundObjectCreationExpression)expression;
            return new BoundObjectCreationExpression(objCreate.Initializer, objCreate.Arguments.Select(LowerExpression).ToImmutableArray());
        case BoundNodeKind.OptionalInjectionExpression:
            var optInj = (BoundOptionalInjectionExpression)expression;
            return new BoundOptionalInjectionExpression(LowerExpression(optInj.Expression), optInj.OptionalType);
        case BoundNodeKind.TryExpression:
            var tryExpr = (BoundTryExpression)expression;
            return new BoundTryExpression(LowerExpression(tryExpr.Expression), tryExpr.ThrownErrorType);
        case BoundNodeKind.EnumCaseCreationExpression:
            var enumCase = (BoundEnumCaseCreationExpression)expression;
            return new BoundEnumCaseCreationExpression(enumCase.Case, enumCase.Arguments.Select(LowerExpression).ToImmutableArray());
        default:
            throw new CodeGenerationException($"Unsupported bound node '{expression.Kind}' in expression lowering", expression);
        }
    }

    static IEnumerable<FunctionSymbol> IncludeBuiltIns(IEnumerable<FunctionSymbol> functions)
    {
        var hasPrint = false;
        foreach (var function in functions)
        {
            hasPrint |= function.IsBuiltIn && function.Name == "print";
            yield return function;
        }
    }

    static FunctionSymbol? FindEntry(IEnumerable<FunctionSymbol> functions) =>
        functions.FirstOrDefault(f => !f.IsBuiltIn && f.Name == "main" && f.Parameters.Length == 0 && f.ReturnType == TypeSymbol.Void);
}

public sealed record LoweredFunction(FunctionSymbol Symbol, BoundBlockStatement Body, TextLocation? Location);

public sealed record LoweredProgram(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<LoweredFunction> LoweredFunctions,
    ImmutableArray<FunctionSymbol> Functions,
    ImmutableArray<NamedTypeSymbol> NamedTypes,
    ImmutableDictionary<MethodSymbol, BoundBlockStatement> MethodBodies,
    ImmutableDictionary<InitializerSymbol, BoundBlockStatement> InitializerBodies,
    ImmutableArray<ProtocolConformance> Conformances,
    FunctionSymbol? EntryPoint);
