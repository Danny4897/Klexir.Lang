# Klexir.Lang

[![CI](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml/badge.svg)](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

The Klexir programming language: a lexer, a recursive-descent parser, a structural type checker, and a tree-walking evaluator — so a Klexir program written as a string can now be tokenized, parsed, type-checked, and actually **run** to a value. Built on [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>` — no exceptions for compiler-control flow, ever, evaluation included. `Option<T>` and `Result<T, E>` aren't just how the compiler is implemented — they're first-class Klexir *types*, with `Some`/`None`/`Ok`/`Err`, exhaustive `match`, and `map`/`bind` for railway-oriented composition inside the language itself. `Int`, `Bool`, `String`, and `List<T>` are the ground data types, and `KlexirInterop` bridges a Klexir program's `Option`/`Result` straight into a real MonadicSharp `Option<T>`/`Result<T>` for a hosting .NET app.

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

Strings and `List<T>` are real ground types too, with `map`/`filter`/`fold` on lists:

```csharp
Run("""fold(filter([1, 2, 3, 4, 5], fun (x: Int) => x > 2), 0, fun (acc: Int) => fun (x: Int) => acc + x)""");
// IntValue(12) — keep 3,4,5, then sum them
```

And `KlexirInterop` bridges a Klexir `Result` straight into a real MonadicSharp one, so a hosting app gets back the type it already knows how to work with:

```csharp
var klexirValue = Run("""let safeDiv = fun (n: Int) => if n == 0 then Err<Int>(true) else Ok<Bool>(100 / n) in safeDiv 4""");
Result<long> bridged = KlexirInterop.ToResult(klexirValue, KlexirInterop.AsInt, e => "division by zero");
bridged.Value; // 25 — a real MonadicSharp Result<long>, not a KlexirValue tree
```

A program is a sequence of top-level declarations (`;`-terminated, no `in`) plus a final expression — closer to what an actual `.klx` source file would look like than one giant `let ... in`:

```csharp
Result<Expr> ast = new Parser(tokens.Value).ParseProgram();
```

```
let square = fun (x: Int) => x * x;
let cube = fun (x: Int) => x * square x;
cube 3   // 27
```

Records are real user-defined product types now, not another `Int` stand-in — construction is checked by field name (order doesn't matter), and `.Field` reads a value back out:

```
record User { Id: Int, Age: Int };
let isAdult = fun (u: User) => u.Age >= 18;
isAdult (User { Age: 25, Id: 1 })   // true
```

---

## What's in the box

| Capability | API | Notes |
|---|---|---|
| Lexer | `Lexer.Tokenize()` | Identifiers/keywords, integers, operators, line/column tracking for diagnostics |
| Parser | `Parser.ParseExpression()` | Recursive-descent; precedence `let`/`if`/`fun` → comparison → `+ -` → `* /` → application → primary |
| Programs | `Parser.ParseProgram()` | A sequence of top-level `let`/`let rec` declarations — each ending in `;`, no `in` needed — followed by a final expression; desugars to the exact same nested-`let` AST, so the checker/evaluator need no changes |
| Records | `record Name { Field: Type, ... };`, `Name { Field: expr, ... }`, `expr.Field` | User-defined product types — top-level declarations only. Construction checks field names/types by name (order doesn't matter); `RecordType` is nominal (equal by name), so a function parameter's record type resolves against the enclosing `record` declaration |
| Types | `KlexirType` (`IntType`/`BoolType`/`FunctionType`) | A real type hierarchy, not a flat enum — functions have function types |
| Type checker | `TypeChecker.Check(ast)` | Name resolution, arithmetic/comparison operand checks, `if`-branch unification, function application checks |
| Closures | `FunExpr`, `AppExpr` | `fun (x: Int) => body`; application is left-associative juxtaposition (`f x y` = `(f x) y`), binds tighter than `* /` |
| Evaluator | `Evaluator.Evaluate(typedExpr)` | Tree-walking; `IntValue`/`BoolValue`/`ClosureValue`; a closure carries its captured environment, so returning a closure from a closure (currying) works |
| Recursion | `let rec name = fun (p: T): R => body in ...` | The function's own name is visible inside its body (a plain `let` still isn't); return type must be written explicitly — no inference |
| `Option<T>` | `Some(v)`, `None<T>`, `KlexirType.OptionType` | Element type is inferred from `v` for `Some`; `None` needs it written explicitly (`None<Int>`), since it carries no value to infer from |
| `Result<T, E>` | `Ok<E>(v)`, `Err<T>(v)`, `KlexirType.ResultType` | The constructor infers the type it can (the value's) and takes the other one explicitly — mirrors `Option`'s `None` |
| Pattern matching | `match e with Some(x) => a \| None => b` / `match e with Ok(x) => a \| Err(e) => b` | Exhaustive by construction (only two variants each); both `match` arms must agree on type |
| Functor/Monad ops | `map(container, mapper)`, `bind(container, mapper)` | `map` transforms Some/Ok and passes None/Err through untouched; `bind` chains a container-returning function and short-circuits on None/Err — Result's error type can't change mid-chain |
| `String` | `"..."`, `+` (concatenation), `==` | Literals support `\" \\ \n` escapes; `+` on two `String`s concatenates instead of adding; ordering (`< > <= >=`) isn't defined for strings |
| `List<T>` | `[e1, e2, ...]`, `[]<T>`, `KlexirType.ListType` | Element type inferred from the elements (must all agree); an empty list needs its type written explicitly, same as `None` |
| List ops | `map(list, fn)`, `filter(list, predicate)`, `fold(list, initial, folder)` | `map` doubles as List's Functor op; `filter` keeps elements where `predicate` is `true`; `fold` is left-fold with a curried `Acc -> Elem -> Acc` folder |
| MonadicSharp bridge | `KlexirInterop.ToOption/ToResult/FromOption/FromResult`, `AsInt/AsBool/AsString` | Converts a Klexir `Some`/`None`/`Ok`/`Err` value to/from a real `MonadicSharp.Option<T>`/`Result<T>` — a hosting .NET app gets the type it already works with, not a `KlexirValue` tree to pattern-match by hand |

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

### Layered architecture: controller → service → repository

Klexir has no user-defined records yet (see [below](#what-cant-klexir-do-yet)), so this still stands in `Int` for an entity id/value and an `Int` error code for what would normally be a typed exception/error enum — `String` exists now, but a real `User` record with named fields doesn't. What it *does* show for real: three independent functions, each with its own single responsibility and its own `Result`/`Option` boundary, composed with `bind` instead of `if (result.IsFailure) return ...` — the railway short-circuits through the whole call chain on the first failure, no branching required at the call site.

```
// --- repository: owns the data, returns Option — "found or not", no notion of *why* ---
let findUserAge = fun (userId: Int) =>
    if userId == 1 then Some(17)        // known user, underage
    else if userId == 2 then Some(25)   // known user, adult
    else None<Int>                      // unknown user
in

// --- adapter: repository's Option becomes the service layer's Result, with a real error code ---
let toLookupResult = fun (age: Option<Int>) =>
    match age with Some(x) => Ok<Int>(x) | None => Err<Int>(1)   // 1 = user not found
in

// --- service: the business rule, oblivious to where the value came from ---
let checkAdult = fun (age: Int) =>
    if age >= 18 then Ok<Int>(age) else Err<Int>(2)              // 2 = underage
in

// --- service: orchestrates repo + rule; bind short-circuits to Err(1) without ever calling checkAdult ---
let getAdultAge = fun (userId: Int) =>
    bind(toLookupResult (findUserAge userId), checkAdult)
in

// --- controller: the only layer allowed to turn a Result back into a plain response value ---
let handleGetAdultAge = fun (userId: Int) =>
    match getAdultAge userId with Ok(age) => age | Err(code) => code
in

handleGetAdultAge 2   // 25 — adult, age passed through
```

`handleGetAdultAge 1` returns `2` (underage, `checkAdult` ran and rejected it); `handleGetAdultAge 99` returns `1` (unknown user — `checkAdult` never even runs, `bind` short-circuited at the repository boundary). Three call sites, one `match`, zero manual "if failed, propagate" checks — that's what `bind` buys you.

## What can't Klexir do yet?

A Klexir *program* — not just an expression — runs end to end today; `Option<T>`/`Result<T, E>` are real, first-class, pattern-matchable types; `String`/`List<T>`/user-defined `record`s cover real data, not just `Int`/`Bool`; and `KlexirInterop` bridges Klexir's `Option`/`Result` to the real MonadicSharp ones for a hosting .NET app. What's still missing before it's a language you'd write a real program in:

1. **A compiler to `Klexir.Runtime` bytecode.** The evaluator interprets the AST directly (tree-walking) — there's no IR, no codegen, no way to produce a standalone `.klx` bytecode file `Klexir.Runtime` can run without this repo present. (`Klexir.Runtime` also has no local-variable or jump opcodes yet, which a real codegen would need.)
2. **Sum types and generics stay closed.** `record` gives you product types, but there's still no user-defined *union*/ADT (`Option`/`Result` are the only two, built into the checker) — and nothing in Klexir is generic: you can't write your own `Pair<A, B>` or a function that works over any `T`. No modules, no I/O, no negative integer literals either. A record type name that's never field-accessed or constructed also isn't validated against an actual `record` declaration — a typo in an unused parameter's type silently type-checks (see `RecordType`'s doc comment in `TypedExpr.cs`).
3. **Calling .NET *from* Klexir.** `KlexirInterop` bridges Klexir values *out* to real MonadicSharp types for a hosting app to consume — but Klexir code itself still can't call an arbitrary C# method or use a .NET library; the bridge is one-directional at the language boundary, not a general FFI.

**If the goal is "build a real solution using Result-oriented, railway-style code today,"** [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) is still where you'd do it, in C#, in production, right now — Klexir.Lang can express the same `bind`/`map` chains and record-shaped data as actual language syntax and hand the result back to MonadicSharp via `KlexirInterop`, but a real compiler backend, sum types, and generics are still the bigger remaining project.

## Requirements

.NET 8 SDK + [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>`.
