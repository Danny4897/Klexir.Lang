## Option e Result: railway-oriented programming

`Option<T>` (`Some`/`None`) dice "trovato o no"; `Result<T, E>` (`Ok`/`Err`) aggiunge un errore vero. Sono tipi di prima classe, non una convenzione — con `match` esaustivo e `andThen` per incatenare passi senza controlli manuali, leggendo il codice come una frase.

```klexir
let findUser = func(Int id) =>
    if id == 1 then Some(42) else None<Int>;

let toResult = func(Int id) =>
    match findUser id with Some(x) => Ok<Bool>(x) | None => Err<Int>(true);

let checkPositive = func(Int x) =>
    if x > 0 then Ok<Bool>(x) else Err<Int>(false);

// "prova toResult, POI checkPositive" -- se il primo fallisce, il secondo non gira nemmeno.
let pipeline = func(Int id) => toResult id andThen checkPositive;

match pipeline 1 with Ok(x) => x | Err(e) => 0 - 1
```

`andThen` e' zucchero sintattico su `bind` — `a andThen f` e' esattamente `bind(a, f)`, solo scritto da sinistra a destra invece che annidato. `bind(container, mapper)` resta utile quando la funzione da incatenare e' un valore qualunque (passata come argomento a un'altra funzione, per esempio), non solo scritta li' per nome. Corto-circuita al primo `Err`/`None`: questo e' esattamente lo stile di [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) (`.Bind(...).Bind(...)`), la libreria .NET da cui Klexir eredita l'idea — Rust ed Elm chiamano la stessa identica operazione `and_then`/`andThen`.

[Apri l'esempio e provalo](command:klexir.openSample.04)
