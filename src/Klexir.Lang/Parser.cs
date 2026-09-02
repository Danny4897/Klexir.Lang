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
            TokenType.Let => ParseLet(),
            TokenType.If => ParseIf(),
            _ => ParseComparison(),
        };

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
        var left = ParsePrimary();
        if (left.IsFailure)
        {
            return left;
        }

        var expr = left.Value;

        while (Current.Type is TokenType.Star or TokenType.Slash)
        {
            var op = Current.Type == TokenType.Star ? BinaryOperator.Mul : BinaryOperator.Div;
            _position++;

            var right = ParsePrimary();
            if (right.IsFailure)
            {
                return right;
            }

            expr = new BinaryExpr(op, expr, right.Value);
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
