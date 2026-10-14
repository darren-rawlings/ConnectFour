# ConnectFour

A polished take on the classic drop-four game, built with **Godot 4.7 (.NET / C#)**. It's our first game, and the goal is to ship something small that looks good.

## Getting started

| Need | Version |
|---|---|
| Godot (.NET edition) | 4.7.2 |
| .NET SDK | 8.x |
| Editor | VS Code + C# Dev Kit + godot-tools |

```bash
dotnet build ConnectFour.csproj        # compile the game
dotnet test tests/ConnectFour.Tests    # run rules tests (no engine needed)
```

Open the folder in Godot and press **F5**, or use the **Play ConnectFour** launch config in VS Code.

**Controls:** click or tap a column, or press keys `1`–`7`.

## Layout

```
src/Core/     Game rules: pure C#, no Godot types, fully unit-tested
src/Game/     Godot nodes: rendering, input, UI
scenes/       .tscn scenes
assets/       art / audio / fonts (empty for now; visuals are procedural)
tests/        xUnit tests for src/Core (hidden from Godot via .gdignore)
docs/         Design doc, roadmap, lessons learned
```

## Docs
- [Design](docs/DESIGN.md)
- [Roadmap](docs/ROADMAP.md)
- [Lessons learned from Cloud Dojo](docs/LESSONS_LEARNED.md)
