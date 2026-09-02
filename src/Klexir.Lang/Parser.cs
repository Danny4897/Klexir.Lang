using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// Recursive-descent parser. Precedence, loosest to tightest: <c>let ... in</c>, additive (+ -), multiplicative (* /), primary.
/// </summary>
public sealed class Parser(IReadOnlyList<Token> tokens)
{
    private int _position;

    private Token Current => tokens[_position];

    public Result<Expr> ParseExpression()
    {
        var expr = ParseLetOrAdditive();
        if (expr.IsFailure)
        {
            return expr;
        }

        return Current.Type == TokenType.Eof
            ? expr
            : Result<Expr>.Failure(Error.Create($"Unexpected token '{Current.Text}' at {Current.Position}."));
    }

    private Result<Expr> ParseLetOrAdditive() =>
        Current.Type == TokenType.Let ? ParseLet() : ParseAdditive();

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

        var value = ParseAdditive();
        if (value.IsFailure)
        {
            return value;
        }

        if (Current.Type != TokenType.In)
        {
            return Result<Expr>.Failure(Error.Create($"Expected 'in' at {Current.Position}."));
        }

        _position++;

        var body = ParseLetOrAdditive();
        return body.IsFailure ? body : Result<Expr>.Success(new LetExpr(name, value.Value, body.Value));
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

            case TokenType.Identifier:
                var name = Current.Text;
                _position++;
                return Result<Expr>.Success(new Identifier(name));

            case TokenType.LParen:
                _position++;
                var inner = ParseLetOrAdditive();
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
