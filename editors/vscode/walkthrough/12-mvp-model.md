## MVP 1/6 — Modella il dominio

Da qui in poi costruiamo, un pezzo alla volta, un vero mini-programma: registrazione utente con validazione, esattamente il dominio delle lezioni precedenti, ma organizzato come lo struttureresti davvero. Primo principio di clean code: **il dato prima di tutto**. Un `record` per l'entita', un `union` per il risultato — invece di restituire un `Int` generico che ora e' un timestamp, ora un codice errore, un tipo dice esplicitamente cosa puo' succedere.

```klexir
record User { CodiceFiscale: String, Age: Int };
union RegistrationOutcome { Registered(Int), Rejected(Int) };

let sample = User { CodiceFiscale: "RSSMRA80A01H501U", Age: 45 };
let outcome = Rejected 3;

match outcome with Registered(ts) => ts | Rejected(code) => 0 - code
```

`RegistrationOutcome` non esiste ancora per davvero nella pipeline — lo useremo nello step 6. Definirlo subito, prima ancora di scrivere la logica, e' la parte di "modellare il dominio": decidi cosa PUO' succedere prima di decidere COME succede.

[Apri l'esempio e provalo](command:klexir.openSample.12)
