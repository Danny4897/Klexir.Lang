## Controllo di flusso e confronti

`< > <= >= == !=` (dove supportato) confrontano `Int`; `==` funziona anche su `Bool`/`String`. Klexir non ha ancora letterali negativi (e' un limite noto, vedi il README) — `0 - 5` e' come si scrive `-5` per ora.

```klexir
let classify = func(Int n) =>
    if n < 0 then "negativo"
    else if n == 0 then "zero"
    else "positivo";

classify (0 - 5)
```

`if` puo' incatenarsi con `else if` come in qualunque linguaggio — resta comunque una singola espressione, non uno statement.

[Apri l'esempio e provalo](command:klexir.openSample.03)
