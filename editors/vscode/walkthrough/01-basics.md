## Tipi primitivi

Klexir ha tre tipi di base: `Int`, `Bool`, `String`. `let nome = valore;` dichiara un binding a livello di programma (niente `in`, si termina con `;`); l'ultima riga senza `;` e' il valore restituito dal programma.

```klexir
let age = 30;
let isAdult = age >= 18;
let greeting = "Ciao, " + "mondo!";

if isAdult then greeting else "troppo giovane"
```

`+` su due `String` concatena invece di sommare. `if/then/else` e' un'espressione: entrambi i rami devono avere lo stesso tipo.

[Apri l'esempio e provalo (Ctrl+F5 per eseguirlo)](command:klexir.openSample.01)
