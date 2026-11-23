using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// One per race track scene: the track's name, menu text and race settings, kept with the scene so the
    /// Race Track Wizard (SHIFT RUSH → Tracks → Race Track Wizard) can rebuild everything from it at any time.
    /// The route is either the scene's road spline or the "Route" points placed by hand (children of this object,
    /// in driving order, as a closed loop). Draws the route, the race line and its checkpoints in the Scene view.
    /// </summary>
    public class TrackInfo : MonoBehaviour
    {
        public enum RouteSource { RoadSpline, RoutePoints }

        [Header("Menu")]
        public string displayName = "New Track";
        [Tooltip("Short unique id for saves and the catalog, lowercase.")]
        public string id = "newtrack";
        [TextArea] public string description = "";
        public string[] loadingTips = new string[0];

        [Header("Race")]
        [Range(1, 10)] public int laps = 3;
        [Tooltip("Half the drivable width in metres (road plus shoulders). Further out counts as off the track.")]
        public float halfWidth = 12f;
        [Tooltip("Metres between checkpoints. Every checkpoint must be passed, in order, for a lap to count.")]
        public float checkpointSpacing = 120f;

        [Header("Route")]
        public RouteSource route = RouteSource.RoutePoints;
        [Tooltip("How round the hand-placed route is between points (0 = straight lines, 1 = smooth curves).")]
        [Range(0f, 1f)] public float smoothing = 1f;
        [Tooltip("Snap the line onto the ground below each point.")]
        public bool snapToGround = true;

        [Header("Extras")]
        public bool trackWalls = true;
        public bool shiftRamps = false;

        /// <summary>The hand-placed route points, in order.</summary>
        public Transform RouteRoot
        {
            get
            {
                var t = transform.Find("Route");
                return t;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // Hand-placed route: numbered spheres joined in a loop.
            var root = RouteRoot;
            if (root != null && route == RouteSource.RoutePoints && root.childCount > 0)
            {
                Gizmos.color = new Color(1f, 0.18f, 0.6f, 0.9f);
                for (int i = 0; i < root.childCount; i++)
                {
                    var a = root.GetChild(i).position;
                    var b = root.GetChild((i + 1) % root.childCount).position;
                    Gizmos.DrawSphere(a, i == 0 ? 2.2f : 1.4f);
                    Gizmos.DrawLine(a + Vector3.up * 0.5f, b + Vector3.up * 0.5f);
                    UnityEditor.Handles.Label(a + Vector3.up * 3f, i == 0 ? "START" : (i + 1).ToString());
                }
            }

            // The built race line and its checkpoints.
            var line = FindFirstObjectByType<RaceLine>();
            if (line == null || line.points == null || line.points.Length < 2) return;
            Gizmos.color = new Color(0.18f, 0.95f, 1f, 0.8f);
            for (int i = 0; i < line.points.Length; i++)
                Gizmos.DrawLine(line.points[i] + Vector3.up * 0.3f, line.points[(i + 1) % line.points.Length] + Vector3.up * 0.3f);
            if (line.gates == null) return;
            for (int g = 0; g < line.gates.Length; g++)
            {
                var gate = line.gates[g];
                var right = Vector3.Cross(Vector3.up, gate.forward).normalized * gate.halfWidth;
                Gizmos.color = g == 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 0.77f, 0.24f, 0.85f);
                Gizmos.DrawLine(gate.position - right + Vector3.up * 0.5f, gate.position + right + Vector3.up * 0.5f);
                Gizmos.DrawLine(gate.position - right, gate.position - right + Vector3.up * 3f);
                Gizmos.DrawLine(gate.position + right, gate.position + right + Vector3.up * 3f);
            }
        }
#endif
    }
}
