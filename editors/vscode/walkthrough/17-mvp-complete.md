## MVP 6/6 — Il programma completo

Ultimo pezzo: `RegistrationOutcome` (definito fin dallo step 1) sostituisce l'`Int` overloaded dello step 5 — `Registered(timestamp)` e `Rejected(codice)` sono esplicitamente due cose diverse, non lo stesso tipo con due significati.

```klexir
// --- Modello ---
record User { CodiceFiscale: String, Age: Int };
union RegistrationOutcome { Registered(Int), Rejected(Int) };

// --- Repository (in-memory) ---
let existingUsers = [
    User { CodiceFiscale: "RSSMRA80A01H501U", Age: 45 },
    User { CodiceFiscale: "VRDLGU90B02F205X", Age: 34 }
];

let existsInRepo = func(String cf) =>
    fold(filter(existingUsers, func(User u) => u.CodiceFiscale == cf), 0,
        func(Int acc) => func(User u) => acc + 1) > 0;

// --- Funzioni atomiche di validazione ---
let checkAdult = func(User u) =>
    if u.Age >= 18 then Ok<Int>(u) else Err<User>(1);

let checkCodiceFiscale = func(User u) =>
    if u.CodiceFiscale == "" then Err<User>(2) else Ok<Int>(u);

let checkNotInDb = func(User u) =>
    if existsInRepo u.CodiceFiscale then Err<User>(3) else Ok<Int>(u);

// --- Service: pipeline di validazione ---
let validateNewUser = func(User u) =>
    checkAdult u andThen checkCodiceFiscale andThen checkNotInDb;

// --- Controller: valido -> registra (timestamp dal plugin Clock), invalido -> motivo ---
let registerUser = func(User u) =>
    match validateNewUser u with
        Ok(valid) => Registered (now true)
        | Err(code) => Rejected code;

// --- "main": prova a registrare un nuovo utente ---
match registerUser (User { CodiceFiscale: "NWUSER01A01A000X", Age: 30 }) with
    Registered(ts) => ts
    | Rejected(code) => 0 - code
```

Esegui con `--plugin=clock`. Prova a cambiare il codice fiscale in `"RSSMRA80A01H501U"` (gia' a DB) o l'eta' in `15` (minorenne) e guarda il risultato diventare negativo — il match finale su `RegistrationOutcome` rende impossibile confondere un timestamp con un errore, a compile time.

### Checklist di clean code applicata

- **Un tipo per ogni concetto** — `User` per il dato, `RegistrationOutcome` per il risultato, mai un `Int` che significa due cose.
- **Funzioni atomiche, un solo motivo di fallimento** — `checkAdult`/`checkCodiceFiscale`/`checkNotInDb` non sanno l'una dell'altra.
- **Composizione esplicita, non nascosta** — `validateNewUser` e' l'UNICO posto che conosce l'ordine dei controlli.
- **Un solo punto di conversione Result -> risposta** — il controller (`registerUser`), mai sparso nei livelli sotto.
- **Nessuna eccezione, nessun `null`** — ogni fallimento e' un valore (`Err`, un codice), gestito con `match`, mai un `try/catch` implicito.
- **Le capacita' esterne (l'orologio) sono un plugin esplicito** — non una chiamata nascosta a `DateTime.Now` sparsa nel codice.

Da qui: prova a spostare `checkNotInDb` come primo controllo nella pipeline, o ad aggiungere un quarto check (per esempio la lunghezza del codice fiscale) — se il resto del codice non ha bisogno di cambiare, la struttura sta funzionando come dovrebbe.

[Apri l'esempio e provalo](command:klexir.openSample.17)
