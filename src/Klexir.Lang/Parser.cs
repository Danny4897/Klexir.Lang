using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// Recursive-descent parser. Precedence, loosest to tightest: <c>let ... in</c> / <c>if ... then ... else</c>,
/// <c>andThen</c> (sugar over <c>bind</c>, left-associative), comparison (== &lt; &gt; &lt;= &gt;=, non-chaining),
/// additive (+ -), multiplicative (* /), primary.
/// </summary>
public sealed class Parser(IReadOnlyList<Token> tokens)
{
    private int _position;

    private Token Current => tokens[_position];

    public Result<Expr> ParseExpression()
    {
        var expr = ParseTop();
        if (expr.IsFailure)
        {
            return expr;
        }

        return Current.Type == TokenType.Eof
            ? expr
            : Result<Expr>.Failure(Error.Create($"Unexpected token '{Current.Text}' at {Current.Position}."));
    }

    private Result<Expr> ParseTop() =>
        Current.Type switch
        {
            TokenType.Let when Peek(1).Type == TokenType.Rec => ParseLetRec(),
            TokenType.Let => ParseLet(),
            TokenType.If => ParseIf(),
            TokenType.Func => ParseFunc(),
            TokenType.Match => ParseMatch(),
            // map/bind/filter/fold are NOT special-cased here on purpose: ParsePrimary already parses them (they're
            // self-delimiting via their own parens, like a function call), so falling through to the normal
            // precedence chain lets 'fold(...) > 0' or 'map(...) + 1' parse — handling them here too used to shadow
            // that and silently return before any trailing operator got a chance to attach.
            _ => ParseAndThen(),
        };

    private Token Peek(int offset) => tokens[Math.Min(_position + offset, tokens.Count - 1)];

    private Result<Expr> ParseLetRec()
    {
        var header = ParseLetRecHeader();
        if (header.IsFailure)
        {
            return Result<Expr>.Failure(header.Error);
        }

        if (Current.Type != TokenType.In)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'in' at {Current.Position}."));
        }

        _position++;

        var letBody = ParseTop();
        return letBody.IsFailure
            ? letBody
            : Result<Expr>.Success(new LetRecExpr(
                header.Value.Name, header.Value.ParamName, header.Value.ParamType, header.Value.ReturnType,
                header.Value.FunctionBody, letBody.Value));
    }

    /// <summary>
    /// Parses <c>let rec name = func(Type1 p1, Type2 p2, ...): ReturnType => functionBody</c>, up to but not
    /// including <c>in</c> — shared by the in-expression <c>let rec ... in ...</c> form and top-level program
    /// declarations, which don't use <c>in</c> at all. Only <c>p1</c> becomes the recursive binding's own
    /// parameter (the AST only carries one); any further params desugar to ordinary nested <see cref="FunExpr"/>s
    /// wrapping the body, with <paramref name="ReturnType"/> becoming the matching <see cref="FunctionType"/> chain
    /// — <c>func(Int a, Int b): Int => body</c> ends up typed exactly like a hand-nested
    /// <c>func(Int a): Int -> Int => func(Int b): Int => body</c> would, just without that type-annotation gymnastics.
    /// </summary>
    private Result<(string Name, string ParamName, KlexirType ParamType, KlexirType ReturnType, Expr FunctionBody)> ParseLetRecHeader()
    {
        _position++; // 'let'
        _position++; // 'rec'

        if (Current.Type != TokenType.Identifier)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(
                Error.Create($"Expected a function name after 'let rec' at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        if (Current.Type != TokenType.Equals)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(
                Error.Create($"Expected '=' after 'let rec {name}' at {Current.Position}."));
        }

        _position++;

        if (Current.Type != TokenType.Func)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(
                Error.Create($"'let rec' requires a function value at {Current.Position}."));
        }

        _position++; // 'func'

        var parameters = ParseFuncParams();
        if (parameters.IsFailure)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(parameters.Error);
        }

        if (Current.Type != TokenType.Colon)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(Error.Create(
                $"'let rec' functions need an explicit return type — ': Type' after the parameter list — at {Current.Position}."));
        }

        _position++;

        var declaredReturnType = ParseTypeAnnotation();
        if (declaredReturnType.IsFailure)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(declaredReturnType.Error);
        }

        if (Current.Type != TokenType.FatArrow)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(
                Error.Create($"Expected '=>' at {Current.Position}."));
        }

        _position++;

        var functionBody = ParseTop();
        if (functionBody.IsFailure)
        {
            return Result<(string, string, KlexirType, KlexirType, Expr)>.Failure(functionBody.Error);
        }

        var (firstName, firstType) = parameters.Value[0];
        var innerBody = functionBody.Value;
        var effectiveReturnType = declaredReturnType.Value;

        for (var i = parameters.Value.Count - 1; i >= 1; i--)
        {
            innerBody = new FunExpr(parameters.Value[i].Name, parameters.Value[i].Type, innerBody);
            effectiveReturnType = new FunctionType(parameters.Value[i].Type, effectiveReturnType);
        }

        return Result<(string, string, KlexirType, KlexirType, Expr)>.Success(
            (name, firstName, firstType, effectiveReturnType, innerBody));
    }

    /// <summary>
    /// Parses <c>func(Type1 name1, Type2 name2, ...) => body</c> — pure surface sugar over currying, not a new
    /// calling convention: <c>func(Int x, Int y) => x + y</c> builds the exact same nested <see cref="FunExpr"/>
    /// tree as writing <c>func(Int x) => func(Int y) => x + y</c> by hand, so application stays <c>f x y</c>
    /// (never <c>f(x, y)</c>) and every existing curried/partial-application pattern keeps working unchanged.
    /// </summary>
    private Result<Expr> ParseFunc()
    {
        _position++; // 'func'

        var parameters = ParseFuncParams();
        if (parameters.IsFailure)
        {
            return Result<Expr>.Failure(parameters.Error);
        }

        if (Current.Type != TokenType.FatArrow)
        {
            return Result<Expr>.Failure(Error.Create($"Expected '=>' at {Current.Position}."));
        }

        _position++;

        var body = ParseTop();
        if (body.IsFailure)
        {
            return body;
        }

        var expr = body.Value;
        for (var i = parameters.Value.Count - 1; i >= 0; i--)
        {
            expr = new FunExpr(parameters.Value[i].Name, parameters.Value[i].Type, expr);
        }

        return Result<Expr>.Success(expr);
    }

    /// <summary>Parses <c>(Type1 name1, Type2 name2, ...)</c> — type before name, no colon, one or more
    /// comma-separated parameters. Shared by <see cref="ParseFunc"/> and <see cref="ParseLetRecHeader"/>.</summary>
    private Result<List<(string Name, KlexirType Type)>> ParseFuncParams()
    {
        if (Current.Type != TokenType.LParen)
        {
            return Result<List<(string Name, KlexirType Type)>>.Failure(
                Error.Create($"Expected '(' after 'func' at {Current.Position}."));
        }

        _position++;

        var parameters = new List<(string Name, KlexirType Type)>();

        while (true)
        {
            var paramType = ParseTypeAnnotation();
            if (paramType.IsFailure)
            {
                return Result<List<(string Name, KlexirType Type)>>.Failure(paramType.Error);
            }

            if (Current.Type != TokenType.Identifier)
            {
                return Result<List<(string Name, KlexirType Type)>>.Failure(
                    Error.Create($"Expected a parameter name at {Current.Position}."));
            }

            parameters.Add((Current.Text, paramType.Value));
            _position++;

            if (Current.Type != TokenType.Comma)
            {
                break;
            }

            _position++;
        }

        if (Current.Type != TokenType.RParen)
        {
            return Result<List<(string Name, KlexirType Type)>>.Failure(Error.Create($"Expected ')' at {Current.Position}."));
        }

        _position++;

        return Result<List<(string Name, KlexirType Type)>>.Success(parameters);
    }

    private Result<KlexirType> ParseTypeAnnotation()
    {
        if (Current.Type != TokenType.Identifier)
        {
            return Result<KlexirType>.Failure(Error.Create($"Expected a type name at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        switch (name)
        {
            case "Int":
                return Result<KlexirType>.Success(KlexirType.Int);

            case "Bool":
                return Result<KlexirType>.Success(KlexirType.Bool);

            case "String":
                return Result<KlexirType>.Success(KlexirType.String);

            case "List":
                if (Current.Type != TokenType.Less)
                {
                    return Result<KlexirType>.Failure(Error.Create($"Expected '<' after 'List' at {Current.Position}."));
                }

                _position++;

                var listElement = ParseTypeAnnotation();
                if (listElement.IsFailure)
                {
                    return listElement;
                }

                if (Current.Type != TokenType.Greater)
                {
                    return Result<KlexirType>.Failure(Error.Create($"Expected '>' at {Current.Position}."));
                }

                _position++;
                return Result<KlexirType>.Success(new ListType(listElement.Value));

            case "Option":
                if (Current.Type != TokenType.Less)
                {
                    return Result<KlexirType>.Failure(Error.Create($"Expected '<' after 'Option' at {Current.Position}."));
                }

                _position++;

                var element = ParseTypeAnnotation();
                if (element.IsFailure)
                {
                    return element;
                }

                if (Current.Type != TokenType.Greater)
                {
                    return Result<KlexirType>.Failure(Error.Create($"Expected '>' at {Current.Position}."));
                }

                _position++;
                return Result<KlexirType>.Success(new OptionType(element.Value));

            case "Result":
                if (Current.Type != TokenType.Less)
                {
                    return Result<KlexirType>.Failure(Error.Create($"Expected '<' after 'Result' at {Current.Position}."));
                }

                _position++;

                var ok = ParseTypeAnnotation();
                if (ok.IsFailure)
                {
                    return ok;
                }

                if (Current.Type != TokenType.Comma)
                {
                    return Result<KlexirType>.Failure(Error.Create($"Expected ',' at {Current.Position}."));
                }

                _position++;

                var err = ParseTypeAnnotation();
                if (err.IsFailure)
                {
                    return err;
                }

                if (Current.Type != TokenType.Greater)
                {
                    return Result<KlexirType>.Failure(Error.Create($"Expected '>' at {Current.Position}."));
                }

                _position++;
                return Result<KlexirType>.Success(new ResultType(ok.Value, err.Value));

            default:
                // Any other identifier is taken as a reference to a user-declared record type — possibly one this
                // parser hasn't reached the 'record' declaration for yet (e.g. inside that record's own fields, or
                // a function declared before it in program order). RecordType compares equal by name alone, so an
                // empty-Fields placeholder here is indistinguishable from the real, fully-populated one once the
                // type checker resolves it against the environment. A name that's never actually a declared record
                // — a typo, say — isn't caught here; it surfaces later, when something tries to construct it or
                // access a field on it.
                return Result<KlexirType>.Success(new RecordType(name, []));
        }
    }

    private Result<Expr> ParseMatch()
    {
        _position++; // 'match'

        var scrutinee = ParseComparison();
        if (scrutinee.IsFailure)
        {
            return scrutinee;
        }

        if (Current.Type != TokenType.With)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'with' at {Current.Position}."));
        }

        _position++;

        return Current.Type switch
        {
            TokenType.Some => ParseMatchOption(scrutinee.Value),
            TokenType.Ok => ParseMatchResult(scrutinee.Value),
            TokenType.Identifier => ParseMatchUnion(scrutinee.Value),
            _ => Result<Expr>.Failure(Error.Create($"Expected 'Some', 'Ok', or a variant name at {Current.Position}.")),
        };
    }

    /// <summary>Parses <c>Variant1(binder, ...) => body1 | Variant2 => body2 | ...</c> for a union match.</summary>
    private Result<Expr> ParseMatchUnion(Expr scrutinee)
    {
        var arms = new List<(string VariantName, IReadOnlyList<string> Binders, Expr Body)>();

        while (true)
        {
            if (Current.Type != TokenType.Identifier)
            {
                return Result<Expr>.Failure(Error.Create($"Expected a variant name at {Current.Position}."));
            }

            var variantName = Current.Text;
            _position++;

            var binders = new List<string>();

            if (Current.Type == TokenType.LParen)
            {
                _position++;

                if (Current.Type != TokenType.RParen)
                {
                    while (true)
                    {
                        if (Current.Type != TokenType.Identifier)
                        {
                            return Result<Expr>.Failure(Error.Create($"Expected a binder name at {Current.Position}."));
                        }

                        binders.Add(Current.Text);
                        _position++;

                        if (Current.Type != TokenType.Comma)
                        {
                            break;
                        }

                        _position++;
                    }
                }

                var closeParen = Expect(TokenType.RParen, "')'");
                if (closeParen.IsFailure)
                {
                    return Result<Expr>.Failure(closeParen.Error);
                }
            }

            var arrow = Expect(TokenType.FatArrow, "'=>'");
            if (arrow.IsFailure)
            {
                return Result<Expr>.Failure(arrow.Error);
            }

            var body = ParseTop();
            if (body.IsFailure)
            {
                return body;
            }

            arms.Add((variantName, binders, body.Value));

            if (Current.Type != TokenType.Pipe)
            {
                break;
            }

            _position++;
        }

        return Result<Expr>.Success(new MatchUnionExpr(scrutinee, arms));
    }

    private Result<Expr> ParseMatchOption(Expr scrutinee)
    {
        _position++; // 'Some'

        var someBinder = ExpectBinderInParens();
        if (someBinder.IsFailure)
        {
            return Result<Expr>.Failure(someBinder.Error);
        }

        var arrow1 = Expect(TokenType.FatArrow, "'=>'");
        if (arrow1.IsFailure)
        {
            return Result<Expr>.Failure(arrow1.Error);
        }

        var someBody = ParseTop();
        if (someBody.IsFailure)
        {
            return someBody;
        }

        var pipe = Expect(TokenType.Pipe, "'|'");
        if (pipe.IsFailure)
        {
            return Result<Expr>.Failure(pipe.Error);
        }

        var none = Expect(TokenType.None, "'None'");
        if (none.IsFailure)
        {
            return Result<Expr>.Failure(none.Error);
        }

        var arrow2 = Expect(TokenType.FatArrow, "'=>'");
        if (arrow2.IsFailure)
        {
            return Result<Expr>.Failure(arrow2.Error);
        }

        var noneBody = ParseTop();
        return noneBody.IsFailure
            ? noneBody
            : Result<Expr>.Success(new MatchOptionExpr(scrutinee, someBinder.Value, someBody.Value, noneBody.Value));
    }

    private Result<Expr> ParseMatchResult(Expr scrutinee)
    {
        _position++; // 'Ok'

        var okBinder = ExpectBinderInParens();
        if (okBinder.IsFailure)
        {
            return Result<Expr>.Failure(okBinder.Error);
        }

        var arrow1 = Expect(TokenType.FatArrow, "'=>'");
        if (arrow1.IsFailure)
        {
            return Result<Expr>.Failure(arrow1.Error);
        }

        var okBody = ParseTop();
        if (okBody.IsFailure)
        {
            return okBody;
        }

        var pipe = Expect(TokenType.Pipe, "'|'");
        if (pipe.IsFailure)
        {
            return Result<Expr>.Failure(pipe.Error);
        }

        var err = Expect(TokenType.Err, "'Err'");
        if (err.IsFailure)
        {
            return Result<Expr>.Failure(err.Error);
        }

        var errBinder = ExpectBinderInParens();
        if (errBinder.IsFailure)
        {
            return Result<Expr>.Failure(errBinder.Error);
        }

        var arrow2 = Expect(TokenType.FatArrow, "'=>'");
        if (arrow2.IsFailure)
        {
            return Result<Expr>.Failure(arrow2.Error);
        }

        var errBody = ParseTop();
        return errBody.IsFailure
            ? errBody
            : Result<Expr>.Success(new MatchResultExpr(scrutinee, okBinder.Value, okBody.Value, errBinder.Value, errBody.Value));
    }

    private Result<Expr> ParseMapOrBind(bool isBind)
    {
        _position++; // 'map' or 'bind'

        var open = Expect(TokenType.LParen, "'('");
        if (open.IsFailure)
        {
            return Result<Expr>.Failure(open.Error);
        }

        var container = ParseTop();
        if (container.IsFailure)
        {
            return container;
        }

        var comma = Expect(TokenType.Comma, "','");
        if (comma.IsFailure)
        {
            return Result<Expr>.Failure(comma.Error);
        }

        var mapper = ParseTop();
        if (mapper.IsFailure)
        {
            return mapper;
        }

        var close = Expect(TokenType.RParen, "')'");
        if (close.IsFailure)
        {
            return Result<Expr>.Failure(close.Error);
        }

        return Result<Expr>.Success(isBind
            ? new BindExpr(container.Value, mapper.Value)
            : new MapExpr(container.Value, mapper.Value));
    }

    private Result<Expr> ParseFilter()
    {
        _position++; // 'filter'

        var open = Expect(TokenType.LParen, "'('");
        if (open.IsFailure)
        {
            return Result<Expr>.Failure(open.Error);
        }

        var list = ParseTop();
        if (list.IsFailure)
        {
            return list;
        }

        var comma = Expect(TokenType.Comma, "','");
        if (comma.IsFailure)
        {
            return Result<Expr>.Failure(comma.Error);
        }

        var predicate = ParseTop();
        if (predicate.IsFailure)
        {
            return predicate;
        }

        var close = Expect(TokenType.RParen, "')'");
        return close.IsFailure
            ? Result<Expr>.Failure(close.Error)
            : Result<Expr>.Success(new FilterExpr(list.Value, predicate.Value));
    }

    private Result<Expr> ParseFold()
    {
        _position++; // 'fold'

        var open = Expect(TokenType.LParen, "'('");
        if (open.IsFailure)
        {
            return Result<Expr>.Failure(open.Error);
        }

        var list = ParseTop();
        if (list.IsFailure)
        {
            return list;
        }

        var comma1 = Expect(TokenType.Comma, "','");
        if (comma1.IsFailure)
        {
            return Result<Expr>.Failure(comma1.Error);
        }

        var initial = ParseTop();
        if (initial.IsFailure)
        {
            return initial;
        }

        var comma2 = Expect(TokenType.Comma, "','");
        if (comma2.IsFailure)
        {
            return Result<Expr>.Failure(comma2.Error);
        }

        var folder = ParseTop();
        if (folder.IsFailure)
        {
            return folder;
        }

        var close = Expect(TokenType.RParen, "')'");
        return close.IsFailure
            ? Result<Expr>.Failure(close.Error)
            : Result<Expr>.Success(new FoldExpr(list.Value, initial.Value, folder.Value));
    }

    /// <summary><c>[]&lt;Type&gt;</c> (empty, explicitly typed) or <c>[e1, e2, ...]</c> (element type inferred).</summary>
    private Result<Expr> ParseListLiteral()
    {
        _position++; // '['

        if (Current.Type == TokenType.RBracket)
        {
            _position++;
            return ParseGenericTypeArgument().Bind(elementType => Result<Expr>.Success(new EmptyListExpr(elementType)));
        }

        var elements = new List<Expr>();

        var first = ParseTop();
        if (first.IsFailure)
        {
            return first;
        }

        elements.Add(first.Value);

        while (Current.Type == TokenType.Comma)
        {
            _position++;

            var next = ParseTop();
            if (next.IsFailure)
            {
                return next;
            }

            elements.Add(next.Value);
        }

        var close = Expect(TokenType.RBracket, "']'");
        return close.IsFailure ? Result<Expr>.Failure(close.Error) : Result<Expr>.Success(new ListExpr(elements));
    }

    /// <summary><c>{ Field1: expr1, Field2: expr2, ... }</c> (the leading type name is already consumed).</summary>
    private Result<Expr> ParseRecordConstruct(string typeName)
    {
        _position++; // '{'

        var fields = new List<(string FieldName, Expr Value)>();

        if (Current.Type != TokenType.RBrace)
        {
            while (true)
            {
                if (Current.Type != TokenType.Identifier)
                {
                    return Result<Expr>.Failure(Error.Create($"Expected a field name at {Current.Position}."));
                }

                var fieldName = Current.Text;
                _position++;

                var colon = Expect(TokenType.Colon, "':'");
                if (colon.IsFailure)
                {
                    return Result<Expr>.Failure(colon.Error);
                }

                var value = ParseTop();
                if (value.IsFailure)
                {
                    return value;
                }

                fields.Add((fieldName, value.Value));

                if (Current.Type != TokenType.Comma)
                {
                    break;
                }

                _position++;
            }
        }

        var close = Expect(TokenType.RBrace, "'}'");
        return close.IsFailure
            ? Result<Expr>.Failure(close.Error)
            : Result<Expr>.Success(new RecordConstructExpr(typeName, fields));
    }

    /// <summary>Consumes <c>(name)</c> and returns <c>name</c>, e.g. the binder in a <c>Some(x)</c> match arm.</summary>
    private Result<string> ExpectBinderInParens()
    {
        var open = Expect(TokenType.LParen, "'('");
        if (open.IsFailure)
        {
            return Result<string>.Failure(open.Error);
        }

        if (Current.Type != TokenType.Identifier)
        {
            return Result<string>.Failure(Error.Create($"Expected a binder name at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        var close = Expect(TokenType.RParen, "')'");
        return close.IsFailure ? Result<string>.Failure(close.Error) : Result<string>.Success(name);
    }

    /// <summary>Consumes <c>(expr)</c> and returns <c>expr</c>, e.g. the wrapped value in <c>Some(5)</c>.</summary>
    private Result<Expr> ParseParenthesizedExpr()
    {
        var open = Expect(TokenType.LParen, "'('");
        if (open.IsFailure)
        {
            return Result<Expr>.Failure(open.Error);
        }

        var value = ParseTop();
        if (value.IsFailure)
        {
            return value;
        }

        var close = Expect(TokenType.RParen, "')'");
        return close.IsFailure ? Result<Expr>.Failure(close.Error) : value;
    }

    /// <summary>Consumes <c>&lt;Type&gt;</c>, e.g. the explicit element type in <c>None&lt;Int&gt;</c>.</summary>
    private Result<KlexirType> ParseGenericTypeArgument()
    {
        var open = Expect(TokenType.Less, "'<'");
        if (open.IsFailure)
        {
            return Result<KlexirType>.Failure(open.Error);
        }

        var type = ParseTypeAnnotation();
        if (type.IsFailure)
        {
            return type;
        }

        var close = Expect(TokenType.Greater, "'>'");
        return close.IsFailure ? Result<KlexirType>.Failure(close.Error) : type;
    }

    private Result<Unit> Expect(TokenType type, string description)
    {
        if (Current.Type != type)
        {
            return Result<Unit>.Failure(Error.Create($"Expected {description} at {Current.Position}."));
        }

        _position++;
        return Result<Unit>.Success(Unit.Value);
    }

    private Result<Expr> ParseLet()
    {
        var header = ParseLetHeader();
        if (header.IsFailure)
        {
            return Result<Expr>.Failure(header.Error);
        }

        if (Current.Type != TokenType.In)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'in' at {Current.Position}."));
        }

        _position++;

        var body = ParseTop();
        return body.IsFailure
            ? body
            : Result<Expr>.Success(new LetExpr(header.Value.Name, header.Value.Value, body.Value));
    }

    /// <summary>Parses <c>let name = value</c>, up to but not including <c>in</c> — shared by the in-expression
    /// <c>let ... in ...</c> form and top-level program declarations, which don't use <c>in</c> at all.</summary>
    private Result<(string Name, Expr Value)> ParseLetHeader()
    {
        _position++; // 'let'

        if (Current.Type != TokenType.Identifier)
        {
            return Result<(string, Expr)>.Failure(Error.Create($"Expected an identifier after 'let' at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        if (Current.Type != TokenType.Equals)
        {
            return Result<(string, Expr)>.Failure(Error.Create($"Expected '=' after 'let {name}' at {Current.Position}."));
        }

        _position++;

        var value = ParseTop();
        return value.IsFailure
            ? Result<(string, Expr)>.Failure(value.Error)
            : Result<(string, Expr)>.Success((name, value.Value));
    }

    /// <summary>
    /// A Klexir program: a sequence of top-level <c>let</c>/<c>let rec</c> declarations — no trailing <c>in</c>
    /// between them — followed by a final expression, desugared into the same nested-<c>let</c> AST a single
    /// <c>let ... in ...</c> expression produces.
    /// </summary>
    public Result<Expr> ParseProgram()
    {
        var body = ParseProgramBody();
        if (body.IsFailure)
        {
            return body;
        }

        return Current.Type == TokenType.Eof
            ? body
            : Result<Expr>.Failure(Error.Create($"Unexpected token '{Current.Text}' at {Current.Position}."));
    }

    private Result<Expr> ParseProgramBody()
    {
        if (Current.Type == TokenType.Let && Peek(1).Type == TokenType.Rec)
        {
            var header = ParseLetRecHeader();
            if (header.IsFailure)
            {
                return Result<Expr>.Failure(header.Error);
            }

            // A ';' terminator is required here — without it, a value like '... func(Int n) => n' directly
            // followed by the next declaration's leading identifier is grammatically indistinguishable from
            // that value being *applied* to it (Klexir's function calls are juxtaposition: 'f x').
            var semicolon = Expect(TokenType.Semicolon, "';' after a top-level 'let rec' declaration");
            if (semicolon.IsFailure)
            {
                return Result<Expr>.Failure(semicolon.Error);
            }

            var rest = ParseProgramBody();
            return rest.IsFailure
                ? rest
                : Result<Expr>.Success(new LetRecExpr(
                    header.Value.Name, header.Value.ParamName, header.Value.ParamType, header.Value.ReturnType,
                    header.Value.FunctionBody, rest.Value));
        }

        if (Current.Type == TokenType.Let)
        {
            var header = ParseLetHeader();
            if (header.IsFailure)
            {
                return Result<Expr>.Failure(header.Error);
            }

            var semicolon = Expect(TokenType.Semicolon, "';' after a top-level 'let' declaration");
            if (semicolon.IsFailure)
            {
                return Result<Expr>.Failure(semicolon.Error);
            }

            var rest = ParseProgramBody();
            return rest.IsFailure
                ? rest
                : Result<Expr>.Success(new LetExpr(header.Value.Name, header.Value.Value, rest.Value));
        }

        if (Current.Type == TokenType.Record)
        {
            var decl = ParseRecordDecl();
            if (decl.IsFailure)
            {
                return Result<Expr>.Failure(decl.Error);
            }

            var semicolon = Expect(TokenType.Semicolon, "';' after a top-level 'record' declaration");
            if (semicolon.IsFailure)
            {
                return Result<Expr>.Failure(semicolon.Error);
            }

            var rest = ParseProgramBody();
            return rest.IsFailure
                ? rest
                : Result<Expr>.Success(new RecordDeclExpr(decl.Value.Name, decl.Value.Fields, rest.Value));
        }

        if (Current.Type == TokenType.Union)
        {
            var decl = ParseUnionDecl();
            if (decl.IsFailure)
            {
                return Result<Expr>.Failure(decl.Error);
            }

            var semicolon = Expect(TokenType.Semicolon, "';' after a top-level 'union' declaration");
            if (semicolon.IsFailure)
            {
                return Result<Expr>.Failure(semicolon.Error);
            }

            var rest = ParseProgramBody();
            return rest.IsFailure
                ? rest
                : Result<Expr>.Success(new UnionDeclExpr(decl.Value.Name, decl.Value.Variants, rest.Value));
        }

        return ParseTop();
    }

    /// <summary>Parses <c>union NAME { Variant1(Type1, Type2), Variant2, ... }</c> — top-level only.</summary>
    private Result<(string Name, IReadOnlyList<(string VariantName, IReadOnlyList<KlexirType> FieldTypes)> Variants)> ParseUnionDecl()
    {
        _position++; // 'union'

        if (Current.Type != TokenType.Identifier)
        {
            return Result<(string, IReadOnlyList<(string, IReadOnlyList<KlexirType>)>)>.Failure(
                Error.Create($"Expected a union type name after 'union' at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        var open = Expect(TokenType.LBrace, "'{'");
        if (open.IsFailure)
        {
            return Result<(string, IReadOnlyList<(string, IReadOnlyList<KlexirType>)>)>.Failure(open.Error);
        }

        var variants = new List<(string VariantName, IReadOnlyList<KlexirType> FieldTypes)>();

        if (Current.Type != TokenType.RBrace)
        {
            while (true)
            {
                if (Current.Type != TokenType.Identifier)
                {
                    return Result<(string, IReadOnlyList<(string, IReadOnlyList<KlexirType>)>)>.Failure(
                        Error.Create($"Expected a variant name at {Current.Position}."));
                }

                var variantName = Current.Text;
                _position++;

                var fieldTypes = new List<KlexirType>();

                if (Current.Type == TokenType.LParen)
                {
                    _position++;

                    if (Current.Type != TokenType.RParen)
                    {
                        while (true)
                        {
                            var fieldType = ParseTypeAnnotation();
                            if (fieldType.IsFailure)
                            {
                                return Result<(string, IReadOnlyList<(string, IReadOnlyList<KlexirType>)>)>.Failure(fieldType.Error);
                            }

                            fieldTypes.Add(fieldType.Value);

                            if (Current.Type != TokenType.Comma)
                            {
                                break;
                            }

                            _position++;
                        }
                    }

                    var closeParen = Expect(TokenType.RParen, "')'");
                    if (closeParen.IsFailure)
                    {
                        return Result<(string, IReadOnlyList<(string, IReadOnlyList<KlexirType>)>)>.Failure(closeParen.Error);
                    }
                }

                variants.Add((variantName, fieldTypes));

                if (Current.Type != TokenType.Comma)
                {
                    break;
                }

                _position++;
            }
        }

        var close = Expect(TokenType.RBrace, "'}'");
        return close.IsFailure
            ? Result<(string, IReadOnlyList<(string, IReadOnlyList<KlexirType>)>)>.Failure(close.Error)
            : Result<(string, IReadOnlyList<(string, IReadOnlyList<KlexirType>)>)>.Success((name, variants));
    }

    /// <summary>Parses <c>record NAME { Field1: Type1, Field2: Type2, ... }</c> — top-level only, no inline form.</summary>
    private Result<(string Name, IReadOnlyList<(string FieldName, KlexirType FieldType)> Fields)> ParseRecordDecl()
    {
        _position++; // 'record'

        if (Current.Type != TokenType.Identifier)
        {
            return Result<(string, IReadOnlyList<(string, KlexirType)>)>.Failure(
                Error.Create($"Expected a record type name after 'record' at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        var open = Expect(TokenType.LBrace, "'{'");
        if (open.IsFailure)
        {
            return Result<(string, IReadOnlyList<(string, KlexirType)>)>.Failure(open.Error);
        }

        var fields = new List<(string FieldName, KlexirType FieldType)>();

        if (Current.Type != TokenType.RBrace)
        {
            while (true)
            {
                if (Current.Type != TokenType.Identifier)
                {
                    return Result<(string, IReadOnlyList<(string, KlexirType)>)>.Failure(
                        Error.Create($"Expected a field name at {Current.Position}."));
                }

                var fieldName = Current.Text;
                _position++;

                var colon = Expect(TokenType.Colon, "':'");
                if (colon.IsFailure)
                {
                    return Result<(string, IReadOnlyList<(string, KlexirType)>)>.Failure(colon.Error);
                }

                var fieldType = ParseTypeAnnotation();
                if (fieldType.IsFailure)
                {
                    return Result<(string, IReadOnlyList<(string, KlexirType)>)>.Failure(fieldType.Error);
                }

                fields.Add((fieldName, fieldType.Value));

                if (Current.Type != TokenType.Comma)
                {
                    break;
                }

                _position++;
            }
        }

        var close = Expect(TokenType.RBrace, "'}'");
        return close.IsFailure
            ? Result<(string, IReadOnlyList<(string, KlexirType)>)>.Failure(close.Error)
            : Result<(string, IReadOnlyList<(string, KlexirType)>)>.Success((name, fields));
    }

    private Result<Expr> ParseIf()
    {
        _position++; // 'if'

        var condition = ParseComparison();
        if (condition.IsFailure)
        {
            return condition;
        }

        if (Current.Type != TokenType.Then)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'then' at {Current.Position}."));
        }

        _position++;

        var thenBranch = ParseTop();
        if (thenBranch.IsFailure)
        {
            return thenBranch;
        }

        if (Current.Type != TokenType.Else)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'else' at {Current.Position}."));
        }

        _position++;

        var elseBranch = ParseTop();
        return elseBranch.IsFailure
            ? elseBranch
            : Result<Expr>.Success(new IfExpr(condition.Value, thenBranch.Value, elseBranch.Value));
    }

    /// <summary>
    /// Pure sugar over <c>bind</c>, left-associative, looser than comparison: <c>a andThen f andThen g</c> parses
    /// to the same <see cref="BindExpr"/> tree as <c>bind(bind(a, f), g)</c> — reads as a left-to-right pipeline
    /// instead of nested parens, short-circuiting on the first <c>Err</c>/<c>None</c> exactly like <c>bind</c>.
    /// </summary>
    private Result<Expr> ParseAndThen()
    {
        var left = ParseComparison();
        if (left.IsFailure)
        {
            return left;
        }

        var expr = left.Value;

        while (Current.Type == TokenType.AndThen)
        {
            _position++;

            var right = ParseComparison();
            if (right.IsFailure)
            {
                return right;
            }

            expr = new BindExpr(expr, right.Value);
        }

        return Result<Expr>.Success(expr);
    }

    private Result<Expr> ParseComparison()
    {
        var left = ParseAdditive();
        if (left.IsFailure)
        {
            return left;
        }

        if (Current.Type is not (TokenType.EqualsEquals or TokenType.Less or TokenType.Greater or TokenType.LessEquals or TokenType.GreaterEquals))
        {
            return left;
        }

        var op = Current.Type switch
        {
            TokenType.EqualsEquals => ComparisonOperator.Equal,
            TokenType.Less => ComparisonOperator.LessThan,
            TokenType.Greater => ComparisonOperator.GreaterThan,
            TokenType.LessEquals => ComparisonOperator.LessThanOrEqual,
            _ => ComparisonOperator.GreaterThanOrEqual,
        };
        _position++;

        var right = ParseAdditive();
        return right.IsFailure
            ? right
            : Result<Expr>.Success(new ComparisonExpr(op, left.Value, right.Value));
    }

    private Result<Expr> ParseAdditive()
    {
        var left = ParseMultiplicative();
        if (left.IsFailure)
        {
            return left;
        }

        var expr = left.Value;

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Current.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Sub;
            _position++;

            var right = ParseMultiplicative();
            if (right.IsFailure)
            {
                return right;
            }

            expr = new BinaryExpr(op, expr, right.Value);
        }

        return Result<Expr>.Success(expr);
    }

    private Result<Expr> ParseMultiplicative()
    {
        var left = ParseApplication();
        if (left.IsFailure)
        {
            return left;
        }

        var expr = left.Value;

        while (Current.Type is TokenType.Star or TokenType.Slash)
        {
            var op = Current.Type == TokenType.Star ? BinaryOperator.Mul : BinaryOperator.Div;
            _position++;

            var right = ParseApplication();
            if (right.IsFailure)
            {
                return right;
            }

            expr = new BinaryExpr(op, expr, right.Value);
        }

        return Result<Expr>.Success(expr);
    }

    /// <summary>Left-associative juxtaposition application (<c>f x y</c> parses as <c>(f x) y</c>), tighter than * /.</summary>
    private Result<Expr> ParseApplication()
    {
        var left = ParsePostfix();
        if (left.IsFailure)
        {
            return left;
        }

        var expr = left.Value;

        // Every token ParsePrimary can start with — kept in sync with it deliberately, since a token missing here
        // doesn't fail to parse an argument, it silently stops taking arguments (found the hard way: applying a
        // function to a String literal, 'f "x"', used to parse as just 'f' with the string ignored downstream).
        while (Current.Type is TokenType.Int or TokenType.String or TokenType.True or TokenType.False
            or TokenType.Identifier or TokenType.Some or TokenType.None or TokenType.Ok or TokenType.Err
            or TokenType.Map or TokenType.Bind or TokenType.Filter or TokenType.Fold
            or TokenType.LBracket or TokenType.LParen)
        {
            var arg = ParsePostfix();
            if (arg.IsFailure)
            {
                return arg;
            }

            expr = new AppExpr(expr, arg.Value);
        }

        return Result<Expr>.Success(expr);
    }

    /// <summary>A primary, then any number of <c>.Field</c> accesses — tighter than application, so <c>f x.y</c> is <c>f (x.y)</c>.</summary>
    private Result<Expr> ParsePostfix()
    {
        var result = ParsePrimary();
        if (result.IsFailure)
        {
            return result;
        }

        var expr = result.Value;

        while (Current.Type == TokenType.Dot)
        {
            _position++;

            if (Current.Type != TokenType.Identifier)
            {
                return Result<Expr>.Failure(Error.Create($"Expected a field name after '.' at {Current.Position}."));
            }

            expr = new FieldAccessExpr(expr, Current.Text);
            _position++;
        }

        return Result<Expr>.Success(expr);
    }

    private Result<Expr> ParsePrimary()
    {
        switch (Current.Type)
        {
            case TokenType.Int:
                var intValue = long.Parse(Current.Text);
                _position++;
                return Result<Expr>.Success(new IntLiteral(intValue));

            case TokenType.String:
                var stringValue = Current.Text;
                _position++;
                return Result<Expr>.Success(new StringLiteral(stringValue));

            case TokenType.True:
                _position++;
                return Result<Expr>.Success(new BoolLiteral(true));

            case TokenType.False:
                _position++;
                return Result<Expr>.Success(new BoolLiteral(false));

            case TokenType.Identifier:
                var name = Current.Text;
                _position++;
                return Current.Type == TokenType.LBrace
                    ? ParseRecordConstruct(name)
                    : Result<Expr>.Success(new Identifier(name));

            case TokenType.Some:
                _position++;
                return ParseParenthesizedExpr().Bind(value => Result<Expr>.Success(new SomeExpr(value)));

            case TokenType.None:
                _position++;
                return ParseGenericTypeArgument().Bind(elementType => Result<Expr>.Success(new NoneExpr(elementType)));

            case TokenType.Ok:
                _position++;
                return ParseGenericTypeArgument()
                    .Bind(errType => ParseParenthesizedExpr()
                        .Bind(value => Result<Expr>.Success(new OkExpr(errType, value))));

            case TokenType.Err:
                _position++;
                return ParseGenericTypeArgument()
                    .Bind(okType => ParseParenthesizedExpr()
                        .Bind(value => Result<Expr>.Success(new ErrExpr(okType, value))));

            case TokenType.Map:
                return ParseMapOrBind(isBind: false);

            case TokenType.Bind:
                return ParseMapOrBind(isBind: true);

            case TokenType.Filter:
                return ParseFilter();

            case TokenType.Fold:
                return ParseFold();

            case TokenType.LBracket:
                return ParseListLiteral();

            case TokenType.LParen:
                _position++;
                var inner = ParseTop();
                if (inner.IsFailure)
                {
                    return inner;
                }

                if (Current.Type != TokenType.RParen)
                {
                    return Result<Expr>.Failure(Error.Create($"Expected ')' at {Current.Position}."));
                }

                _position++;
                return inner;

            default:
                return Result<Expr>.Failure(Error.Create($"Unexpected token '{Current.Text}' at {Current.Position}."));
        }
    }
}
