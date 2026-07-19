using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Generics;
using Martin.Compiler.Syntax;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.Compiler.Binding;

internal sealed class Binder
{
    BoundScope _scope;
    readonly GenericTypeFactory _genericTypes;
    readonly ICollection<GenericTypeConstruction> _genericConstructions;
    readonly ICollection<GenericCallableConstruction> _genericCallableConstructions;
    readonly SourceText _text;
    readonly FunctionSymbol? _function;
    readonly NamedTypeSymbol? _type;
    readonly Symbol? _member;
    readonly DiagnosticBag _d;
    readonly Dictionary<SyntaxNode, Symbol> _decl;
    readonly Dictionary<SyntaxNode, Symbol> _sym;
    readonly Dictionary<ExpressionSyntax, TypeSymbol> _types;
    readonly Dictionary<ExpressionSyntax, Conversion> _conv;
    readonly Dictionary<SyntaxNode, GenericUseInfo> _genericUses;
    readonly Dictionary<SyntaxNode, TypedErrorUseInfo> _typedErrorUses;
    readonly CancellationToken _cancellationToken;
    bool _suppressMissingTryDiagnostics;
    public Binder(SourceText text, BoundScope parent, GenericTypeFactory genericTypes, ICollection<GenericTypeConstruction> genericConstructions, ICollection<GenericCallableConstruction> genericCallableConstructions, FunctionSymbol? function, NamedTypeSymbol? type, Symbol? member, DiagnosticBag d, Dictionary<SyntaxNode, Symbol> decl, Dictionary<SyntaxNode, Symbol> sym, Dictionary<ExpressionSyntax, TypeSymbol> types, Dictionary<ExpressionSyntax, Conversion> conv, Dictionary<SyntaxNode, GenericUseInfo> genericUses, Dictionary<SyntaxNode, TypedErrorUseInfo> typedErrorUses, CancellationToken cancellationToken)
    {
        _genericTypes = genericTypes;
        _genericConstructions = genericConstructions;
        _genericCallableConstructions = genericCallableConstructions;
        _cancellationToken = cancellationToken;
        _text = text;
        _scope = new(parent);
        _function = function;
        _type = type;
        _member = member;
        _d = d;
        _decl = decl;
        _sym = sym;
        _types = types;
        _conv = conv;
        _genericUses = genericUses;
        _typedErrorUses = typedErrorUses;
        if (function != null)
        {
            foreach (var tp in function.TypeParameters)
                _scope.TryDeclareType(tp);
            foreach (var p in function.Parameters)
                _scope.TryDeclareVariable(p);
        }
        if (type != null)
        {
            foreach (var tp in type.TypeParameters)
                _scope.TryDeclareType(tp);
            _scope.TryDeclareVariable(new SelfParameterSymbol(type, member is MethodSymbol { IsMutating : true } || type.IsClass || member is InitializerSymbol, type.Locations));
        }
    }
    public void DeclareParameter(ParameterSymbol p) => _scope.TryDeclareVariable(p);
    TypeSymbol BindTypeSyntax(SyntaxNode node)
    {
        if (node is GenericSyntaxNode gn && gn.Kind == SyntaxKind.GenericName)
        {
            var result = GenericTypeBinding.Bind(gn, _text, name => _scope.TryLookupType(name, out var type) ? type : null, BindTypeSyntax, _genericTypes, _d, _genericConstructions);
            if (!ReferenceEquals(result, TypeSymbol.Error))
                _sym[node] = result;
            return result;
        }
        if (node is GenericSyntaxNode g && g.Kind == SyntaxKind.OptionalType)
        {
            var optional = new OptionalTypeSymbol(BindTypeSyntax(g.Children[0]));
            _sym[node] = optional;
            return optional;
        }
        var tok = node.GetChildren().OfType<SyntaxToken>().FirstOrDefault(t => t.Kind == SyntaxKind.IdentifierToken) ?? node as SyntaxToken;
        if (tok != null && _scope.TryLookupType(tok.Text, out var type))
        {
            if (type is ProtocolTypeSymbol)
                _d.Report(new("MRT2167", DiagnosticSeverity.Error, $"Protocol existential values are not supported in this language version.", Loc(tok)));
            _sym[node] = type;
            return type;
        }
        if (tok != null)
            _d.Report(new("MRT2014", DiagnosticSeverity.Error, $"Type '{tok.Text}' is not defined.", Loc(tok)));
        return TypeSymbol.Error;
    }
    public BoundStatement BindStatement(StatementSyntax s)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var bound = s.Kind switch { SyntaxKind.BlockStatement => BindBlock((GenericStatementSyntax)s), SyntaxKind.VariableDeclarationStatement => BindVar((GenericStatementSyntax)s), SyntaxKind.ExpressionStatement => new BoundExpressionStatement(BindExpression((ExpressionSyntax)s.GetChildren().First())), SyntaxKind.IfStatement => BindIf((GenericStatementSyntax)s), SyntaxKind.IfLetStatement => BindIfLet((GenericStatementSyntax)s), SyntaxKind.WhileStatement => BindWhile((GenericStatementSyntax)s), SyntaxKind.ReturnStatement => BindReturn((GenericStatementSyntax)s), SyntaxKind.ThrowStatement => BindThrow((GenericStatementSyntax)s), SyntaxKind.DoCatchStatement => BindDoCatch((GenericStatementSyntax)s), SyntaxKind.SwitchStatement => BindSwitch((GenericStatementSyntax)s),
                                    _ => new BoundExpressionStatement(new BoundErrorExpression()) };
        bound.Location ??= Loc(s);
        return bound;
    }
    BoundStatement BindThrow(GenericStatementSyntax s)
    {
        var expressionSyntax = s.Children.OfType<ExpressionSyntax>().First();
        var expression = BindExpression(expressionSyntax);
        var declaredErrorType = _function?.ErrorType ?? (_member as MethodSymbol)?.ErrorType ?? (_member as InitializerSymbol)?.ErrorType;

        if (declaredErrorType is null)
        {
            _d.Report(new("MRT2195", DiagnosticSeverity.Error, "Throw statement is not valid in a nonthrowing callable.", Loc(s.Children[0])));
            return new BoundThrowStatement(expression, TypeSymbol.Error, isValid: false);
        }

        // Expression binding already reported the primary error.  Preserve the
        // callable's exact wrapping metadata, but prevent a later propagation pass
        // from treating this malformed throw as another valid error path.
        if (expression.Type == TypeSymbol.Error)
            return new BoundThrowStatement(expression, declaredErrorType, isValid: false);

        var conversion = Conversion.Classify(expression.Type, declaredErrorType);
        if (!conversion.Exists)
        {
            _d.Report(new("MRT2191", DiagnosticSeverity.Error, $"Thrown expression must have error type '{declaredErrorType}', but has type '{expression.Type}'.", Loc(expressionSyntax)));
            return new BoundThrowStatement(expression, declaredErrorType, isValid: false);
        }

        _conv[expressionSyntax] = conversion;
        var convertedExpression = conversion.IsIdentity
                                      ? expression
                                      : new BoundConversionExpression(declaredErrorType, expression);
        _typedErrorUses[s] = new(new ErrorEffect(true, declaredErrorType), true, false, true, declaredErrorType, Loc(s));
        return new BoundThrowStatement(convertedExpression, declaredErrorType);
    }
    BoundStatement BindDoCatch(GenericStatementSyntax s)
    {
        var bodySyntax = s.Children.OfType<StatementSyntax>().First();
        var body = BindStatement(bodySyntax);
        var bodyEffect = AggregateAcknowledgedEffect(body);
        var catchInputType = bodyEffect.CanThrow && bodyEffect.ErrorType is not null && !bodyEffect.IsConflicting ? bodyEffect.ErrorType : TypeSymbol.Error;
        if (bodyEffect.IsConflicting)
            _d.Report(new("MRT2402", DiagnosticSeverity.Error, "Do-catch body contains incompatible error effects.", Loc(bodySyntax)));
        else if (!bodyEffect.CanThrow)
            _d.Report(new("MRT2401", DiagnosticSeverity.Error, "Do-catch body contains no throwing operation.", Loc(s.Children[0])));
        var clauses = ImmutableArray.CreateBuilder<BoundCatchClause>();
        foreach (var c in s.Children.OfType<GenericSyntaxNode>().Where(x => x.Kind == SyntaxKind.CatchClause))
        {
            var prev = _scope;
            var catchScope = new BoundScope(prev);
            _scope = catchScope;
            var pat = c.Children.OfType<PatternSyntax>().FirstOrDefault();
            BoundPattern boundPattern = pat is null || pat is IdentifierPatternSyntax { Identifier.IsMissing : true }
                                            ? new BoundWildcardPattern(catchInputType, Loc(c), catchInputType == TypeSymbol.Error)
                                            : new PatternBinder(_decl, _sym).Bind(pat, new PatternBindingContext { InputType = catchInputType, EnclosingScope = prev, CaseScope = catchScope, SourceText = _text, NestingDepth = 0, ExpectedEnum = catchInputType is EnumTypeSymbol || catchInputType is ConstructedTypeSymbol { GenericDefinition : EnumTypeSymbol } ? catchInputType : null, ExpectedOptional = catchInputType as OptionalTypeSymbol }, _d, _cancellationToken);
            var b = BindStatement(c.Children.OfType<StatementSyntax>().First());
            _scope = prev;
            _typedErrorUses[c] = new(new ErrorEffect(true, catchInputType), true, true, false, catchInputType, Loc(c));
            clauses.Add(new BoundCatchClause(boundPattern, b, Loc(c)));
        }
        var immutableClauses = clauses.ToImmutable();
        if (immutableClauses.IsEmpty)
            _d.Report(new("MRT2403", DiagnosticSeverity.Error, "Do statement requires at least one catch clause.", Loc(s)));
        SwitchAnalysisResult? analysis = null;
        PatternDecisionGraph? decisionGraph = null;
        if (catchInputType != TypeSymbol.Error && bodyEffect.CanThrow && !immutableClauses.IsEmpty)
        {
            try
            {
                analysis = new PatternCoverageAnalyzer().Analyze(catchInputType, immutableClauses.Select(c => c.Pattern).ToImmutableArray(), _cancellationToken);
            }
            catch (PatternMatrixCapacityException)
            {
                _d.Report(new("MRT2406", DiagnosticSeverity.Error, "Typed-error analysis exceeded the supported complexity limit.", Loc(s)));
                analysis = SwitchAnalysisResult.Error;
            }
            foreach (var unreachable in analysis.UnreachableCases)
                _d.Report(new("MRT2198", DiagnosticSeverity.Error, "Catch clause is unreachable.", unreachable.Location));
            if (!analysis.HasErrors && !analysis.IsExhaustive)
                _d.Report(new("MRT2193", DiagnosticSeverity.Error, $"Do-catch is not exhaustive. Missing: {string.Join(", ", analysis.MissingWitnesses.Select(MissingPatternWitnessDiagnosticRenderer.Render))}.", Loc(s)));
            if (!analysis.HasErrors && analysis.IsExhaustive)
                MarkCaught(bodySyntax.Span, catchInputType);
            if (!analysis.HasErrors && analysis.IsExhaustive && !immutableClauses.Any(c => c.Pattern.HasErrors))
            {
                var errorValueInput = new CompilerGeneratedLocalVariableSymbol("$catch_error_value", catchInputType, Loc(s));
                decisionGraph = new PatternDecisionGraphBuilder().BuildCatch(errorValueInput, immutableClauses, Loc(s), _cancellationToken);
            }
        }
        return new BoundDoCatchStatement(body, catchInputType, immutableClauses, analysis, decisionGraph);
    }
    ErrorEffect AggregateAcknowledgedEffect(BoundStatement statement)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return statement switch {
            BoundBlockStatement block => ErrorEffect.Combine(block.Statements.Select(AggregateAcknowledgedEffect)),
            BoundVariableDeclaration variable => variable.Initializer.ErrorEffect,
            BoundExpressionStatement expression => expression.Expression.ErrorEffect,
            BoundReturnStatement ret => ret.Expression?.ErrorEffect ?? ErrorEffect.None,
            BoundThrowStatement { SuppressesPropagationDiagnostics : true } => ErrorEffect.None,
            BoundThrowStatement thr => new ErrorEffect(true, thr.ErrorType),
            BoundIfStatement iff => ErrorEffect.Combine((new[] { iff.Condition.ErrorEffect, AggregateAcknowledgedEffect(iff.ThenStatement), iff.ElseStatement is null ? ErrorEffect.None : AggregateAcknowledgedEffect(iff.ElseStatement) })),
            BoundIfLetStatement iff => ErrorEffect.Combine((new[] { iff.OptionalExpression.ErrorEffect, AggregateAcknowledgedEffect(iff.ThenStatement), iff.ElseStatement is null ? ErrorEffect.None : AggregateAcknowledgedEffect(iff.ElseStatement) })),
            BoundWhileStatement loop => ErrorEffect.Combine([loop.Condition.ErrorEffect, AggregateAcknowledgedEffect(loop.Body)]),
            BoundSwitchStatement sw => ErrorEffect.Combine([sw.Expression.ErrorEffect, ..sw.Cases.Select(c => AggregateAcknowledgedEffect(c.Body))]),
            BoundDoCatchStatement doCatch => ErrorEffect.Combine(doCatch.CatchClauses.Select(c => AggregateAcknowledgedEffect(c.Body))),
            _ => ErrorEffect.None
        };
    }
    BoundBlockStatement BindBlock(GenericStatementSyntax s)
    {
        var prev = _scope;
        _scope = new(prev);
        var arr = s.Children.OfType<StatementSyntax>().Select(BindStatement).ToImmutableArray();
        _scope = prev;
        return new(arr);
    }
    BoundStatement BindVar(GenericStatementSyntax s)
    {
        var kids = s.Children;
        var ro = ((SyntaxToken)kids[0]).Kind == SyntaxKind.LetKeyword;
        var name = (SyntaxToken)kids[1];
        TypeSymbol? explicitType = null;
        var tc = kids.OfType<GenericSyntaxNode>().FirstOrDefault(x => x.Kind == SyntaxKind.TypeClause);
        if (tc != null)
        {
            explicitType = BindTypeSyntax(tc.Children.Last());
        }
        var initSyntax = kids.OfType<ExpressionSyntax>().LastOrDefault();
        if (initSyntax == null)
        {
            _d.Report(new("MRT2015", DiagnosticSeverity.Error, "Variable declaration requires an initializer.", Loc(name)));
            initSyntax = new GenericExpressionSyntax(SyntaxKind.LiteralExpression, new SyntaxToken(SyntaxKind.IntegerLiteralToken, name.Position, "", 0, true));
        }
        var init = explicitType == null ? BindExpression(initSyntax) : BindConversion(initSyntax, explicitType);
        var type = explicitType ?? init.Type;
        if (type == TypeSymbol.Nil)
        {
            _d.Report(new("MRT2016", DiagnosticSeverity.Error, "Cannot infer type from nil without an explicit optional annotation.", Loc(name)));
            _d.Report(new("MRT2123", DiagnosticSeverity.Error, "Cannot infer the wrapped type of nil.", Loc(name)));
            type = TypeSymbol.Error;
        }
        var v = new LocalVariableSymbol(name.Text, ro, type, [Loc(name)]);
        _decl[s] = v;
        if (!_scope.TryDeclareVariable(v))
            _d.Report(new("MRT2002", DiagnosticSeverity.Error, $"Symbol '{v.Name}' is already declared in this scope.", Loc(name)));
        return new BoundVariableDeclaration(v, init);
    }
    TextLocation Loc(SyntaxNode n) => new(_text, n.Span);
    BoundStatement BindIfLet(GenericStatementSyntax s)
    {
        var toks = s.Children.OfType<SyntaxToken>().ToArray();
        var name = toks.First(t => t.Kind == SyntaxKind.IdentifierToken);
        var opt = BindExpression(s.Children.OfType<ExpressionSyntax>().First());
        if (opt.Type is not OptionalTypeSymbol ot)
        {
            if (opt.Type != TypeSymbol.Error)
                _d.Report(new("MRT2122", DiagnosticSeverity.Error, "Conditional binding requires an optional value.", Loc(name)));
            ot = new OptionalTypeSymbol(TypeSymbol.Error);
        }
        var v = new LocalVariableSymbol(name.Text, true, ot.ElementType, [Loc(name)]);
        var prev = _scope;
        _scope = new(prev);
        if (!_scope.TryDeclareVariable(v))
            _d.Report(new("MRT2127", DiagnosticSeverity.Error, $"Conditional binding name '{v.Name}' is already declared in this scope.", Loc(name)));
        var then = BindStatement(s.Children.OfType<StatementSyntax>().First());
        _scope = prev;
        var els = s.Children.OfType<GenericSyntaxNode>().FirstOrDefault(x => x.Kind == SyntaxKind.ElseClause)?.Children.OfType<StatementSyntax>().FirstOrDefault();
        _decl[s] = v;
        return new BoundIfLetStatement(opt, v, then, els == null ? null : BindStatement(els));
    }
    BoundStatement BindSwitch(GenericStatementSyntax s)
    {
        var expr = BindExpression(s.Children.OfType<ExpressionSyntax>().First());
        if (expr.Type != TypeSymbol.Error && !IsSupportedSwitchInputType(expr.Type))
        {
            _d.Report(new("MRT2207", DiagnosticSeverity.Error, $"Switch input type '{expr.Type}' is not supported. Expected an enum, optional, Bool, Int, Double, or String value.", Loc(s.Children.OfType<ExpressionSyntax>().First())));
            var invalidCases = s.Children.OfType<GenericSyntaxNode>().Where(x => x.Kind == SyntaxKind.SwitchCase).Select(cs =>
                                                                                                                         {
                                                                                                                             var body = new BoundBlockStatement(cs.Children.OfType<StatementSyntax>().Select(BindStatement).ToImmutableArray());
                                                                                                                             return new BoundSwitchCase(new BoundWildcardPattern(expr.Type, Loc(cs), hasErrors: true), body, Loc(cs));
                                                                                                                         })
                                   .ToImmutableArray();
            return new BoundSwitchStatement(expr, invalidCases, SwitchAnalysisResult.Error);
        }
        var cases = new List<BoundSwitchCase>();
        foreach (var cs in s.Children.OfType<GenericSyntaxNode>().Where(x => x.Kind == SyntaxKind.SwitchCase))
        {
            var pat = cs.Children.OfType<PatternSyntax>().FirstOrDefault();
            var prev = _scope;
            var caseScope = new BoundScope(prev);
            _scope = caseScope;
            var caseLocation = Loc(cs);
            BoundPattern boundPattern;
            if (pat == null)
                boundPattern = new BoundWildcardPattern(expr.Type, caseLocation);
            else
            {
                var patternBinder = new PatternBinder(_decl, _sym);
                boundPattern = patternBinder.Bind(pat, new PatternBindingContext { InputType = expr.Type, EnclosingScope = prev, CaseScope = caseScope, SourceText = _text, NestingDepth = 0, ExpectedEnum = expr.Type is EnumTypeSymbol || expr.Type is ConstructedTypeSymbol { GenericDefinition : EnumTypeSymbol } ? expr.Type : null, ExpectedOptional = expr.Type as OptionalTypeSymbol }, _d, _cancellationToken);
            }
            var body = new BoundBlockStatement(cs.Children.OfType<StatementSyntax>().Select(BindStatement).ToImmutableArray());
            _scope = prev;
            cases.Add(new BoundSwitchCase(boundPattern, body, caseLocation));
        }
        var immutableCases = cases.ToImmutableArray();
        SwitchAnalysisResult analysis;
        try
        {
            analysis = new PatternCoverageAnalyzer().Analyze(expr.Type, immutableCases.Select(c => c.Pattern).ToImmutableArray(), _cancellationToken);
        }
        catch (PatternMatrixCapacityException)
        {
            _d.Report(new("MRT2197", DiagnosticSeverity.Error, "Pattern analysis exceeded the supported complexity limit.", Loc(s)));
            analysis = SwitchAnalysisResult.Error;
        }
        foreach (var unreachable in analysis.UnreachableCases)
            _d.Report(new("MRT2141", DiagnosticSeverity.Error, "Case is unreachable because it is already covered by an earlier case.", unreachable.Location));
        if (!analysis.HasErrors && expr.Type != TypeSymbol.Error && !analysis.IsExhaustive)
            _d.Report(new("MRT2140", DiagnosticSeverity.Error, $"Switch is not exhaustive. Missing: {string.Join(", ", analysis.MissingWitnesses.Select(MissingPatternWitnessDiagnosticRenderer.Render))}.", Loc(s)));
        var graph = analysis.HasErrors || expr.Type == TypeSymbol.Error || immutableCases.Any(c => c.Pattern.HasErrors) ? null : new PatternDecisionGraphBuilder().Build(expr, immutableCases, Loc(s), _cancellationToken);
        return new BoundSwitchStatement(expr, immutableCases, analysis, graph);
    }
    static bool IsSupportedSwitchInputType(TypeSymbol type) => type is EnumTypeSymbol or OptionalTypeSymbol
                                                               || type is ConstructedTypeSymbol { GenericDefinition : EnumTypeSymbol } || type == TypeSymbol.Bool || type == TypeSymbol.Int || type == TypeSymbol.Double || type == TypeSymbol.String;

    BoundStatement BindIf(GenericStatementSyntax s)
    {
        var expr = s.Children.OfType<ExpressionSyntax>().First();
        var cond = BindConversion(expr, TypeSymbol.Bool);
        var blocks = s.Children.OfType<StatementSyntax>().ToArray();
        var then = BindStatement(blocks[0]);
        var els = s.Children.OfType<GenericSyntaxNode>().FirstOrDefault(x => x.Kind == SyntaxKind.ElseClause)?.Children.OfType<StatementSyntax>().FirstOrDefault();
        return new BoundIfStatement(cond, then, els == null ? null : BindStatement(els));
    }
    BoundStatement BindWhile(GenericStatementSyntax s)
    {
        var cond = BindConversion(s.Children.OfType<ExpressionSyntax>().First(), TypeSymbol.Bool);
        return new BoundWhileStatement(cond, BindStatement(s.Children.OfType<StatementSyntax>().First()));
    }
    BoundStatement BindReturn(GenericStatementSyntax s)
    {
        var expr = s.Children.OfType<ExpressionSyntax>().FirstOrDefault();
        var returnType = _function?.ReturnType ?? (_member as MethodSymbol)?.ReturnType;
        if (_member is InitializerSymbol && expr != null)
            _d.Report(new("MRT2106", DiagnosticSeverity.Error, "Initializer cannot return a value.", Loc(s.Children[0])));
        if (_function == null && _member == null)
            _d.Report(new("MRT2012", DiagnosticSeverity.Error, "Return statement is not valid outside a function.", Loc(s.Children[0])));
        if (returnType == TypeSymbol.Void && expr != null)
            _d.Report(new("MRT2010", DiagnosticSeverity.Error, "Cannot return a value from a Void function.", Loc(s.Children[0])));
        if (returnType != null && returnType != TypeSymbol.Void && expr == null)
            _d.Report(new("MRT2011", DiagnosticSeverity.Error, $"Function must return a value of type '{returnType}'.", Loc(s.Children[0])));
        return new BoundReturnStatement(expr == null ? null : (returnType == null || returnType == TypeSymbol.Void ? BindExpression(expr) : BindConversion(expr, returnType)));
    }
    BoundExpression BindExpression(ExpressionSyntax e)
    {
        BoundExpression r = e.Kind switch { SyntaxKind.LiteralExpression => BindLiteral((GenericExpressionSyntax)e), SyntaxKind.IdentifierExpression => BindName((GenericExpressionSyntax)e), SyntaxKind.AssignmentExpression => BindAssign((GenericExpressionSyntax)e), SyntaxKind.PropertyAssignmentExpression => BindPropertyAssign((GenericExpressionSyntax)e), SyntaxKind.MemberAccessExpression => BindMemberAccess((GenericExpressionSyntax)e), SyntaxKind.UnaryExpression => BindUnary((GenericExpressionSyntax)e), SyntaxKind.BinaryExpression => BindBinary((GenericExpressionSyntax)e), SyntaxKind.ParenthesizedExpression => BindExpression((ExpressionSyntax)e.GetChildren().ElementAt(1)), SyntaxKind.TryExpression => BindTry((GenericExpressionSyntax)e), SyntaxKind.CallExpression => BindCall((GenericExpressionSyntax)e, null),
                                            _ => new BoundErrorExpression() };
        _types[e] = r.Type;
        _conv.TryAdd(e, Conversion.Identity);
        return r;
    }
    BoundExpression BindConversion(ExpressionSyntax e, TypeSymbol target)
    {
        var ex = e.Kind == SyntaxKind.CallExpression ? BindCall((GenericExpressionSyntax)e, target) : BindExpression(e);
        _types[e] = ex.Type;
        return ConvertBound(e, ex, target);
    }
    BoundExpression ConvertBound(ExpressionSyntax e, BoundExpression ex, TypeSymbol target)
    {
        var c = Conversion.Classify(ex.Type, target);
        _conv[e] = c;
        if (!c.Exists)
        {
            if (ex.Type == TypeSymbol.Nil)
                _d.Report(new("MRT2022", DiagnosticSeverity.Error, $"Nil cannot be converted to non-optional type '{target}'.", Loc(e)));
            else if (ex.Type != TypeSymbol.Error)
                _d.Report(new("MRT2003", DiagnosticSeverity.Error, $"Cannot convert value of type '{ex.Type}' to type '{target}'.", Loc(e)));
            return new BoundErrorExpression();
        }
        if (c.IsIdentity)
            return ex;
        if (target is OptionalTypeSymbol ot)
            return ex.Type == TypeSymbol.Nil ? new BoundNilExpression(ot) : new BoundOptionalInjectionExpression(ex, ot);
        return new BoundConversionExpression(target, ex);
    }
    BoundExpression BindTry(GenericExpressionSyntax e)
    {
        var old = _suppressMissingTryDiagnostics;
        _suppressMissingTryDiagnostics = true;
        var inner = BindExpression((ExpressionSyntax)e.Children[1]);
        _suppressMissingTryDiagnostics = old;
        _cancellationToken.ThrowIfCancellationRequested();
        var effect = inner.UnacknowledgedErrorEffect;
        if (!effect.CanThrow)
        {
            _d.Report(new("MRT2196", DiagnosticSeverity.Error, "Try requires an expression with a throwing effect.", Loc(e.Children[0])));
            _typedErrorUses[e] = new(ErrorEffect.None, true, false, false, null, Loc(e));
            return new BoundTryExpression(inner, TypeSymbol.Error);
        }
        if (effect.IsConflicting)
        {
            _d.Report(new("MRT2402", DiagnosticSeverity.Error, "Expression contains incompatible error effects.", Loc(e)));
            _typedErrorUses[e] = new(effect, true, false, true, TypeSymbol.Error, Loc(e));
            return new BoundTryExpression(inner, TypeSymbol.Error);
        }
        if (effect.ErrorType == TypeSymbol.Error)
        {
            _typedErrorUses[e] = new(effect, true, false, true, TypeSymbol.Error, Loc(e));
            return new BoundTryExpression(inner, TypeSymbol.Error);
        }
        _typedErrorUses[e] = new(effect, true, false, true, effect.ErrorType, Loc(e));
        return new BoundTryExpression(inner, effect.ErrorType!);
    }
    BoundExpression BindLiteral(GenericExpressionSyntax e)
    {
        var t = (SyntaxToken)e.Children[0];
        return t.Kind switch { SyntaxKind.IntegerLiteralToken => new BoundLiteralExpression(t.Value, TypeSymbol.Int), SyntaxKind.FloatingPointLiteralToken => new BoundLiteralExpression(t.Value, TypeSymbol.Double), SyntaxKind.StringLiteralToken => new BoundLiteralExpression(t.Value, TypeSymbol.String), SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword => new BoundLiteralExpression(t.Value, TypeSymbol.Bool), SyntaxKind.NilKeyword => new BoundLiteralExpression(null, TypeSymbol.Nil),
                               _ => new BoundErrorExpression() };
    }
    BoundExpression BindName(GenericExpressionSyntax e)
    {
        var t = (SyntaxToken)e.Children[0];
        if (_scope.TryLookupVariable(t.Text, out var v))
        {
            _sym[e] = v;
            return new BoundVariableExpression(v);
        }
        if (_type != null)
        {
            var p = _type.Properties.FirstOrDefault(p => p.Name == t.Text);
            if (p != null)
            {
                var self = new BoundVariableExpression(new SelfParameterSymbol(_type, _member is MethodSymbol { IsMutating : true } or InitializerSymbol, _type.Locations));
                _sym[e] = p;
                return new BoundMemberAccessExpression(self, p);
            }
        }
        _d.Report(new("MRT2001", DiagnosticSeverity.Error, $"Undeclared identifier '{t.Text}'.", Loc(t)));
        return new BoundErrorExpression();
    }
    BoundExpression BindAssign(GenericExpressionSyntax e)
    {
        var left = e.Children[0];
        if (left is not GenericExpressionSyntax ge || ge.Kind != SyntaxKind.IdentifierExpression)
            return new BoundErrorExpression();
        var name = (SyntaxToken)ge.Children[0];
        var rhs = (ExpressionSyntax)e.Children[2];
        if (_scope.TryLookupVariable(name.Text, out var v))
        {
            if (v.IsReadOnly)
                _d.Report(new("MRT2004", DiagnosticSeverity.Error, $"Cannot assign to '{v.Name}' because it is immutable.", Loc(name)));
            return new BoundAssignmentExpression(v, BindConversion(rhs, v.Type));
        }
        if (_type != null)
        {
            var p = _type.Properties.FirstOrDefault(p => p.Name == name.Text);
            if (p != null)
            {
                if (p.IsReadOnly && _member is not InitializerSymbol)
                    _d.Report(new("MRT2104", DiagnosticSeverity.Error, $"Immutable property '{p.Name}' cannot be assigned.", Loc(name)));
                var self = new BoundVariableExpression(new SelfParameterSymbol(_type, _member is MethodSymbol { IsMutating : true } or InitializerSymbol, _type.Locations));
                return new BoundPropertyAssignmentExpression(self, p, BindConversion(rhs, p.Type));
            }
        }
        _d.Report(new("MRT2001", DiagnosticSeverity.Error, $"Undeclared identifier '{name.Text}'.", Loc(name)));
        return new BoundErrorExpression();
    }
    BoundExpression BindMemberAccess(GenericExpressionSyntax e)
    {
        var recv = BindExpression((ExpressionSyntax)e.Children[0]);
        var name = (SyntaxToken)e.Children[2];
        if (recv.Type is OptionalTypeSymbol opt)
        {
            _d.Report(new("MRT2126", DiagnosticSeverity.Error, $"Optional value of type '{opt}' has no member '{name.Text}' without unwrapping.", Loc(name)));
            return new BoundErrorExpression();
        }
        if ((recv.Type is NamedTypeSymbol nt || recv.Type is ConstructedTypeSymbol ct && (nt = ct.GenericDefinition) != null))
        {
            var props = recv.Type is ConstructedTypeSymbol ctt ? ctt.Properties : nt.Properties;
            var methodsLookup = recv.Type is ConstructedTypeSymbol ctm ? ctm.Methods : nt.Methods;
            var p = props.FirstOrDefault(p => p.Name == name.Text);
            if (p != null)
            {
                _sym[e] = p;
                return new BoundMemberAccessExpression(recv, p);
            }
            if (methodsLookup.Any(m => m.Name == name.Text))
                return new BoundErrorExpression();
            _d.Report(new("MRT2102", DiagnosticSeverity.Error, $"Type '{nt.Name}' has no member named '{name.Text}'.", Loc(name)));
        }
        return new BoundErrorExpression();
    }
    BoundExpression BindPropertyAssign(GenericExpressionSyntax e)
    {
        var access = (GenericExpressionSyntax)e.Children[0];
        var recv = BindExpression((ExpressionSyntax)access.Children[0]);
        var name = (SyntaxToken)access.Children[2];
        NamedTypeSymbol nt;
        ImmutableArray<PropertySymbol> props;
        if (recv.Type is ConstructedTypeSymbol ctt)
        {
            nt = ctt.GenericDefinition;
            props = ctt.Properties;
        }
        else if (recv.Type is NamedTypeSymbol named)
        {
            nt = named;
            props = named.Properties;
        }
        else
            return new BoundErrorExpression();
        var p = props.FirstOrDefault(p => p.Name == name.Text);
        if (p == null)
        {
            _d.Report(new("MRT2102", DiagnosticSeverity.Error, $"Type '{nt.Name}' has no member named '{name.Text}'.", Loc(name)));
            return new BoundErrorExpression();
        }
        if (p.IsReadOnly && _member is not InitializerSymbol)
            _d.Report(new("MRT2104", DiagnosticSeverity.Error, $"Immutable property '{p.Name}' cannot be assigned.", Loc(name)));
        if (nt.IsStruct && recv is BoundVariableExpression { Variable : { IsReadOnly : true } v } && v is not SelfParameterSymbol)
            _d.Report(new("MRT2105", DiagnosticSeverity.Error, $"Cannot modify a member of immutable struct value '{v.Name}'.", Loc(name)));
        return new BoundPropertyAssignmentExpression(recv, p, BindConversion((ExpressionSyntax)e.Children[2], p.Type));
    }
    BoundExpression BindUnary(GenericExpressionSyntax e)
    {
        var op = (SyntaxToken)e.Children[0];
        var operand = BindExpression((ExpressionSyntax)e.Children[1]);
        var bound = BoundUnaryOperator.Bind(op.Kind, operand.Type);
        if (bound == null)
        {
            if (operand.Type != TypeSymbol.Error)
                _d.Report(new("MRT2005", DiagnosticSeverity.Error, $"Unary operator '{op.Text}' is not defined for type '{operand.Type}'.", Loc(op)));
            return new BoundErrorExpression();
        }
        return new BoundUnaryExpression(bound, operand);
    }
    BoundExpression BindBinary(GenericExpressionSyntax e)
    {
        var left = BindExpression((ExpressionSyntax)e.Children[0]);
        var op = (SyntaxToken)e.Children[1];
        var right = BindExpression((ExpressionSyntax)e.Children[2]);
        if (op.Kind is SyntaxKind.EqualEqualToken or SyntaxKind.BangEqualToken)
        {
            if (left.Type is OptionalTypeSymbol lot && right.Type == TypeSymbol.Nil)
                right = new BoundNilExpression(lot);
            else if (right.Type is OptionalTypeSymbol rot && left.Type == TypeSymbol.Nil)
                left = new BoundNilExpression(rot);
        }
        if (left.Type == TypeSymbol.Int && right.Type == TypeSymbol.Double)
            left = new BoundConversionExpression(TypeSymbol.Double, left);
        if (left.Type == TypeSymbol.Double && right.Type == TypeSymbol.Int)
            right = new BoundConversionExpression(TypeSymbol.Double, right);
        var bound = BoundBinaryOperator.Bind(op.Kind, left.Type, right.Type);
        if (bound == null)
        {
            if (left.Type != TypeSymbol.Error && right.Type != TypeSymbol.Error)
                _d.Report(new("MRT2006", DiagnosticSeverity.Error, $"Binary operator '{op.Text}' is not defined for types '{left.Type}' and '{right.Type}'.", Loc(op)));
            return new BoundErrorExpression();
        }
        return new BoundBinaryExpression(left, bound, right);
    }
    BoundExpression BindCall(GenericExpressionSyntax e, TypeSymbol? expectedType)
    {
        var target = (ExpressionSyntax)e.Children[0];
        ImmutableArray<TypeSymbol> explicitTypes = [];
        GenericExpressionSyntax? genericTarget = null;
        if (target is GenericExpressionSyntax generic && generic.Kind == SyntaxKind.GenericName)
        {
            genericTarget = generic;
            target = (ExpressionSyntax)generic.Children[0];
            var list = generic.Children.OfType<GenericSyntaxNode>().Single(n => n.Kind == SyntaxKind.TypeArgumentList);
            explicitTypes = list.Children.Where(n => n.Kind is SyntaxKind.TypeClause or SyntaxKind.OptionalType or SyntaxKind.GenericName).Select(BindTypeSyntax).ToImmutableArray();
            if (explicitTypes.Any(t => ReferenceEquals(t, TypeSymbol.Error)))
                return new BoundErrorExpression();
        }
        var argNodes = e.Children.OfType<GenericSyntaxNode>().Where(x => x.Kind == SyntaxKind.Argument).ToArray();
        var args = argNodes.Select(a => (ExpressionSyntax)a.Children.Last()).ToArray();
        var boundArguments = args.Select(BindExpression).ToImmutableArray();
        // Generic value construction is deliberately explicit: a containing generic type
        // must be constructed at the call site, except that an expected constructed enum
        // may provide the containing type for a qualified case name.
        if (genericTarget != null && target is GenericExpressionSyntax { Kind : SyntaxKind.IdentifierExpression })
        {
            var constructed = BindCallType(genericTarget);
            if (constructed is ConstructedTypeSymbol constructedType && !constructedType.GenericDefinition.IsEnum)
                return BindInitializerCall(e, genericTarget, constructedType, argNodes, args, boundArguments);
            if (ReferenceEquals(constructed, TypeSymbol.Error))
                return new BoundErrorExpression();
        }
        if (target is GenericExpressionSyntax enumAccess && enumAccess.Kind == SyntaxKind.MemberAccessExpression && TryResolveEnumType(enumAccess, expectedType, out var enumType))
            return BindEnumCaseCall(e, enumAccess, enumType, argNodes, args, boundArguments);
        if (target is GenericExpressionSyntax mg && mg.Kind == SyntaxKind.MemberAccessExpression)
        {
            var n = (SyntaxToken)mg.Children[2];
            var recv = BindExpression((ExpressionSyntax)mg.Children[0]);
            if (recv.Type is TypeParameterSymbol constrainedParameter)
            {
                var requirements = constrainedParameter.Constraints.OfType<ProtocolConstraint>().SelectMany(c => c.Protocol.Requirements).OfType<ProtocolMethodRequirementSymbol>().Where(r => r.Name == n.Text).ToArray();
                var requirement = requirements.FirstOrDefault(r => r.Parameters.Length == boundArguments.Length && ArgumentsConvert(boundArguments, r.Parameters));
                if (requirement != null)
                {
                    var ba = args.Select((a, i) => ConvertBound(a, boundArguments[i], requirement.Parameters[i].Type)).ToImmutableArray();
                    _sym[mg] = requirement;
                    _sym[e] = requirement;
                    if (requirement.IsThrowing)
                    {
                        RecordTypedErrorUse(e, requirement.ErrorType, _suppressMissingTryDiagnostics);
                        if (!_suppressMissingTryDiagnostics)
                            ReportMissingTry(requirement.Name, n);
                    }
                    return new BoundProtocolRequirementCallExpression(recv, requirement, ba);
                }
                if (requirements.Length > 0)
                    return NoCallableMatch(n, requirements[0].Parameters.Length, args.Length);
                _d.Report(new("MRT2102", DiagnosticSeverity.Error, $"Type parameter '{constrainedParameter.Name}' has no constrained member named '{n.Text}'.", Loc(n)));
                return new BoundErrorExpression();
            }
            if ((recv.Type is NamedTypeSymbol nt || recv.Type is ConstructedTypeSymbol ctt && (nt = ctt.GenericDefinition) != null))
            {
                var definitions = (recv.Type is ConstructedTypeSymbol cm ? cm.Methods : nt.Methods).Where(m => m.Name == n.Text).ToArray();
                if (definitions.Length > 0)
                {
                    var methodCandidates = ApplyMethodTypes(definitions, explicitTypes, n, genericTarget != null, boundArguments, expectedType);
                    var m = methodCandidates.FirstOrDefault(candidate => ArgumentsConvert(boundArguments, candidate.Parameters));
                    if (m == null)
                        return NoCallableMatch(n, definitions[0].Parameters.Length, args.Length);
                    var ba = args.Select((a, i) => ConvertBound(a, boundArguments[i], m.Parameters[i].Type)).ToImmutableArray();
                    var methodCallTypes = genericTarget != null ? explicitTypes : GetConstructedTypeArguments(m);
                    _sym[mg] = m;
                    _sym[e] = m;
                    if (genericTarget != null)
                        _sym[genericTarget] = m;
                    RecordGenericUse(e, m, m.OriginalDefinition, methodCallTypes, genericTarget == null);
                    if (nt.IsStruct && m.IsMutating && recv is BoundVariableExpression { Variable : { IsReadOnly : true } })
                        _d.Report(new("MRT2109", DiagnosticSeverity.Error, $"Mutating method '{m.Name}' requires a mutable receiver.", Loc(n)));
                    if (m.IsThrowing)
                    {
                        RecordTypedErrorUse(e, m.ErrorType, _suppressMissingTryDiagnostics);
                        if (!_suppressMissingTryDiagnostics)
                            ReportMissingTry(m.Name, n);
                    }
                    return new BoundMethodCallExpression(recv, m, ba, methodCallTypes);
                }
                _d.Report(new("MRT2102", DiagnosticSeverity.Error, $"Type '{nt.Name}' has no member named '{n.Text}'.", Loc(n)));
            }
            return new BoundErrorExpression();
        }
        var name = target is GenericExpressionSyntax g && g.Kind == SyntaxKind.IdentifierExpression ? (SyntaxToken)g.Children[0] : null;
        if (name == null)
            return new BoundErrorExpression();
        if (genericTarget == null && _scope.TryLookupType(name.Text, out var constructorType) && constructorType is NamedTypeSymbol ntc)
        {
            if (ntc.IsGeneric)
            {
                _d.Report(new("MRT2309", DiagnosticSeverity.Error, $"Generic type '{ntc.Name}' requires explicit containing-type arguments for construction.", Loc(name)));
                return new BoundErrorExpression();
            }
            return BindInitializerCall(e, target, ntc, argNodes, args, boundArguments);
        }
        var overloads = _scope.LookupFunctions(name.Text);
        if (overloads.IsDefaultOrEmpty)
        {
            _d.Report(new("MRT2001", DiagnosticSeverity.Error, $"Undeclared identifier '{name.Text}'.", Loc(name)));
            return new BoundErrorExpression();
        }
        var candidates = ApplyFunctionTypes(overloads, explicitTypes, name, genericTarget != null, boundArguments, expectedType);
        var f = candidates.FirstOrDefault(candidate => candidate.Parameters.Length == args.Length && ArgumentsConvert(boundArguments, candidate.Parameters));
        if (f == null)
            return NoCallableMatch(name, overloads[0].Parameters.Length, args.Length);
        var boundArgs = args.Select((a, i) => ConvertBound(a, boundArguments[i], f.Parameters[i].Type)).ToImmutableArray();
        if (f.IsThrowing)
        {
            RecordTypedErrorUse(e, f.ErrorType, _suppressMissingTryDiagnostics);
            if (!_suppressMissingTryDiagnostics)
                ReportMissingTry(f.Name, name);
        }
        _sym[e] = f;
        _sym[target] = f;
        if (genericTarget != null)
            _sym[genericTarget] = f;
        var callTypes = genericTarget != null ? explicitTypes : GetConstructedTypeArguments(f);
        RecordGenericUse(e, f, f.OriginalDefinition, callTypes, genericTarget == null);
        return new BoundCallExpression(f, boundArgs, callTypes);
    }
    void RecordTypedErrorUse(SyntaxNode syntax, TypeSymbol? errorType, bool acknowledged) => _typedErrorUses[syntax] = new(ErrorEffect.FromCallable(true, errorType), acknowledged, false, acknowledged, errorType, Loc(syntax));
    void MarkCaught(TextSpan bodySpan, TypeSymbol errorType)
    {
        foreach (var pair in _typedErrorUses.Where(pair => bodySpan.Start <= pair.Key.Span.Start && pair.Key.Span.End <= bodySpan.End).ToArray())
            _typedErrorUses[pair.Key] = pair.Value with { IsCaught = true, IsPropagated = false, DeclaredErrorType = errorType };
    }
    void ReportMissingTry(string name, SyntaxNode location) => _d.Report(new("MRT2190", DiagnosticSeverity.Error, $"Call to throwing callable '{name}' requires try.", Loc(location)));
    void RecordGenericUse(SyntaxNode syntax, Symbol constructed, Symbol original, ImmutableArray<TypeSymbol> arguments, bool inferred)
    {
        if (arguments.IsDefaultOrEmpty)
            return;
        _genericUses[syntax] = new(original, constructed, arguments, inferred, Loc(syntax));
    }
    TypeSymbol BindCallType(GenericExpressionSyntax generic)
    {
        var identifierExpression = (GenericExpressionSyntax)generic.Children[0];
        var identifier = (SyntaxToken)identifierExpression.Children[0];
        var arguments = generic.Children.OfType<GenericSyntaxNode>().Single(node => node.Kind == SyntaxKind.TypeArgumentList);
        return BindTypeSyntax(new GenericSyntaxNode(SyntaxKind.GenericName, identifier, arguments));
    }
    BoundExpression BindInitializerCall(GenericExpressionSyntax call, SyntaxNode target, TypeSymbol type, GenericSyntaxNode[] argumentNodes, ExpressionSyntax[] arguments, ImmutableArray<BoundExpression> boundArguments)
    {
        var initializers = type switch { ConstructedTypeSymbol constructed => constructed.Initializers, NamedTypeSymbol named => named.Initializers,
                                         _ => [] };
        var sameShape = initializers.Where(candidate => candidate.Parameters.Length == arguments.Length && LabelsMatch(argumentNodes, candidate.Parameters)).ToArray();
        var initializer = sameShape.FirstOrDefault(candidate => ArgumentsConvert(boundArguments, candidate.Parameters)) ?? sameShape.FirstOrDefault();
        if (initializer == null)
        {
            var arity = initializers.FirstOrDefault(candidate => candidate.Parameters.Length == arguments.Length);
            if (arity != null && !LabelsMatch(argumentNodes, arity.Parameters))
                ReportLabelMismatch(argumentNodes, arity.Parameters);
            else
            {
                var expected = initializers.FirstOrDefault()?.Parameters.Length ?? 0;
                _d.Report(new("MRT2111", DiagnosticSeverity.Error, $"Type '{type.Name}' cannot be constructed because no matching initializer exists (expected {expected} arguments, received {arguments.Length}).", Loc(target)));
            }
            return new BoundErrorExpression();
        }
        var converted = arguments.Select((argument, index) => ConvertBound(argument, boundArguments[index], initializer.Parameters[index].Type)).ToImmutableArray();
        _sym[call] = initializer;
        _sym[target] = initializer;
        if (initializer.IsThrowing)
        {
            RecordTypedErrorUse(call, initializer.ErrorType, _suppressMissingTryDiagnostics);
            if (!_suppressMissingTryDiagnostics)
                ReportMissingTry("init", target);
        }
        return new BoundObjectCreationExpression(initializer, converted);
    }
    bool TryResolveEnumType(GenericExpressionSyntax access, TypeSymbol? expectedType, out TypeSymbol enumType)
    {
        enumType = TypeSymbol.Error;
        var receiver = (ExpressionSyntax)access.Children[0];
        if (receiver is GenericExpressionSyntax generic && generic.Kind == SyntaxKind.GenericName)
        {
            var bound = BindCallType(generic);
            if (bound is ConstructedTypeSymbol { GenericDefinition.IsEnum : true })
            {
                enumType = bound;
                return true;
            }
            return false;
        }
        if (receiver is not GenericExpressionSyntax { Kind : SyntaxKind.IdentifierExpression } identifier)
            return false;
        var token = (SyntaxToken)identifier.Children[0];
        if (!_scope.TryLookupType(token.Text, out var definition) || definition is not NamedTypeSymbol { IsEnum : true } named)
            return false;
        if (!named.IsGeneric)
        {
            enumType = named;
            return true;
        }
        if (expectedType is ConstructedTypeSymbol expected && ReferenceEquals(expected.GenericDefinition, named))
        {
            enumType = expected;
            return true;
        }
        _d.Report(new("MRT2311", DiagnosticSeverity.Error, $"Generic enum case '{named.Name}.{((SyntaxToken)access.Children[2]).Text}' requires an expected constructed enum type or explicit type arguments.", Loc(token)));
        return true;
    }
    BoundExpression BindEnumCaseCall(GenericExpressionSyntax call, GenericExpressionSyntax access, TypeSymbol enumType, GenericSyntaxNode[] argumentNodes, ExpressionSyntax[] arguments, ImmutableArray<BoundExpression> boundArguments)
    {
        if (ReferenceEquals(enumType, TypeSymbol.Error))
            return new BoundErrorExpression();
        var name = (SyntaxToken)access.Children[2];
        var cases = enumType is ConstructedTypeSymbol constructed ? constructed.Cases : ((NamedTypeSymbol)enumType).Cases;
        var enumCase = cases.FirstOrDefault(candidate => candidate.Name == name.Text);
        if (enumCase == null)
        {
            _d.Report(new("MRT2102", DiagnosticSeverity.Error, $"Type '{enumType.Name}' has no enum case named '{name.Text}'.", Loc(name)));
            return new BoundErrorExpression();
        }
        if (enumCase.AssociatedValues.Length != arguments.Length)
            return NoCallableMatch(name, enumCase.AssociatedValues.Length, arguments.Length);
        if (!LabelsMatch(argumentNodes, enumCase.AssociatedValues))
        {
            ReportLabelMismatch(argumentNodes, enumCase.AssociatedValues);
            return new BoundErrorExpression();
        }
        var converted = arguments.Select((argument, index) => ConvertBound(argument, boundArguments[index], enumCase.AssociatedValues[index].Type)).ToImmutableArray();
        _sym[call] = enumCase;
        _sym[access] = enumCase;
        return new BoundEnumCaseCreationExpression(enumCase, converted);
    }
    static bool LabelsMatch(GenericSyntaxNode[] arguments, ImmutableArray<ParameterSymbol> parameters) => arguments.Select(ArgumentLabel).SequenceEqual(parameters.Select(parameter => parameter.Label), StringComparer.Ordinal);
    static string? ArgumentLabel(GenericSyntaxNode argument) => argument.Children.Count >= 3 && argument.Children[0] is SyntaxToken token && argument.Children[1].Kind == SyntaxKind.ColonToken ? token.Text : null;
    void ReportLabelMismatch(GenericSyntaxNode[] arguments, ImmutableArray<ParameterSymbol> parameters)
    {
        var index = Enumerable.Range(0, Math.Min(arguments.Length, parameters.Length)).First(i => ArgumentLabel(arguments[i]) != parameters[i].Label);
        var actual = ArgumentLabel(arguments[index]) ?? "_";
        var expected = parameters[index].Label ?? "_";
        _d.Report(new("MRT2310", DiagnosticSeverity.Error, $"Argument {index + 1} has label '{actual}', but '{expected}' is required.", Loc(arguments[index])));
    }
    static bool ArgumentsConvert(ImmutableArray<BoundExpression> arguments, ImmutableArray<ParameterSymbol> parameters) => arguments.Length == parameters.Length && arguments.Select((a, i) => Conversion.Classify(a.Type, parameters[i].Type)).All(c => c.Exists && c.IsImplicit);
    ImmutableArray<TypeSymbol> GetConstructedTypeArguments(FunctionSymbol function)
    {
        if (!function.IsGeneric)
            return [];
        var definition = function.OriginalDefinition;
        return new GenericInferenceEngine().Infer(definition.TypeParameters, definition.Parameters.Select((p, i) => new InferenceEquation(p.Type, function.Parameters[i].Type)).Append(new InferenceEquation(definition.ReturnType, function.ReturnType)), _cancellationToken).TypeArguments;
    }
    ImmutableArray<TypeSymbol> GetConstructedTypeArguments(MethodSymbol method)
    {
        if (method.TypeParameters.IsEmpty)
            return [];
        var definition = method.OriginalDefinition;
        return new GenericInferenceEngine().Infer(definition.TypeParameters, definition.Parameters.Select((p, i) => new InferenceEquation(p.Type, method.Parameters[i].Type)).Append(new InferenceEquation(definition.ReturnType, method.ReturnType)), _cancellationToken).TypeArguments;
    }
    BoundExpression NoCallableMatch(SyntaxToken name, int expected, int actual)
    {
        _d.Report(new("MRT2007", DiagnosticSeverity.Error, $"Function '{name.Text}' expects {expected} arguments, but {actual} were provided.", Loc(name)));
        return new BoundErrorExpression();
    }
    IEnumerable<FunctionSymbol> ApplyFunctionTypes(IEnumerable<FunctionSymbol> callables, ImmutableArray<TypeSymbol> arguments, SyntaxToken location, bool supplied, ImmutableArray<BoundExpression> boundArguments, TypeSymbol? expectedType)
    {
        var definitions = callables.Where(f => f.Parameters.Length == boundArguments.Length).ToArray();
        if (supplied)
            return ApplyExplicitTypes(definitions, arguments, location, true);
        var results = new List<FunctionSymbol>();
        GenericInferenceResult? failure = null;
        foreach (var f in definitions)
        {
            if (!f.IsGeneric)
            {
                results.Add(f);
                continue;
            }
            var inference = Infer(f.TypeParameters, f.Parameters, boundArguments, f.ReturnType, expectedType);
            if (inference.Succeeded)
            {
                _genericCallableConstructions.Add(new(f.Name, f.TypeParameters, inference.TypeArguments, Loc(location)));
                results.Add(new TypeSubstitution(inference.Substitutions, _genericTypes).Substitute(f));
            }
            else
                failure ??= inference;
        }
        if (results.Count == 0 && failure != null)
            ReportInferenceFailure(failure, location);
        return results;
    }
    IEnumerable<MethodSymbol> ApplyMethodTypes(IEnumerable<MethodSymbol> callables, ImmutableArray<TypeSymbol> arguments, SyntaxToken location, bool supplied, ImmutableArray<BoundExpression> boundArguments, TypeSymbol? expectedType)
    {
        var definitions = callables.Where(m => m.Parameters.Length == boundArguments.Length).ToArray();
        if (supplied)
            return ApplyExplicitTypes(definitions, arguments, location, true);
        var results = new List<MethodSymbol>();
        GenericInferenceResult? failure = null;
        foreach (var m in definitions)
        {
            if (m.TypeParameters.IsEmpty)
            {
                results.Add(m);
                continue;
            }
            var inference = Infer(m.TypeParameters, m.Parameters, boundArguments, m.ReturnType, expectedType);
            if (inference.Succeeded)
            {
                _genericCallableConstructions.Add(new(m.Name, m.TypeParameters, inference.TypeArguments, Loc(location)));
                results.Add(new TypeSubstitution(inference.Substitutions, _genericTypes).Substitute(m, m.ContainingType));
            }
            else
                failure ??= inference;
        }
        if (results.Count == 0 && failure != null)
            ReportInferenceFailure(failure, location);
        return results;
    }
    GenericInferenceResult Infer(ImmutableArray<TypeParameterSymbol> typeParameters, ImmutableArray<ParameterSymbol> parameters, ImmutableArray<BoundExpression> arguments, TypeSymbol returnType, TypeSymbol? expectedType)
    {
        var equations = parameters.Select((p, i) => new InferenceEquation(p.Type, arguments[i].Type)).ToList();
        var argumentResult = new GenericInferenceEngine().Infer(typeParameters, equations, _cancellationToken);
        if (argumentResult.Succeeded || expectedType == null || !argumentResult.Conflicts.IsEmpty)
            return argumentResult;
        equations.Add(new(returnType, expectedType, InferenceSource.ExpectedType));
        return new GenericInferenceEngine().Infer(typeParameters, equations, _cancellationToken);
    }
    void ReportInferenceFailure(GenericInferenceResult result, SyntaxToken location)
    {
        foreach (var conflict in result.Conflicts)
            _d.Report(new("MRT2307", DiagnosticSeverity.Error, $"Conflicting types were inferred for '{conflict.Parameter.Name}': {string.Join(", ", conflict.Candidates.Select(t => t.Name))}.", Loc(location)));
        foreach (var unresolved in result.UnresolvedParameters)
            _d.Report(new("MRT2306", DiagnosticSeverity.Error, $"Type parameter '{unresolved.Name}' could not be inferred; specify explicit type arguments.", Loc(location)));
    }
    IEnumerable<FunctionSymbol> ApplyExplicitTypes(IEnumerable<FunctionSymbol> callables, ImmutableArray<TypeSymbol> arguments, SyntaxToken location, bool supplied)
    {
        var definitions = callables.ToArray();
        if (!supplied)
            return definitions;
        if (definitions.All(f => !f.IsGeneric))
        {
            _d.Report(new("MRT2308", DiagnosticSeverity.Error, $"Explicit type arguments cannot be supplied to nongeneric callable '{location.Text}'.", Loc(location)));
            return [];
        }
        var arity = definitions.Where(f => f.IsGeneric && f.TypeParameters.Length == arguments.Length).ToArray();
        if (arity.Length == 0)
        {
            var expected = definitions.First(f => f.IsGeneric).TypeParameters.Length;
            _d.Report(new("MRT2309", DiagnosticSeverity.Error, $"Generic callable '{location.Text}' expects {expected} type arguments, but {arguments.Length} were provided.", Loc(location)));
            return [];
        }
        return arity.Select(f =>
                            { _genericCallableConstructions.Add(new(f.Name, f.TypeParameters, arguments, Loc(location))); return new TypeSubstitution(f.TypeParameters.Zip(arguments).ToImmutableDictionary(pair => pair.First, pair => pair.Second), _genericTypes).Substitute(f); });
    }
    IEnumerable<MethodSymbol> ApplyExplicitTypes(IEnumerable<MethodSymbol> callables, ImmutableArray<TypeSymbol> arguments, SyntaxToken location, bool supplied)
    {
        var definitions = callables.ToArray();
        if (!supplied)
            return definitions;
        if (definitions.All(m => m.TypeParameters.IsEmpty))
        {
            _d.Report(new("MRT2308", DiagnosticSeverity.Error, $"Explicit type arguments cannot be supplied to nongeneric callable '{location.Text}'.", Loc(location)));
            return [];
        }
        var arity = definitions.Where(m => m.TypeParameters.Length == arguments.Length && m.TypeParameters.Length > 0).ToArray();
        if (arity.Length == 0)
        {
            var expected = definitions.First(m => m.TypeParameters.Length > 0).TypeParameters.Length;
            _d.Report(new("MRT2309", DiagnosticSeverity.Error, $"Generic callable '{location.Text}' expects {expected} type arguments, but {arguments.Length} were provided.", Loc(location)));
            return [];
        }
        return arity.Select(m =>
                            { _genericCallableConstructions.Add(new(m.Name, m.TypeParameters, arguments, Loc(location))); return new TypeSubstitution(m.TypeParameters.Zip(arguments).ToImmutableDictionary(pair => pair.First, pair => pair.Second), _genericTypes).Substitute(m, m.ContainingType); });
    }
}

internal static class ConstantFolder
{
    public static BoundConstant? Unary(BoundUnaryOperator op, BoundConstant? v)
    {
        if (v == null)
            return null;
        try
        {
            return op.Kind switch { BoundUnaryOperatorKind.Identity => v, BoundUnaryOperatorKind.Negation when v.Value is int i => new(-i), BoundUnaryOperatorKind.Negation when v.Value is long i => IntConstant(-i), BoundUnaryOperatorKind.Negation when v.Value is double d => new(-d), BoundUnaryOperatorKind.LogicalNegation when v.Value is bool b => new(!b),
                                    _ => null };
        }
        catch
        {
            return null;
        }
    }
    public static BoundConstant? Binary(BoundConstant? l, BoundBinaryOperator op, BoundConstant? r)
    {
        if (l == null || r == null)
            return null;
        try
        {
            if (IsIntegral(l.Value) && IsIntegral(r.Value))
            {
                var a = Convert.ToInt64(l.Value);
                var b = Convert.ToInt64(r.Value);
                return op.Kind switch { BoundBinaryOperatorKind.Addition => IntConstant(a + b), BoundBinaryOperatorKind.Subtraction => IntConstant(a - b), BoundBinaryOperatorKind.Multiplication => IntConstant(a * b), BoundBinaryOperatorKind.Division when b !=
                                                                                                                                                                                                                             0 => IntConstant(a / b),
                                        _ => null };
            }
            return (l.Value, r.Value, op.Kind) switch { (double a, double b, BoundBinaryOperatorKind.Addition) => new(a + b), (double a, double b, BoundBinaryOperatorKind.Subtraction) => new(a - b), (double a, double b, BoundBinaryOperatorKind.Multiplication) => new(a * b), (double a, double b, BoundBinaryOperatorKind.Division)when b !=
                                                                                                                                                                                                                                                                                       0 => new(a / b),
                                                        (string a, string b, BoundBinaryOperatorKind.Addition) => new(a + b), (bool a, bool b, BoundBinaryOperatorKind.LogicalAnd) => new(a && b), (bool a, bool b, BoundBinaryOperatorKind.LogicalOr) => new(a || b), (_, _, BoundBinaryOperatorKind.Equals) => new(Equals(l.Value, r.Value)), (_, _, BoundBinaryOperatorKind.NotEquals) => new(!Equals(l.Value, r.Value)),
                                                        _ => null };
        }
        catch
        {
            return null;
        }
    }
    static bool IsIntegral(object? value) => value is int or long;
    static BoundConstant IntConstant(long value) => value is >= int.MinValue and <= int.MaxValue ? new((int)value) : new(value);
}
