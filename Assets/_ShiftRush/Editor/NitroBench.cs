using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Cars → Nitro Bench: a new empty scene with every catalog car in a row, seen from behind, each
    /// with its nitro effect showing at its exhausts. Move the "Exhaust" markers (or add/remove them), set the
    /// effect, angle and size on the car's Nitro Rig, then press Save to Catalog. The scene itself is a scratch
    /// space; the catalog is what the game reads, so there's no need to save the scene.
    /// </summary>
    public static class NitroBench
    {
        const string CatalogPath = "Assets/_ShiftRush/Resources/RaceCatalog.asset";
        const float Spacing = 7f;

        [MenuItem("SHIFT RUSH/Cars/Nitro Bench")]
        static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var cat = AssetDatabase.LoadAssetAtPath<RaceCatalog>(CatalogPath);
            if (cat == null) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(cat.cars.Count * Spacing / 8f + 2f, 1f, 3f);
            floor.transform.position = new Vector3((cat.cars.Count - 1) * Spacing * 0.5f, 0f, 0f);

            GameObject first = null;
            for (int i = 0; i < cat.cars.Count; i++)
            {
                var car = cat.cars[i];
                if (car.prefab == null) continue;
                var go = Object.Instantiate(car.prefab, new Vector3(i * Spacing, 0.65f, 0f), Quaternion.identity);
                go.name = car.displayName;
                Strip(go);
                var rig = go.AddComponent<NitroRig>();
                rig.carId = car.id;
                rig.nitroVfx = car.nitroVfx;
                rig.nitroRotation = car.nitroRotation;
                rig.nitroScale = car.nitroScale;
                rig.nitroHide = car.nitroHide;
                var points = car.nitroExhausts != null && car.nitroExhausts.Length > 0 ? car.nitroExhausts : DefaultPoints(go.transform);
                for (int k = 0; k < points.Length; k++) AddMarker(go.transform, points[k], k + 1);
                rig.Refresh(true);
                if (first == null) first = go;
            }
            if (first != null)
            {
                Selection.activeGameObject = first;
                var view = SceneView.lastActiveSceneView;
                if (view != null)
                {
                    // Behind the first car, looking at its tail.
                    view.LookAtDirect(first.transform.position + new Vector3(0f, 0.3f, -2f), Quaternion.Euler(12f, 0f, 0f), 4.5f);
                }
            }
            Debug.Log("[SHIFT//RUSH] Nitro Bench: move the Exhaust markers, then press Save to Catalog on each car's Nitro Rig.");
        }

        static void AddMarker(Transform car, Vector3 local, int n)
        {
            var m = new GameObject("Exhaust " + n).transform;
            m.SetParent(car, false);
            m.localPosition = local;
        }

        /// <summary>Writes this rig's exhausts, effect, angle and size to the car's catalog entry.</summary>
        public static void Save(NitroRig rig)
        {
            var cat = AssetDatabase.LoadAssetAtPath<RaceCatalog>(CatalogPath);
            var car = cat != null ? cat.cars.Find(c => c.id == rig.carId) : null;
            if (car == null) { Debug.LogWarning("[SHIFT//RUSH] No catalog car " + rig.carId); return; }
            var pts = new List<Vector3>();
            foreach (Transform m in rig.transform)
                if (m.name.StartsWith("Exhaust")) pts.Add(Round(m.localPosition));
            Undo.RecordObject(cat, "Save nitro");
            car.nitroExhausts = pts.ToArray();
            car.nitroVfx = rig.nitroVfx;
            car.nitroRotation = rig.nitroRotation;
            car.nitroScale = rig.nitroScale;
            car.nitroHide = rig.nitroHide;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Saved " + pts.Count + " nitro points for " + car.displayName + ".");
        }

        static Vector3 Round(Vector3 v)
        {
            return new Vector3(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f, Mathf.Round(v.z * 1000f) / 1000f);
        }

        public static void AddExhaust(NitroRig rig)
        {
            int n = 1;
            Transform last = null;
            foreach (Transform m in rig.transform) if (m.name.StartsWith("Exhaust")) { n++; last = m; }
            var pos = last != null ? last.localPosition + new Vector3(0.3f, 0f, 0f) : DefaultPoints(rig.transform)[0];
            AddMarker(rig.transform, pos, n);
            var created = rig.transform.Find("Exhaust " + n);
            Undo.RegisterCreatedObjectUndo(created.gameObject, "Add exhaust");
            rig.Refresh(true);
            Selection.activeTransform = created;
        }

        // The game's own default (see CarFX): two points low at the back either side of centre.
        static Vector3[] DefaultPoints(Transform car)
        {
            var b = new Bounds(Vector3.up * 0.6f, new Vector3(1.9f, 1.2f, 4.4f));
            bool any = false;
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = car.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            float back = b.min.z + 0.05f, low = b.min.y + Mathf.Max(0.3f, b.size.y * 0.28f);
            return new[] { new Vector3(-b.size.x * 0.12f, low, back), new Vector3(b.size.x * 0.12f, low, back) };
        }

        // Only the looks: no physics, wheels or scripts running in the bench.
        static void Strip(GameObject root)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var list = t.GetComponents<MonoBehaviour>();
                    for (int i = list.Length - 1; i >= 0; i--) if (list[i] != null) { Object.DestroyImmediate(list[i]); any = true; }
                }
                if (!any) break;
            }
            var kill = new List<Component>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup) continue;
                kill.Add(c);
            }
            kill.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            foreach (var c in kill) if (c != null) Object.DestroyImmediate(c);
        }

        static int Rank(Component c) { return c is Joint ? 0 : c is Collider ? 1 : c is Rigidbody ? 2 : 3; }
    }

    [CustomEditor(typeof(NitroRig))]
    public class NitroRigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Move the \"Exhaust\" children to where the nitro should come out (the magenta sphere and line point backwards). Duplicate or delete them for more or fewer. Then Save to Catalog.", MessageType.Info);
            DrawDefaultInspector();
            var rig = (NitroRig)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Exhaust")) NitroBench.AddExhaust(rig);
                if (GUILayout.Button("Refresh Preview")) rig.Refresh(true);
            }
            GUI.backgroundColor = new Color(1f, 0.45f, 0.9f);
            if (GUILayout.Button("Save to Catalog", GUILayout.Height(32))) NitroBench.Save(rig);
            GUI.backgroundColor = Color.white;
        }
    }
}
