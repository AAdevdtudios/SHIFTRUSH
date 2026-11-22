using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// A wind aura wrapped around the player's car: spinning rings of wind at both ends. One set wraps the front
    /// bumper and streams back over the bonnet, the other wraps the rear bumper and trails off behind the car, where
    /// the chase camera sees it. The rings are fitted to the car's own width and height, so a low sports car gets a low, flat aura
    /// and a tall truck a tall one. It only shows while the car spiral rolls (<see cref="GripMoves.Rolling"/>): a burst
    /// of rings the moment the roll starts, a stream through it, then the last rings blow away after the landing.
    /// Speed drives the flow: the faster the car, the faster the rings stream and spin and the more of them there are.
    /// The rings are the PixPlays Wind Aura (a paid, git-ignored pack, set in <see cref="VFXKit.windAura"/>).
    /// Without the pack the component switches itself off.
    /// </summary>
    public class SpeedAura : MonoBehaviour
    {
        [Tooltip("The aura prefab. Empty: taken from the VFX kit.")]
        public GameObject prefab;

        [Header("Speed (km/h)")]
        [Tooltip("Bottom of the speed range: the slowest flow.")]
        public float showKmh = 35f;
        [Tooltip("Top of the speed range: the fastest flow.")]
        public float fullKmh = 200f;

        [Header("Flow")]
        [Tooltip("How fast the rings stream and spin at the bottom of the speed range (1 = the pack's own pace).")]
        public float minSimSpeed = 0.5f;
        [Tooltip("How fast they stream and spin at full speed.")]
        public float maxSimSpeed = 3.2f;
        [Tooltip("New rings per real second at the bottom of the speed range.")]
        public float minRingsPerSecond = 8f;
        [Tooltip("New rings per real second at full speed.")]
        public float maxRingsPerSecond = 18f;
        [Tooltip("Rings thrown out at once the moment a roll starts, per end, so it reads instantly.")]
        public int burstRings = 3;

        [Header("Look")]
        public Color tint = new Color(0.72f, 0.93f, 1f, 1f);
        [Range(0f, 1f)] public float maxAlpha = 0.85f;
        [Tooltip("Brightens the rings (the pack shader's alpha boost; the pack's own is 1).")]
        public float glow = 1.8f;
        [Tooltip("How far each set of rings travels, as a share of the car's length.")]
        public float ringRun = 0.6f;
        [Tooltip("How far past the rear bumper the rear rings trail, as a share of the car's length.")]
        public float rearTrail = 0.35f;
        [Tooltip("How far out from the body the rings sit, as a multiple of the car's half width.")]
        public float widthPad = 1.25f;
        [Tooltip("As a multiple of the car's half height.")]
        public float heightPad = 1.35f;

        // The pack's ring mesh is about 1.18 m in radius at particle size 1, rings travel 4 units over their life and
        // the ring child is stretched 1.5 along its length: the numbers the fit below is built on.
        const float RingRadius = 1.18f, TravelUnits = 4f * 1.5f;

        IVehicle vehicle;
        Rigidbody body;
        GripMoves moves;
        DriftMoves drift;
        bool wasRolling;
        ParticleSystem[] rings;
        float amount;

        void Start()
        {
            vehicle = GetComponent<IVehicle>();
            body = GetComponent<Rigidbody>();
            moves = GetComponent<GripMoves>();
            drift = GetComponent<DriftMoves>();
            if (prefab == null && VFXKit.Instance != null) prefab = VFXKit.Instance.windAura;
            if (prefab == null) { enabled = false; return; }

            Bounds b = CarBounds();
            float len = b.size.z;
            rings = new ParticleSystem[2];
            // Front set starts at the front bumper, rear set ends rearTrail past the rear bumper.
            rings[0] = MakeRings(prefab, "Wind Aura Front", b, b.max.z, len * ringRun);
            rings[1] = MakeRings(prefab, "Wind Aura Rear", b, b.min.z - len * rearTrail + len * ringRun, len * ringRun);
            if (rings[0] == null || rings[1] == null) { rings = null; enabled = false; }
        }

        ParticleSystem MakeRings(GameObject source, string name, Bounds b, float startZ, float run)
        {
            var go = Instantiate(source, transform);
            go.name = name;
            // The pack's own scripts play the effect once and then destroy it. This aura lives as long as the car.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);

            // The pack's aura rises along its up axis. Laid on its side it runs front to back: its up becomes the car's
            // back, its depth the car's up. Centred on the middle of the car's height, so it follows the car's shape.
            var rig = go.transform;
            rig.localPosition = new Vector3(b.center.x, b.center.y, startZ);
            rig.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            rig.localScale = new Vector3(
                b.size.x * 0.5f * widthPad / RingRadius,
                run / TravelUnits,
                b.size.y * 0.5f * heightPad / RingRadius);

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps == null) return null;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // rides with the car, even mid roll
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            // The pack fires a burst of rings every time its loop restarts (every few seconds). Off: rings come only from
            // a roll (the rate above and the start-of-roll burst in LateUpdate).
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_AlphaBoost"))
                r.material.SetFloat("_AlphaBoost", glow); // own copy, the pack's material is left alone
            ps.Play(true);
            return ps;
        }

        void LateUpdate()
        {
            if (rings == null) return;
            float kmh = vehicle != null ? Mathf.Abs(vehicle.SpeedKmh) : body != null ? body.linearVelocity.magnitude * 3.6f : 0f;
            float target = Mathf.InverseLerp(showKmh, fullKmh, kmh);
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * 2f);

            // Spiral rolls, and a drift at its top charge tier.
            bool rolling = (moves != null && moves.Rolling) || (drift != null && drift.Drifting && drift.Tier >= 3);
            float sim = Mathf.Lerp(minSimSpeed, maxSimSpeed, amount);
            if (!rolling) sim = Mathf.Max(sim, 2f); // after the roll the leftover rings blow away quickly
            float alpha = maxAlpha * (Core.GameSettings.ReducedMotion ? 0.5f : 1f);
            foreach (var ps in rings)
            {
                var main = ps.main;
                main.simulationSpeed = sim;
                main.startColor = new Color(tint.r, tint.g, tint.b, alpha);
                // Emission runs on simulation time too, so divide it back out: the rings per real second are what's set.
                var emission = ps.emission;
                emission.rateOverTime = rolling ? Mathf.Lerp(minRingsPerSecond, maxRingsPerSecond, amount) / sim : 0f;
                if (rolling && !wasRolling) ps.Emit(burstRings);
            }
            wasRolling = rolling;
        }

        /// <summary>The car's body in its own space, from its meshes (the same measure CarFX uses).</summary>
        Bounds CarBounds()
        {
            var b = new Bounds(Vector3.up * 0.6f, new Vector3(1.9f, 1.2f, 4.4f));
            bool any = false;
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}
