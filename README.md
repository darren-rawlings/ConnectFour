# ConnectFour

A tactile 3D take on the classic drop-four game for phones, built with **Godot 4.7 (.NET / C#)**. Spin the board, pick your materials, and listen to the pieces clatter.

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

**Controls:** tap or click a column to drop (from the front or back), drag sideways to spin the board, or press keys `1`–`7`. **Style** picks the board and piece materials.

**Developer demo:** to record or check a full round without input, run the game with arguments after `--`:

```bash
Godot.exe --path . -- --demo --board=Wood --pieces=Metal --spin=35
```

`--demo[=3,3,4,4,5,5,6]` plays the moves, then releases the pieces. `--board` and `--pieces` take `Plastic`, `Wood`, `Metal`, `Glass` or `Marble` and aren't saved. `--spin` is in degrees. Add `--write-movie out.png --fixed-fps 60 --quit-after 840` before the `--` to capture frames and audio.

## Layout

```
src/Core/     Pure C#, no Godot types, fully unit-tested: game rules and the sound synthesiser
src/Game/     Godot nodes: 3D board, physics discs, materials, audio, HUD
shaders/      Procedural material shaders (plastic, wood, metal, marble, glass)
scenes/       .tscn scenes
assets/       art / audio / fonts (empty for now; looks and sounds are procedural)
tests/        xUnit tests for src/Core (hidden from Godot via .gdignore)
docs/         Design doc, roadmap, lessons learned
```

## Docs
- [Design](docs/DESIGN.md)
- [Roadmap](docs/ROADMAP.md)
- [Lessons learned from Cloud Dojo](docs/LESSONS_LEARNED.md)
