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
- [ ] Decide distribution (see "Hosting" below)
- [ ] Export presets plus GitHub Actions to build releases automatically
- [ ] Landing page

## v1.0: Monetise
- [ ] Ads integration
- [ ] Dedicated domain, host site, and support email
- [ ] Privacy policy (required for ads)

## Hosting: open decision
Godot 4 C# can't export to the web yet (expected no earlier than 4.8). Options:
1. **Stay C#, ship downloads/mobile now.** Windows build on itch.io / GitHub Releases, Android on Google Play. Ads via **AdMob** (Google's mobile ads). Add a web build when Godot supports it.
2. **Switch to GDScript.** Web export works today and can be hosted on GitHub Pages / itch.io, with ads via **AdSense** on our own site.
3. **Hybrid.** Keep C# and wait for 4.8, with no web build until then.
