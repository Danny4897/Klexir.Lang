# Klexir.Lang

[![CI](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml/badge.svg)](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

The Klexir programming language: a lexer, a recursive-descent parser, a structural type checker, and a tree-walking evaluator — so a Klexir program written as a string can now be tokenized, parsed, type-checked, and actually **run** to a value. Built on [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>` — no exceptions for compiler-control flow, ever, evaluation included.

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

### Language sample

```
let square = fun (x: Int) => x * x in
let isBig  = fun (x: Int) => x > 100 in
if isBig (square 11) then square 11 else 0
```

## What can't Klexir do yet?

A Klexir *expression* runs end to end today (see the quick example above). What's still missing before it's a language you'd write a real program in:

1. **A compiler to `Klexir.Runtime` bytecode.** The evaluator interprets the AST directly (tree-walking) — there's no IR, no codegen, no way to produce a standalone `.klx` bytecode file `Klexir.Runtime` can run without this repo present. (`Klexir.Runtime` also has no local-variable or jump opcodes yet, which a real codegen would need.)
2. **Real language features.** No strings, no collections, no records/ADTs, no pattern matching, no modules, no I/O, no recursion (a `let`-bound name isn't visible inside its own value, so a function can't call itself by name yet). The language today is `let`, `if`, arithmetic, comparisons, booleans, and closures over `Int`/`Bool` — enough for real (if tiny) programs, not enough for anything with state, text, or recursion.
3. **Interop with .NET/MonadicSharp.** Klexir code has no way to call into C# or use `Result<T>`/`Option<T>` itself — those ideas would need to be *modeled inside* the language (philosophically the point, per the study plan, but that design doesn't exist yet).

**If the goal is "build a real solution using Result-oriented, railway-style code today,"** that's exactly what [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) already does, in C#, in production, right now. Klexir.Lang becoming a language you'd reach for is still a ways off (recursion + a couple of data types would make it genuinely useful for small programs; a real compiler backend is the bigger remaining project).

## Requirements

.NET 8 SDK + [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>`.
