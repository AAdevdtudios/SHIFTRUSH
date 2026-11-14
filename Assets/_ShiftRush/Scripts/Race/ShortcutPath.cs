using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// A small cut-through off the main road: a centreline from where it leaves the circuit to where it rejoins.
    /// The track builder makes these (dirt lanes across the inside of tight bends); rivals take them sometimes,
    /// and the track limits let cars use them. Checkpoints are never placed on the stretch a shortcut skips.
    /// </summary>
    public class ShortcutPath : MonoBehaviour
    {
        [Tooltip("Centreline from entry (on the main road) to exit (back on the main road).")]
        public Vector3[] points;
        [Tooltip("Half the drivable width of the lane, metres.")]
        public float halfWidth = 4.5f;
        [Tooltip("Race-line distance where the shortcut leaves and where it rejoins.")]
        public float entryDistance, exitDistance;

        public static readonly List<ShortcutPath> All = new List<ShortcutPath>();

        float[] speeds;
        float entrySpeed;

        /// <summary>Slowest speed (m/s, grip 1) needed through the turn-in off the main road.</summary>
        public float EntrySpeed { get { CornerSpeed(0); return entrySpeed; } }

        /// <summary>Which side of the road the lane leaves from: +1 right, -1 left.</summary>
        public float Side
        {
            get
            {
                var line = RaceLine.Current;
                if (line == null || points.Length < 3) return 0f;
                int i = line.Nearest(points[0]);
                return Mathf.Sign(line.Lateral(points[Mathf.Min(6, points.Length - 1)], i));
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Corner-limited speeds (m/s, grip 1) along the lane.</summary>
        public float CornerSpeed(int i)
        {
            if (speeds == null)
            {
                // Include the turn off the road and back onto it: a little of the race line either side.
                var line = RaceLine.Current;
                var ext = new List<Vector3>();
                if (line != null)
                {
                    for (int k = 4; k >= 1; k--) ext.Add(line.PointAt(entryDistance - k * 4f));
                }
                int lead = ext.Count;
                ext.AddRange(points);
                if (line != null) for (int k = 1; k <= 4; k++) ext.Add(line.PointAt(exitDistance + k * 4f));
                var all = RaceLine.BuildCornerSpeeds(ext.ToArray(), false);
                speeds = new float[points.Length];
                for (int k = 0; k < points.Length; k++) speeds[k] = all[k + lead];
                entrySpeed = float.MaxValue;
                for (int k = 0; k <= lead + 4 && k < all.Length; k++) entrySpeed = Mathf.Min(entrySpeed, all[k]);
            }
            return speeds[Mathf.Clamp(i, 0, points.Length - 1)];
        }

        /// <summary>XZ distance to the centreline; `index` is the nearer end of the closest segment, `along` the
        /// distance from the entry to the closest point.</summary>
        public float DistanceTo(Vector3 p, out int index, out float along)
        {
            index = 0; along = 0f;
            if (points == null || points.Length < 2) return float.MaxValue;
            float best = float.MaxValue, run = 0f;
            var p2 = new Vector2(p.x, p.z);
            for (int i = 0; i < points.Length - 1; i++)
            {
                var a = new Vector2(points[i].x, points[i].z); var b = new Vector2(points[i + 1].x, points[i + 1].z);
                var ab = b - a; float len = ab.magnitude;
                float k = Mathf.Clamp01(Vector2.Dot(p2 - a, ab) / Mathf.Max(len * len, 1e-4f));
                float d = (p2 - (a + ab * k)).sqrMagnitude;
                if (d < best) { best = d; index = k < 0.5f ? i : i + 1; along = run + len * k; }
                run += len;
            }
            return Mathf.Sqrt(best);
        }

        public float DistanceTo(Vector3 p) { int i; float a; return DistanceTo(p, out i, out a); }

        /// <summary>Point a given distance along the lane from its entry (clamped to the exit).</summary>
        public Vector3 PointAlong(float along)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                float len = Vector3.Distance(points[i], points[i + 1]);
                if (along <= len) return Vector3.Lerp(points[i], points[i + 1], along / Mathf.Max(len, 1e-4f));
                along -= len;
            }
            return points[points.Length - 1];
        }

        public float Length
        {
            get { float l = 0f; for (int i = 0; i < points.Length - 1; i++) l += Vector3.Distance(points[i], points[i + 1]); return l; }
        }

        /// <summary>True when a point is on any shortcut lane (with a little slack for the verges).</summary>
        public static bool OnAny(Vector3 p, float slack = 2f)
        {
            foreach (var s in All) if (s.DistanceTo(p) < s.halfWidth + slack) return true;
            return false;
        }

        void OnDrawGizmos()
        {
            if (points == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            for (int i = 0; i < points.Length - 1; i++) Gizmos.DrawLine(points[i] + Vector3.up * 0.4f, points[i + 1] + Vector3.up * 0.4f);
        }
    }
}
