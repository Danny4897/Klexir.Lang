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

## Running a file

Open any `.klx` file and hit `Ctrl+F5` (`Cmd+F5` on macOS), or run **Klexir: Run File** from the Command Palette. The extension looks for a `Klexir.Cli.csproj` anywhere in your open workspace automatically. If it can't find one — e.g. your `.klx` files live in a separate solution — set `klexir.cliProjectPath` in Settings to the full path of `Klexir.Cli.csproj`.

## What's highlighted

`let rec in if then else fun match with record union` as keywords, `map bind filter fold` as the functor/monad/list operations, `Some Ok` / `None Err` as the success/failure constructors, any capitalized identifier as a type (built-in or your own `record`/`union`), strings, numbers, and `// line comments`.
