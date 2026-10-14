# Lessons Learned: Cloud Dojo (2026)

Cloud Dojo was a warrior-management idle game prototyped as a single HTML file. It ended when its server died. The table below lists what we're carrying into ConnectFour.

| # | What happened | Lesson | How ConnectFour applies it |
|---|---|---|---|
| 1 | The project lived in loose files on the Desktop (`v3.html`, `design-doc-v5.docx`), with versions tracked by filename. | Use real source control from day one, backed up off-machine. | Git repo from the first commit, pushed to GitHub. |
| 2 | The self-managed Ubuntu server died and took the project's future with it. | Don't depend on one hand-maintained server. | Host on managed static hosting (GitHub Pages / Releases / itch.io). Code stays on GitHub. |
| 3 | The whole game was one 150 KB HTML file with all state in a global `G` object. | Monoliths get fragile fast. | Rules (`src/Core`) are separate from presentation (`src/Game`). Small files. |
| 4 | Huge design scope (belts up to Lv 5,623, 10 dans, achievements) before the core battle loop existed. | Finish a small playable loop before expanding. | Connect Four is a deliberately small, finishable scope. New features go in the roadmap, not the build. |
| 5 | "Master Chen" showed up in requests but was never defined in the design doc. | Define things in the doc before building them. | `docs/DESIGN.md` is the single source of truth and lives in the repo. |
| 6 | The session summary pointed at design doc **v4** while **v5** existed. | Docs outside the repo drift. | Docs are markdown in the repo and versioned with the code. |
| 7 | Font sizes had to be tripled later; corner toasts were missed. | Design for mobile readability from the start. | Portrait 720×1280 base, large type, important messages centred. |
| 8 | Saves were `localStorage` only, plus a manual backup button that was added later. | Plan persistence and backups up front. | Rules are logic-only and easy to serialise when saves arrive. |
| 9 | No automated tests. | Rules bugs (like the `buildBuilding is not defined` crash) are cheap to catch with tests. | xUnit tests for `src/Core`; run with `dotnet test`. |
| 10 | Monetising with ads requires a dedicated host site and a separate support email. | Plan the business side alongside the game. | Tracked in the roadmap; kept light for game #1. |

## Things that worked and are worth keeping
- Center-screen alerts that need acknowledgment, for important events
- Auto-backup and restore of save data
- A curated free toolchain (Blender, Krita, GIMP, Inkscape, Audacity, Tiled, LDtk, Pixelorama)
- Clear per-asset size conventions (for example, buildings at 200×240 and NPCs at 56×140)
