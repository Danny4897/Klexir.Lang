## MVP 5/6 — Controller e un plugin vero

Il controller e' l'unico livello a cui e' permesso trasformare un `Result` in una risposta — e qui entra un plugin per davvero: alla registrazione riuscita associamo un timestamp reale, letto dal `Clock` plugin (`now`), non inventato.

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

let validateNewUser = fun (u: User) =>
    checkAdult u andThen checkCodiceFiscale andThen checkNotInDb;

// Controller: valido -> timestamp di registrazione (dal plugin Clock), invalido -> il codice di errore.
let registerUser = fun (u: User) =>
    match validateNewUser u with Ok(valid) => now true | Err(code) => code;

registerUser (User { CodiceFiscale: "NWUSER01A01A000X", Age: 30 })
```

Esegui questo file con `--plugin=clock` (o `klexir.plugins: ["clock"]` nelle Impostazioni per `Ctrl+F5`). Il risultato e' un `Int` che a volte e' un timestamp enorme, a volte un piccolo codice errore — funziona, ma non e' chiaro leggendolo. E' esattamente il problema che il `RegistrationOutcome` dello step 1 risolve: nell'ultimo step lo colleghiamo per davvero.

[Apri l'esempio e provalo](command:klexir.openSample.16)
