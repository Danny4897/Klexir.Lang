## union: tipi somma

`union Nome { Variante(T1, T2), Variante2, ... };` dichiara un tipo somma: un valore e' esattamente una delle varianti. Una variante con campi si costruisce con applicazione curried ordinaria — nessuna sintassi speciale.

```klexir
union Shape { Circle(Int), Rectangle(Int, Int) };

let area = func(Shape s) =>
    match s with Circle(r) => r * r * 3 | Rectangle(w, h) => w * h;

area (Rectangle 3 5)
```

`match` e' esaustivo per costruzione: manca una variante? Errore di tipo, non un bug scoperto a runtime.

[Apri l'esempio e provalo](command:klexir.openSample.07)
