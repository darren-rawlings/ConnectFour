# Roadmap

## v0.1: Foundation ✅
- [x] Project structure, C# rules engine and unit tests
- [x] Playable hot-seat match with polished procedural visuals
- [x] Git repo

## v0.2: 3D board ✅
- [x] 3D board with physics discs that slide down the slots and stack
- [x] Drag to spin the board (Y axis only, with inertia); tap to drop from either side
- [x] Style menu: 5 boards (Classic, Oak, Steel, Glass, Marble) × 5 piece sets, each with procedural patterns
- [x] Synthesised per-material sounds: impacts and piece-on-piece clacks (the sliding scrape was removed: it masked landings on phone speakers)
- [x] End of round: tray slides out and the pieces tumble onto the table
- [x] Sound on/off; style and sound choices saved between sessions

## v0.2.5: Feel & polish
- [ ] Test on a real phone (performance, touch feel, spin sensitivity)
- [ ] Win fanfare and music
- [ ] Win celebration (particles / confetti)
- [ ] Custom font, title screen
- [ ] App icon

## v0.3: Single player ✅
- [x] Computer opponent: Easy (takes wins, usually blocks, else random), Medium (looks 3 moves ahead), Hard (7 moves ahead, alpha-beta pruning); searches on a worker thread. Each was lowered by one move after playtesting
- [x] "Player vs Player" / "Player vs Computer" buttons between Style and Sound; picking the computer pops up Easy / Medium / Hard

## v0.4: Ship
- [x] Decide distribution: phone-only, Android + AdMob (see "Hosting" below)
- [ ] Export presets plus GitHub Actions to build releases automatically
- [ ] Landing page

## v1.0: Monetise
- [x] Ad flow: a between-games ad after every 5th finished game (count saved), and Undo (vs computer only) behind a rewarded ad. Desktop shows a stand-in ad screen
- [ ] Real AdMob ads on Android (Google test ads first, then Darren's own AdMob account and ad unit IDs)
- [ ] Decide the audience (general vs. kids). Kids means Google Play Families policy rules for ads
- [ ] Dedicated domain, host site, and support email
- [ ] Privacy policy (required for ads)

## Hosting: decided (2026-10-14)
**Stay C#, phone-only (Android first).** Ship on Google Play with ads via **AdMob**. Desktop and web builds are out of scope for now. Revisit a web build when Godot C# supports it (4.8+).

Testing: run in the Godot editor (portrait 540x960 window, touch emulated from mouse), then one-click deploy to a real Android phone over USB.

## Backlog
- [x] Background style menu: Midnight, Games Room, Sunset, Daylight, Dojo (sky, lighting and tabletop; the table also sets the landing sound)
- [ ] **Renegade Games startup logo.** Replace the Godot logo shown at launch (requested 2026-11-01; Pest Control has it working).
  - Copy `C:\GameDev\Branding\renegade-games-splash-friendly.png` to `assets/splash/renegade_games.png`. It's the kid-friendly logo: smiling skull, no guns or bullets. Don't use the other splash images in `Branding` here; they show revolvers and are for 13+ games.
  - In `project.godot`, under `[application]`, set:
    ```
    boot_splash/bg_color=Color(0.1647, 0.1686, 0.2039, 1)
    boot_splash/image="res://assets/splash/renegade_games.png"
    boot_splash/minimum_display_time=1500
    ```
  - Check on the phone that the logo shows at launch, then bump the version code for the next upload.
  - **Launch screen only:** this game is listed for everyone including kids (Families policy, child-directed ads, Teacher Approved), so keep the logo off the icon and store listing, and check that the content rating answers still hold before shipping.
