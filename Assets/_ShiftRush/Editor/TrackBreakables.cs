using System.Collections.Generic;
using ShiftRush.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Tracks → Make Everything Breakable: on the open track, every solid thing near the circuit that
    /// isn't a house or part of the track itself (road, terrain, bridges, tunnels, walls) becomes a BreakableProp:
    /// cars go through it and it goes flying. Trees, rocks, signs, lamp posts, containers, fences, crates, all of it.
    /// Heavier things get more mass, so a lamp post swings and a cone flies. Run on each track; safe to rerun.
    /// </summary>
    public static class TrackBreakables
    {
        const float Reach = 70f; // metres from the race line

        // Never breakable: buildings, and the pieces the circuit is made of.
        static readonly string[] Keep =
        {
            "Building", "House", "Hotel", "Office", "Skyscraper", "Church", "Mosque", "Factory", "Warehouse", "Hospital",
            "Road", "Spline", "Terrain", "Wall", "Bridge", "Gallery", "Tunnel", "Ground", "Quay", "Deck", "Pillar",
            "Parapet", "Gantry", "Rail", "Guard", "Kerb", "Floor_", "Plaza", "Roof", "Port_04", "Lawn", "Sea", "Water",
            "StartLine", "Chequer", "Start Grid", "Race Line", "Heavy Lifter", "Hangar", "Cabin", "Barn", "Lookout",
        };

        [MenuItem("SHIFT RUSH/Tracks/Make Everything Breakable")]
        public static string Run()
        {
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (line == null || line.points == null) return "No race line in this scene";
            var done = new HashSet<GameObject>();
            int made = 0, kept = 0;
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col == null) continue; // removed while converting an earlier prop
                if (col.isTrigger || col is TerrainCollider || col is WheelCollider) continue;
                if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue; // cars, live props
                var go = PrefabUtility.GetOutermostPrefabInstanceRoot(col.gameObject) ?? col.gameObject;
                if (done.Contains(go)) continue;
                done.Add(go);
                if (go.GetComponent<BreakableProp>() != null) continue;
                if (go.GetComponentInParent<IVehicleMarker>() != null) continue;
                if (Protected(go.transform)) { kept++; continue; }
                var b = Bounds(go);
                if (Far(line, b)) continue;
                // Something the size of a house is a building whatever it's called.
                if (b.size.x * b.size.z > 140f && b.size.y > 6f) { kept++; continue; }
                Make(go, b);
                made++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            string report = "Breakable: " + made + " made, " + kept + " kept solid";
            Debug.Log("[TRACK] " + report);
            return report;
        }

        interface IVehicleMarker { }

        static bool Protected(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.GetComponent<Vehicles.IVehicle>() != null) return true;
                foreach (var k in Keep) if (p.name.Contains(k)) return true;
            }
            return false;
        }

        static bool Far(RaceLine line, Bounds b)
        {
            var c = b.center;
            int i = line.Nearest(c);
            var flat = line.points[i] - c; flat.y = 0;
            return flat.magnitude - Mathf.Max(b.extents.x, b.extents.z) > Reach;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Make(GameObject go, Bounds b)
        {
            // One box on the root (BreakableProp turns its own colliders into triggers, then physical).
            if (go.GetComponent<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                var sz = go.transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                foreach (var c in go.GetComponentsInChildren<Collider>()) if (c != box) Object.DestroyImmediate(c);
            }
            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var prop = go.AddComponent<BreakableProp>();
            float volume = b.size.x * b.size.y * b.size.z;
            var so = new SerializedObject(prop);
            so.FindProperty("mass").floatValue = Mathf.Clamp(volume * 6f, 1f, 80f);
            so.FindProperty("breakSpeed").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Static batching would freeze it in place: let it move.
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            foreach (Transform t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
    }
}
