## Tutto insieme: controller / service / repository

Tre livelli, ciascuno con una sola responsabilita' e il proprio confine `Result`/`Option`, composti con `bind` invece di `if (result.IsFailure) return ...` a ogni chiamata.

```klexir
// Repository: possiede i dati, restituisce Option -- "trovato o no", senza un perche'.
let findUserAge = func(Int userId) =>
    if userId == 1 then Some(17)        // utente noto, minorenne
    else if userId == 2 then Some(25)   // utente noto, maggiorenne
    else None<Int>;                     // utente sconosciuto

// Adapter: l'Option del repository diventa un Result per il service layer, con un errore vero.
let toLookupResult = func(Option<Int> age) =>
    match age with Some(x) => Ok<Int>(x) | None => Err<Int>(1);   // 1 = utente non trovato

// Service: la regola di business, indifferente a da dove viene il valore.
let checkAdult = func(Int age) =>
    if age >= 18 then Ok<Int>(age) else Err<Int>(2);              // 2 = minorenne

// Service: orchestratore -- bind salta a Err(1) senza mai chiamare checkAdult.
let getAdultAge = func(Int userId) =>
    bind(toLookupResult (findUserAge userId), checkAdult);

// Controller: l'unico livello a cui e' permesso trasformare un Result in una risposta semplice.
let handleGetAdultAge = func(Int userId) =>
    match getAdultAge userId with Ok(age) => age | Err(code) => code;

handleGetAdultAge 2   // 25 -- maggiorenne, l'eta' passa cosi' com'e'
```

`handleGetAdultAge 1` da' `2` (minorenne, `checkAdult` gira e rifiuta). `handleGetAdultAge 99` da' `1` (utente sconosciuto — `checkAdult` non gira nemmeno, `bind` ha gia' interrotto al repository). Tre punti di chiamata, un `match`, zero controlli manuali di propagazione: e' quello che compra `bind`.

Da qui in poi il resto e' compilare il sorgente reale del linguaggio (`src/Klexir.Lang`), leggere il README per i limiti noti, o provare i plugin per aggiungere capacita' native.

[Apri l'esempio e provalo](command:klexir.openSample.10)
