## Workflow multi-step veri

Terzo pezzo dell'ecosistema: `defineStep`/`runWorkflow` girano su un vero `Klexir.Workflow.WorkflowEngine` — checkpoint dopo ogni passo, si ferma al primo fallimento, esattamente come per un chiamante C#.

```klexir
let validate = defineStep "checkout" "validate" (func(String order) =>
    if order == "" then Err<String>("carrello vuoto") else Ok<String>(order));
let charge = defineStep "checkout" "charge" (func(String order) => Ok<String>(order + " [addebitato]"));
let ship = defineStep "checkout" "ship" (func(String order) => Ok<String>(order + " [spedito]"));

runWorkflow "checkout" "ordine#42"
```

```bash
klexir run --plugin=workflow 21-workflow.klx
```

`defineStep "nome-workflow" "nome-passo" comportamento` aggiunge UN passo alla volta — puoi chiamarlo piu' volte per accumulare l'intera sequenza, un passo per riga, nell'ordine in cui li scrivi. Ogni passo e' `String -> Result<String, String>`: `Ok` fa proseguire al passo successivo con quel valore, `Err` ferma tutto il workflow li'.

Prova a cambiare `"ordine#42"` in `""` — `validate` fallisce, e ne' `charge` ne' `ship` girano mai: `runWorkflow` ritorna `Err(...)` subito, con il messaggio del fallimento. Questo e' esattamente il motivo per cui esiste un motore di workflow invece di incatenare `andThen`: passi con NOME, tracciabili (checkpoint reali, uno per passo, dentro `Klexir.Workflow`), non solo funzioni anonime in sequenza.

[Apri l'esempio e provalo](command:klexir.openSample.21)
