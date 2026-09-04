## Pipeline di validazione: piu' funzioni atomiche, una `bind` alla volta

Ogni check e' una funzione **atomica**: fa un solo controllo, non sa nulla degli altri, e restituisce un `Result<NewUser, Int>` — l'utente stesso se va bene, un codice d'errore se no. Comporli e' `bind(bind(check1 u, check2), check3)`: la stessa idea del `.Bind(...).Bind(...)` di [MonadicSharp](https://www.nuget.org/packages/MonadicSharp/) in C#, scritta come chiamate a funzione invece che a metodo — in Klexir non c'e' sintassi `a.b()`, solo applicazione.

```klexir
record NewUser { Age: Int, CodiceFiscale: String };

// Funzione atomica 1: e' maggiorenne?
let checkAdult = fun (u: NewUser) =>
    if u.Age >= 18 then Ok<Int>(u) else Err<NewUser>(1);   // 1 = minorenne

// Funzione atomica 2: il codice fiscale e' valorizzato?
let checkCodiceFiscale = fun (u: NewUser) =>
    if u.CodiceFiscale == "" then Err<NewUser>(2) else Ok<Int>(u);   // 2 = codice fiscale mancante

// Funzione atomica 3: non e' gia' a DB? ("DB" simulato: un solo codice fiscale gia' noto)
let checkNotInDb = fun (u: NewUser) =>
    if u.CodiceFiscale == "RSSMRA80A01H501U" then Err<NewUser>(3) else Ok<Int>(u);   // 3 = gia' presente

// La pipeline: ogni bind incatena il passo successivo SOLO se il precedente e' andato bene.
let validateNewUser = fun (u: NewUser) =>
    bind(bind(checkAdult u, checkCodiceFiscale), checkNotInDb);

// Controller: l'unico punto che trasforma il Result in una risposta semplice.
let addUser = fun (u: NewUser) =>
    match validateNewUser u with Ok(valid) => 0 | Err(code) => code;

addUser (NewUser { Age: 25, CodiceFiscale: "ABCDEF12G34H567I" })
```

Prova a cambiare `Age` a `15`, poi `CodiceFiscale` a `""`, poi a `"RSSMRA80A01H501U"` — vedrai rispettivamente `1`, `2`, `3`: il primo controllo che fallisce vince, quelli dopo non girano nemmeno (esattamente come `checkAdult` che salta `checkCodiceFiscale` nell'esempio del layered architecture). Ogni funzione atomica resta testabile e leggibile da sola — la pipeline e' solo composizione, zero `if (result.IsFailure) return ...` sparsi ovunque.

[Apri l'esempio e provalo](command:klexir.openSample.11)
