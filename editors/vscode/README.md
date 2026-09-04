# Klexir Language Support (VS Code)

Syntax highlighting for `.klx` files, plus a **Klexir: Run File** command (`Ctrl+F5` / `Cmd+F5`) that runs the current file through `Klexir.Cli` in an integrated terminal.

No npm/build step — this is a plain declarative + CommonJS extension, loaded as-is.

## Install (pick one)

**Try it without installing** — open this `editors/vscode` folder in VS Code and press `F5`. A new "Extension Development Host" window opens with the extension active; open any `.klx` file there.

**Install permanently** — copy this whole `editors/vscode` folder into your VS Code extensions directory, then restart VS Code:

- Windows: `%USERPROFILE%\.vscode\extensions\klexir-lang-0.1.0\`
- macOS/Linux: `~/.vscode/extensions/klexir-lang-0.1.0/`

```powershell
Copy-Item -Recurse "editors\vscode" "$env:USERPROFILE\.vscode\extensions\klexir-lang-0.1.0"
```

## Tutorial interattivo

Icona **Klexir Tutorial** nell'Activity Bar (a sinistra, come Vim/Neovim) — sempre a un click di distanza, non un wizard usa-e-getta. Elenca 10 lezioni, dai tipi primitivi (`Int`/`Bool`/`String`) fino a un vero controller/service/repository composto con `bind`, passando per funzioni/closures, `if`, `Option`/`Result`, `String`/`List`, `record`, `union`, `let rec` e i plugin.

Cliccando una lezione si apre uno schermo diviso: a sinistra il file `.klx` eseguibile della lezione (copiato in `<workspace>/klexir-tutorial/` alla prima apertura, cosi' le tue modifiche restano tue — puoi editarlo e lanciarlo con `Ctrl+F5`), a destra l'anteprima Markdown della spiegazione. La lezione si segna completata (spunta verde nella sidebar) non appena la apri; il bottone di reset in alto nella vista azzera il progresso.

## Running a file

Open any `.klx` file and hit `Ctrl+F5` (`Cmd+F5` on macOS), or run **Klexir: Run File** from the Command Palette. The extension looks for a `Klexir.Cli.csproj` anywhere in your open workspace automatically. If it can't find one — e.g. your `.klx` files live in a separate solution — set `klexir.cliProjectPath` in Settings to the full path of `Klexir.Cli.csproj`. Set `klexir.plugins` (e.g. `["clock"]`) to have `Ctrl+F5` pass `--plugin=<name>` automatically.

## What's highlighted

`let rec in if then else fun match with record union` as keywords, `map bind filter fold` as the functor/monad/list operations, `Some Ok` / `None Err` as the success/failure constructors, any capitalized identifier as a type (built-in or your own `record`/`union`), strings, numbers, and `// line comments`.
