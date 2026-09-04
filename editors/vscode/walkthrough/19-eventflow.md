## Pub/sub vero: il plugin EventFlow

Klexir ha un intero ecosistema di librerie .NET (`Klexir.EventFlow`, `Klexir.Actor`, `Klexir.Workflow`, `Klexir.Engine`) costruite a parte, in C#. Il plugin `eventflow` collega la prima di queste per davvero — non una simulazione: `subscribe`/`publish` girano sopra un vero `Klexir.EventFlow.InMemoryEventBus`.

```klexir
let registered = subscribe "UserRegistered" (func(String email) => email == "alice@example.com");

publish "UserRegistered" "alice@example.com"
```

```bash
klexir run --plugin=eventflow 19-eventflow.klx
```

`subscribe` registra una closure Klexir contro un tag testuale; `publish` fa partire un evento reale sul bus, e OGNI subscriber registrato su quel tag viene richiamato — sul serio, tramite lo stesso `Evaluator` che sta eseguendo il programma. E' il primo plugin dove succede il contrario di tutti gli altri: non e' Klexir che chiama fuori (`now`, `delay`), e' **.NET che richiama dentro Klexir**, quando l'evento arriva.

Se un subscriber ritorna `false` (o fallisce), la chiamata a `publish` fallisce — prova a cambiare l'email atteso nel subscriber e vedrai un errore invece di `true`. Non e' un dettaglio del plugin: e' il vero meccanismo di retry/dead-letter di `Klexir.EventFlow` che vede il fallimento, esattamente come vedrebbe quello di un handler C#.

[Apri l'esempio e provalo](command:klexir.openSample.19)
