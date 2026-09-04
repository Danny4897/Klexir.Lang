## Option e Result: railway-oriented programming

`Option<T>` (`Some`/`None`) dice "trovato o no"; `Result<T, E>` (`Ok`/`Err`) aggiunge un errore vero. Sono tipi di prima classe, non una convenzione — con `match` esaustivo e `bind` per incatenare passi senza controlli manuali.

```klexir
let findUser = fun (id: Int) =>
    if id == 1 then Some(42) else None<Int>;

let toResult = fun (id: Int) =>
    match findUser id with Some(x) => Ok<Bool>(x) | None => Err<Int>(true);

let checkPositive = fun (x: Int) =>
    if x > 0 then Ok<Bool>(x) else Err<Int>(false);

let pipeline = fun (id: Int) => bind(toResult id, checkPositive);

match pipeline 1 with Ok(x) => x | Err(e) => 0 - 1
```

`bind` corto-circuita al primo `Err`/`None` — se `toResult` fallisce, `checkPositive` non gira nemmeno. Questo e' esattamente lo stile di [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/), la libreria .NET da cui Klexir eredita l'idea.

[Apri l'esempio e provalo](command:klexir.openSample.04)
