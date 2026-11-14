using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.VFX
{
    /// <summary>
    /// Settings → Visuals → Toon Look. On: every car (player, rivals, the garage car) wears the toon shader, built
    /// from its own palette so it keeps its paint, and ink outlines are drawn over the scene, softening at speed.
    /// Off: the cars get their original materials back and the outline pass is switched off. Spawns itself.
    /// </summary>
    public class ToonLook : MonoBehaviour
    {
        const string LitShader = "Universal Render Pipeline/Lit";
        static ToonLook instance;
        static readonly int SpeedSoften = Shader.PropertyToID("_ToonSpeedSoften");

        readonly Dictionary<Material, Material> toonOf = new Dictionary<Material, Material>();
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly List<Renderer> gone = new List<Renderer>();
        float nextScan;
        bool applied, outlineOn = true;
        CameraFX.SpeedFeel speed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("Toon Look");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ToonLook>();
        }

        void Update()
        {
            var kit = ToonKit.Instance;
            if (kit == null || kit.carShader == null) return;
            bool on = GameSettings.ToonLook;
            if (on != outlineOn) SetOutline(kit, on);

            if (Time.unscaledTime >= nextScan || on != applied)
            {
                nextScan = Time.unscaledTime + 0.5f; // rivals and the garage car come and go
                Scan(kit, on);
                applied = on;
                if (speed == null) speed = FindFirstObjectByType<CameraFX.SpeedFeel>();
            }
            Shader.SetGlobalFloat(SpeedSoften, speed != null ? speed.Intensity : 0f);
        }

        void Scan(ToonKit kit, bool on)
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IVehicle) Apply(kit, mb.gameObject, on);
            var showroom = FindFirstObjectByType<UI.Showroom>();
            if (showroom != null && showroom.CurrentCar != null) Apply(kit, showroom.CurrentCar, on);

            gone.Clear();
            foreach (var r in originals.Keys) if (r == null) gone.Add(r);
            foreach (var r in gone) originals.Remove(r);
        }

        void Apply(ToonKit kit, GameObject root, bool on)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue; // not particles or trails
                if (on)
                {
                    if (originals.ContainsKey(r)) continue;
                    var mats = r.sharedMaterials;
                    var swapped = new Material[mats.Length];
                    bool any = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var t = Toon(kit, mats[i]);
                        swapped[i] = t != null ? t : mats[i];
                        any |= t != null;
                    }
                    if (!any) continue;
                    originals[r] = mats;
                    r.sharedMaterials = swapped;
                }
                else
                {
                    Material[] o;
                    if (originals.TryGetValue(r, out o)) { r.sharedMaterials = o; originals.Remove(r); }
                }
            }
        }

        // The toon twin of a car's Lit material: its palette, glow and cut-out (for livery decals).
        Material Toon(ToonKit kit, Material m)
        {
            if (m == null || m.shader == null || m.shader.name != LitShader) return null;
            Material t;
            if (toonOf.TryGetValue(m, out t)) return t;
            t = new Material(kit.carShader) { name = m.name + " (Toon)" };
            if (m.HasProperty("_BaseMap"))
            {
                t.SetTexture("_BaseMap", m.GetTexture("_BaseMap"));
                t.SetTextureScale("_BaseMap", m.GetTextureScale("_BaseMap"));
                t.SetTextureOffset("_BaseMap", m.GetTextureOffset("_BaseMap"));
            }
            if (m.HasProperty("_BaseColor")) t.SetColor("_BaseColor", m.GetColor("_BaseColor"));
            // Glow carries over (lamps, lit trim); black when the source doesn't glow.
            bool glows = m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor");
            t.SetColor("_EmissionColor", glows ? m.GetColor("_EmissionColor") : Color.black);
            if (glows && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") != null) t.SetTexture("_EmissionMap", m.GetTexture("_EmissionMap"));
            else if (glows) t.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            // Cut-out decals (liveries) stay cut out, in their shadows and depth too.
            if (m.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                t.EnableKeyword("_ALPHATEST_ON");
                t.SetFloat("_AlphaClip", 1f);
                if (m.HasProperty("_Cutoff")) t.SetFloat("_Cutoff", m.GetFloat("_Cutoff"));
                t.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            toonOf[m] = t;
            return t;
        }

        void SetOutline(ToonKit kit, bool on)
        {
            outlineOn = on;
            foreach (var f in kit.outlineFeatures) if (f != null) f.SetActive(on);
        }

        void OnDestroy()
        {
            // Leave the renderer assets as built (outline on), so the editor and a fresh build start the same.
            var kit = ToonKit.Instance;
            if (kit != null) foreach (var f in kit.outlineFeatures) if (f != null) f.SetActive(true);
            if (instance == this) instance = null;
        }
    }
}
