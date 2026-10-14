# Roadmap

## v0.1: Foundation ✅
- [x] Project structure, C# rules engine and unit tests
- [x] Playable hot-seat match with polished procedural visuals
- [x] Git repo

## v0.2: Feel & polish
- [ ] Sound effects (drop clack, win fanfare) and music
- [ ] Win celebration (particles / confetti), screen shake on landing
- [ ] Custom font, title screen, settings (sound on/off)
- [ ] App icon

## v0.3: Single player
- [ ] AI opponent (Easy: random+block; Medium: minimax depth 4; Hard: minimax + alpha-beta depth 8)
- [ ] Mode select: vs Friend / vs Computer

## v0.4: Ship
- [x] Decide distribution: phone-only, Android + AdMob (see "Hosting" below)
- [ ] Export presets plus GitHub Actions to build releases automatically
- [ ] Landing page

## v1.0: Monetise
- [ ] Ads integration
- [ ] Dedicated domain, host site, and support email
- [ ] Privacy policy (required for ads)

## Hosting: decided (2026-10-14)
**Stay C#, phone-only (Android first).** Ship on Google Play with ads via **AdMob**. Desktop and web builds are out of scope for now. Revisit a web build when Godot C# supports it (4.8+).

Testing: run in the Godot editor (portrait 540x960 window, touch emulated from mouse), then one-click deploy to a real Android phone over USB.
