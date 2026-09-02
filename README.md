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

`TypeChecker.Check(ast)` walks the AST into a `TypedExpr` tree (`TypedIntLiteral`/`TypedIdentifier`/`TypedBinaryExpr`/`TypedLetExpr`), threading a `let`-scoped environment. The language has exactly one type today (`KlexirType.Int`), so the meaningful check right now is that every identifier is bound before use — and that a `let`'s binding never leaks outside its own body. Real type mismatches become checkable once the language grows a second type.

Pattern matching/ADTs, closures/modules, and the IR/bytecode emitter targeting `Klexir.Runtime` follow in later increments.
