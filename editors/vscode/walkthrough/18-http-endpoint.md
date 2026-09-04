## Un endpoint HTTP vero

Fino ad ora ogni programma girava una volta e finiva. `klexir serve` e' diverso: valuta il programma UNA volta per ottenere la funzione finale, poi la richiama — sul serio, con `.NET HttpListener` — una volta per ogni richiesta reale che arriva. E' il primo caso dove l'I/O guida Klexir, invece del contrario (un plugin, dove Klexir chiama fuori).

```klexir
record HttpRequest { Method: String, Path: String, Body: String };
record HttpResponse { Status: Int, Body: String };

let handleRequest = func(HttpRequest req) =>
    if req.Path == "/hello" then HttpResponse { Status: 200, Body: "ciao dal server Klexir" }
    else HttpResponse { Status: 404, Body: "non trovato" };

// L'espressione finale DEVE essere la funzione stessa -- klexir serve la applica lui.
handleRequest
```

`HttpRequest`/`HttpResponse` non sono tipi magici — li dichiari TU, con quei nomi e quei campi esatti (Klexir non ha modo per un plugin di darti un tipo che non hai dichiarato). Prova:

```bash
klexir serve --port=5000 18-http-endpoint.klx
```

poi, in un altro terminale:

```bash
curl http://localhost:5000/hello
curl http://localhost:5000/qualunquealtracosa
```

Il primo risponde `200 ciao dal server Klexir`, il secondo `404 non trovato` — la STESSA funzione, applicata a richieste diverse, con `Ctrl+C` per fermare il server.

[Apri l'esempio e provalo](command:klexir.openSample.18)
