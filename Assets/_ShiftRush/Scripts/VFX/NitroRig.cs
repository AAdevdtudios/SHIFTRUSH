using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// Nitro Bench only (SHIFT RUSH → Cars → Nitro Bench): sits on a car laid out in the bench scene. Each child
    /// named "Exhaust ..." is one nitro point; this keeps a live preview of the car's nitro effect on each so you can
    /// drag them into place, then "Save to Catalog" writes the points, angle and size back to the RaceCatalog.
    /// Does nothing in play mode or in a build.
    /// </summary>
    [ExecuteAlways]
    public class NitroRig : MonoBehaviour
    {
        public string carId;
        [Tooltip("The effect at each exhaust while nitro burns.")]
        public GameObject nitroVfx;
        [Tooltip("Turns the effect at each exhaust, in degrees, after it is pointed straight back.")]
        public Vector3 nitroRotation;
        [Range(0.05f, 3f)] public float nitroScale = 1f;
        [Tooltip("Children of the effect to hide, by name.")]
        public string[] nitroHide = { "SpaceshipMesh" };

#if UNITY_EDITOR
        GameObject shownVfx;
        Vector3 shownRotation;
        float shownScale;

        void OnEnable() { Refresh(true); }

        void Update()
        {
            if (Application.isPlaying) return;
            bool changed = shownVfx != nitroVfx || shownRotation != nitroRotation || !Mathf.Approximately(shownScale, nitroScale);
            Refresh(changed);
        }

        /// <summary>Keeps one preview of the effect on every exhaust marker (rebuilt when the effect, angle or size changes).</summary>
        public void Refresh(bool rebuild)
        {
            if (Application.isPlaying) return;
            foreach (Transform marker in transform)
            {
                if (!marker.name.StartsWith("Exhaust")) continue;
                marker.localRotation = Quaternion.identity; // the angle lives on the rig, the same for every exhaust
                var preview = marker.Find("Preview");
                if (preview != null && !rebuild) continue;
                if (preview != null) DestroyImmediate(preview.gameObject);
                if (nitroVfx == null) continue;
                var g = Instantiate(nitroVfx, marker);
                g.name = "Preview";
                g.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                g.transform.localPosition = Vector3.zero;
                g.transform.localRotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(nitroRotation);
                g.transform.localScale = Vector3.one * nitroScale;
                foreach (var t in g.GetComponentsInChildren<Transform>(true))
                {
                    t.hideFlags = HideFlags.DontSave | HideFlags.NotEditable | HideFlags.HideInHierarchy;
                    if (nitroHide != null && System.Array.IndexOf(nitroHide, t.name) >= 0) t.gameObject.SetActive(false);
                }
                g.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.transform.parent == g.transform || ps.transform == g.transform) ps.Simulate(0.6f, true, true); // a frozen frame you can see
            }
            shownVfx = nitroVfx; shownRotation = nitroRotation; shownScale = nitroScale;
        }

        void OnDrawGizmos()
        {
            foreach (Transform marker in transform)
            {
                if (!marker.name.StartsWith("Exhaust")) continue;
                Gizmos.color = new Color(1f, 0.25f, 0.85f, 1f);
                Gizmos.DrawWireSphere(marker.position, 0.08f);
                Gizmos.DrawLine(marker.position, marker.position - transform.forward * 0.6f);
            }
        }
#endif
    }
}
