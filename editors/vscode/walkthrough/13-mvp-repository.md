## MVP 2/6 — Repository

Il repository possiede i dati e risponde a UNA sola domanda ("questo codice fiscale c'e' gia'?"), senza sapere nulla di eta', validazione o registrazione. E' il principio di **singola responsabilita'**: una funzione, un motivo per cambiare.

```klexir
record User { CodiceFiscale: String, Age: Int };

let existingUsers = [
    User { CodiceFiscale: "RSSMRA80A01H501U", Age: 45 },
    User { CodiceFiscale: "VRDLGU90B02F205X", Age: 34 }
];

let existsInRepo = func(String cf) =>
    fold(filter(existingUsers, func(User u) => u.CodiceFiscale == cf), 0,
        func(Int acc) => func(User u) => acc + 1) > 0;

if existsInRepo "RSSMRA80A01H501U" then 1 else 0
```

`filter` tiene solo gli utenti con quel codice fiscale, `fold` li conta, `> 0` dice se ce n'e' almeno uno — tre passaggi, una riga sola, letta da sinistra a destra.

[Apri l'esempio e provalo](command:klexir.openSample.13)
