using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// The racing line of a circuit: its centreline (sampled from the road spline, starting at the start/finish
    /// line and running in race direction) and the gates a car must cross in order for a lap to count. Gate 0 is
    /// the start/finish line. Built by SHIFT RUSH → Tracks → Add Race Line (or when a track is rebuilt).
    /// </summary>
    public class RaceLine : MonoBehaviour
    {
        [System.Serializable]
        public struct Gate
        {
            public Vector3 position;
            public Vector3 forward;
            public float halfWidth;
        }

        [Tooltip("Laps in a race on this circuit.")]
        public int laps = 3;
        [Tooltip("Closed centreline in race direction; point 0 sits on the start/finish line.")]
        public Vector3[] points;
        [Tooltip("Distance along the line to each point (metres).")]
        public float[] distances;
        public float length;
        [Tooltip("Half the drivable width (asphalt + shoulders) in metres. Cars further out than this are off the track.")]
        public float halfWidth = 12f;
        [Tooltip("Checkpoints in order. Gate 0 is the start/finish line.")]
        public Gate[] gates;

        [Header("Minimap")]
        public Sprite mapSprite;
        [Tooltip("World XZ rectangle the map sprite covers.")]
        public Rect mapBounds;

        public static RaceLine Current { get; private set; }

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        /// <summary>Nearest point index to a position, searching near a hint first (cheap when called every frame).</summary>
        public int Nearest(Vector3 p, int hint = -1)
        {
            if (points == null || points.Length == 0) return 0;
            int n = points.Length, best = 0;
            float bestD = float.MaxValue;
            if (hint >= 0)
            {
                for (int k = -25; k <= 25; k++)
                {
                    int i = ((hint + k) % n + n) % n;
                    float d = (points[i] - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (bestD < 30f * 30f) return best;
            }
            for (int i = 0; i < n; i++)
            {
                float d = (points[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public Vector3 Direction(int i)
        {
            int n = points.Length;
            var d = points[(i + 1) % n] - points[(i - 1 + n) % n];
            d.y = 0f;
            return d.sqrMagnitude > 0f ? d.normalized : Vector3.forward;
        }

        /// <summary>Point on the line at a distance from the start (wraps round the lap).</summary>
        public Vector3 PointAt(float distance)
        {
            int n = points.Length;
            distance = Mathf.Repeat(distance, length);
            int a = IndexAt(distance), b = (a + 1) % n;
            float da = distances[a], db = b == 0 ? length : distances[b];
            return Vector3.Lerp(points[a], points[b], Mathf.InverseLerp(da, db, distance));
        }

        /// <summary>Index of the last point at or before a distance.</summary>
        public int IndexAt(float distance)
        {
            distance = Mathf.Repeat(distance, length);
            int lo = 0, hi = points.Length - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (distances[mid] <= distance) lo = mid; else hi = mid - 1; }
            return lo;
        }

        /// <summary>Signed sideways distance from the line at point i (positive = right of race direction).</summary>
        public float Lateral(Vector3 p, int i)
        {
            return Vector3.Dot(p - points[i], Vector3.Cross(Vector3.up, Direction(i)));
        }

        public Vector3 Right(int i) { return Vector3.Cross(Vector3.up, Direction(i)); }

        float[] cornerSpeed;

        /// <summary>
        /// Fastest sensible speed (m/s) at point i for a car with grip 1 (scale by sqrt(grip)): the corner limit
        /// v = sqrt(g·R), with braking for the corners ahead already worked in, so a driver only reads it here.
        /// </summary>
        public float CornerSpeed(int i)
        {
            if (cornerSpeed == null || cornerSpeed.Length != points.Length) cornerSpeed = BuildCornerSpeeds(points, true);
            return cornerSpeed[((i % points.Length) + points.Length) % points.Length];
        }

        /// <summary>Corner-limited speeds along a polyline (shared with shortcuts). Brakes at ~0.8 g.</summary>
        public static float[] BuildCornerSpeeds(Vector3[] pts, bool loop)
        {
            int n = pts.Length;
            var v = new float[n];
            const float g = 9.81f, cap = 120f, brake = 0.8f * g;
            for (int i = 0; i < n; i++)
            {
                int a = loop ? (i - 3 + n) % n : Mathf.Max(0, i - 3), b = loop ? (i + 3) % n : Mathf.Min(n - 1, i + 3);
                int a1 = loop ? (a + 1) % n : Mathf.Min(n - 1, a + 1), b1 = loop ? (b - 1 + n) % n : Mathf.Max(0, b - 1);
                Vector3 d0 = pts[a1] - pts[a], d1 = pts[b] - pts[b1];
                d0.y = 0f; d1.y = 0f;
                float ang = Vector3.Angle(d0, d1) * Mathf.Deg2Rad;
                float arc = 0f;
                for (int k = a; k != b; k = loop ? (k + 1) % n : k + 1) { int k1 = loop ? (k + 1) % n : k + 1; if (k1 >= n) break; arc += Vector3.Distance(pts[k], pts[k1]); }
                float kappa = ang / Mathf.Max(arc, 1f);
                v[i] = kappa < 1e-4f ? cap : Mathf.Min(cap, Mathf.Sqrt(g / kappa));
            }
            // Braking: can't arrive faster than we can slow down for what's next.
            for (int pass = 0; pass < (loop ? 2 : 1); pass++)
                for (int i = n - 2 + (loop ? 1 : 0); i >= 0; i--)
                {
                    int nx = (i + 1) % n;
                    float ds = Vector3.Distance(pts[i], pts[nx]);
                    v[i] = Mathf.Min(v[i], Mathf.Sqrt(v[nx] * v[nx] + 2f * brake * ds));
                }
            return v;
        }

        /// <summary>World XZ position → 0..1 on the minimap.</summary>
        public Vector2 MapUV(Vector3 p)
        {
            return new Vector2((p.x - mapBounds.xMin) / mapBounds.width, (p.z - mapBounds.yMin) / mapBounds.height);
        }

        void OnDrawGizmos()
        {
            if (points == null || points.Length < 2) return;
            Gizmos.color = new Color(1f, 0.25f, 0.85f, 0.9f);
            for (int i = 0; i < points.Length; i++)
                Gizmos.DrawLine(points[i] + Vector3.up * 0.3f, points[(i + 1) % points.Length] + Vector3.up * 0.3f);
            if (gates == null) return;
            for (int g = 0; g < gates.Length; g++)
            {
                var gate = gates[g];
                var right = Vector3.Cross(Vector3.up, gate.forward).normalized * gate.halfWidth;
                Gizmos.color = g == 0 ? Color.white : new Color(0.2f, 0.9f, 1f, 0.9f);
                var a = gate.position - right; var b = gate.position + right;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(a + Vector3.up * 3f, b + Vector3.up * 3f);
                Gizmos.DrawLine(a, a + Vector3.up * 3f);
                Gizmos.DrawLine(b, b + Vector3.up * 3f);
            }
        }
    }
}
