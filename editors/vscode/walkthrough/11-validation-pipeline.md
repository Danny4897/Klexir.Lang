## Pipeline di validazione: piu' funzioni atomiche, una `andThen` alla volta

Ogni check e' una funzione **atomica**: fa un solo controllo, non sa nulla degli altri, e restituisce un `Result<NewUser, Int>` — l'utente stesso se va bene, un codice d'errore se no. Comporli e' `checkAdult u andThen checkCodiceFiscale andThen checkNotInDb`: si legge come una frase, "prova questo, POI questo, POI questo" — la stessa idea del `.Bind(...).Bind(...)` di [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) in C#, senza sintassi a metodo.

```klexir
record NewUser { Age: Int, CodiceFiscale: String };

// Funzione atomica 1: e' maggiorenne?
let checkAdult = func(NewUser u) =>
    if u.Age >= 18 then Ok<Int>(u) else Err<NewUser>(1);   // 1 = minorenne

// Funzione atomica 2: il codice fiscale e' valorizzato?
let checkCodiceFiscale = func(NewUser u) =>
    if u.CodiceFiscale == "" then Err<NewUser>(2) else Ok<Int>(u);   // 2 = codice fiscale mancante

// Funzione atomica 3: non e' gia' a DB? ("DB" simulato: un solo codice fiscale gia' noto)
let checkNotInDb = func(NewUser u) =>
    if u.CodiceFiscale == "RSSMRA80A01H501U" then Err<NewUser>(3) else Ok<Int>(u);   // 3 = gia' presente

// La pipeline, letta come una frase: ogni andThen incatena il passo successivo
// SOLO se il precedente e' andato bene.
let validateNewUser = func(NewUser u) =>
    checkAdult u andThen checkCodiceFiscale andThen checkNotInDb;

// Controller: l'unico punto che trasforma il Result in una risposta semplice.
let addUser = func(NewUser u) =>
    match validateNewUser u with Ok(valid) => 0 | Err(code) => code;

addUser (NewUser { Age: 25, CodiceFiscale: "ABCDEF12G34H567I" })
```

`andThen` e' zucchero sintattico: `a andThen f` e' esattamente `bind(a, f)`, associativo a sinistra — `x andThen f andThen g` e' `bind(bind(x, f), g)`. Prova a cambiare `Age` a `15`, poi `CodiceFiscale` a `""`, poi a `"RSSMRA80A01H501U"` — vedrai rispettivamente `1`, `2`, `3`: il primo controllo che fallisce vince, quelli dopo non girano nemmeno. Ogni funzione atomica resta testabile e leggibile da sola — la pipeline e' solo composizione, zero `if (result.IsFailure) return ...` sparsi ovunque.

[Apri l'esempio e provalo](command:klexir.openSample.11)
