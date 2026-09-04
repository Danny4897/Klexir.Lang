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
/// <c>let rec name = func(ParamType param): ReturnType => functionBody in letBody</c>. Unlike <see cref="LetExpr"/>,
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

/// <summary>
/// <c>record NAME { Field1: Type1, Field2: Type2, ... };</c>, scoped over the rest of the program (<see cref="Body"/>)
/// — a top-level-only declaration, not an inline expression form.
/// </summary>
public sealed record RecordDeclExpr(string Name, IReadOnlyList<(string FieldName, KlexirType FieldType)> Fields, Expr Body) : Expr;

/// <summary><c>NAME { Field1: expr1, Field2: expr2, ... }</c>. Field order doesn't matter, but every declared field must appear exactly once.</summary>
public sealed record RecordConstructExpr(string TypeName, IReadOnlyList<(string FieldName, Expr Value)> Fields) : Expr;

/// <summary><c>receiver.FieldName</c>.</summary>
public sealed record FieldAccessExpr(Expr Receiver, string FieldName) : Expr;

/// <summary>
/// <c>union NAME { Variant1(T1, T2), Variant2, ... };</c>, scoped over <see cref="Body"/> — top-level only, like
/// <see cref="RecordDeclExpr"/>. Unlike a record, this doesn't disappear after type-checking: each variant name
/// is bound to a (possibly curried) constructor, so evaluating this node has to inject real constructor values
/// into the runtime environment before evaluating <see cref="Body"/>.
/// </summary>
public sealed record UnionDeclExpr(
    string Name, IReadOnlyList<(string VariantName, IReadOnlyList<KlexirType> FieldTypes)> Variants, Expr Body) : Expr;

/// <summary>
/// <c>match scrutinee with Variant1(binder, ...) => body1 | Variant2 => body2 | ...</c> — exhaustive over every
/// variant of the scrutinee's union type, each listed exactly once, in any order, with one positional binder per
/// field.
/// </summary>
public sealed record MatchUnionExpr(Expr Scrutinee, IReadOnlyList<(string VariantName, IReadOnlyList<string> Binders, Expr Body)> Arms) : Expr;
