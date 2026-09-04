# {{PROJECT_NAME}}

Progetto Klexir generato da `klexir new` — struttura pronta per clean code, layer per layer.

## Struttura

```
{{PROJECT_NAME}}/
  Program.klx           <- il programma vero, quello che esegui con klexir run
  samples/
    Model.klx            <- esempio: record + union, isolato ed eseguibile da solo
    Repository.klx        <- esempio: dati in-memory + una funzione di lookup
    Validators.klx          <- esempio: funzioni atomiche di validazione
    Service.klx               <- esempio: orchestrazione dei validator con andThen
    Controller.klx              <- esempio: Result -> risposta finale
```

Klexir non ha ancora moduli/import multi-file (`.klx` e' sempre un file singolo) — `samples/` non e' collegato a `Program.klx`, e' materiale di riferimento: guardi il pattern in isolamento, poi lo scrivi dentro `Program.klx` adattato al tuo dominio. Ogni file in `samples/` gira da solo con `klexir run`.

## Come procedere

1. Apri `Program.klx` — gira gia' cosi' com'e' (`klexir run Program.klx`), TODO inclusi.
2. Sostituisci `Entity` nella sezione MODEL con il tuo dominio reale.
3. Aggiorna REPOSITORY con i tuoi dati (o collega un plugin per uno storage vero).
4. Aggiungi un validator atomico alla volta in VALIDATORS — un solo motivo di fallimento ciascuno.
5. Incatenali con `andThen` in SERVICE, nell'ordine che ti serve.
6. CONTROLLER resta l'unico punto che legge il `Result` e decide la risposta finale.

Ogni volta che aggiungi un validator, guarda `samples/Validators.klx` e `samples/Service.klx` per la forma esatta.

## Eseguire

```bash
klexir run Program.klx
klexir compile Program.klx    # core subset (numeri/bool/confronti/let/if/closures/let rec) via bytecode reale
```

## Approfondire

Il tutorial completo (dalla sidebar "Klexir Tutorial" in VS Code, o `editors/vscode/walkthrough` nel repo `Klexir.Lang`) copre ogni concetto usato qui, dai tipi primitivi fino a questo stesso schema controller/service/repository/validators.
