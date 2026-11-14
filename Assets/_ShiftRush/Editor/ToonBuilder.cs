using ShiftRush.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Build Toon Look: makes the outline material, adds a "Toon Outline" Full Screen Pass (depth and
    /// normals in, ink lines out, before transparents so particles and the HUD draw clean on top) to each URP
    /// renderer, and saves Resources/SR_ToonKit for <see cref="ToonLook"/>. Safe to run again.
    /// </summary>
    public static class ToonBuilder
    {
        const string KitPath = "Assets/_ShiftRush/Resources/SR_ToonKit.asset";
        const string OutlineMatPath = "Assets/_ShiftRush/Art/Materials/M_ToonOutline.mat";
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
        const string FeatureName = "Toon Outline";

        [MenuItem("SHIFT RUSH/Build Toon Look")]
        public static void Build()
        {
            var outlineShader = Shader.Find("ShiftRush/Toon Outline");
            var carShader = Shader.Find("ShiftRush/Toon Car");
            if (outlineShader == null || carShader == null) { Debug.LogError("[SHIFT//RUSH] Toon shaders missing or not compiled."); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            if (mat == null) { mat = new Material(outlineShader); AssetDatabase.CreateAsset(mat, OutlineMatPath); }
            mat.shader = outlineShader;

            var kit = AssetDatabase.LoadAssetAtPath<ToonKit>(KitPath);
            if (kit == null) { kit = ScriptableObject.CreateInstance<ToonKit>(); AssetDatabase.CreateAsset(kit, KitPath); }
            kit.carShader = carShader;
            kit.outline = mat;
            kit.outlineFeatures.Clear();

            foreach (var path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;
                FullScreenPassRendererFeature feature = null;
                foreach (var f in data.rendererFeatures) if (f is FullScreenPassRendererFeature fs && f.name == FeatureName) feature = fs;
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = FeatureName;
                    AssetDatabase.AddObjectToAsset(feature, data);
                    data.rendererFeatures.Add(feature);
                    AssetDatabase.SaveAssets();
                    // The renderer keeps a map of its features' file ids alongside the list.
                    string guid; long id;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out guid, out id))
                    {
                        var so = new SerializedObject(data);
                        var map = so.FindProperty("m_RendererFeatureMap");
                        map.arraySize++;
                        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                feature.passMaterial = mat;
                feature.passIndex = 0;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
                feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
                feature.fetchColorBuffer = true;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
                kit.outlineFeatures.Add(feature);
            }
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Toon look built: " + kit.outlineFeatures.Count + " renderer(s) have the outline pass.");
        }
    }
}
