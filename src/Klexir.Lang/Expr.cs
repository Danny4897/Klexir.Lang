namespace Klexir.Lang;

public abstract record Expr;

public sealed record IntLiteral(long Value) : Expr;

public sealed record StringLiteral(string Value) : Expr;

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

/// <summary>
/// <c>let rec name = fun (param: ParamType): ReturnType => functionBody in letBody</c>. Unlike <see cref="LetExpr"/>,
/// <c>name</c> is visible inside <c>functionBody</c> too, so the function can call itself. A recursive binding's
/// return type must be written explicitly (no inference), since checking the body needs the binding's type
/// before the body itself has been checked.
/// </summary>
public sealed record LetRecExpr(
    string Name, string ParamName, KlexirType ParamType, KlexirType ReturnType, Expr FunctionBody, Expr LetBody) : Expr;

/// <summary><c>Some(value)</c>. Its <c>Option&lt;T&gt;</c> element type is inferred from <c>value</c>.</summary>
public sealed record SomeExpr(Expr Value) : Expr;

/// <summary><c>None&lt;ElementType&gt;</c>. Carries no value, so its element type must be written explicitly.</summary>
public sealed record NoneExpr(KlexirType ElementType) : Expr;

/// <summary><c>Ok&lt;ErrType&gt;(value)</c>. The Ok type is inferred from <c>value</c>; the Err type can't be, so it's explicit.</summary>
public sealed record OkExpr(KlexirType ErrType, Expr Value) : Expr;

/// <summary><c>Err&lt;OkType&gt;(value)</c>. The Err type is inferred from <c>value</c>; the Ok type can't be, so it's explicit.</summary>
public sealed record ErrExpr(KlexirType OkType, Expr Value) : Expr;

/// <summary><c>match scrutinee with Some(binder) => someBody | None => noneBody</c>.</summary>
public sealed record MatchOptionExpr(Expr Scrutinee, string SomeBinder, Expr SomeBody, Expr NoneBody) : Expr;

/// <summary><c>match scrutinee with Ok(okBinder) => okBody | Err(errBinder) => errBody</c>.</summary>
public sealed record MatchResultExpr(Expr Scrutinee, string OkBinder, Expr OkBody, string ErrBinder, Expr ErrBody) : Expr;

/// <summary>
/// <c>map(container, mapper)</c> — the Functor operation. Transforms the value inside an <c>Option</c>/<c>Ok</c>
/// container and leaves <c>None</c>/<c>Err</c> untouched.
/// </summary>
public sealed record MapExpr(Expr Container, Expr Mapper) : Expr;

/// <summary>
/// <c>bind(container, mapper)</c> — the Monad operation. Chains a container-returning function and short-circuits
/// on <c>None</c>/<c>Err</c>, enabling railway-oriented composition.
/// </summary>
public sealed record BindExpr(Expr Container, Expr Mapper) : Expr;

/// <summary><c>[e1, e2, ...]</c>. The element type is inferred from the (at least one) element expressions.</summary>
public sealed record ListExpr(IReadOnlyList<Expr> Elements) : Expr;

/// <summary><c>[]&lt;ElementType&gt;</c>. An empty list carries no elements, so its type must be written explicitly.</summary>
public sealed record EmptyListExpr(KlexirType ElementType) : Expr;

/// <summary><c>filter(list, predicate)</c> — keeps only the elements for which <c>predicate</c> returns <c>true</c>.</summary>
public sealed record FilterExpr(Expr List, Expr Predicate) : Expr;

/// <summary><c>fold(list, initial, folder)</c> — left-fold; <c>folder</c> is curried as <c>Acc -> Elem -> Acc</c>.</summary>
public sealed record FoldExpr(Expr List, Expr Initial, Expr Folder) : Expr;
