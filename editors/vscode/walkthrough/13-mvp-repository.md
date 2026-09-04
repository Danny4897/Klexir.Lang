## MVP 2/6 — Repository

Il repository possiede i dati e risponde a UNA sola domanda ("questo codice fiscale c'e' gia'?"), senza sapere nulla di eta', validazione o registrazione. E' il principio di **singola responsabilita'**: una funzione, un motivo per cambiare.

```klexir
record User { CodiceFiscale: String, Age: Int };

let existingUsers = [
    User { CodiceFiscale: "RSSMRA80A01H501U", Age: 45 },
    User { CodiceFiscale: "VRDLGU90B02F205X", Age: 34 }
];

let existsInRepo = fun (cf: String) =>
    let matches = fold(filter(existingUsers, fun (u: User) => u.CodiceFiscale == cf), 0,
        fun (acc: Int) => fun (u: User) => acc + 1) in
    matches > 0;

if existsInRepo "RSSMRA80A01H501U" then 1 else 0
```

`filter` tiene solo gli utenti con quel codice fiscale, `fold` li conta, `> 0` dice se ce n'e' almeno uno. Nota il `let matches = ... in matches > 0` invece di scrivere `fold(...) > 0` direttamente: oggi in Klexir un `fold`/`filter`/`map`/`bind` non puo' essere seguito da un operatore di confronto nella stessa espressione (limite noto del parser) — dargli un nome con `let` e' anche piu' leggibile, non solo un aggiramento.

[Apri l'esempio e provalo](command:klexir.openSample.13)
