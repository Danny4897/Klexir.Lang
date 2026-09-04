## String e List

`String` supporta letterali con escape (`\" \\ \n`) e concatenazione con `+`. `List<T>` ha letterali `[e1, e2, ...]` e le operazioni funzionali `map`/`filter`/`fold`.

```klexir
let greeting = "Klexir" + " " + "rocks";

let numbers = [1, 2, 3, 4, 5];
let evens = filter(numbers, fun (x: Int) => x == (x / 2) * 2);
let sum = fold(evens, 0, fun (acc: Int) => fun (x: Int) => acc + x);

sum
```

Niente operatore `%` ancora — `x == (x / 2) * 2` e' come si controlla la parita' con la sola divisione intera. `fold` e' un left-fold: il folder e' curried, `Acc -> Elem -> Acc`.

[Apri l'esempio e provalo](command:klexir.openSample.05)
