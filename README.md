# SHIFT//RUSH

An arcade racer for **GitHub Game Off 2026** (theme: **SHIFT**) by **Razorbill Studio**.

Hit a SHIFT ramp, and the race drops into slow motion for three seconds. Press **Shift** while your pick sits in the centre of the selector to drop into a short challenge world (**Ring Rush** or **Trap Run**), or let the timer run out for a guaranteed nitro boost. Win the challenge, come back with a full tank, and boost past the field.

**Race → Jump → Choose in 3 seconds → Challenge or guaranteed nitro → Back to the race → Boost → Finish.**

## Controls

| Key | Action |
|---|---|
| W / ↑ | Accelerate |
| S / ↓ | Brake / reverse |
| A D / ← → | Steer |
| Space | Drift |
| Left Shift | Pick a SHIFT world (only mid-air on a SHIFT ramp) |
| Left Ctrl | Nitro |
| R | Reset car |
| Esc | Pause |

## Tech

- Unity **6000.3.10f1**, Universal Render Pipeline, Input System
- Target: Windows (desktop)

## Third-party packs (not in this repo)

The game uses these paid packs. Their licences don't allow the source files to be shared publicly, so their folders are git-ignored. To open and build the project, buy and import each one into `Assets/` at its default location:

| Pack | Store | Used for |
|---|---|---|
| Realistic Car Controller Pro (BoneCracker Games) | Unity Asset Store | Vehicle physics, AI racers |
| ARCADE – Ultimate Vehicles Pack | Unity Asset Store | Cars |
| Spline Architect + Spline Architect Terrain Tools | Unity Asset Store | Track and terrain |
| POLYGON Alpine Mountain – Nature Biomes (Synty, Unity 6 URP version) | Synty Store | Alpine scenery |

Without these packs, the project's own code (`Assets/_ShiftRush/`) is still public and readable, but scenes will show missing references.

## Credits

- **Razorbill Studio**: design, code, level design
- **Concept & Inspiration:** Jennifer Ugwoke
- Third-party packs as listed above

Any AI-generated art or audio used in the game will be listed here.
