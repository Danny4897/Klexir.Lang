## Attori veri: il plugin Actor

Secondo pezzo dell'ecosistema collegato per davvero: `spawn`/`tell`/`ask` girano su un vero `Klexir.Actor` — mailbox `Channel`-backed, una transizione di stato alla volta, senza lock scritti a mano.

```klexir
let started = spawn "cart" "" (func(String item) => func(String state) => state + item);

let afterMela = ask "cart" "mela;";
let afterPane = ask "cart" "pane;";
ask "cart" "vino;"
```

```bash
klexir run --plugin=actor 20-actor.klx
```

`spawn nome statoIniziale comportamento` crea un attore con nome; il comportamento e' una closure curried `String -> String -> String` (messaggio, poi stato corrente, ritorna il nuovo stato) — la STESSA idea del comportamento di `Klexir.Actor.Actor<TMessage,TState>` in C#, solo scritta come funzione invece che come sottoclasse (Klexir non ha classi). `ask` invia un messaggio e ASPETTA lo stato risultante; `tell` lo invia e va avanti subito, senza aspettare.

Ogni `ask` sul CARRELLO gira in ordine, una alla volta — `pane` vede lo stato lasciato da `mela`, `vino` vede quello lasciato da `pane`. Prova a spawnare due attori diversi (nomi diversi): girano indipendentemente, ciascuno con la propria mailbox.

[Apri l'esempio e provalo](command:klexir.openSample.20)
