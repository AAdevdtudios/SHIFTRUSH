# Grip Pro: how the cars work

A guided read of the vehicle code, written so you can understand it, defend it, and then rebuild the custom parts yourself. Nothing here changes the code. Read it with the scripts open next to it.

Folder: `Assets/_ShiftRush/Scripts/Vehicles/` (plus a few callers in `Race/`, `VFX/` and `CameraFX/`).

---

## 0. The big picture in one paragraph

Realistic Car Controller Pro (RCC) is the **engine room**. It owns the wheels, suspension, engine, gearbox, brakes, nitro and the normal driving feel. Grip Pro never talks to RCC directly except in **one file**, `RCCPVehicleAdapter`. Every other script talks to small C# interfaces (`IVehicle`, `IDriftInput`, `INitroTank` and so on). On top of RCC sit Grip Pro's own **moves**: the held drift (`DriftMoves`), the spiral roll and wall riding (`GripMoves`), the knock tumble (`KnockTumble`), air levelling (`AirControl`) and a few helpers. Those moves don't use RCC at all. They work on Unity's `Rigidbody` directly, which is why they would survive a switch to another car controller.

Think of it like a web app: RCC is a third-party library, the adapter is the one service class that wraps it, the interfaces are the contracts, and the moves are your own feature modules.

```
 Player keys / gamepad          Rival AI (RivalDriver)
        |                               |
        v                               v
  RCC input actions  <---  RCCPVehicleAdapter  --->  IVehicle, IDriftInput, INitroTank,
  (remapped by                (the only RCC                IDriverInput, IVehicleDash
   ControlBindings)             bridge)                         |
        |                               |                       v
        v                               |          DriftMoves, GripMoves, AirControl,
  RCC car controller  --- forces --->  Rigidbody  <--- KnockTumble, KnockStabilizer,
  (wheels, engine, nitro)                               PartDownforce, VehicleReset
```

---

## 1. Each script and how they talk

### The contracts (interfaces)

| Interface | What it promises | C# comparison |
|---|---|---|
| `IVehicle` | speed, grounded checks, steer and pitch input, "is this the player", teleport, enable/disable control | like `IRepository`: callers don't care what's behind it |
| `IDriverInput` | lets code drive the car (`SetDriverInput`) | an "override" API, used by the rival AI |
| `IDriftInput` | raw handbrake, raw steer intent, and a switch to hide the handbrake from RCC | a tiny adapter for one feature's needs |
| `INitroTank` | read, set, add nitro, turn regeneration and the nitro key on or off | a wallet with deposit and withdraw |
| `IVehicleDash` | RPM, gear label, nitro, for the HUD | a read-only view model |

The point: `DriftMoves` asks `GetComponent<IVehicle>()`, not `GetComponent<RCCP_CarController>()`. That's **dependency inversion**. You could write `SimcadeVehicleAdapter : IVehicle` and every move keeps working.

### The bridge: `RCCP/RCCPVehicleAdapter.cs`

One `MonoBehaviour` that implements all five interfaces by reading and writing RCC's own objects:

- `SpeedKmh` reads `car.speed`. `AnyWheelGrounded` loops RCC's wheel colliders.
- `Nitro01`, `SetNitro`, `SetRegeneration` read and write RCC's `RCCP_Nos` add-on.
- `SetDriverInput` builds an `RCCP_Inputs` object and calls `car.Inputs.OverrideInputs(...)`. That's how rivals drive.
- `Update()` does two jobs: records the raw handbrake (so a drift can read it) and, when `MuteHandbrake` is on, zeroes RCC's handbrake. It also applies **Auto Accelerate** from Settings by forcing `throttleInput = 1`.
- `RegisterSpawner()` runs before any scene (`RuntimeInitializeOnLoadMethod`) and plugs RCC's spawn functions into `VehicleSpawn`, so race code can spawn a rival without knowing RCC exists.
- `[DefaultExecutionOrder(50)]` makes it run **after** RCC has read the keys this frame, so its overrides win.

### Set-up helpers

- `RCCPSessionSetup` (order -1000): calls `ControlBindings.Apply()` when a scene starts.
- `ControlBindings` (static): remaps RCC's Input System actions at runtime. It frees Left Shift and Left Ctrl from gear shifting and puts nitro on them, unbinds RCC keys that clash with Grip Pro (Q, E, R), then loads the player's rebinds from `PlayerPrefs`. RCC's own asset file is never edited.
- `VehicleSpawn` (static): three `Func`/`Action` delegates (`Player`, `Rival`, `Boost`). Default implementations just `Instantiate`. The adapter replaces them. This is the **strategy pattern with delegates**.

### Grip Pro's moves (all custom)

| Script | Job | Runs in |
|---|---|---|
| `DriftMoves` (order 61) | the held drift: start, swing out, hold, recover, tiers, boost | `FixedUpdate` |
| `GripMoves` (order 60) | spiral roll (Q/E), forward roll (Q+E), knocks during a roll, wall riding, rolling on and off walls, landing | input in `Update`, physics in `FixedUpdate` |
| `KnockTumble` (order 60) | a car hit by a roll: thrown, lifted, barrel rolled, dropped upright, and can knock the next car (chain) | `FixedUpdate` |
| `KnockStabilizer` | after a knock: keeps the car upright and sliding for a moment | `FixedUpdate` |
| `AirControl` | when airborne: levels the car and lets steering yaw it | `FixedUpdate` |
| `PartDownforce` | a fitted spoiler pushes the car down, more with speed squared | `FixedUpdate` |
| `VehicleReset` | R key, falling off, flipped, beached, off track: put the car back | `Update` |
| `DriftTuning` (asset) | the three drift characters' numbers, editable live | data |
| `CarLoadout`, `LoadoutPart` | fit tyres and spoilers, adjust drift and downforce | set-up |
| `CarTypes`, `GripSurface`, `VehicleBounds` | small helpers and markers | data |

### How they find each other

Nothing is wired in the Inspector. Each script calls `GetComponent<...>()` in `Awake()` and caches it. For example `DriftMoves` grabs `IVehicle`, `IDriftInput` and `GripMoves`. They also check each other's state:

- `DriftMoves.Busy` is true when `GripMoves.Leaping`, `GripMoves.WallRiding` or `KnockTumble.Tumbling`. A drift won't start, and ends, while another move has the car.
- `GripMoves` and `KnockTumble` switch `AirControl.enabled` off while they fly the car, then back on when they land.

And they **broadcast** with static C# events, so the HUD and audio can listen without the moves knowing about them:

- `DriftMoves.Started`, `TierUp`, `Ended`
- `GripMoves.Leapt`, `Landed`
- `KnockTumble.Knocked`

`StreamHud` subscribes to these to pay out Hype and show callouts. That's the **observer pattern**, exactly like `button.Click += Handler`.

---

## 2. How input flows

There are two input roads, and knowing which is which is the key to the whole system.

**Road A: through RCC (driving, braking, handbrake, nitro).**

1. Unity Input System reads the keys into RCC's input actions (remapped by `ControlBindings`).
2. RCC's own input script fills `car.Inputs` (`steerInput`, `throttleInput`, `brakeInput`, `handbrakeInput`, `nosInput`).
3. `RCCPVehicleAdapter.Update()` runs next (order 50). It notes the raw handbrake, may mute it, and may force throttle for Auto Accelerate.
4. RCC's `FixedUpdate` turns those inputs into wheel torque, brake torque and steering angle.

For a rival, step 1 and 2 are replaced by `RivalDriver` calling `IDriverInput.SetDriverInput(steer, throttle, brake, handbrake, nitro)` every physics step.

**Road B: straight from the keyboard (Grip Pro's moves).**

- `GripMoves.Update()` reads `Keyboard.current.qKey` and `eKey` (and gamepad shoulders) directly. It only **queues** the move (`queuedLeap`). `FixedUpdate` starts it on the next physics step. Reading in `Update` and acting in `FixedUpdate` is the correct pattern: key presses are per frame, physics is per fixed step, and doing physics in `Update` makes it frame-rate dependent.
- `DriftMoves` reads the handbrake and steer through `IDriftInput`, so it gets the **raw** values before RCC changes them. Why: RCC limits how far the wheels steer at high speed, which would make a drift impossible to start above about 150 km/h. The drift needs the player's intent, not the clamped wheel angle.
- `VehicleReset` reads `Keyboard.current.rKey` directly.

**The handbrake handshake (the cleverest bit):** above drift speed, `DriftMoves` sets `MuteHandbrake = true` on the adapter. The adapter keeps recording the real handbrake for the drift, but tells RCC "no handbrake". So the same key that locks the wheels in a slow parking turn becomes a smooth drift at speed, and RCC never locks the rear wheels and kills the slide.

---

## 3. What RCC does and what is custom

| Mechanic | RCC | Grip Pro custom |
|---|---|---|
| Engine, gears, clutch, differential, top speed, acceleration | yes | |
| Wheels, suspension, tyre friction, braking, ABS/TCS/ESP stability aids | yes | |
| Normal steering, speed-sensitive steering limit | yes | |
| Nitro: tank, burn, regeneration, extra torque | yes (`RCCP_Nos`) | adapter wraps it, races switch regeneration off, SHIFT ramps fill it |
| Chase camera | yes | `RCCPSpeedCamera` + `SpeedFeel` add FOV, shake, speed lines, jolts |
| Held drift with tiers and boost | | `DriftMoves` |
| Spiral roll, forward roll, knocks | | `GripMoves`, `KnockTumble`, `KnockStabilizer` |
| Wall riding | | `GripMoves.WallGrip` |
| Air levelling and air steering | | `AirControl` (and the scripted arcs in the moves) |
| Spoiler downforce | | `PartDownforce` |
| Resets and respawn | | `VehicleReset` |
| Control layout, rebinds | RCC's actions | `ControlBindings` remaps them |
| Handling preset | RCC's "Arcade" preset per car | drift character per car type |

A good one-line answer if someone asks: *"RCC is the car. Everything that makes it Grip Pro, the drift, the rolls, the knocks, wall riding, the camera feel, is our own code on the Rigidbody."*

---

## 4. Rigidbody physics: forces, torque, rotation, timing

Unity gives you four ways to move a `Rigidbody`. The codebase uses all four on purpose, and seeing **which one each move uses** explains most of the behaviour.

| Method | What it means | Who uses it |
|---|---|---|
| `AddForce(f, ForceMode.Acceleration)` | "push gently", mass ignored, physics stays in charge | `GripMoves.WallGrip` (fake gravity into the wall), `PartDownforce` (uses default mode, so mass counts) |
| `AddTorque(t, ForceMode.Acceleration)` | "turn gently" | `AirControl`, `KnockStabilizer`, wall alignment |
| `linearVelocity = v` | "you are now moving exactly like this" | `DriftMoves` every step while drifting, every scripted arc (rolls, tumbles, latch, landing) |
| `MoveRotation(q)` + `angularVelocity = 0` | "you are now facing exactly this way" | rolls and tumbles |

**Soft control (forces and torque)** keeps the physics engine in charge: collisions and RCC's wheels still matter. `AirControl` is a good example. Every step it computes the rotation that would bring `transform.up` back to world up, turns it into an axis and angle, and applies torque proportional to the angle (`levelStrength`), minus damping on the tilt spin (`tiltDamping`). That's a **PD controller** (proportional plus damping). The same shape appears in `KnockStabilizer` and in wall alignment.

**Hard control (setting velocity and rotation)** is how the moves get the exact, readable arcs a game needs. The spiral roll is pure maths:

- Pick a height `h` and an air time `T`.
- Start vertical speed `vy = 4h / T`, gravity `g = 8h / T²`. With those, the car peaks at exactly `h` after exactly `T/2` and lands at `T`. Check: peak height is `vy² / 2g = (16h²/T²) / (16h/T²) = h`.
- Every step: `vy -= g * dt`, then `linearVelocity = forward * speed + side * lateral + up * vy`.
- Rotation: an eased value `e` from 0 to 1 (smoothstep `r*r*(3-2r)`) turns into `AngleAxis(360 * turns * e, forward)`. The roll finishes at 82% of the air time (`rollFinishAt`) so the car meets the road on its wheels.
- During the move `useGravity = false` and `AirControl` is switched off, because the script **is** gravity. On landing both come back.

**The drift is a hybrid.** While drifting, `DriftMoves` keeps its own speed (`driftSpeed`) and direction of travel (`lineDir`). Each step it:

1. bends `lineDir` at a rate a real car could corner at that speed: `turn rate = latAccel / speed` (that's `a = v²/r` rearranged),
2. takes off only the drag it chooses (`dragKmh` and friends), so tyre scrub doesn't bleed the speed,
3. sets `linearVelocity = lineDir * driftSpeed` (keeping vertical speed),
4. steers the nose `SlipAngle` degrees off the line by setting the yaw part of `angularVelocity` from the angle error (`yawGain`),
5. drops the tyres' side grip to 15% (`sideGripWhileDrifting`) so the wheel colliders don't fight the slide.

If the car hits a wall, the real velocity drops by more than 6 m/s in a step, and `driftSpeed = Min(driftSpeed, actual + 6)` lets the crash win. That one line is what stops the drift ploughing through walls.

**Timing:**

- Physics runs in `FixedUpdate` at `Time.fixedDeltaTime` (50 Hz by default). Every move multiplies by `dt` so behaviour doesn't change with frame rate.
- Smoothing uses `1 - Mathf.Exp(-rate * dt)` instead of `Lerp(a, b, 0.1f)`. A fixed lerp factor changes with frame rate, the exponential form doesn't. You'll see it everywhere: drift angle response, gravity direction on walls, camera.
- **Execution order** matters because several scripts write to the same body: adapter 50, `GripMoves` and `KnockTumble` 60, `DriftMoves` 61. Later wins within a step.
- Cooldowns use `Time.time`, so the SHIFT slow-motion (`Time.timeScale`) slows them too. Camera and UI use `unscaledTime` so they don't freeze.

---

## 5. The numbers that matter

**Acceleration and top speed:** RCC's `RCCP_Engine` (`maximumTorqueAsNM`, `maximumSpeed`) and gearbox, set per car prefab. Difficulty scales rivals with `VehicleSpawn.Boost` (torque, top speed and tyre grip times a factor).

**Drift** (`DriftMoves`, or live in `Resources/SR_DriftTuning`, one profile per character):

| Value | Effect |
|---|---|
| `minKmh`, `steerToStart` | when a drift may start |
| `startAngle`, `maxAngle`, `kickRate` | how far the tail steps out, and how fast holding Space opens it |
| `angleResponse` | how quickly the body follows the asked-for angle |
| `recoverRate`, `endAngle` | how fast it straightens after Space is released, and when it counts as over |
| `latAccel` | how tight the drift line can bend |
| `dragKmh`, `dragKmhOnThrottle`, `handbrakeDragKmh` | speed lost while drifting |
| `sideGripWhileDrifting` | tyre side grip during the slide |
| `tierCharge`, `tierBoostKmh`, `chargeRate`, `boostScale` | tiers and the exit boost |
| `partAngleScale` | spoilers narrow the slide |

Weight nudges the profile in `Weigh()`: heavier cars swing slower and keep momentum (divides by `sqrt(mass / 1300)`).

**Nitro:** RCC's `RCCP_Nos` (`durationTime`, `regenerateRate`, extra torque). Races call `SetRegeneration(false)` so nitro only comes from SHIFT ramps.

**Airborne:** `AirControl.levelStrength`, `tiltDamping`, `yawStrength`, `airborneDelay`. Moves: `rollHeight`, `rollAirTime`, `rollSide`, `rollTurns`, `rollFinishAt`, `landBoostKmh`, `landSlam`.

**Stability:** RCC's stability aids plus `KnockStabilizer.levelStrength`, `damping`, `slideDecay`, plus `PartDownforce.level` and `weightShareAt180`.

---

## 6. Dependencies, conflicts, performance and likely bugs

These are things I found reading the code. Each is worth fixing in your rewrite, and each is a good talking point to show you understand it.

**Conflicts (two systems driving one Rigidbody)**

1. **Many writers, no owner.** `DriftMoves`, `GripMoves`, `KnockTumble`, `KnockStabilizer` and RCC can all write velocity or rotation. It works because of execution order and `Busy` checks, but nothing **guarantees** only one move has the car. A rival hit mid-drift relies on `Busy` noticing the tumble one step later.
2. **`useGravity` and `AirControl.enabled` are toggled by three scripts.** `GripMoves.WallGrip` sets `body.useGravity` every physics step, `KnockTumble` sets it while tumbling, and both switch `AirControl` on and off. If one forgets to restore it (an exception, a disabled component), the car floats. Each has an `OnDisable` safety net, which is a sign of the risk.
3. **Tyre friction tug of war.** `DriftMoves` edits `WheelCollider.sidewaysFriction` directly, and re-applies it every step while drifting. That repeat suggests RCC overwrites friction on its own schedule. If RCC wins a step, the slide grips for a moment.
4. **Execution order as glue.** Correctness depends on 50, 60, 61. Move one number and moves silently fight.

**Performance**

5. `DriftMoves.FixedUpdate` calls `ApplyCharacter()` **every physics step on every car** so live tuning works. That includes `GetComponent<Rigidbody>()` in `Weigh()`. Fine for 6 cars, wasteful for more, and it also overwrites any number you type into the Inspector. Live tuning should be editor-only or event-driven.
6. `DriftMoves.Busy` calls `GetComponent<KnockTumble>()` every step instead of caching it.
7. `GripMoves.CheckKnock` and `KnockTumble.Chain` use `Physics.OverlapBox`, which allocates a new array each call, and `CheckKnock` also allocates a new `HashSet` per hit. `OverlapBoxNonAlloc` with a reused buffer removes the garbage.
8. `AirControl.logJumps` defaults to true: a `Debug.Log` on every landing, for every car. Logging is slow and spams the console in builds.

**Likely bugs or design debt**

9. **Hard-coded keys.** `GripMoves` (Q/E) and `VehicleReset` (R) read the keyboard directly, so they ignore the player's rebinds in Settings, unlike driving and nitro.
10. **Grounded checks use any wheel.** `AnyWheelGrounded` is true with one wheel touching. A roll landing on a kerb with one wheel can count as landed early. The moves guard with "late in the arc" checks (`k > 0.8`), which is a patch over the root cause.
11. **Two kinds of speed.** `GripMoves.Kmh` uses full 3D speed, `DriftMoves.Kmh` uses flat speed, `IVehicle.SpeedKmh` is RCC's signed forward speed. Three definitions of "speed" will disagree on slopes and in the air.
12. **Static events never unsubscribed by the sender.** Fine as long as every listener unsubscribes in `OnDisable`. A listener that forgets leaks and keeps receiving events after a scene change.
13. **The adapter writes RCC's inputs in `Update` while RCC reads them in `FixedUpdate`.** At low frame rates several physics steps share one input write, which is normal, but Auto Accelerate forcing throttle in `Update` means a brake tapped for less than a frame can be missed.

---

## 7. A cleaner design you could build yourself

Keep what's good (the interfaces, the maths, the events, the tuning asset), and add three ideas.

### Idea 1: one owner for the Rigidbody

A `VehicleMotor` component decides **who drives the car this step**. Each move asks for control and gives it back:

```csharp
public enum MotionOwner { Wheels, Drift, Scripted }

public class VehicleMotor : MonoBehaviour
{
    public MotionOwner Owner { get; private set; } = MotionOwner.Wheels;

    public bool TryTake(MotionOwner who, int priority) { /* higher priority wins, e.g. Scripted > Drift */ }
    public void Release(MotionOwner who) { /* back to Wheels, restores gravity and air control in ONE place */ }
}
```

Now gravity, `AirControl` and tyre friction are restored in one place, and "a tumble cancels a drift" is a rule, not a timing accident. C# comparison: a `lock` or a mutex around shared state.

### Idea 2: each move is a small class with the same shape

```csharp
public interface IVehicleMove
{
    bool CanStart(in MoveContext ctx);   // speed, grounded, cooldown
    void Begin(in MoveContext ctx);
    bool Tick(in MoveContext ctx, float dt); // false when finished
    void End(bool interrupted);
}
```

`SpiralRoll`, `ForwardRoll`, `WallLatch`, `WallRoll`, `HeldDrift` and `Tumble` each become one class, plain C# where possible (not `MonoBehaviour`), driven by one `MoveRunner`. That's the **state pattern**: today `GripMoves` is a 600-line class with five flags (`leapT`, `rollT`, `latchT`, `unrollT`, `awaitLand`) acting as a hidden state machine. Making the states explicit removes impossible combinations.

### Idea 3: one input source for players and AI

```csharp
public struct DriveIntent { public float steer, throttle, brake; public bool drift, nitro, rollLeft, rollRight; }
public interface IIntentSource { DriveIntent Read(); }
```

`PlayerIntent` reads the Input System actions (so Q, E and R become rebindable), `RivalIntent` comes from the AI. Every move reads `DriveIntent` and never `Keyboard.current`. Same idea as passing an `IClock` into a class instead of calling `DateTime.Now`: easy to test, easy to swap.

### Keep the maths pure

Pull the formulas out into static functions you can unit test without Unity running:

```csharp
public static class Arc
{
    public static (float vy, float g) ForHeight(float h, float t) => (4f * h / t, 8f * h / (t * t));
}
```

A test asserting the peak equals `h` is the kind of thing that proves you understand the system.

### A rebuild order that keeps the game playable

1. `DriveIntent` and `PlayerIntent` (rebindable Q, E, R). Moves read it. Nothing else changes.
2. `VehicleMotor` with `TryTake` and `Release`. Move gravity and air-control toggling into it.
3. Port the spiral roll to `IVehicleMove`. Compare against the old one side by side in the Moves Lab.
4. Port wall riding, then the tumble, then the drift last (it's the most tuned, so it's the riskiest).
5. Fix the performance items (cache components, `NonAlloc` overlaps, editor-only live tuning, logging off).

Do one step per commit, play-test after each, and you'll end with code you wrote and can explain line by line, on top of the same feel players already like.

---

## Glossary

- **Rigidbody**: Unity's physics object. It has mass, velocity and angular velocity, and the physics engine moves it.
- **FixedUpdate**: called on a fixed timer for physics. **Update**: called once per rendered frame.
- **WheelCollider**: Unity's raycast wheel with suspension and tyre friction. RCC wraps it.
- **Execution order**: which scripts run first in a frame (`[DefaultExecutionOrder]`).
- **PD controller**: steer toward a target by an amount proportional to the error, minus some of the current speed so it doesn't wobble.
- **Smoothstep**: `x*x*(3-2x)`, an ease-in-out curve from 0 to 1.
