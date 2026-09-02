# Klexir.Lang

Klexir programming language and compiler, built on [MonadicSharp](https://www.nuget.org/packages/MonadicSharp) `Result<T>` — no exceptions for compiler-control flow.

Only `Klexir.Lang.Abstractions` is a public NuGet package (`SourcePosition`).

The first increment is a lexer and recursive-descent parser for arithmetic and `let ... in` expressions:

```csharp
var tokens = new Lexer("let x = 5 in x + 3").Tokenize();
var ast = new Parser(tokens.Value).ParseExpression();
// LetExpr("x", IntLiteral(5), BinaryExpr(Add, Identifier("x"), IntLiteral(3)))
```

`Tokenize()`/`ParseExpression()` return `Result<T>` — an unexpected character, a dangling operator, a malformed `let`, or a trailing token after a complete expression all fail the result. Precedence: `let ... in` loosest, then `+ -`, then `* /`, then primary (literals, identifiers, parenthesized expressions).

`TypeChecker.Check(ast)` walks the AST into a `TypedExpr` tree, threading a `let`-scoped environment: every identifier must be bound before use, a `let`'s binding never leaks outside its own body, arithmetic operators require `Int` operands, comparisons (`== < > <= >=`, non-chaining) require `Int` operands and produce `Bool`, and `if`/`then`/`else` requires a `Bool` condition with both branches unifying to the same type.

```csharp
new Lexer("if 1 < 2 then 10 else 20").Tokenize()
// If, Int, Less, Int, Then, Int, Else, Int, Eof
```

`KlexirType` is now a small type hierarchy (`IntType`/`BoolType`/`FunctionType`), not a flat enum — `fun (x: Int) => x + 1` parses to a `FunExpr` and type-checks to `FunctionType(Int, Int)`; application is left-associative juxtaposition (`f x y` = `(f x) y`) and binds tighter than `* /`, so `f x + 1` parses as `(f x) + 1`. Applying a non-function, or a function to the wrong argument type, fails the check. Parameter type annotations are limited to `Int`/`Bool` — a parameter typed as a function itself (higher-order functions) isn't parseable yet.

Pattern matching/ADTs, modules, and the IR/bytecode emitter targeting `Klexir.Runtime` follow in later increments.
