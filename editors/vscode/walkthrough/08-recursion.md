## let rec: ricorsione

Un `let` normale non vede il proprio nome dentro il proprio valore — `let rec` si', proprio per permettere la ricorsione. Il tipo di ritorno va scritto esplicitamente, niente inferenza.

```klexir
let rec fact = func(Int n): Int =>
    if n < 2 then 1 else n * fact (n - 1);

fact 6
```

Ogni chiamata ricorsiva ha una propria attivazione indipendente — `sum 10 + sum 5` con due `let rec` distinti non si "confondono" a vicenda.

[Apri l'esempio e provalo](command:klexir.openSample.08)
