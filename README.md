# Klexir.Lang

[![CI](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml/badge.svg)](https://github.com/Danny4897/Klexir.Lang/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

The Klexir programming language — currently a **front end only**: a lexer, a recursive-descent parser, and a structural type checker. Built on [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>` — no exceptions for compiler-control flow, ever.

> **⚠️ This cannot run programs yet.** There is no evaluator for the AST and no compiler emitting `Klexir.Runtime` bytecode. `Lexer` and `Parser` and `TypeChecker` will tell you whether a Klexir program is well-formed and well-typed — they will not execute it. See [Can I write something real in Klexir yet?](#can-i-write-something-real-in-klexir-yet) below.

> **Status: private research repo, not published to NuGet.** Reference the project directly until/unless it's published.

---

## Quick example

```csharp
var source = "let double = fun (x: Int) => x * 2 in if double 5 > 8 then double 5 else 0";

Result<IReadOnlyList<Token>> tokens = new Lexer(source).Tokenize();
Result<Expr> ast = new Parser(tokens.Value).ParseExpression();
Result<TypedExpr> typed = new TypeChecker().Check(ast.Value);

typed.Value.Type; // KlexirType.Int
```

Every stage returns `Result<T>` — an unbound identifier, a type mismatch, a malformed `let`, applying a non-function: all come back as a failed `Result` with a message and source position, never a thrown exception.

---

## What's in the box

| Capability | API | Notes |
|---|---|---|
| Lexer | `Lexer.Tokenize()` | Identifiers/keywords, integers, operators, line/column tracking for diagnostics |
| Parser | `Parser.ParseExpression()` | Recursive-descent; precedence `let`/`if`/`fun` → comparison → `+ -` → `* /` → application → primary |
| Types | `KlexirType` (`IntType`/`BoolType`/`FunctionType`) | A real type hierarchy, not a flat enum — functions have function types |
| Type checker | `TypeChecker.Check(ast)` | Name resolution, arithmetic/comparison operand checks, `if`-branch unification, function application checks |
| Closures | `FunExpr`, `AppExpr` | `fun (x: Int) => body`; application is left-associative juxtaposition (`f x y` = `(f x) y`), binds tighter than `* /` |

### Language sample

```
let square = fun (x: Int) => x * x in
let isBig  = fun (x: Int) => x > 100 in
if isBig (square 11) then square 11 else 0
```

## Can I write something real in Klexir yet?

**No — not as standalone Klexir code that runs on its own.** Three pieces are still missing:

1. **An evaluator or a compiler backend.** `TypeChecker` produces a `TypedExpr` tree, but nothing walks it to produce a value, and nothing emits `Klexir.Runtime` bytecode from it (`Klexir.Runtime` has no local-variable or jump opcodes yet either — see that repo's README). Until one of those exists, a well-typed Klexir program can be checked but not run.
2. **Real language features.** No strings, no collections, no records/ADTs, no pattern matching, no modules, no I/O. The language today is `let`, `if`, arithmetic, comparisons, booleans, and single-argument functions over `Int`/`Bool` — enough to prove the front-end pipeline, not enough to write a program that does something.
3. **Interop with .NET/MonadicSharp.** Even once Klexir code can run, it has no way today to call into C# or use `Result<T>`/`Option<T>` itself — those ideas would need to be *modeled inside* the language (which is philosophically the point — "the language's own error handling is `Result`/`Option`-shaped," per the study plan — but that design doesn't exist yet).

**What you *can* do today** is use `Klexir.Lang` as a parser/checker library from C# — e.g. to validate that a string is well-formed Klexir — or keep building toward an evaluator as the next increment.

**If the goal is "build a real solution using Result-oriented, railway-style code today,"** that's exactly what [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) already does, in C#, in production, right now — no interpreter required. Klexir.Lang becoming a usable language is a from-scratch compiler project (front end ✅ here → IR/codegen → an evaluator or a Runtime target with more opcodes → a standard library), realistically weeks of further work, not the next commit.

## Requirements

.NET 8 SDK + [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) `Result<T>`.
