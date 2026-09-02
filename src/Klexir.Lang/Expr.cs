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

public sealed record BoolLiteral(bool Value) : Expr;

public enum ComparisonOperator
{
    Equal,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
}

public sealed record ComparisonExpr(ComparisonOperator Operator, Expr Left, Expr Right) : Expr;

public sealed record IfExpr(Expr Condition, Expr Then, Expr Else) : Expr;

public sealed record FunExpr(string ParamName, KlexirType ParamType, Expr Body) : Expr;

public sealed record AppExpr(Expr Function, Expr Argument) : Expr;
