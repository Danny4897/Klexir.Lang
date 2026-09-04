## Plugin: capacita' native opt-in

Un plugin aggiunge funzioni native (anche asincrone: I/O vero) all'ambiente di un programma — ma solo se l'host lo abilita esplicitamente. Non e' scopribile a runtime ne' scelto dal sorgente `.klx`: e' una whitelist decisa da chi ospita Klexir.

```klexir
delay (now true - now true + 5)
```

`Klexir.Lang.Plugins.ClockPlugin` (`now`, `delay`) e' il plugin di riferimento. Da terminale:

```bash
dotnet run --project src/Klexir.Cli -- run --plugin=clock 09-plugins.klx
```

In VS Code: imposta `klexir.plugins` su `["clock"]` nelle Impostazioni, poi `Ctrl+F5` esegue il file gia' con il plugin abilitato.

[Apri l'esempio](command:klexir.openSample.09)
