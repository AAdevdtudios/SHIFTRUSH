using System.Collections.Generic;
using ShiftRush.Audio;
using ShiftRush.Core;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// Race furniture laid along the circuit at load, from the Street Racer kit:
    /// - Checkpoint arches: a truss arch over the road at the start line (FINISH towards you, START behind) and at
    ///   each quarter of the lap, CHECKPOINT boards hanging from it and flags at its feet. Driving through one fires
    ///   confetti from both feet and a chime.
    /// - Corner kit at the tightest corners: chevron boards along the outside, tyre stacks and crash barrels on the
    ///   inside, and the kit's animated direction arrows pointing into the turn.
    /// Everything sits outside the invisible walls (or high over the road), so it never needs a collider: colliders
    /// are stripped, nothing here costs physics or can stop a car. The kit's effects were made for the old built-in
    /// pipeline; <see cref="Fx"/> re-skins them with the URP additive material so they render.
    /// </summary>
    public class TrackDressing : MonoBehaviour
    {
        class Arch { public float d; public Transform root; public List<Transform> feet = new List<Transform>(); public bool start; }

        readonly List<Arch> arches = new List<Arch>();
        SpectacleKit kit;
        RaceLine line;
        RaceManager race;
        int hint = -1;
        float lastD = -1f;

        public static TrackDressing Build(RaceLine line, SpectacleKit kit, Transform parent)
        {
            var go = new GameObject("Track Dressing");
            go.transform.SetParent(parent, false);
            var td = go.AddComponent<TrackDressing>();
            td.kit = kit; td.line = line;
            td.Make();
            return td;
        }

        float Half { get { return line.halfWidth > 0f ? line.halfWidth : 9f; } }

        void Make()
        {
            if (line == null || line.points == null || line.points.Length < 20 || kit == null) return;
            if (kit.checkpointArch != null)
            {
                MakeArch(0f, true);
                for (int q = 1; q < 4; q++) MakeArch(line.length * q / 4f, false);
            }
            Corners();
        }

        // ------------------------------------------------------------------ arches

        void MakeArch(float d, bool start)
        {
            int i = line.IndexAt(d);
            Vector3 p = line.PointAt(d), dir = Vector3.ProjectOnPlane(line.Direction(i), Vector3.up).normalized;
            float scale = (Half * 2f + 6f) / 12f; // the truss is 12 m wide
            var root = new GameObject(start ? "Start Arch" : "Checkpoint Arch").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(p, Quaternion.LookRotation(dir, Vector3.up));
            var arch = Place(kit.checkpointArch, root, Vector3.zero, Quaternion.identity, scale);
            float top = 8f * scale;
            // Boards hanging from the top: the one you drive towards, and one for the way back.
            var front = start ? kit.finishSign : kit.checkpointSign;
            var back = start ? kit.startSign : kit.checkpointSign;
            // The boards' printed face is their +z side: turned round to face the cars driving at them.
            if (front != null) Place(front, root, new Vector3(0f, top * 0.72f, -0.4f), Quaternion.Euler(0f, 180f, 0f), 3.6f);
            if (back != null) Place(back, root, new Vector3(0f, top * 0.72f, 0.4f), Quaternion.identity, 3.6f);
            var a = new Arch { d = d, root = root, start = start };
            for (int s = -1; s <= 1; s += 2)
            {
                var foot = new GameObject("Foot").transform;
                foot.SetParent(root, false);
                foot.localPosition = new Vector3(s * (Half + 2.4f), 0f, 0f);
                a.feet.Add(foot);
                if (kit.checkpointFlag != null) Place(kit.checkpointFlag, root, new Vector3(s * (Half + 4.2f), 0f, 0f), Quaternion.identity, 2f);
            }
            arches.Add(a);
            _ = arch;
        }

        void Update()
        {
            if (race == null) race = RaceManager.Current;
            var p = race != null ? race.Player : null;
            if (p == null || p.vehicle == null || arches.Count == 0) return;
            hint = line.Nearest(p.vehicle.Transform.position, hint);
            float d = line.distances[hint];
            if (lastD >= 0f)
            {
                // Crossed an arch going forwards (including the start line, where the lap wraps).
                foreach (var a in arches)
                {
                    bool crossed = lastD < a.d && d >= a.d && d - lastD < 60f;
                    bool wrapped = a.d == 0f && lastD > line.length - 60f && d < 60f;
                    if (crossed || wrapped) Pass(a);
                }
            }
            lastD = d;
        }

        void Pass(Arch a)
        {
            if (kit.confettiFx != null)
                foreach (var f in a.feet) Fx.Burst(kit.confettiFx, kit.additive, f.position + Vector3.up * 1.5f, Quaternion.LookRotation(Vector3.up), 4f);
            GameSfx.Play(Sfx.Overtake, a.start ? 1.25f : 1.1f, 0.8f);
        }

        // ------------------------------------------------------------------ corners

        void Corners()
        {
            int n = line.points.Length;
            var idx = new List<int>();
            for (int i = 0; i < n; i += 3) idx.Add(i);
            idx.Sort((x, y) => line.CornerSpeed(x).CompareTo(line.CornerSpeed(y)));
            var picked = new List<int>();
            foreach (var i in idx)
            {
                if (line.CornerSpeed(i) > 45f) break; // only real corners
                bool ok = true;
                foreach (var p in picked) { float dd = Mathf.Abs(line.distances[i] - line.distances[p]); if (Mathf.Min(dd, line.length - dd) < 220f) { ok = false; break; } }
                foreach (var a in arches) { float dd = Mathf.Abs(line.distances[i] - a.d); if (Mathf.Min(dd, line.length - dd) < 60f) { ok = false; break; } }
                if (!ok) continue;
                picked.Add(i);
                Corner(i);
                if (picked.Count >= 6) break;
            }
        }

        void Corner(int apex)
        {
            int n = line.points.Length;
            float turn = Vector3.SignedAngle(line.Direction((apex - 6 + n) % n), line.Direction((apex + 6) % n), Vector3.up);
            float outside = turn > 0f ? -1f : 1f;
            float apexD = line.distances[apex];
            // Chevrons along the outside, from a little before the apex, each facing the cars coming at it.
            if (kit.chevronBoard != null)
                for (int k = -2; k <= 2; k++)
                {
                    float d = Mathf.Repeat(apexD + k * 9f, line.length);
                    int i = line.IndexAt(d);
                    Vector3 dir = line.Direction(i);
                    Vector3 pos = line.PointAt(d) + line.Right(i) * outside * (Half + 2.6f) + Vector3.up * 0.2f;
                    var face = Quaternion.LookRotation(-Vector3.ProjectOnPlane(dir, Vector3.up).normalized, Vector3.up);
                    // Mirror the arrows for left-hand turns.
                    var board = Place(kit.chevronBoard, transform, pos, face, 2.2f, true);
                    if (outside > 0f) board.localScale = new Vector3(-board.localScale.x, board.localScale.y, board.localScale.z);
                }
            // Tyre stacks and crash barrels on the inside of the apex.
            for (int k = -1; k <= 1; k++)
            {
                var prefab = k == 0 ? kit.crashBarrel : kit.tyreStack;
                if (prefab == null) continue;
                float d = Mathf.Repeat(apexD + k * 3.5f, line.length);
                int i = line.IndexAt(d);
                Vector3 pos = line.PointAt(d) + line.Right(i) * -outside * (Half + 2.4f);
                Place(prefab, transform, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), 1.4f, true);
            }
            // The animated arrows, over the outside edge at the corner's entry, pointing through the turn.
            if (kit.arrowsFx != null)
            {
                float d = Mathf.Repeat(apexD - 25f, line.length);
                int i = line.IndexAt(d);
                Vector3 pos = line.PointAt(d) + line.Right(i) * outside * (Half + 1.5f) + Vector3.up * 2.2f;
                var fx = Fx.Spawn(kit.arrowsFx, kit.additive, transform);
                fx.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.ProjectOnPlane(line.Direction(apex), Vector3.up).normalized, Vector3.up));
                fx.localScale = Vector3.one * 2f;
            }
        }

        // ------------------------------------------------------------------ helpers

        static Transform Place(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot, float scale, bool world = false)
        {
            var go = Instantiate(prefab, parent);
            if (world) go.transform.SetPositionAndRotation(pos, rot);
            else { go.transform.localPosition = pos; go.transform.localRotation = rot; }
            go.transform.localScale = Vector3.one * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic = false;
            return go.transform;
        }
    }

    /// <summary>
    /// The Street Racer and Particle FX effects use the old pipeline's particle shaders, which don't draw in URP.
    /// Spawning through here swaps each one for a copy of the URP additive material carrying the same texture
    /// and tint (cached per source material).
    /// </summary>
    public static class Fx
    {
        static readonly Dictionary<Material, Material> swapped = new Dictionary<Material, Material>();

        public static Transform Spawn(GameObject prefab, Material additive, Transform parent)
        {
            var go = Object.Instantiate(prefab, parent);
            Convert(go, additive);
            return go.transform;
        }

        /// <summary>A one-shot effect at a spot, cleaned up after <paramref name="life"/> seconds.</summary>
        public static void Burst(GameObject prefab, Material additive, Vector3 pos, Quaternion rot, float life)
        {
            var go = Object.Instantiate(prefab, pos, rot);
            Convert(go, additive);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            Object.Destroy(go, life);
        }

        public static void Convert(GameObject go, Material additive)
        {
            if (additive == null) return;
            foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var src = r.sharedMaterial;
                if (src == null || src.shader == null || src.shader.name.StartsWith("Universal")) continue;
                if (!swapped.TryGetValue(src, out var m) || m == null)
                {
                    m = new Material(additive) { name = src.name + " (URP)" };
                    var tex = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
                    if (tex != null) m.SetTexture("_BaseMap", tex);
                    Color tint = src.HasProperty("_TintColor") ? src.GetColor("_TintColor") * 2f : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
                    tint.a = Mathf.Clamp01(tint.a);
                    m.SetColor("_BaseColor", tint);
                    swapped[src] = m;
                }
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
