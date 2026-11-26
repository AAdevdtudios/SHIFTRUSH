using ShiftRush.Core;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// A driving line painted on the road ahead of the player: a ribbon of glowing chevrons fixed to the tarmac
    /// (so they stream towards you as you drive), starting from where you are and easing onto the race line.
    /// Each stretch is coloured by what it asks of you at your current speed: cyan flat out, amber getting quick
    /// for the corner coming, magenta brake. Off with Settings (SR_DrivingLine).
    /// One small mesh rebuilt each frame (about 60 quads), one draw call, no lights, no physics.
    /// </summary>
    public class DrivingLine : MonoBehaviour
    {
        const float Ahead = 140f, Step = 2.4f, Width = 0.85f, StartAhead = 7f, EaseIn = 45f, Lift = 0.14f;
        static readonly Color Go = new Color32(46, 242, 255, 255);
        static readonly Color Ease = new Color32(255, 196, 46, 255);
        static readonly Color Brake = new Color32(255, 46, 154, 255);

        RaceLine line;
        RaceManager race;
        Mesh mesh;
        MeshRenderer rend;
        int hint = -1;
        Vector3[] verts; Color[] cols; Vector2[] uvs; int[] tris;

        public static DrivingLine Build(RaceLine line, SpectacleKit kit, Transform parent)
        {
            if (kit == null || kit.additive == null) return null;
            var go = new GameObject("Driving Line");
            go.transform.SetParent(parent, false);
            var dl = go.AddComponent<DrivingLine>();
            dl.line = line;
            dl.mesh = new Mesh { name = "Driving Line" };
            dl.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = dl.mesh;
            dl.rend = go.AddComponent<MeshRenderer>();
            var mat = new Material(kit.additive) { name = "Driving Line" };
            mat.SetTexture("_BaseMap", Chevron());
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f));
            dl.rend.sharedMaterial = mat;
            dl.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dl.rend.receiveShadows = false;
            int n = Mathf.CeilToInt(Ahead / Step) + 1;
            dl.verts = new Vector3[n * 2]; dl.cols = new Color[n * 2]; dl.uvs = new Vector2[n * 2];
            dl.tris = new int[(n - 1) * 6];
            for (int k = 0; k < n - 1; k++)
            {
                int v = k * 2, t = k * 6;
                dl.tris[t] = v; dl.tris[t + 1] = v + 2; dl.tris[t + 2] = v + 1;
                dl.tris[t + 3] = v + 1; dl.tris[t + 4] = v + 2; dl.tris[t + 5] = v + 3;
            }
            return dl;
        }

        void LateUpdate()
        {
            if (race == null) race = RaceManager.Current;
            var p = race != null ? race.Player : null;
            bool show = GameSettings.DrivingLine && p != null && p.vehicle != null && !p.finished && race.CurrentState == RaceManager.State.Racing;
            rend.enabled = show;
            if (!show) return;

            var car = p.vehicle.Transform;
            hint = line.Nearest(car.position, hint);
            float d0 = line.distances[hint] + StartAhead;
            float lat0 = line.Lateral(car.position, hint);
            float speed = Mathf.Abs(p.vehicle.SpeedKmh) / 3.6f;
            int n = verts.Length / 2;
            var bounds = new Bounds(car.position, Vector3.one);
            for (int k = 0; k < n; k++)
            {
                float along = k * Step;
                float d = Mathf.Repeat(d0 + along, line.length);
                int i = line.IndexAt(d);
                Vector3 right = line.Right(i);
                // Starts under the car's own lane, eases onto the race line.
                float lat = lat0 * (1f - Mathf.SmoothStep(0f, 1f, along / EaseIn));
                Vector3 c = line.PointAt(d) + right * lat + Vector3.up * Lift;
                verts[k * 2] = c - right * Width; verts[k * 2 + 1] = c + right * Width;
                // What this stretch asks of you, looking a little further ahead to the corner it feeds.
                float limit = Mathf.Min(line.CornerSpeed(i), line.CornerSpeed(line.IndexAt(Mathf.Repeat(d + 25f, line.length))));
                float over = speed / Mathf.Max(5f, limit);
                Color col = over > 1.08f ? Brake : over > 0.95f ? Color.Lerp(Ease, Brake, Mathf.InverseLerp(0.95f, 1.08f, over)) : over > 0.85f ? Color.Lerp(Go, Ease, Mathf.InverseLerp(0.85f, 0.95f, over)) : Go;
                // Fade in from the car and out at the far end.
                col.a = Mathf.Clamp01(along / 10f) * Mathf.Clamp01((Ahead - along) / 35f);
                cols[k * 2] = cols[k * 2 + 1] = col;
                // Chevrons fixed to the road: v by distance round the lap.
                float v = d0 / Step + k;
                uvs[k * 2] = new Vector2(0f, v); uvs[k * 2 + 1] = new Vector2(1f, v);
                bounds.Encapsulate(c);
            }
            mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs;
            if (mesh.GetIndexCount(0) == 0) mesh.triangles = tris;
            bounds.Expand(4f);
            mesh.bounds = bounds;
        }

        /// <summary>One chevron pointing up the texture (forward along the road), soft edged, tiling along v.</summary>
        static Texture2D Chevron()
        {
            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f;   // -1..1 across
                    float v = (y + 0.5f) / H;              // 0..1 along
                    // A chevron pointing forward (up the road): the stroke's tip leads at the centre, the arms trail
                    // back towards the edges.
                    float centre = 0.72f - Mathf.Abs(u) * 0.45f;
                    float dist = Mathf.Abs(v - centre);
                    float a = Mathf.Clamp01((0.11f - dist) / 0.04f) * Mathf.Clamp01((0.98f - Mathf.Abs(u)) / 0.08f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(true);
            return tex;
        }
    }
}
