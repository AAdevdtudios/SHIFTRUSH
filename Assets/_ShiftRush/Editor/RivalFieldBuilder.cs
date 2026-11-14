using System.IO;
using ShiftRush.Race;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Makes Resources/SR_RivalField: five rivals in the Zephyr, each in one of the ARCADE pack's paint schemes
    /// (the fifth a blacked-out version), named after seabirds to sit with the Razorbill crest. Rerun to reset it.
    /// </summary>
    public static class RivalFieldBuilder
    {
        const string AssetPath = "Assets/_ShiftRush/Resources/SR_RivalField.asset";
        const string Paints = "Assets/ARCADE - Ultimate Vehicles Pack/Materials/Color Variations/";
        const string Liveries = "Assets/_ShiftRush/Art/Liveries/";

        [MenuItem("SHIFT RUSH/Race/Build Rival Field")]
        public static RivalField Build()
        {
            Directory.CreateDirectory(Liveries);
            var f = AssetDatabase.LoadAssetAtPath<RivalField>(AssetPath);
            if (f == null) { f = ScriptableObject.CreateInstance<RivalField>(); AssetDatabase.CreateAsset(f, AssetPath); }

            f.carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ShiftRush/Prefabs/Vehicles/Car_Zephyr.prefab");
            f.paintToReplace = AssetDatabase.LoadAssetAtPath<Material>(Paints + "ColorVar1_Material.mat");

            // Stealth livery: the pack's second scheme, darkened.
            string stealthPath = Liveries + "M_Livery_Gannet.mat";
            var stealth = AssetDatabase.LoadAssetAtPath<Material>(stealthPath);
            var var2 = AssetDatabase.LoadAssetAtPath<Material>(Paints + "ColorVar2_Material.mat");
            if (stealth == null && var2 != null) { stealth = new Material(var2); AssetDatabase.CreateAsset(stealth, stealthPath); }
            if (stealth != null) { stealth.SetColor("_BaseColor", new Color(0.26f, 0.26f, 0.3f)); EditorUtility.SetDirty(stealth); }

            f.rivals.Clear();
            Add(f, "KESTREL", Load("ColorVar2"), 1.32f, 232f, 0.6f);
            Add(f, "SHRIKE", Load("ColorVar3"), 1.28f, 228f, 0.35f);
            Add(f, "PETREL", Load("ColorVar4"), 1.24f, 225f, 0.5f);
            Add(f, "MERLIN", Load("ColorVar5"), 1.38f, 236f, 0.7f);
            Add(f, "GANNET", stealth, 1.30f, 230f, 0.45f);
            f.rivals[4].color = new Color(0.62f, 0.66f, 0.78f);

            // PETREL's scheme is white on this car, like the player's: an icy tint of its own.
            var petrel = f.rivals[2];
            string petrelPath = Liveries + "M_Livery_Petrel.mat";
            var ice = AssetDatabase.LoadAssetAtPath<Material>(petrelPath);
            var var4 = Load("ColorVar4");
            if (ice == null && var4 != null) { ice = new Material(var4); AssetDatabase.CreateAsset(ice, petrelPath); }
            if (ice != null)
            {
                ice.CopyPropertiesFromMaterial(var4);
                ice.SetColor("_BaseColor", new Color(0.55f, 0.9f, 1f));
                EditorUtility.SetDirty(ice);
                petrel.livery = ice;
                petrel.color = new Color(0.55f, 0.9f, 1f);
            }

            EditorUtility.SetDirty(f);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Rival field: " + f.rivals.Count + " rivals");
            return f;
        }

        static Material Load(string name) { return AssetDatabase.LoadAssetAtPath<Material>(Paints + name + "_Material.mat"); }

        static void Add(RivalField f, string name, Material livery, float grip, float top, float shortcut)
        {
            f.rivals.Add(new RivalField.Rival
            {
                name = name, livery = livery, grip = grip, topSpeedKmh = top, shortcutChance = shortcut,
                color = livery != null ? Vivid(livery.GetTexture("_BaseMap") as Texture2D, f.carPrefab, f.paintToReplace) : Color.white,
            });
        }

        /// <summary>The body colour of a paint scheme on this car, for its minimap dot: the texture sampled at the
        /// car's own UVs (the paint is an atlas shared by the whole vehicle pack), most common hue wins.</summary>
        static Color Vivid(Texture2D tex, GameObject car, Material paint)
        {
            if (tex == null || car == null) return Color.white;
            const int n = 256;
            var px = Read(tex, n);
            var hist = new float[25]; var sums = new Color[25];
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                if (r == null || mf.sharedMesh == null || System.Array.IndexOf(r.sharedMaterials, paint) < 0) continue;
                var uv = mf.sharedMesh.uv;
                foreach (var u in uv)
                {
                    int x = Mathf.Clamp((int)(Mathf.Repeat(u.x, 1f) * n), 0, n - 1), y = Mathf.Clamp((int)(Mathf.Repeat(u.y, 1f) * n), 0, n - 1);
                    var c = px[y * n + x];
                    float h, s, v; Color.RGBToHSV(c, out h, out s, out v);
                    if (s < 0.25f || v < 0.2f) continue; // trim, glass, tyres
                    int bin = Mathf.Clamp((int)(h * 24f), 0, 23);
                    hist[bin] += 1f; sums[bin] += c;
                }
            }
            int best = 0;
            for (int i = 1; i < 25; i++) if (hist[i] > hist[best]) best = i;
            if (hist[best] <= 0f) return Color.white;
            var col = sums[best] / hist[best];
            float hh, ss, vv; Color.RGBToHSV(col, out hh, out ss, out vv);
            return Color.HSVToRGB(hh, Mathf.Max(ss, 0.6f), Mathf.Max(vv, 0.85f));
        }

        static Color[] Read(Texture2D tex, int n)
        {
            var rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var read = new Texture2D(n, n, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, n, n), 0, 0); read.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = read.GetPixels();
            Object.DestroyImmediate(read);
            return px;
        }
    }
}
