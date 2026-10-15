# ConnectFour: Design Doc

> Single source of truth. If it isn't written here, it isn't built yet.

## Concept
A tactile 3D Connect Four for phones. Pieces of real-feeling materials slide, clack and tumble, and you can spin the board in your hand. It's our first shippable game.

## Rules
- 7 columns × 6 rows. Two players alternate dropping discs (Red and Yellow by default; the names follow the piece style).
- The first to connect 4 in a row (horizontal, vertical, or diagonal) wins. A full board is a draw.
- The starting player alternates each round. The score persists for the session.

## Platforms & tech
- Godot 4.7.2 .NET (C#), Compatibility renderer (broadest device support)
- Portrait base resolution 720×1280; the 3D camera keeps a fixed horizontal FOV so the board fits any phone aspect
- **Constraint:** Godot 4 cannot export C# projects to the web (as of 4.7). Distribution options are in ROADMAP.md.

## Current state (v0.2: 3D board)
- Local two-player (hot-seat) on one device
- Real 3D board: two holed panels with a slot between them, column dividers, a sliding tray at the bottom, and legs about two rows tall, so a full board of released pieces falls clear underneath
- Discs are physics bodies (Jolt, 120 Hz, exaggerated gravity so the ~6-unit board feels table-sized). Once a disc settles it is frozen and parented to the board, so it spins with it
- Spin: drag sideways to turn the board around its vertical (Y) axis, with inertia. Only the board moves; the camera and table stay put. Spinning is locked while any disc is moving
- Tap a column to drop, from the front or the back. Keys 1-7 also work
- End of round: "Release Pieces" slides the tray out; discs fall through, tip over and scatter on the table, then the next round starts
- The win glow is a pulsing additive overlay on the four winning discs

## Styles
Boards and pieces are chosen independently in the Style menu and saved in `user://settings.cfg`.

| Style | Board | Pieces | Sound |
|---|---|---|---|
| Plastic | Classic blue | Red / Yellow | short bright clack |
| Wood | Oak | Walnut / Maple | dull knock |
| Metal | Brushed steel | Copper / Silver | long inharmonic ring |
| Glass | Frosted glass | Ruby / Sapphire | bright tink |
| Marble | Grey marble | Onyx / Ivory | heavy click |

**Backgrounds** (also in the Style menu): Midnight (navy baize), Games Room (green baize under warm lamplight), Sunset (dark plank table), Daylight (light plank table), Dojo (tatami). Each sets the sky colours, the key and fill lights, and the tabletop, which also decides what released pieces sound like when they land on it. They live in `Scenery.cs`.

- **Looks:** procedural shaders (`shaders/`) computed in object space, so there are no textures to license. Every number lives in `Styles.cs`.
- **Sounds:** synthesised at startup (`src/Core/Synth`) from modal recipes: decaying resonant modes plus a noise click. Impacts are detected from velocity changes gravity doesn't explain, and the volume follows how hard the hit was. A disc striking another material also plays that material's sound, more quietly.

## Not yet decided
- AI opponent difficulty levels
- Real recorded sound samples, if the synthesised ones don't feel real enough on a phone
- Theme / skin (classic, or something with personality, such as a dojo theme?)
- Music
