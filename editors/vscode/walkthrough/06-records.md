## record: tipi prodotto

`record Nome { Campo: Tipo, ... };` dichiara un tipo prodotto a livello di programma. La costruzione controlla i nomi dei campi (l'ordine non conta); `.Campo` legge un valore.

```klexir
record User { Id: Int, Age: Int };

let isAdult = fun (u: User) => u.Age >= 18;

isAdult (User { Age: 25, Id: 1 })
```

Un `record` e' nominale: due dichiarazioni con lo stesso nome di campi ma nomi di tipo diversi NON sono compatibili tra loro.

[Apri l'esempio e provalo](command:klexir.openSample.06)
