using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.UI;
using TMPro;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// The festival around the circuit: fan zones on the outside of the biggest corners, each a tiered stand packed
    /// with a cheering crowd, flag poles in the broadcast colours, a CHANNEL 13 banner and a tethered balloon. When
    /// the player comes through, the crowd jumps and coloured smoke flares go off.
    /// Built to be cheap: the crowd is the driver pack's modular people dressed in random outfits, posed (arms down,
    /// one up, both up), then each frozen into one static mesh per material at load, so a few shared meshes are
    /// drawn many times with no skinning and no animator. A single script bounces every fan.
    /// No colliders anywhere: nothing here can stop a car.
    /// </summary>
    public class FanZones : MonoBehaviour
    {
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);
        static readonly Color Gold = new Color32(255, 196, 46, 255);
        static readonly Color Ink = new Color32(16, 12, 26, 255);

        const int MaxZones = 5;
        const int Variants = 8;
        const float MinGap = 320f;

        class Fan { public Transform t; public Vector3 home; public float phase, hop; }
        class Zone { public Vector3 centre; public readonly List<Fan> fans = new List<Fan>(); public ParticleSystem[] flares; public float hype; public bool fired; }

        readonly List<Zone> zones = new List<Zone>();
        readonly List<GameObject> crowdVariants = new List<GameObject>();
        RaceManager race;
        Material standMat, trimMat;

        public static FanZones Build(RaceLine line, SpectacleKit kit, Transform parent)
        {
            var go = new GameObject("Fan Zones");
            go.transform.SetParent(parent, false);
            var fz = go.AddComponent<FanZones>();
            fz.Make(line, kit);
            return fz;
        }

        void Make(RaceLine line, SpectacleKit kit)
        {
            if (line == null || line.points == null || line.points.Length < 20) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            standMat = new Material(lit) { color = new Color32(38, 34, 52, 255) };
            trimMat = new Material(lit) { color = Magenta };
            BakeCrowd();

            foreach (var i in PickCorners(line))
            {
                var z = MakeZone(line, i, kit);
                if (z != null) zones.Add(z);
                if (zones.Count >= MaxZones) break;
            }
            foreach (var v in crowdVariants) v.SetActive(false);
        }

        // ------------------------------------------------------------------ where

        /// <summary>The tightest corners, spread round the lap, tightest first.</summary>
        static List<int> PickCorners(RaceLine line)
        {
            int n = line.points.Length;
            var idx = new List<int>();
            for (int i = 0; i < n; i += 3) idx.Add(i);
            idx.Sort((a, b) => line.CornerSpeed(a).CompareTo(line.CornerSpeed(b)));
            var picked = new List<int>();
            foreach (var i in idx)
            {
                bool far = true;
                foreach (var p in picked)
                {
                    float d = Mathf.Abs(line.distances[i] - line.distances[p]);
                    d = Mathf.Min(d, line.length - d);
                    if (d < MinGap) { far = false; break; }
                }
                if (far) picked.Add(i);
                if (picked.Count >= MaxZones * 3) break;
            }
            return picked;
        }

        Zone MakeZone(RaceLine line, int i, SpectacleKit kit)
        {
            int n = line.points.Length;
            Vector3 dir = line.Direction(i), right = line.Right(i);
            // The outside of the corner, where the crowd sees the cars come at them.
            float turn = Vector3.SignedAngle(line.Direction((i - 6 + n) % n), line.Direction((i + 6) % n), Vector3.up);
            float side = turn > 0f ? -1f : 1f;
            float half = line.halfWidth > 0f ? line.halfWidth : 9f;
            Vector3 road = line.points[i];
            Vector3 centre = road + right * side * (half + 13f);
            if (!Ground(centre, road.y, out float gy)) return null;
            centre.y = gy;
            // Room for a stand: nothing big and solid in the way (trees and props are fine, buildings aren't).
            var face = Quaternion.LookRotation(-right * side, Vector3.up); // stand faces the road
            foreach (var c in Physics.OverlapBox(centre + Vector3.up * 4f, new Vector3(14f, 3.5f, 5f), face, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c is TerrainCollider || c.attachedRigidbody != null) continue;
                if (c.bounds.size.magnitude > 9f && c.bounds.size.y > 3f) return null;
            }

            var z = new Zone { centre = centre };
            var root = new GameObject("Fan Zone").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(centre, face);
            Stand(root, z);
            Flags(root);
            Banner(root, kit);
            z.flares = Flares(root, kit);
            return z;
        }

        static bool Ground(Vector3 p, float roadY, out float y)
        {
            y = roadY;
            if (!Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 90f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y - roadY) > 7f) return false; // a cliff or a drop: no stand here
            y = hit.point.y;
            return true;
        }

        // ------------------------------------------------------------------ the stand and its crowd

        void Stand(Transform root, Zone z)
        {
            // The zone's forward points at the road. Tiers step back and up away from it; the crowd faces it.
            const int Rows = 3, PerRow = 10;
            for (int r = 0; r < Rows; r++)
            {
                float top = 0.6f + r * 1.0f, depth = -(1.5f + r * 1.6f);
                Box(root, "Tier " + (r + 1), new Vector3(0f, top * 0.5f, depth), new Vector3(26f, top, 1.6f), standMat);
                Box(root, "Trim", new Vector3(0f, top, depth + 0.8f), new Vector3(26f, 0.12f, 0.1f), trimMat);
                for (int k = 0; k < PerRow; k++)
                {
                    if (crowdVariants.Count == 0) break;
                    var v = crowdVariants[Random.Range(0, crowdVariants.Count)];
                    var fan = Instantiate(v, root);
                    fan.SetActive(true);
                    var lp = new Vector3(-11.5f + k * 2.55f + Random.Range(-0.35f, 0.35f), top, depth);
                    fan.transform.localPosition = lp;
                    fan.transform.localRotation = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);
                    z.fans.Add(new Fan { t = fan.transform, home = lp, phase = Random.value * 6.28f, hop = Random.Range(0.6f, 1.2f) });
                }
            }
            // A low dark back wall with a magenta stripe: the crowd is the colour, not the wall.
            Box(root, "Back", new Vector3(0f, 2.3f, -6.6f), new Vector3(26.5f, 4.6f, 0.3f), standMat);
            Box(root, "Stripe", new Vector3(0f, 4.45f, -6.42f), new Vector3(26.5f, 0.35f, 0.05f), trimMat);
        }

        static Transform Box(Transform parent, string name, Vector3 local, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Flags(Transform root)
        {
            var colours = new[] { Magenta, Cyan, Gold, Magenta, Cyan };
            for (int k = 0; k < colours.Length; k++)
            {
                float x = -12f + k * 6f;
                Box(root, "Pole", new Vector3(x, 5f, -7f), new Vector3(0.15f, 10f, 0.15f), standMat);
                var mat = new Material(standMat) { color = colours[k] };
                var flag = Box(root, "Flag", new Vector3(x + 1.3f, 9f, -7f), new Vector3(2.6f, 1.6f, 0.05f), mat);
                flag.gameObject.AddComponent<Wave>();
            }
        }

        void Banner(Transform root, SpectacleKit kit)
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(root, false);
            // A board above the back wall, text readable from the road.
            Box(root, "Board", new Vector3(0f, 6.1f, -6.62f), new Vector3(16f, 2.6f, 0.25f), trimMat);
            go.transform.localPosition = new Vector3(0f, 6.1f, -6.45f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TMP reads from behind its forward
            var t = go.AddComponent<TextMeshPro>();
            if (kit != null && kit.bannerFont != null) t.font = kit.bannerFont;
            t.text = "CHANNEL 13  //  GRIP PRO";
            t.fontSize = 14f; t.alignment = TextAlignmentOptions.Center; t.color = Ink;
            t.fontStyle = FontStyles.Italic;
            t.rectTransform.sizeDelta = new Vector2(15f, 2.4f);
            t.enableAutoSizing = true; t.fontSizeMin = 4f; t.fontSizeMax = 14f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        ParticleSystem[] Flares(Transform root, SpectacleKit kit)
        {
            var colours = new[] { Magenta, Cyan, Gold };
            var list = new ParticleSystem[colours.Length];
            for (int k = 0; k < colours.Length; k++)
            {
                var go = new GameObject("Flare");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(-9f + k * 9f, 6.8f, -6.4f);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false; main.duration = 3f; main.playOnAwake = false;
                main.startLifetime = 3.5f; main.startSpeed = 4.5f; main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
                main.startColor = colours[k]; main.maxParticles = 60; main.gravityModifier = -0.08f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = 16f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.3f;
                sh.rotation = new Vector3(-90f, 0f, 0f);
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(colours[k], 0f), new GradientColorKey(Color.Lerp(colours[k], Color.white, 0.4f), 1f) },
                          new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var rend = go.GetComponent<ParticleSystemRenderer>();
                if (kit != null && kit.smoke != null) rend.sharedMaterial = kit.smoke;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                list[k] = ps;
            }
            return list;
        }

        // ------------------------------------------------------------------ crowd baking

        /// <summary>A handful of dressed, posed people, each frozen into a static mesh per material.</summary>
        void BakeCrowd()
        {
            var cat = DriverCatalog.Instance;
            if (cat == null) return;
            for (int v = 0; v < Variants; v++)
            {
                var body = v % 2 == 0 ? DriverBody.BodyA : DriverBody.BodyB;
                var prefab = cat.Body(body);
                if (prefab == null) continue;
                var holder = new GameObject("Bake"); holder.SetActive(false);
                var model = Instantiate(prefab, holder.transform);
                foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
                int hash = PortraitStudio.Hash("fan" + v + Random.Range(0, 9999));
                DriverLook.Apply(model, body, wornOf: s => PortraitStudio.Outfit(cat, body, s, hash));
                var anim = model.GetComponentInChildren<Animator>(true);
                if (anim != null) anim.enabled = false;
                holder.SetActive(true);
                Pose(model.transform, holder.transform, v % 3);
                // Baked relative to the holder, not the model: the model's root carries the import rotation that
                // stands the character up.
                var baked = Bake(model.transform, holder.transform);
                baked.name = "Fan " + v;
                baked.transform.SetParent(transform, false);
                crowdVariants.Add(baked);
                Destroy(holder);
            }
        }

        /// <summary>0 arms down, 1 one arm up, 2 both arms up (cheering).</summary>
        static void Pose(Transform root, Transform space, int pose)
        {
            Transform Find(string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            void Aim(string arm, string fore, Vector3 want)
            {
                var a = Find(arm); var f = Find(fore);
                if (a == null || f == null) return;
                Vector3 cur = (f.position - a.position).normalized;
                a.rotation = Quaternion.FromToRotation(cur, want.normalized) * a.rotation;
            }
            // The holder's axes, not the model's: the model root carries an import rotation.
            Vector3 up = space.up, left = -space.right, rightSide = space.right;
            Aim("Arm.L", "ForeArm.L", pose >= 1 ? up * 1f + left * 0.35f : -up + left * 0.18f);
            Aim("Arm.R", "ForeArm.R", pose >= 2 ? up * 1f + rightSide * 0.35f : -up + rightSide * 0.18f);
        }

        static GameObject Bake(Transform root, Transform space)
        {
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 toRoot = space.worldToLocalMatrix;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || smr.sharedMesh == null) continue;
                var m = new Mesh();
                smr.BakeMesh(m, true);
                var mats = smr.sharedMaterials;
                var xf = toRoot * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int s = 0; s < m.subMeshCount && s < mats.Length; s++) Add(byMat, mats[s], m, s, xf);
            }
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (!mr.enabled || mf == null || mf.sharedMesh == null) continue;
                var mats = mr.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++) Add(byMat, mats[s], mf.sharedMesh, s, toRoot * mr.transform.localToWorldMatrix);
            }
            var go = new GameObject("Fan");
            foreach (var kv in byMat)
            {
                if (kv.Key == null) continue;
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var part = new GameObject(kv.Key.name);
                part.transform.SetParent(go.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        static void Add(Dictionary<Material, List<CombineInstance>> by, Material mat, Mesh mesh, int sub, Matrix4x4 xf)
        {
            if (mat == null) return;
            if (!by.TryGetValue(mat, out var list)) { list = new List<CombineInstance>(); by[mat] = list; }
            list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = xf });
        }

        // ------------------------------------------------------------------ the crowd reacting

        void Update()
        {
            if (race == null) race = RaceManager.Current;
            var p = race != null ? race.Player : null;
            Vector3 car = p != null && p.vehicle != null ? p.vehicle.Transform.position : new Vector3(1e6f, 0f, 0f);
            float t = Time.time, dt = Time.deltaTime;
            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : car;
            foreach (var z in zones)
            {
                float d = (z.centre - car).magnitude;
                z.hype = Mathf.MoveTowards(z.hype, d < 90f ? 1f : 0.15f, dt * (d < 90f ? 3f : 0.4f));
                if (d < 70f && !z.fired) { z.fired = true; foreach (var f in z.flares) if (f != null) f.Play(); }
                if (d > 250f) z.fired = false;
                // Far from the camera: don't spend time bouncing people nobody can see.
                if ((z.centre - camPos).sqrMagnitude > 350f * 350f) continue;
                foreach (var fan in z.fans)
                {
                    float hop = Mathf.Max(0f, Mathf.Sin(t * (4f + z.hype * 5f) + fan.phase)) * fan.hop * (0.12f + z.hype * 0.55f);
                    fan.t.localPosition = fan.home + Vector3.up * hop;
                }
            }
        }

        /// <summary>A flag flapping.</summary>
        class Wave : MonoBehaviour
        {
            Quaternion home; float seed;
            void Start() { home = transform.localRotation; seed = Random.value * 10f; }
            void Update() { transform.localRotation = home * Quaternion.Euler(0f, Mathf.Sin(Time.time * 5f + seed) * 14f, Mathf.Sin(Time.time * 3.3f + seed) * 3f); }
        }
    }
}
