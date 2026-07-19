using Martin.Compiler.Text;
using Martin.Compiler.Diagnostics;
namespace Martin.Compiler.Syntax;

internal sealed class Parser
{
    readonly SourceText _text;
    readonly SyntaxToken[] _tokens;
    readonly CancellationToken _cancellationToken;
    int _position;
    public DiagnosticBag Diagnostics { get; } = [];
    public Parser(SourceText text, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        _text = text;
        var l = new Lexer(text, cancellationToken);
        var toks = new List<SyntaxToken>();
        SyntaxToken t;
        do
        {
            t = l.Lex();
            if (t.Kind != SyntaxKind.BadToken)
                toks.Add(t);
        } while (t.Kind != SyntaxKind.EndOfFileToken);
        Diagnostics.AddRange(l.Diagnostics);
        _tokens = toks.ToArray();
    }
    SyntaxToken Current => Peek(0);
    SyntaxToken Peek(int o) => _position + o >= _tokens.Length ? _tokens[^1] : _tokens[_position + o];
    SyntaxToken NextToken()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return _tokens[Math.Min(_position++, _tokens.Length - 1)];
    }
    SyntaxToken Match(SyntaxKind k)
    {
        if (Current.Kind == k)
            return NextToken();
        Diagnostics.ReportUnexpectedToken(new(_text, Current.Span), Current.Kind, k);
        return new(k, Current.Position, "", null, true);
    }
    public CompilationUnitSyntax ParseCompilationUnit()
    {
        var ms = new List<MemberSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var s = _position;
            ms.Add(ParseMember());
            if (_position == s)
                NextToken();
        }
        return new(ms, Match(SyntaxKind.EndOfFileToken));
    }
    MemberSyntax ParseMember() => Current.Kind switch { SyntaxKind.FuncKeyword => ParseFunction(), SyntaxKind.StructKeyword => ParseTypeDecl(true), SyntaxKind.ClassKeyword => ParseTypeDecl(false), SyntaxKind.EnumKeyword => ParseEnumDecl(), SyntaxKind.ProtocolKeyword => ParseProtocolDecl(),
                                                        _ => new GlobalStatementSyntax(ParseStatement()) };
    MemberSyntax ParseProtocolDecl()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.ProtocolKeyword), Match(SyntaxKind.IdentifierToken), Match(SyntaxKind.OpenBraceToken) };
        while (Current.Kind != SyntaxKind.CloseBraceToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var req = new List<SyntaxNode>();
            if (Current.Kind == SyntaxKind.StaticKeyword)
                req.Add(NextToken());
            if (Current.Kind == SyntaxKind.MutatingKeyword)
                req.Add(NextToken());
            if (Current.Kind == SyntaxKind.FuncKeyword)
            {
                req.Add(NextToken());
                req.Add(Match(SyntaxKind.IdentifierToken));
                if (Current.Kind == SyntaxKind.LessToken)
                    req.Add(ParseTypeParameterList());
                req.Add(ParseParameterList());
                if (Current.Kind == SyntaxKind.ThrowsKeyword)
                    req.Add(new GenericSyntaxNode(SyntaxKind.ThrowsClause, NextToken(), ParseType()));
                if (Current.Kind == SyntaxKind.ArrowToken)
                    req.Add(new GenericSyntaxNode(SyntaxKind.ReturnTypeClause, NextToken(), ParseType()));
                if (Current.Kind == SyntaxKind.SemicolonToken)
                    req.Add(NextToken());
                kids.Add(new GenericSyntaxNode(SyntaxKind.ProtocolMethodRequirement, req.ToArray()));
            }
            else if (Current.Kind is SyntaxKind.LetKeyword or SyntaxKind.VarKeyword)
            {
                req.Add(NextToken());
                req.Add(Match(SyntaxKind.IdentifierToken));
                req.Add(Match(SyntaxKind.ColonToken));
                req.Add(ParseType());
                if (Current.Kind == SyntaxKind.SemicolonToken)
                    req.Add(NextToken());
                kids.Add(new GenericSyntaxNode(SyntaxKind.ProtocolPropertyRequirement, req.ToArray()));
            }
            else
            {
                kids.Add(ParseStatement());
            }
        }
        kids.Add(Match(SyntaxKind.CloseBraceToken));
        return new GenericMemberSyntax(SyntaxKind.ProtocolDeclaration, kids.ToArray());
    }
    MemberSyntax ParseEnumDecl()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.EnumKeyword), Match(SyntaxKind.IdentifierToken) };
        if (Current.Kind == SyntaxKind.LessToken)
            kids.Add(ParseTypeParameterList());
        if (Current.Kind == SyntaxKind.ColonToken)
            kids.Add(ParseConformanceClause());
        kids.Add(Match(SyntaxKind.OpenBraceToken));
        while (Current.Kind != SyntaxKind.CloseBraceToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var ck = Match(SyntaxKind.CaseKeyword);
            var name = Match(SyntaxKind.IdentifierToken);
            var list = new List<SyntaxNode> { ck, name };
            if (Current.Kind == SyntaxKind.OpenParenthesisToken)
                list.Add(ParseEnumParameterList());
            if (Current.Kind == SyntaxKind.SemicolonToken)
                list.Add(NextToken());
            kids.Add(new GenericSyntaxNode(SyntaxKind.EnumCaseDeclaration, list.ToArray()));
        }
        kids.Add(Match(SyntaxKind.CloseBraceToken));
        return new GenericMemberSyntax(SyntaxKind.EnumDeclaration, kids.ToArray());
    }
    MemberSyntax ParseTypeDecl(bool isStruct)
    {
        var kids = new List<SyntaxNode> { Match(isStruct ? SyntaxKind.StructKeyword : SyntaxKind.ClassKeyword), Match(SyntaxKind.IdentifierToken) };
        if (Current.Kind == SyntaxKind.LessToken)
            kids.Add(ParseTypeParameterList());
        if (Current.Kind == SyntaxKind.ColonToken)
            kids.Add(ParseConformanceClause());
        kids.Add(Match(SyntaxKind.OpenBraceToken));
        while (Current.Kind != SyntaxKind.CloseBraceToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var start = _position;
            if (Current.Kind is SyntaxKind.LetKeyword or SyntaxKind.VarKeyword)
                kids.Add(ParseProperty());
            else if (Current.Kind == SyntaxKind.InitKeyword)
                kids.Add(ParseInitializer());
            else if (Current.Kind is SyntaxKind.FuncKeyword or SyntaxKind.MutatingKeyword)
                kids.Add(ParseMethod());
            else
                kids.Add(ParseStatement());
            if (_position == start)
                NextToken();
        }
        kids.Add(Match(SyntaxKind.CloseBraceToken));
        return new GenericMemberSyntax(isStruct ? SyntaxKind.StructDeclaration : SyntaxKind.ClassDeclaration, kids.ToArray());
    }
    GenericSyntaxNode ParseConformanceClause()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.ColonToken) };
        while (Current.Kind != SyntaxKind.OpenBraceToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            kids.Add(ParseType());
            if (Current.Kind == SyntaxKind.CommaToken)
                kids.Add(NextToken());
            else
                break;
        }
        return new GenericSyntaxNode(SyntaxKind.ProtocolConformanceClause, kids.ToArray());
    }

    GenericSyntaxNode ParseTypeParameterList()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.LessToken) };
        while (Current.Kind != SyntaxKind.GreaterToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var pk = new List<SyntaxNode> { Match(SyntaxKind.IdentifierToken) };
            if (Current.Kind == SyntaxKind.ColonToken)
            {
                pk.Add(NextToken());
                pk.Add(ParseType());
            }
            kids.Add(new GenericSyntaxNode(SyntaxKind.TypeParameter, pk.ToArray()));
            if (Current.Kind == SyntaxKind.CommaToken)
                kids.Add(NextToken());
            else
                break;
        }
        kids.Add(Match(SyntaxKind.GreaterToken));
        return new GenericSyntaxNode(SyntaxKind.TypeParameterList, kids.ToArray());
    }
    GenericSyntaxNode ParseTypeArgumentList()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.LessToken) };
        while (Current.Kind != SyntaxKind.GreaterToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            kids.Add(ParseType());
            if (Current.Kind != SyntaxKind.CommaToken)
                break;
            kids.Add(NextToken());
            if (Current.Kind == SyntaxKind.GreaterToken)
                break;
        }
        kids.Add(Match(SyntaxKind.GreaterToken));
        return new GenericSyntaxNode(SyntaxKind.TypeArgumentList, kids.ToArray());
    }
    GenericSyntaxNode ParseProperty()
    {
        var kids = new List<SyntaxNode> { NextToken(), Match(SyntaxKind.IdentifierToken), Match(SyntaxKind.ColonToken), ParseType() };
        if (Current.Kind == SyntaxKind.EqualToken)
        {
            kids.Add(NextToken());
            kids.Add(ParseExpression());
        }
        if (Current.Kind == SyntaxKind.SemicolonToken)
            kids.Add(NextToken());
        return new GenericSyntaxNode(SyntaxKind.PropertyDeclaration, kids.ToArray());
    }
    GenericSyntaxNode ParseMethod()
    {
        var kids = new List<SyntaxNode>();
        if (Current.Kind == SyntaxKind.MutatingKeyword)
            kids.Add(NextToken());
        kids.Add(Match(SyntaxKind.FuncKeyword));
        kids.Add(Match(SyntaxKind.IdentifierToken));
        if (Current.Kind == SyntaxKind.LessToken)
            kids.Add(ParseTypeParameterList());
        kids.Add(ParseParameterList());
        if (Current.Kind == SyntaxKind.ThrowsKeyword)
            kids.Add(new GenericSyntaxNode(SyntaxKind.ThrowsClause, NextToken(), ParseType()));
        if (Current.Kind == SyntaxKind.ArrowToken)
            kids.Add(new GenericSyntaxNode(SyntaxKind.ReturnTypeClause, NextToken(), ParseType()));
        kids.Add(Current.Kind == SyntaxKind.OpenBraceToken ? ParseBlock() : new GenericStatementSyntax(SyntaxKind.BlockStatement, Match(SyntaxKind.OpenBraceToken), Match(SyntaxKind.CloseBraceToken)));
        return new GenericSyntaxNode(SyntaxKind.MethodDeclaration, kids.ToArray());
    }
    GenericSyntaxNode ParseInitializer()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.InitKeyword), ParseParameterList() };
        if (Current.Kind == SyntaxKind.ThrowsKeyword)
            kids.Add(new GenericSyntaxNode(SyntaxKind.ThrowsClause, NextToken(), ParseType()));
        kids.Add(Current.Kind == SyntaxKind.OpenBraceToken ? ParseBlock() : new GenericStatementSyntax(SyntaxKind.BlockStatement, Match(SyntaxKind.OpenBraceToken), Match(SyntaxKind.CloseBraceToken)));
        return new GenericSyntaxNode(SyntaxKind.InitializerDeclaration, kids.ToArray());
    }
    MemberSyntax ParseFunction()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.FuncKeyword), Match(SyntaxKind.IdentifierToken) };
        if (Current.Kind == SyntaxKind.LessToken)
            kids.Add(ParseTypeParameterList());
        kids.Add(ParseParameterList());
        if (Current.Kind == SyntaxKind.ThrowsKeyword)
            kids.Add(new GenericSyntaxNode(SyntaxKind.ThrowsClause, NextToken(), ParseType()));
        if (Current.Kind == SyntaxKind.ArrowToken)
            kids.Add(new GenericSyntaxNode(SyntaxKind.ReturnTypeClause, NextToken(), ParseType()));
        kids.Add(Current.Kind == SyntaxKind.OpenBraceToken ? ParseBlock() : new GenericStatementSyntax(SyntaxKind.BlockStatement, Match(SyntaxKind.OpenBraceToken), Match(SyntaxKind.CloseBraceToken)));
        return new GenericMemberSyntax(SyntaxKind.FunctionDeclaration, kids.ToArray());
    }
    GenericSyntaxNode ParseEnumParameterList()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.OpenParenthesisToken) };
        var ordinal = 0;
        while (Current.Kind != SyntaxKind.CloseParenthesisToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var pk = new List<SyntaxNode>();
            if (Current.Kind == SyntaxKind.IdentifierToken && Peek(1).Kind == SyntaxKind.ColonToken)
            {
                pk.Add(NextToken());
                pk.Add(NextToken());
                pk.Add(ParseType());
            }
            else
            {
                pk.Add(new SyntaxToken(SyntaxKind.IdentifierToken, Current.Position, "value" + ordinal, null, true));
                pk.Add(ParseType());
            }
            kids.Add(new GenericSyntaxNode(SyntaxKind.Parameter, pk.ToArray()));
            ordinal++;
            if (Current.Kind == SyntaxKind.CommaToken)
                kids.Add(NextToken());
            else
                break;
        }
        kids.Add(Match(SyntaxKind.CloseParenthesisToken));
        return new(SyntaxKind.ParameterList, kids.ToArray());
    }
    GenericSyntaxNode ParseParameterList()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.OpenParenthesisToken) };
        while (Current.Kind != SyntaxKind.CloseParenthesisToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            kids.Add(ParseParameter());
            if (Current.Kind == SyntaxKind.CommaToken)
                kids.Add(NextToken());
            else
                break;
        }
        kids.Add(Match(SyntaxKind.CloseParenthesisToken));
        return new(SyntaxKind.ParameterList, kids.ToArray());
    }
    GenericSyntaxNode ParseParameter()
    {
        var kids = new List<SyntaxNode>();
        kids.Add(Match(SyntaxKind.IdentifierToken));
        if (Current.Kind == SyntaxKind.IdentifierToken)
            kids.Add(NextToken());
        kids.Add(Match(SyntaxKind.ColonToken));
        kids.Add(ParseType());
        return new(SyntaxKind.Parameter, kids.ToArray());
    }
    GenericSyntaxNode ParseType()
    {
        SyntaxNode type = Match(SyntaxKind.IdentifierToken);
        if (Current.Kind == SyntaxKind.LessToken)
            type = new GenericSyntaxNode(SyntaxKind.GenericName, type, ParseTypeArgumentList());
        while (Current.Kind == SyntaxKind.QuestionToken)
            type = new GenericSyntaxNode(SyntaxKind.OptionalType, type, NextToken());
        return (GenericSyntaxNode)(type is GenericSyntaxNode g ? g : new GenericSyntaxNode(SyntaxKind.TypeClause, type));
    }
    StatementSyntax ParseStatement() => Current.Kind switch { SyntaxKind.OpenBraceToken => ParseBlock(), SyntaxKind.LetKeyword or SyntaxKind.VarKeyword => ParseVar(), SyntaxKind.IfKeyword => ParseIf(), SyntaxKind.WhileKeyword => new GenericStatementSyntax(SyntaxKind.WhileStatement, NextToken(), ParseExpression(), ParseBlock()), SyntaxKind.ReturnKeyword => ParseReturn(), SyntaxKind.ThrowKeyword => ParseThrow(), SyntaxKind.DoKeyword => ParseDoCatch(), SyntaxKind.SwitchKeyword => ParseSwitch(),
                                                              _ => ParseExprStmt() };
    StatementSyntax ParseThrow()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.ThrowKeyword), ParseExpression() };
        if (Current.Kind == SyntaxKind.SemicolonToken)
            kids.Add(NextToken());
        return new GenericStatementSyntax(SyntaxKind.ThrowStatement, kids.ToArray());
    }
    StatementSyntax ParseDoCatch()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.DoKeyword), ParseBlock() };
        while (Current.Kind == SyntaxKind.CatchKeyword)
        {
            var ck = new List<SyntaxNode> { Match(SyntaxKind.CatchKeyword), ParsePattern(PatternParseContext.CatchClause), ParseBlock() };
            kids.Add(new GenericSyntaxNode(SyntaxKind.CatchClause, ck.ToArray()));
        }
        return new GenericStatementSyntax(SyntaxKind.DoCatchStatement, kids.ToArray());
    }
    StatementSyntax ParseSwitch()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.SwitchKeyword), ParseExpression(), Match(SyntaxKind.OpenBraceToken) };
        while (Current.Kind != SyntaxKind.CloseBraceToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var caseStart = _position;
            var ck = Current.Kind == SyntaxKind.DefaultKeyword ? NextToken() : Match(SyntaxKind.CaseKeyword);
            var list = new List<SyntaxNode> { ck };
            if (ck.Kind != SyntaxKind.DefaultKeyword)
                list.Add(ParsePattern(PatternParseContext.SwitchCase));
            list.Add(Match(SyntaxKind.ColonToken));
            while (!IsSwitchCaseBoundary(Current.Kind))
            {
                var statementStart = _position;
                list.Add(ParseStatement());
                if (_position == statementStart)
                    NextToken();
            }
            kids.Add(new GenericSyntaxNode(SyntaxKind.SwitchCase, list.ToArray()));
            if (_position == caseStart)
                NextToken();
        }
        kids.Add(Match(SyntaxKind.CloseBraceToken));
        return new GenericStatementSyntax(SyntaxKind.SwitchStatement, kids.ToArray());
    }

    enum PatternParseContext
    {
        SwitchCase,
        CatchClause,
        AssociatedValue
    }
    const int MaxPatternNestingDepth = 256;

    PatternSyntax ParsePattern(PatternParseContext context, int nestingDepth = 0)
    {
        if (nestingDepth >= MaxPatternNestingDepth)
            return MissingPattern();
        if (Current.Kind == SyntaxKind.IdentifierToken && Current.Text == "_")
            return new WildcardPatternSyntax(NextToken());
        if (Current.Kind == SyntaxKind.LetKeyword)
            return new ValueBindingPatternSyntax(NextToken(), Match(SyntaxKind.IdentifierToken));
        if (Current.Kind == SyntaxKind.NilKeyword)
            return new NilPatternSyntax(NextToken());
        if (Current.Kind is SyntaxKind.IntegerLiteralToken or SyntaxKind.FloatingPointLiteralToken or SyntaxKind.StringLiteralToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword)
            return new LiteralPatternSyntax(NextToken());
        if (Current.Kind == SyntaxKind.DotToken)
        {
            var dot = NextToken();
            var name = Match(SyntaxKind.IdentifierToken);
            if (name.Text == "some" && Current.Kind == SyntaxKind.OpenParenthesisToken)
            {
                var open = NextToken();
                var value = ParsePattern(PatternParseContext.AssociatedValue, nestingDepth + 1);
                return new OptionalSomePatternSyntax(dot, name, open, value, Match(SyntaxKind.CloseParenthesisToken));
            }
            return new EnumCasePatternSyntax(null, dot, name, ParsePatternArguments(nestingDepth));
        }
        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            var identifier = NextToken();
            if (Current.Kind == SyntaxKind.DotToken)
            {
                var dot = NextToken();
                return new EnumCasePatternSyntax(identifier, dot, Match(SyntaxKind.IdentifierToken), ParsePatternArguments(nestingDepth));
            }
            // A bare name deliberately stays an identifier; whether it denotes an enum case is semantic.
            if (Current.Kind != SyntaxKind.OpenParenthesisToken)
                return new IdentifierPatternSyntax(identifier);
            return new EnumCasePatternSyntax(null, null, identifier, ParsePatternArguments(nestingDepth));
        }
        return MissingPattern();
    }

    PatternArgumentListSyntax? ParsePatternArguments(int nestingDepth)
    {
        if (Current.Kind != SyntaxKind.OpenParenthesisToken)
            return null;
        var open = NextToken();
        var args = new List<PatternSyntax>();
        var commas = new List<SyntaxToken>();
        while (!IsPatternArgumentBoundary(Current.Kind))
        {
            var argumentStart = _position;
            args.Add(ParsePattern(PatternParseContext.AssociatedValue, nestingDepth + 1));
            if (_position == argumentStart)
                break;
            if (Current.Kind != SyntaxKind.CommaToken)
                break;
            commas.Add(NextToken());
            // A trailing comma is recovered by the missing pattern only when another token follows it.
            if (IsPatternArgumentBoundary(Current.Kind))
                break;
        }
        return new PatternArgumentListSyntax(open, args, commas, Match(SyntaxKind.CloseParenthesisToken));
    }

    PatternSyntax MissingPattern() => new IdentifierPatternSyntax(Match(SyntaxKind.IdentifierToken));
    static bool IsPatternArgumentBoundary(SyntaxKind kind) => kind is SyntaxKind.CloseParenthesisToken or SyntaxKind.ColonToken or SyntaxKind.CaseKeyword or SyntaxKind.DefaultKeyword or SyntaxKind.CloseBraceToken or SyntaxKind.EndOfFileToken;
    static bool IsSwitchCaseBoundary(SyntaxKind kind) => kind is SyntaxKind.CaseKeyword or SyntaxKind.DefaultKeyword or SyntaxKind.CloseBraceToken or SyntaxKind.EndOfFileToken;
    StatementSyntax ParseBlock()
    {
        var kids = new List<SyntaxNode> { Match(SyntaxKind.OpenBraceToken) };
        while (Current.Kind != SyntaxKind.CloseBraceToken && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var s = _position;
            kids.Add(ParseStatement());
            if (_position == s)
                NextToken();
        }
        kids.Add(Match(SyntaxKind.CloseBraceToken));
        return new GenericStatementSyntax(SyntaxKind.BlockStatement, kids.ToArray());
    }
    StatementSyntax ParseVar()
    {
        var kids = new List<SyntaxNode> { NextToken(), Match(SyntaxKind.IdentifierToken) };
        if (Current.Kind == SyntaxKind.ColonToken)
            kids.Add(new GenericSyntaxNode(SyntaxKind.TypeClause, NextToken(), ParseType()));
        if (Current.Kind == SyntaxKind.EqualToken)
        {
            kids.Add(NextToken());
            kids.Add(ParseExpression());
        }
        if (Current.Kind == SyntaxKind.SemicolonToken)
            kids.Add(NextToken());
        return new GenericStatementSyntax(SyntaxKind.VariableDeclarationStatement, kids.ToArray());
    }
    StatementSyntax ParseIf()
    {
        if (Peek(1).Kind == SyntaxKind.LetKeyword)
        {
            var k = new List<SyntaxNode> { Match(SyntaxKind.IfKeyword), Match(SyntaxKind.LetKeyword), Match(SyntaxKind.IdentifierToken), Match(SyntaxKind.EqualToken), ParseExpression(), ParseBlock() };
            if (Current.Kind == SyntaxKind.ElseKeyword)
            {
                var e = NextToken();
                k.Add(new GenericSyntaxNode(SyntaxKind.ElseClause, e, Current.Kind == SyntaxKind.IfKeyword ? ParseIf() : ParseBlock()));
            }
            return new GenericStatementSyntax(SyntaxKind.IfLetStatement, k.ToArray());
        }
        var kids = new List<SyntaxNode> { Match(SyntaxKind.IfKeyword), ParseExpression(), ParseBlock() };
        if (Current.Kind == SyntaxKind.ElseKeyword)
        {
            var e = NextToken();
            kids.Add(new GenericSyntaxNode(SyntaxKind.ElseClause, e, Current.Kind == SyntaxKind.IfKeyword ? ParseIf() : ParseBlock()));
        }
        return new GenericStatementSyntax(SyntaxKind.IfStatement, kids.ToArray());
    }
    StatementSyntax ParseReturn()
    {
        var kids = new List<SyntaxNode> { NextToken() };
        if (SyntaxFacts.CanStartExpression(Current.Kind))
            kids.Add(ParseExpression());
        if (Current.Kind == SyntaxKind.SemicolonToken)
            kids.Add(NextToken());
        return new GenericStatementSyntax(SyntaxKind.ReturnStatement, kids.ToArray());
    }
    StatementSyntax ParseExprStmt()
    {
        var e = ParseExpression();
        if (Current.Kind == SyntaxKind.SemicolonToken)
            return new GenericStatementSyntax(SyntaxKind.ExpressionStatement, e, NextToken());
        return new GenericStatementSyntax(SyntaxKind.ExpressionStatement, e);
    }
    ExpressionSyntax ParseExpression() => ParseAssignment();
    ExpressionSyntax ParseAssignment()
    {
        var left = ParseBinary();
        if (Current.Kind == SyntaxKind.EqualToken)
        {
            return new GenericExpressionSyntax(left.Kind == SyntaxKind.MemberAccessExpression ? SyntaxKind.PropertyAssignmentExpression : SyntaxKind.AssignmentExpression, left, NextToken(), ParseAssignment());
        }
        return left;
    }
    ExpressionSyntax ParseBinary(int parent = 0)
    {
        ExpressionSyntax left;
        var unary = SyntaxFacts.GetUnaryOperatorPrecedence(Current.Kind);
        if (unary != 0 && unary >= parent)
            left = new GenericExpressionSyntax(SyntaxKind.UnaryExpression, NextToken(), ParseBinary(unary));
        else
            left = ParseCall();
        while (true)
        {
            var prec = SyntaxFacts.GetBinaryOperatorPrecedence(Current.Kind);
            if (prec == 0 || prec <= parent)
                break;
            left = new GenericExpressionSyntax(SyntaxKind.BinaryExpression, left, NextToken(), ParseBinary(prec));
        }
        return left;
    }
    ExpressionSyntax ParseCall()
    {
        var expr = ParsePrimary();
        while (Current.Kind == SyntaxKind.DotToken || Current.Kind == SyntaxKind.OpenParenthesisToken || IsExplicitCallTypeArgumentList())
        {
            if (Current.Kind == SyntaxKind.DotToken)
            {
                expr = new GenericExpressionSyntax(SyntaxKind.MemberAccessExpression, expr, NextToken(), Match(SyntaxKind.IdentifierToken));
                continue;
            }
            if (Current.Kind == SyntaxKind.LessToken)
            {
                expr = new GenericExpressionSyntax(SyntaxKind.GenericName, expr, ParseTypeArgumentList());
                continue;
            }
            var kids = new List<SyntaxNode> { expr, Match(SyntaxKind.OpenParenthesisToken) };
            while (Current.Kind != SyntaxKind.CloseParenthesisToken && Current.Kind != SyntaxKind.EndOfFileToken)
            {
                var ak = new List<SyntaxNode>();
                if (Current.Kind == SyntaxKind.IdentifierToken && Peek(1).Kind == SyntaxKind.ColonToken)
                {
                    ak.Add(NextToken());
                    ak.Add(NextToken());
                }
                ak.Add(ParseExpression());
                kids.Add(new GenericSyntaxNode(SyntaxKind.Argument, ak.ToArray()));
                if (Current.Kind == SyntaxKind.CommaToken)
                    kids.Add(NextToken());
                else
                    break;
            }
            kids.Add(Match(SyntaxKind.CloseParenthesisToken));
            expr = new GenericExpressionSyntax(SyntaxKind.CallExpression, kids.ToArray());
        }
        return expr;
    }
    bool IsExplicitCallTypeArgumentList()
    {
        if (Current.Kind != SyntaxKind.LessToken || Peek(1).Kind != SyntaxKind.IdentifierToken)
            return false;
        var depth = 0;
        for (var offset = 0;; offset++)
        {
            var kind = Peek(offset).Kind;
            if (kind == SyntaxKind.EndOfFileToken)
                return false;
            if (kind == SyntaxKind.LessToken)
                depth++;
            else if (kind == SyntaxKind.GreaterToken)
            {
                depth--;
                if (depth == 0)
                    return Peek(offset + 1).Kind is SyntaxKind.OpenParenthesisToken or SyntaxKind.DotToken;
            }
        }
    }
    ExpressionSyntax ParsePrimary()
    {
        if (Current.Kind == SyntaxKind.TryKeyword)
            return new GenericExpressionSyntax(SyntaxKind.TryExpression, NextToken(), ParseCall());
        if (Current.Kind == SyntaxKind.OpenParenthesisToken)
            return new GenericExpressionSyntax(SyntaxKind.ParenthesizedExpression, NextToken(), ParseExpression(), Match(SyntaxKind.CloseParenthesisToken));
        if (Current.Kind is SyntaxKind.IntegerLiteralToken or SyntaxKind.FloatingPointLiteralToken or SyntaxKind.StringLiteralToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.NilKeyword)
            return new GenericExpressionSyntax(SyntaxKind.LiteralExpression, NextToken());
        if (Current.Kind is SyntaxKind.IdentifierToken or SyntaxKind.SelfKeyword)
            return new GenericExpressionSyntax(SyntaxKind.IdentifierExpression, NextToken());
        Diagnostics.ReportExpectedExpression(new(_text, Current.Span));
        return new GenericExpressionSyntax(SyntaxKind.LiteralExpression, new SyntaxToken(SyntaxKind.IntegerLiteralToken, Current.Position, "", 0, true));
    }
}
public sealed class GenericMemberSyntax(SyntaxKind kind, params SyntaxNode[] children) : MemberSyntax
{
    public override SyntaxKind Kind { get; } = kind;
    public IReadOnlyList<SyntaxNode> Children { get; } = children;
    public override IEnumerable<SyntaxNode> GetChildren() => Children;
}
