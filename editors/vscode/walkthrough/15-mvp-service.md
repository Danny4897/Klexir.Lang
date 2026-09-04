## MVP 4/6 — Service: comporre con andThen

Il service non valida nulla da solo — orchestra le tre funzioni atomiche dello step precedente. Questa e' la parte che nei linguaggi senza `Result`/`andThen` diventa una piramide di `if (result.IsFailure) return ...`: qui e' una riga.

```klexir
record User { CodiceFiscale: String, Age: Int };

let existingUsers = [
    User { CodiceFiscale: "RSSMRA80A01H501U", Age: 45 }
];

let existsInRepo = func(String cf) =>
    fold(filter(existingUsers, func(User u) => u.CodiceFiscale == cf), 0,
        func(Int acc) => func(User u) => acc + 1) > 0;

let checkAdult = func(User u) =>
    if u.Age >= 18 then Ok<Int>(u) else Err<User>(1);

let checkCodiceFiscale = func(User u) =>
    if u.CodiceFiscale == "" then Err<User>(2) else Ok<Int>(u);

let checkNotInDb = func(User u) =>
    if existsInRepo u.CodiceFiscale then Err<User>(3) else Ok<Int>(u);

let validateNewUser = func(User u) =>
    checkAdult u andThen checkCodiceFiscale andThen checkNotInDb;

match validateNewUser (User { CodiceFiscale: "NWUSER01A01A000X", Age: 30 }) with Ok(x) => 0 | Err(code) => code
```

`validateNewUser` e' l'unico posto che conosce l'ORDINE dei controlli — cambiarlo (per esempio controllare prima il codice fiscale) significa toccare solo questa riga, non ognuna delle tre funzioni atomiche.

[Apri l'esempio e provalo](command:klexir.openSample.15)
