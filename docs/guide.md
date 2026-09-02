# Quick example

```
// hello.klx
record User { Id: Int, Age: Int };
let isAdult = fun (u: User) => u.Age >= 18;
isAdult (User { Id: 1, Age: 25 })
```

```bash
$ dotnet run --project src/Klexir.Cli -- run hello.klx
true
```

Unions are sum types — a value that's exactly one of several variants, checked exhaustively:

```
union Shape { Circle(Int), Rectangle(Int, Int) };
let area = fun (s: Shape) => match s with Circle(r) => r * r * 3 | Rectangle(w, h) => w * h;
area (Rectangle 3 5)   // 15 — a variant with fields constructs via ordinary curried application
```

And `Option<T>`/`Result<T, E>` are real, pattern-matchable types with `map`/`bind` for railway-oriented composition:

```
match bind(Ok<Bool>(5), fun (x: Int) => if x > 0 then Ok<Bool>(x * 2) else Err<Int>(false))
with Ok(x) => x | Err(e) => 0
```

See the [full README](https://github.com/Danny4897/Klexir.Lang#readme) on GitHub for the complete syntax reference, a controller/service/repository example, and the current gaps (no generics yet, no recursive ADTs, no calling .NET *from* Klexir).
