using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// Recursive-descent parser. Precedence, loosest to tightest: <c>let ... in</c> / <c>if ... then ... else</c>,
/// comparison (== &lt; &gt; &lt;= &gt;=, non-chaining), additive (+ -), multiplicative (* /), primary.
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
            TokenType.Fun => ParseFun(),
            TokenType.Match => ParseMatch(),
            TokenType.Map => ParseMapOrBind(isBind: false),
            TokenType.Bind => ParseMapOrBind(isBind: true),
            _ => ParseComparison(),
        };

    private Token Peek(int offset) => tokens[Math.Min(_position + offset, tokens.Count - 1)];

    private Result<Expr> ParseLetRec()
    {
        _position++; // 'let'
        _position++; // 'rec'

        if (Current.Type != TokenType.Identifier)
        {
            return Result<Expr>.Failure(Error.Create($"Expected a function name after 'let rec' at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        if (Current.Type != TokenType.Equals)
        {
            return Result<Expr>.Failure(Error.Create($"Expected '=' after 'let rec {name}' at {Current.Position}."));
        }

        _position++;

        if (Current.Type != TokenType.Fun)
        {
            return Result<Expr>.Failure(Error.Create($"'let rec' requires a function value at {Current.Position}."));
        }

        _position++; // 'fun'

        if (Current.Type != TokenType.LParen)
        {
            return Result<Expr>.Failure(Error.Create($"Expected '(' after 'fun' at {Current.Position}."));
        }

        _position++;

        if (Current.Type != TokenType.Identifier)
        {
            return Result<Expr>.Failure(Error.Create($"Expected a parameter name at {Current.Position}."));
        }

        var paramName = Current.Text;
        _position++;

        if (Current.Type != TokenType.Colon)
        {
            return Result<Expr>.Failure(Error.Create($"Expected ':' after parameter name at {Current.Position}."));
        }

        _position++;

        var paramType = ParseTypeAnnotation();
        if (paramType.IsFailure)
        {
            return Result<Expr>.Failure(paramType.Error);
        }

        if (Current.Type != TokenType.RParen)
        {
            return Result<Expr>.Failure(Error.Create($"Expected ')' at {Current.Position}."));
        }

        _position++;

        if (Current.Type != TokenType.Colon)
        {
            return Result<Expr>.Failure(Error.Create(
                $"'let rec' functions need an explicit return type — ': Type' after the parameter list — at {Current.Position}."));
        }

        _position++;

        var returnType = ParseTypeAnnotation();
        if (returnType.IsFailure)
        {
            return Result<Expr>.Failure(returnType.Error);
        }

        if (Current.Type != TokenType.FatArrow)
        {
            return Result<Expr>.Failure(Error.Create($"Expected '=>' at {Current.Position}."));
        }

        _position++;

        var functionBody = ParseTop();
        if (functionBody.IsFailure)
        {
            return functionBody;
        }

        if (Current.Type != TokenType.In)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'in' at {Current.Position}."));
        }

        _position++;

        var letBody = ParseTop();
        return letBody.IsFailure
            ? letBody
            : Result<Expr>.Success(new LetRecExpr(name, paramName, paramType.Value, returnType.Value, functionBody.Value, letBody.Value));
    }

    private Result<Expr> ParseFun()
    {
        _position++; // 'fun'

        if (Current.Type != TokenType.LParen)
        {
            return Result<Expr>.Failure(Error.Create($"Expected '(' after 'fun' at {Current.Position}."));
        }

        _position++;

        if (Current.Type != TokenType.Identifier)
        {
            return Result<Expr>.Failure(Error.Create($"Expected a parameter name at {Current.Position}."));
        }

        var paramName = Current.Text;
        _position++;

        if (Current.Type != TokenType.Colon)
        {
            return Result<Expr>.Failure(Error.Create($"Expected ':' after parameter name at {Current.Position}."));
        }

        _position++;

        var paramType = ParseTypeAnnotation();
        if (paramType.IsFailure)
        {
            return Result<Expr>.Failure(paramType.Error);
        }

        if (Current.Type != TokenType.RParen)
        {
            return Result<Expr>.Failure(Error.Create($"Expected ')' at {Current.Position}."));
        }

        _position++;

        if (Current.Type != TokenType.FatArrow)
        {
            return Result<Expr>.Failure(Error.Create($"Expected '=>' at {Current.Position}."));
        }

        _position++;

        var body = ParseTop();
        return body.IsFailure
            ? body
            : Result<Expr>.Success(new FunExpr(paramName, paramType.Value, body.Value));
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
                return Result<KlexirType>.Failure(Error.Create($"Unknown type '{name}' at {Current.Position}."));
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
            _ => Result<Expr>.Failure(Error.Create($"Expected 'Some' or 'Ok' at {Current.Position}.")),
        };
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
        _position++; // 'let'

        if (Current.Type != TokenType.Identifier)
        {
            return Result<Expr>.Failure(Error.Create($"Expected an identifier after 'let' at {Current.Position}."));
        }

        var name = Current.Text;
        _position++;

        if (Current.Type != TokenType.Equals)
        {
            return Result<Expr>.Failure(Error.Create($"Expected '=' after 'let {name}' at {Current.Position}."));
        }

        _position++;

        var value = ParseTop();
        if (value.IsFailure)
        {
            return value;
        }

        if (Current.Type != TokenType.In)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'in' at {Current.Position}."));
        }

        _position++;

        var body = ParseTop();
        return body.IsFailure ? body : Result<Expr>.Success(new LetExpr(name, value.Value, body.Value));
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
        var left = ParsePrimary();
        if (left.IsFailure)
        {
            return left;
        }

        var expr = left.Value;

        while (Current.Type is TokenType.Int or TokenType.True or TokenType.False or TokenType.Identifier or TokenType.LParen)
        {
            var arg = ParsePrimary();
            if (arg.IsFailure)
            {
                return arg;
            }

            expr = new AppExpr(expr, arg.Value);
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

            case TokenType.True:
                _position++;
                return Result<Expr>.Success(new BoolLiteral(true));

            case TokenType.False:
                _position++;
                return Result<Expr>.Success(new BoolLiteral(false));

            case TokenType.Identifier:
                var name = Current.Text;
                _position++;
                return Result<Expr>.Success(new Identifier(name));

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
