---
layout: home

hero:
  name: "Klexir.Lang"
  text: "The Klexir programming language"
  tagline: A real, runnable language — records, unions, Option/Result railway composition, lists — built on MonadicSharp Result<T>, with a CLI and VS Code syntax highlighting.
  actions:
    - theme: brand
      text: Quick example
      link: /guide
    - theme: alt
      text: Full README on GitHub
      link: https://github.com/Danny4897/Klexir.Lang
    - theme: alt
      text: Klexir Ecosystem
      link: https://danny4897.github.io/MonadicSharp/ecosystem

features:
  - title: Option & Result, for real
    details: Some/None, Ok/Err, exhaustive match, and map/bind for railway-oriented composition — first-class Klexir types, mirroring MonadicSharp's design inside the language itself.
  - title: Run it, don't just embed it
    details: dotnet run --project src/Klexir.Cli -- run file.klx runs a .klx file end to end; the VS Code extension adds syntax highlighting and a Run command.
  - title: Records & unions
    details: User-defined product and sum types — a real domain model, not just Int/Bool arithmetic.
---
