## Funzioni e closures

`fun (x: Int) => corpo` crea una funzione — il parametro vuole sempre un'annotazione di tipo esplicita, niente inferenza. L'applicazione e' currying "vera": una funzione a due argomenti e' una funzione che ne restituisce un'altra.

```klexir
let square = fun (x: Int) => x * x;
let add = fun (x: Int) => fun (y: Int) => x + y;

let addFive = add 5;   // applicazione parziale: la closure ricorda x = 5

square (addFive 3)
```

Una closure cattura per davvero l'ambiente in cui e' stata creata — `addFive` continua a "vedere" `x = 5` anche fuori da `add`.

[Apri l'esempio e provalo](command:klexir.openSample.02)
