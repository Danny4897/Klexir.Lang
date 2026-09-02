# Klexir.Lang

[![CI](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml/badge.svg)](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

The Klexir programming language: a lexer, a recursive-descent parser, a structural type checker, and a tree-walking evaluator — so a Klexir program written as a string can now be tokenized, parsed, type-checked, and actually **run** to a value. Built on [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>` — no exceptions for compiler-control flow, ever, evaluation included. `Option<T>` and `Result<T, E>` aren't just how the compiler is implemented — they're first-class Klexir *types*, with `Some`/`None`/`Ok`/`Err`, exhaustive `match`, and `map`/`bind` for railway-oriented composition inside the language itself.

> **Status: private research repo, not published to NuGet.** No compiler to `Klexir.Runtime` bytecode yet — this evaluator interprets the typed AST directly. See [What can't Klexir do yet?](#what-cant-klexir-do-yet) below. Reference the project directly until/unless it's published.

---

## Quick example

```csharp
var source = "let double = fun (x: Int) => x * 2 in if double 5 > 8 then double 5 else 0";

Result<IReadOnlyList<Token>> tokens = new Lexer(source).Tokenize();
Result<Expr> ast = new Parser(tokens.Value).ParseExpression();
Result<TypedExpr> typed = new TypeChecker().Check(ast.Value);
Result<KlexirValue> value = new Evaluator().Evaluate(typed.Value);

value.Value; // IntValue(10)
```

Every stage returns `Result<T>` — an unbound identifier, a type mismatch, a malformed `let`, applying a non-function, a division by zero: all come back as a failed `Result` with a message (and, through parsing, a source position), never a thrown exception.

Closures capture their defining environment for real, so currying works:

```csharp
Run("let add = fun (x: Int) => fun (y: Int) => x + y in add 3 4"); // IntValue(7)
```

`let rec` gives a function access to its own name, so it can call itself — a plain `let` still can't:

```csharp
Run("""
    let rec fact = fun (n: Int): Int =>
        if n < 2 then 1 else n * fact (n - 1)
    in fact 5
    """); // IntValue(120)
```

`Option<T>` and `Result<T, E>` are real types, not a library convention — `Some`/`None`, `Ok`/`Err`, exhaustive `match`, and `map`/`bind` for railway-oriented chaining, straight from MonadicSharp's design:

```csharp
Run("""
    match bind(Ok<Bool>(5), fun (x: Int) => if x > 0 then Ok<Bool>(x * 2) else Err<Int>(false))
    with Ok(x) => x | Err(e) => 0
    """); // IntValue(10) — bind short-circuits to the Err branch the moment any step fails
```

---

## What's in the box

| Capability | API | Notes |
|---|---|---|
| Lexer | `Lexer.Tokenize()` | Identifiers/keywords, integers, operators, line/column tracking for diagnostics |
| Parser | `Parser.ParseExpression()` | Recursive-descent; precedence `let`/`if`/`fun` → comparison → `+ -` → `* /` → application → primary |
| Types | `KlexirType` (`IntType`/`BoolType`/`FunctionType`) | A real type hierarchy, not a flat enum — functions have function types |
| Type checker | `TypeChecker.Check(ast)` | Name resolution, arithmetic/comparison operand checks, `if`-branch unification, function application checks |
| Closures | `FunExpr`, `AppExpr` | `fun (x: Int) => body`; application is left-associative juxtaposition (`f x y` = `(f x) y`), binds tighter than `* /` |
| Evaluator | `Evaluator.Evaluate(typedExpr)` | Tree-walking; `IntValue`/`BoolValue`/`ClosureValue`; a closure carries its captured environment, so returning a closure from a closure (currying) works |
| Recursion | `let rec name = fun (p: T): R => body in ...` | The function's own name is visible inside its body (a plain `let` still isn't); return type must be written explicitly — no inference |
| `Option<T>` | `Some(v)`, `None<T>`, `KlexirType.OptionType` | Element type is inferred from `v` for `Some`; `None` needs it written explicitly (`None<Int>`), since it carries no value to infer from |
| `Result<T, E>` | `Ok<E>(v)`, `Err<T>(v)`, `KlexirType.ResultType` | The constructor infers the type it can (the value's) and takes the other one explicitly — mirrors `Option`'s `None` |
| Pattern matching | `match e with Some(x) => a \| None => b` / `match e with Ok(x) => a \| Err(e) => b` | Exhaustive by construction (only two variants each); both `match` arms must agree on type |
| Functor/Monad ops | `map(container, mapper)`, `bind(container, mapper)` | `map` transforms Some/Ok and passes None/Err through untouched; `bind` chains a container-returning function and short-circuits on None/Err — Result's error type can't change mid-chain |

### Language sample

```
let square = fun (x: Int) => x * x in
let isBig  = fun (x: Int) => x > 100 in
if isBig (square 11) then square 11 else 0
```

```
let rec safeDiv = fun (n: Int): Result<Int, Bool> =>
    if n == 0 then Err<Int>(true) else Ok<Bool>(100 / n)
in
match bind(safeDiv 4, fun (x: Int) => Ok<Bool>(x + 1))
with Ok(x) => x | Err(e) => 0
```

## What can't Klexir do yet?

A Klexir *expression* runs end to end today (see the quick example above), and `Option<T>`/`Result<T, E>` are real, first-class, pattern-matchable types in the language now — not just how the compiler happens to be written. What's still missing before it's a language you'd write a real program in:

1. **A compiler to `Klexir.Runtime` bytecode.** The evaluator interprets the AST directly (tree-walking) — there's no IR, no codegen, no way to produce a standalone `.klx` bytecode file `Klexir.Runtime` can run without this repo present. (`Klexir.Runtime` also has no local-variable or jump opcodes yet, which a real codegen would need.)
2. **Real language features.** No strings, no collections, no user-defined records/ADTs, no negative integer literals, no modules, no I/O. `Option`/`Result` are the only sum types — they're built into the checker/evaluator, not something a Klexir program can define for itself.
3. **.NET interop.** Klexir code has no way to call into C# or exchange values with a hosting .NET application — `Option`/`Result` are modeled *inside* the language now, but a Klexir `SomeValue`/`OkValue` and a real MonadicSharp `Option<T>`/`Result<T>` are still two separate types with no bridge between them.

**If the goal is "build a real solution using Result-oriented, railway-style code today,"** [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) is still where you'd do it, in C#, in production, right now — Klexir.Lang can express the same `bind`/`map` chains as actual language syntax, but a real compiler backend and a bridge back to .NET are still the bigger remaining project.

## Requirements

.NET 8 SDK + [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>`.
