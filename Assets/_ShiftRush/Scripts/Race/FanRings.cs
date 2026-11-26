using System.Collections.Generic;
using ShiftRush.Audio;
using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// A fan dare on the road: a line of glowing magenta rings laid out ahead of the player on the race line, weaving
    /// across the lanes. Drive through one and it flashes white, chimes and pays a sip of nitro. No colliders: a ring
    /// never stops a car. Spawned by <see cref="StreamHud"/> for a "THREAD THE RINGS" dare and cleared when it ends.
    /// </summary>
    public class FanRings : MonoBehaviour
    {
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);
        const float Radius = 3f;
        const float Tube = 0.22f;
        const float NitroPerRing = 0.05f;

        class Ring { public Transform t; public Renderer r; public bool passed; public float side = 1f; public float flash; }

        readonly List<Ring> rings = new List<Ring>();
        Transform player;
        IVehicle vehicle;
        Material mat;
        static Mesh torus;

        public int Count { get { return rings.Count; } }
        public int Passed { get; private set; }
        /// <summary>The player has driven past the last ring (through it or not).</summary>
        public bool Behind { get; private set; }
        /// <summary>Rings already behind the player that it didn't go through.</summary>
        public int Missed { get; private set; }
        public event System.Action RingPassed;

        /// <summary>Lays <paramref name="count"/> rings from <paramref name="ahead"/> metres in front of the player, every <paramref name="gap"/> metres.</summary>
        public static FanRings Spawn(RaceLine line, IVehicle player, int count, float ahead = 140f, float gap = 75f)
        {
            if (line == null || player == null) return null;
            var go = new GameObject("Fan Rings");
            var fr = go.AddComponent<FanRings>();
            fr.Build(line, player, count, ahead, gap);
            return fr;
        }

        void Build(RaceLine line, IVehicle v, int count, float ahead, float gap)
        {
            vehicle = v; player = v.Transform;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(shader) { color = Magenta };
            if (torus == null) torus = MakeTorus(Radius, Tube, 48, 10);
            int i0 = line.Nearest(player.position);
            float d0 = line.distances[i0];
            float half = Mathf.Max(2f, line.halfWidth - Radius * 0.6f);
            float[] weave = { 0f, 0.7f, -0.7f, 0.35f, -0.35f, 0.8f, -0.8f };
            for (int n = 0; n < count; n++)
            {
                float d = Mathf.Repeat(d0 + ahead + n * gap, line.length);
                int k = line.IndexAt(d);
                Vector3 dir = line.Direction(k), right = line.Right(k);
                Vector3 p = line.PointAt(d) + right * (weave[n % weave.Length] * half);
                // Sit on the road surface (the highest solid, non-moving hit near the line's height).
                float ground = float.MinValue;
                foreach (var hit in Physics.RaycastAll(p + Vector3.up * 6f, Vector3.down, 16f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.rigidbody == null && !hit.collider.isTrigger && hit.point.y > ground) ground = hit.point.y;
                p.y = (ground > float.MinValue ? ground : p.y) + Radius;

                var go = new GameObject("Ring " + (n + 1));
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir, Vector3.up));
                go.AddComponent<MeshFilter>().sharedMesh = torus;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ring = new Ring { t = go.transform, r = r };
                ring.side = Mathf.Sign(go.transform.InverseTransformPoint(player.position).z);
                rings.Add(ring);
            }
        }

        void Update()
        {
            if (player == null) return;
            Vector3 pc = player.position + player.up * 0.7f;
            float t = Time.time;
            bool allBehind = true;
            int missed = 0;
            for (int i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                Vector3 local = ring.t.InverseTransformPoint(pc);
                float side = Mathf.Sign(local.z);
                if (!ring.passed && side != ring.side && ring.side < 0f && Mathf.Abs(local.z) < 6f)
                {
                    // Crossed the ring's plane going forwards: inside the hoop counts.
                    if (new Vector2(local.x, local.y).magnitude < Radius - 0.2f) Pass(ring);
                }
                ring.side = side;
                if (side < 0f) allBehind = false;
                else if (!ring.passed && local.z > 4f) missed++;

                // Breathe; passed rings flash white and stay pale cyan.
                float s = 1f + Mathf.Sin(t * 4f + i) * 0.03f;
                ring.flash = Mathf.MoveTowards(ring.flash, 0f, Time.deltaTime * 2f);
                ring.t.localScale = Vector3.one * (s + ring.flash * 0.25f);
                if (ring.passed) ring.r.material.color = Color.Lerp(new Color(Cyan.r, Cyan.g, Cyan.b, 1f), Color.white, ring.flash);
            }
            Behind = allBehind && rings.Count > 0;
            Missed = missed;
        }

        void Pass(Ring ring)
        {
            ring.passed = true;
            ring.flash = 1f;
            ring.r.material = new Material(mat) { color = Color.white };
            Passed++;
            if (vehicle is INitroTank tank && tank.Nitro01 >= 0f) tank.AddNitro(NitroPerRing * Core.Difficulty.Current.bonusNitro);
            GameSfx.Play(Sfx.RingPass, 1f + 0.08f * Passed, 0.9f);
            RingPassed?.Invoke();
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }

        static Mesh MakeTorus(float radius, float tube, int seg, int sides)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b));
                    verts.Add(c + n * tube);
                }
            }
            int row = sides + 1;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1);
                    tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
            var m = new Mesh { name = "Fan Ring" };
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
