namespace Klexir.Lang;

public abstract record Expr;

public sealed record IntLiteral(long Value) : Expr;

public sealed record Identifier(string Name) : Expr;

public enum BinaryOperator
{
    Add,
    Sub,
    Mul,
    Div,
}

public sealed record BinaryExpr(BinaryOperator Operator, Expr Left, Expr Right) : Expr;

public sealed record LetExpr(string Name, Expr Value, Expr Body) : Expr;
