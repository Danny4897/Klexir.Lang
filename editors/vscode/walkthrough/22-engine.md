## Un database vero: il plugin Engine

Quarto e ultimo pezzo dell'ecosistema collegato per davvero — e l'unico diverso da tutti gli altri: **sopravvive alla fine del programma**. `dbOpen`/`dbPut`/`dbGet` girano su un vero `Klexir.Engine` B+Tree page-backed, su un file reale.

```klexir
let opened = dbOpen "klexir-demo.db";
let seeded = match dbPut 42 1234 with Ok(x) => true | Err(e) => true;

match dbGet 42 with Ok(v) => v | Err(e) => 0 - 1
```

```bash
klexir run --plugin=engine 22-engine.klx
```

Esegui questo file **due volte di seguito** — la seconda volta trova ancora `1234`, scritto dalla PRIMA esecuzione, un processo `klexir` completamente diverso. E' l'unico plugin che si comporta cosi': `Clock`/`EventFlow`/`Actor`/`Workflow` vivono e muoiono con un singolo `klexir run`, `Engine` no.

Nota `match dbPut ... with Ok(x) => true | Err(e) => true`: il B+Tree e' insert-only, scrivere una chiave gia' presente non sovrascrive — torna `Err`, catturabile con `match` come qualsiasi altro `Result`, non un crash. Qui lo ignoriamo apposta (il valore e' gia' quello giusto), ma potresti gestirlo diversamente — per esempio contando quante volte il seed e' gia' stato tentato.

Chiavi e valori sono entrambi `Int` — l'unico plugin dell'ecosistema senza bisogno di convertire da/verso `String` al confine, perche' il B+Tree di `Klexir.Engine` e' gia' indicizzato per `long`.

[Apri l'esempio e provalo](command:klexir.openSample.22)
