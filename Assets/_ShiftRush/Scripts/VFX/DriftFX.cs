using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// What a drift looks and sounds like: thick tyre smoke and sparks off the rear wheels, light trails off the tail,
    /// and a tyre screech that rises with the slide angle. All of it takes the colour of the charge tier (white, then
    /// cyan, magenta, gold), so the player reads the charge without looking at the HUD. A tier-up throws a burst of
    /// sparks. Added by <see cref="DriftMoves"/>. Materials and the screech come from the <see cref="VFXKit"/>.
    /// </summary>
    public class DriftFX : MonoBehaviour
    {
        public Color[] tierColours =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color32(56, 225, 255, 255),
            new Color32(255, 64, 217, 255),
            new Color32(255, 200, 60, 255),
        };
        [Tooltip("Smoke particles per second per wheel at full intensity.")]
        public float smokeRate = 60f;
        [Tooltip("Sparks per second per wheel at full intensity (none until tier 1).")]
        public float sparkRate = 70f;
        [Range(0f, 1f)] public float screechVolume = 0.8f;

        DriftMoves drift;
        ParticleSystem[] smoke = new ParticleSystem[2], sparks = new ParticleSystem[2];
        TrailRenderer[] trails = new TrailRenderer[2];
        AudioSource screech;
        Rigidbody body;

        void Start()
        {
            drift = GetComponent<DriftMoves>();
            body = GetComponent<Rigidbody>();
            var kit = VFXKit.Instance;
            Bounds b = VehicleBounds.Local(transform);

            // Rear wheels: the two wheel colliders furthest back, else the rear corners of the body.
            Vector3[] rear = { new Vector3(-b.extents.x * 0.8f, b.min.y + 0.1f, b.min.z + 0.6f), new Vector3(b.extents.x * 0.8f, b.min.y + 0.1f, b.min.z + 0.6f) };
            var wc = GetComponentsInChildren<WheelCollider>(true);
            if (wc.Length >= 2)
            {
                System.Array.Sort(wc, (a, c) => transform.InverseTransformPoint(a.transform.position).z.CompareTo(transform.InverseTransformPoint(c.transform.position).z));
                for (int i = 0; i < 2; i++)
                {
                    Vector3 p = transform.InverseTransformPoint(wc[i].transform.position);
                    rear[i] = new Vector3(p.x, p.y - wc[i].radius * 0.8f, p.z);
                }
            }

            for (int i = 0; i < 2; i++)
            {
                smoke[i] = Smoke(rear[i], kit != null ? kit.driftSmoke : null);
                sparks[i] = Sparks(rear[i], kit != null ? kit.driftSparks : null);
            }
            // Trails off the tail lights.
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Drift Trail");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * b.extents.x * 0.7f, b.center.y + b.extents.y * 0.1f, b.min.z + 0.05f);
                var t = go.AddComponent<TrailRenderer>();
                t.time = 0.45f; t.minVertexDistance = 0.2f; t.widthMultiplier = 0.22f;
                t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.receiveShadows = false;
                if (kit != null && kit.nitroTrail != null) t.sharedMaterial = kit.nitroTrail;
                t.emitting = false;
                trails[i] = t;
            }

            if (kit != null && kit.driftScreech != null)
            {
                screech = gameObject.AddComponent<AudioSource>();
                screech.clip = kit.driftScreech;
                screech.loop = true;
                screech.playOnAwake = false;
                screech.spatialBlend = 0.8f;
                screech.volume = 0f;
                screech.minDistance = 6f;
            }

            DriftMoves.TierUp += OnTier;
        }

        void OnDestroy() { DriftMoves.TierUp -= OnTier; }

        void OnTier(DriftMoves d, int tier)
        {
            if (d != drift) return;
            Color c = Colour(tier);
            foreach (var s in sparks)
            {
                if (s == null) continue;
                var p = new ParticleSystem.EmitParams { startColor = c };
                s.Emit(p, 30);
            }
        }

        Color Colour(int tier) { return tierColours[Mathf.Clamp(tier, 0, tierColours.Length - 1)]; }

        void Update()
        {
            if (drift == null) return;
            bool on = drift.Drifting;
            float k = on ? drift.Intensity : 0f;
            Color c = Colour(drift.Tier);
            for (int i = 0; i < 2; i++)
            {
                if (smoke[i] != null)
                {
                    var e = smoke[i].emission; e.rateOverTime = smokeRate * k;
                    var m = smoke[i].main; m.startColor = Color.Lerp(new Color(0.85f, 0.85f, 0.9f, 0.55f), new Color(c.r, c.g, c.b, 0.55f), drift.Tier > 0 ? 0.45f : 0f);
                }
                if (sparks[i] != null)
                {
                    var e = sparks[i].emission; e.rateOverTime = drift.Tier > 0 ? sparkRate * k : 0f;
                    var m = sparks[i].main; m.startColor = c;
                }
                if (trails[i] != null)
                {
                    trails[i].emitting = on && drift.Tier > 0;
                    trails[i].startColor = new Color(c.r, c.g, c.b, 0.85f);
                    trails[i].endColor = new Color(c.r, c.g, c.b, 0f);
                }
            }
            if (screech != null)
            {
                float want = on ? screechVolume * Mathf.Lerp(0.5f, 1f, k) : 0f;
                screech.volume = Mathf.MoveTowards(screech.volume, want, Time.deltaTime * (on ? 6f : 3f));
                screech.pitch = Mathf.Lerp(0.85f, 1.15f, drift.SlipAngle / Mathf.Max(1f, drift.maxAngle));
                if (screech.volume > 0.01f && !screech.isPlaying) screech.Play();
                else if (screech.volume <= 0.01f && screech.isPlaying) screech.Stop();
            }
        }

        ParticleSystem Smoke(Vector3 localPos, Material mat)
        {
            var go = new GameObject("Drift Smoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.playOnAwake = true;
            m.simulationSpace = ParticleSystemSimulationSpace.World; // left behind on the road
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.maxParticles = 400;
            m.gravityModifier = -0.05f;
            var e = ps.emission; e.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (mat != null) r.sharedMaterial = mat;
            ps.Play();
            return ps;
        }

        ParticleSystem Sparks(Vector3 localPos, Material mat)
        {
            var go = new GameObject("Drift Sparks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back + Vector3.up * 0.4f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.playOnAwake = true;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            m.gravityModifier = 1.4f;
            m.maxParticles = 300;
            var e = ps.emission; e.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 28f; sh.radius = 0.1f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f; r.lengthScale = 2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (mat != null) r.sharedMaterial = mat;
            ps.Play();
            return ps;
        }
    }
}
