# ConnectFour: Design Doc

> Single source of truth. If it isn't written here, it isn't built yet.

## Concept
A clean, satisfying Connect Four with premium-feeling visuals: glossy discs, bouncy drops, and a celebratory win. It's mobile-first, and it's our first shippable game.

## Rules
- 7 columns × 6 rows. Two players (Red, Yellow) alternate dropping discs.
- The first to connect 4 in a row (horizontal, vertical, or diagonal) wins. A full board is a draw.
- The starting player alternates each round. The score persists for the session.

## Platforms & tech
- Godot 4.7.2 .NET (C#), Compatibility renderer (broadest device support)
- Portrait base resolution 720×1280, stretch mode `canvas_items` / `expand`
- **Constraint:** Godot 4 cannot export C# projects to the web (as of 4.7). Distribution options are in ROADMAP.md.

## Current state (v0.1 foundation)
- Local two-player (hot-seat) on one device
- Procedural visuals: gradient background, shadowed board, shaded discs, bounce drop, hover preview, pulsing win glow
- Input: mouse or touch, plus keys 1–7

## Not yet decided
- AI opponent difficulty levels
- Art direction (procedural/flat vs. painted/3D-rendered assets)
- Theme / skin (classic, or something with personality, such as a dojo theme?)
- Audio style
