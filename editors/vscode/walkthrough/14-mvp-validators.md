## MVP 3/6 — Funzioni atomiche di validazione

Tre funzioni, tre domande indipendenti, ciascuna con un solo motivo di fallimento. Nessuna sa dell'esistenza delle altre — questo e' cio' che le rende testabili una per una e riusabili altrove.

```klexir
record User { CodiceFiscale: String, Age: Int };

let existingUsers = [
    User { CodiceFiscale: "RSSMRA80A01H501U", Age: 45 }
];

let existsInRepo = fun (cf: String) =>
    let matches = fold(filter(existingUsers, fun (u: User) => u.CodiceFiscale == cf), 0,
        fun (acc: Int) => fun (u: User) => acc + 1) in
    matches > 0;

let checkAdult = fun (u: User) =>
    if u.Age >= 18 then Ok<Int>(u) else Err<User>(1);

let checkCodiceFiscale = fun (u: User) =>
    if u.CodiceFiscale == "" then Err<User>(2) else Ok<Int>(u);

let checkNotInDb = fun (u: User) =>
    if existsInRepo u.CodiceFiscale then Err<User>(3) else Ok<Int>(u);

match checkNotInDb (User { CodiceFiscale: "RSSMRA80A01H501U", Age: 40 }) with Ok(x) => 0 | Err(code) => code
```

Nota la firma: ognuna prende un `User` e restituisce `Result<User, Int>` — l'utente stesso se il controllo passa (cosi' il prossimo controllo puo' continuare a lavorarci), un codice se fallisce. Firme identiche = componibili direttamente, che e' esattamente il prossimo step.

[Apri l'esempio e provalo](command:klexir.openSample.14)
