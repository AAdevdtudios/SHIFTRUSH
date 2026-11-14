using ShiftRush.Race;
using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// The life on a car: nitro flames (or the car's own nitro effect from the <see cref="Core.RaceCatalog"/>) and a
    /// light trail while nitro burns, underglow in the car's colour, a dust
    /// (or snow) puff on landing from a jump, and dirt spray while it's on a shortcut lane. Added to every car by
    /// <see cref="FXDirector"/>; works on the player and rivals alike.
    /// </summary>
    public class CarFX : MonoBehaviour
    {
        IVehicle vehicle;
        IVehicleDash dash;
        VFXKit kit;
        Color colour;
        bool isPlayer;

        ParticleSystem[] flames;
        GameObject[] thrusters;
        TrailRenderer[] trails;
        ParticleSystem puff, dirt;
        Light glow;
        Bounds local;
        float airTime, glowBase, nitroOffAt = -1f;
        bool wasNitro;

        public void Init(IVehicle v, Color c, bool player, Core.RaceCatalog.Car spec = null)
        {
            vehicle = v; dash = v as IVehicleDash; colour = c; isPlayer = player;
            kit = VFXKit.Instance;
            local = LocalBounds();
            if (kit == null) return;

            // Nitro: two flames at the exhausts (lower rear, either side of centre) and one trail between.
            float back = local.min.z + 0.05f, low = local.min.y + Mathf.Max(0.3f, local.size.y * 0.28f);
            if (spec != null && spec.nitroVfx != null) MakeThrusters(spec, back, low);
            else if (kit.nitroFlame != null)
            {
                flames = new ParticleSystem[2];
                for (int s = 0; s < 2; s++)
                {
                    var f = Instantiate(kit.nitroFlame, transform);
                    f.transform.localPosition = new Vector3((s == 0 ? -1 : 1) * local.size.x * 0.12f, low, back);
                    f.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                    var main = f.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(Color.white, colour);
                    f.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    flames[s] = f;
                }
            }
            if (kit.nitroTrail != null)
            {
                // Two thin light streaks off the tail lights.
                trails = new TrailRenderer[2];
                for (int k = 0; k < 2; k++)
                {
                    var go = new GameObject("Nitro Trail");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3((k == 0 ? -1 : 1) * local.size.x * 0.36f, local.min.y + local.size.y * 0.55f, back + 0.1f);
                    var t = go.AddComponent<TrailRenderer>();
                    t.sharedMaterial = kit.nitroTrail;
                    t.time = 0.3f;
                    t.minVertexDistance = 0.5f;
                    t.widthCurve = new AnimationCurve(new Keyframe(0f, 0.28f), new Keyframe(1f, 0f));
                    t.colorGradient = Fade(colour);
                    t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    t.emitting = false;
                    t.enabled = false; // off entirely between burns: a resting trail still draws a stub
                    trails[k] = t;
                }
            }
            if (kit.landingPuff != null)
            {
                puff = Instantiate(kit.landingPuff, transform);
                puff.transform.localPosition = new Vector3(0f, local.min.y + 0.1f, local.center.z);
                var main = puff.main; main.startColor = TrackAmbience.DustColour;
                var sh = puff.shape; sh.scale = new Vector3(local.size.x * 1.1f, local.size.z * 0.8f, 0.1f);
            }
            if (kit.dirtSpray != null)
            {
                dirt = Instantiate(kit.dirtSpray, transform);
                dirt.transform.localPosition = new Vector3(0f, local.min.y + 0.15f, local.min.z + 0.4f);
                dirt.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.6f, -1f));
                var sh = dirt.shape; sh.scale = new Vector3(local.size.x * 0.9f, 0.1f, 0.1f);
                dirt.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Underglow: a soft pool of the car's colour on the road. A spot pointing straight down, so it can't
            // light the bodywork above (lights here cast no shadows and would shine through the car).
            var lg = new GameObject("Underglow");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, local.min.y + 0.12f, local.center.z);
            lg.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Spot;
            glow.spotAngle = 160f;
            glow.innerSpotAngle = 60f;
            glow.color = colour;
            glow.range = 2.5f;
            glowBase = isPlayer ? 14f : 11f;
            glow.intensity = glowBase;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForcePixel;
        }

        void Update()
        {
            if (vehicle == null) return;
            float dt = Time.deltaTime;
            bool grounded = vehicle.AnyWheelGrounded;
            float speed = Mathf.Abs(vehicle.SpeedKmh);

            // Nitro on/off.
            bool nitro = dash != null && dash.NitroActive;
            if (nitro != wasNitro)
            {
                if (flames != null) foreach (var f in flames) { if (nitro) f.Play(true); else f.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
                if (thrusters != null) foreach (var g in thrusters)
                {
                    if (nitro) { g.SetActive(true); foreach (var ps in g.GetComponentsInChildren<ParticleSystem>()) ps.Play(false); }
                    else foreach (var ps in g.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
                if (trails != null) foreach (var t in trails)
                {
                    if (nitro) { t.Clear(); t.enabled = true; }
                    t.emitting = nitro;
                }
                nitroOffAt = nitro ? -1f : Time.time;
                if (isPlayer) ScreenFX.Set(ScreenFX.Overlay.Nitro, nitro);
                wasNitro = nitro;
            }
            // Let the streaks and thrusters fade out, then switch them off.
            if (!nitro && nitroOffAt > 0f && Time.time - nitroOffAt > 0.4f)
            {
                if (trails != null) foreach (var t in trails) t.enabled = false;
                if (thrusters != null) foreach (var g in thrusters) g.SetActive(false);
                nitroOffAt = -1f;
            }
            if (glow != null) glow.intensity = Mathf.MoveTowards(glow.intensity, glowBase * (nitro ? 1.8f : 1f), dt * 12f);

            // Landing from a jump: a puff scaled by how long it was in the air.
            if (!grounded) airTime += dt;
            else
            {
                if (airTime > 0.35f && puff != null)
                {
                    var em = puff.emission;
                    puff.Emit(Mathf.RoundToInt(Mathf.Lerp(8f, 22f, Mathf.InverseLerp(0.35f, 2f, airTime))));
                }
                airTime = 0f;
            }

            // Dirt off the tyres on a shortcut lane.
            if (dirt != null)
            {
                bool spray = grounded && speed > 25f && ShortcutPath.All.Count > 0 && ShortcutPath.OnAny(transform.position, 0.5f);
                if (spray && !dirt.isEmitting) dirt.Play(true);
                else if (!spray && dirt.isEmitting) dirt.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (spray) { var em = dirt.emission; em.rateOverTimeMultiplier = Mathf.Lerp(20f, 70f, speed / 180f); }
            }
        }

        void OnDisable()
        {
            if (isPlayer && wasNitro) ScreenFX.Set(ScreenFX.Overlay.Nitro, false);
        }

        /// <summary>The car's own nitro effect at each exhaust, scaled, with the pack's demo parts hidden; off until nitro.</summary>
        void MakeThrusters(Core.RaceCatalog.Car spec, float back, float low)
        {
            var points = spec.nitroExhausts != null && spec.nitroExhausts.Length > 0 ? spec.nitroExhausts
                : new[] { new Vector3(-local.size.x * 0.12f, low, back), new Vector3(local.size.x * 0.12f, low, back) };
            thrusters = new GameObject[points.Length];
            for (int k = 0; k < points.Length; k++)
            {
                var g = Instantiate(spec.nitroVfx, transform);
                g.name = "Nitro " + (k + 1);
                g.transform.localPosition = points[k];
                g.transform.localRotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(spec.nitroRotation);
                g.transform.localScale = Vector3.one * spec.nitroScale;
                foreach (var t in g.GetComponentsInChildren<Transform>(true))
                    if (spec.nitroHide != null && System.Array.IndexOf(spec.nitroHide, t.name) >= 0) t.gameObject.SetActive(false);
                foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy; // follow the car-sized scale
                    main.playOnAwake = false;
                }
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.SetActive(false);
                thrusters[k] = g;
            }
        }

        /// <summary>One-shot shockwave at the car (arriving back from a SHIFT world).</summary>
        public void Arrive()
        {
            if (kit == null || kit.arrivalBurst == null) return;
            var b = Instantiate(kit.arrivalBurst, transform.position + Vector3.up * 0.2f, Quaternion.LookRotation(transform.forward));
            var main = b.main; main.startColor = new ParticleSystem.MinMaxGradient(colour, kit.cyan);
            b.Play(true);
            Destroy(b.gameObject, 3f);
        }

        /// <summary>The car body's box in its own space (from the meshes, so it's exact at any heading).</summary>
        Bounds LocalBounds()
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

        static Gradient Fade(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.25f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
