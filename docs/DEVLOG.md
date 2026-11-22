# Grip Pro: Channel 13 devlog

How the game got here, decision by decision. Each entry says what was built, why, and who decided it. **Razorbill Studio** is the designer and director. **Claude Code** is the AI coding assistant that writes code and UI from that direction.

---

## 9 to 14 November 2026: SHIFT//RUSH, the jam build

The game began as **SHIFT//RUSH**, an entry for GitHub Game Off 2026 (theme: SHIFT).

- **Greybox and car (9 Nov):** a test track, Realistic Car Controller Pro wired in behind the game's own vehicle interfaces, reset and air control.
- **Menus (10 Nov):** main menu, settings and pause menu on the Heat UI framework, plus a speed effect.
- **Circuits (11 Nov):** Alpine Pass and Glacier Valley.
- **The SHIFT idea (14 Nov):** hit a SHIFT ramp, the race drops into slow motion, and you choose a short challenge world or a guaranteed nitro. Rivals, shortcuts, VFX, music, a tutorial coach, Rush bonuses, per-car nitro effects, new cars, a toon look and three difficulty levels followed.
- **First web build:** fixes for build-only problems.

---

## 22 November 2026: from jam game to Grip Pro

One long working day that turned the jam racer into the start of the real game. Razorbill Studio set every direction below and play-tested each step. Claude Code built it.

### The moves

**Decided by Razorbill Studio:** rolls in normal driving, not just on ramps. Q spirals left, E spirals right, and it has to be "a very visible spiral roll that stands out". Then a forward spiral roll on Q + E (not a flip). Then wider sideways rolls, because "imagine a big fireball like the one in Blur and I press E only to still get hit".

**Built:**
- **Spiral rolls** on scripted arcs (height and air time set the launch speed and gravity, so the roll always lands where it should). About 3 m high, one full turn, 0.9 s in the air, and about 8 m sideways so a roll actually dodges.
- **Wall roll-off:** when riding a wall, a roll spins toward the open side, with an extra full spin.
- **Wind aura** (PixPlays) that shows only while rolling, sized to the car and sped up with speed.
- **Knocks:** a roll that hits a car snaps it into a scripted tumble. Light cars bounce off heavy ones, the knock lands before any damage, and a knocked car can knock up to two more (a chain) at reduced force. A short hit-stop sells the impact, and it's skipped when Reduced Motion is on.
- **AI rivals use every move:** rolling into cars alongside them, forward-rolling into slower cars ahead, and dodging incoming rolls.

### The drift

This took the most iteration, all driven by Razorbill Studio's feedback.

1. First version felt like "just turning left with sounds". The car turned harder but never slid.
2. Reworked into a gradual model: "a light tap on space and the drift can be very smooth".
3. **Feedback:** "too drifty, give me open variables to tweak". Built a live tuning panel in the practice yard: sliders on keys 1, 2 and 3 for the three drift characters, with copy and reset.
4. **Feedback:** the drift kept going after letting go of Space. "It's supposed to obey the time I held the space bar, like a real handbrake." Reworked so holding Space opens the slide and letting go closes it at a set rate.
5. **Razorbill Studio tuned the Drifty character by hand** in the yard: start 45 km/h, steer 0.30, tap 10°, max 53°, open 44°/s, swing 3.5, let-go recovery 137°/s, tyre grip 0.65, corner grip 12, speed loss 8 / 1.5 / 10. Those values are now the shipped defaults.

**Built:**
- Handbrake drift with three boost tiers (+12, +24, +38 km/h) for holding a drift.
- **Three drift characters** (Drifty, Balanced, Grippy) stored in one tuning asset that cars read live. Heavier cars swing slower and keep their momentum.
- Rear smoke and sparks, tyre trails and screech, coloured by tier. The camera swings out and rumbles.
- Rivals drift corners too, more or less eagerly depending on their car.

### Cars and handling direction

**Decided by Razorbill Studio:** a Forza Horizon feel with light assists that players can change in Settings. Cars should "feel like they look". Stay on Realistic Car Controller Pro. Six cars, one per type, and bring Phobos back.

**Built:** a car type for every car, each with the drift character that fits it:

| Car | Type | Drift |
|---|---|---|
| Zephyr | All-rounder | Balanced |
| Fireburst | Supercar | Balanced |
| Predator | Race car | Grippy |
| Blackout | Muscle | Drifty |
| Tetsuo | Tuner | Drifty |
| Phobos | Heavy | Grippy |

Plus a **Car Setup wizard** that adds every feature to a car, sets its type and drift, and warns when a drift doesn't fit the type.

### Name and story

**Decided by Razorbill Studio:** the race is an illegal sport broadcast on a pirate stream, cars arrive and leave by air, and the crew tunes cars like spell casting. The finish is the crew's aircraft catching the car as it escapes the police. The name stays **Grip Pro**, and the subtitle **Channel 13** is the pirate stream itself. The story is planned as a trilogy, with Act 2 and Act 3 going off-world.

### Progression

**Decided by Razorbill Studio:** one currency called **Hype**, a garage of owned cars, and tuning through visible parts: tyres, spoiler, hood inlet and roof. Players can shift a car's drift character in the garage, but only one step within its type.

**Built so far:** Hype is earned in races and saved to the profile, and the garage shows the four tuning slots as coming soon. Buying cars and parts is next.

### The look: a pirate stream, not a racing menu

![Main menu](media/menu-d-home-final.png)

**Asked by Razorbill Studio:** "a AAA standard UI, change everything from main menu to game HUD".

Claude Code drew style frames first, on a shared design canvas, so the look could be judged before any Unity work. **Razorbill Studio picked and shaped the direction:**
- Chose the pirate stream broadcast look, three power-up slots, and a light-touch stream HUD.
- Asked for more main menu options, then picked **menu D**: a full live shot, the menu along the bottom, and the chat column. "The chat thing is great."
- Moved the title to the top left and the live chips to the right.
- Asked for a slanted title with game flair, then cut it back to just the text and its two stripes.
- Asked for **fan dares**: cards from viewers at random moments. Decided with Claude that there's no accept button (doing the dare takes it).
- Moved the power-ups to the middle.
- Chose the police evidence board style for the future story map.

**Built in Unity, all generated by editor builders:**
- **Fonts:** Big Shoulders Display, Barlow Condensed and JetBrains Mono (open licence).
- **Home:** the GRIP PRO logo with its cyan echo, LIVE and viewer chips, a row of stream panels as the menu (Story locked as coming soon, Quick Race, Garage, Free Roam), and **The Feed**: a live chat column with a pinned story, news, tips, rumours and rival trash talk.
- **Garage, track select, settings, loading screen and splash** in the same style.

### The menu backdrop: your car under the aircraft at night

![Night airstrip](media/menu-showcase-airstrip.png)

**Asked by Razorbill Studio:** make the showcase "feel epic", with environment VFX, and "make it worth it". Later, use the **Aglaea** heavy-lift helicopter from the ARCADE pack instead of a plane. Then: the garage camera was too low, big cars like trucks need the camera further back, the player should be able to turn the camera with the mouse, and the camera must not go into the ground.

**Built:**
- A rainy night airstrip: wet tarmac with puddles, runway and taxiway lights running into the fog, floodlight towers with light beams, police lights flashing far off, hangars and a city skyline, a moonlit cloud sky, rain, ground mist and dust in the light.
- Aglaea parked behind the car with its rotors idling, light pouring from the tail and blinking beacons.
- A handheld camera drift so it reads as a live shot.
- Mouse-drag and right-stick orbit in the menu and garage. A higher camera in the garage, and the camera stepping back for big cars. The camera is kept above the ground.

### The race HUD and Hype

![Race HUD](media/hud-stream-events.png)

**Asked by Razorbill Studio:** the HUD needs power-ups, nitro and move cooldowns, plus the fan dares.

**Built:**
- Position and leaderboard top left. Top right, the stream corner: LIVE, viewers that climb with your style, a **Hype streak** from x1.0 to x3.0, and chat pops after big moves.
- Move callouts with the Hype they paid ("CHAIN KNOCK x2, +300 HYPE").
- The drift tier meter, roll cooldowns, three power-up slots (power-ups come later), speed, gear, RPM and nitro.
- **Fan dares:** at most two per race, never early or on the final lap. Hit drift tier 3, knock a rival, land two clean rolls, or hold top speed. Ignoring one costs nothing.
- **Results:** the stream pays out line by line, with a total and your new balance.

![Results](media/hud-results-final.png)

Hype amounts are first guesses and will be tuned through play-testing.

---

## Next

- Buying cars and tuning parts with Hype
- Power-ups
- Assist levels in Settings (Full, Light, Off) and grip handling per car type
- Wide 6 to 8 lane roads with traffic, and the aircraft drop-in start
- Story mode, with the evidence board map
- Optional: real Twitch chat in The Feed, with chat voting on fan dares
