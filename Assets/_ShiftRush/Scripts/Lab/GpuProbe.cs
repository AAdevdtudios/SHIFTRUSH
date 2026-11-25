using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.Lab
{
    /// <summary>
    /// Tuning aid: measures what each rendering feature costs on the GPU in the current scene. Switches one thing
    /// off at a time (ambient occlusion, outlines, shadows, post, particles, half resolution), averages the GPU frame
    /// time from FrameTimingManager for each, puts everything back and logs a table tagged [GPU]. Start it with
    /// GpuProbe.Run(); read the result from the console or GpuProbe.Report.
    /// </summary>
    public class GpuProbe : MonoBehaviour
    {
        public static string Report { get; private set; } = "";
        public static bool Busy { get; private set; }

        public static void Run()
        {
            if (Busy) return;
            new GameObject("GPU Probe").AddComponent<GpuProbe>();
        }

        /// <summary>Second mode: hides one group of the scene at a time (each root, and the children of any root
        /// named in <paramref name="expand"/>) and logs what each group costs, tagged [GPU-G].</summary>
        public static void RunGroups(params string[] expand)
        {
            if (Busy) return;
            var p = new GameObject("GPU Probe").AddComponent<GpuProbe>();
            p.groupsMode = true; p.expand = expand;
        }

        bool groupsMode; string[] expand;

        void Start() { Busy = true; Report = "running"; StartCoroutine(groupsMode ? Groups() : Probe()); }

        IEnumerator Groups()
        {
            var groups = new List<Transform>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == gameObject || !root.activeInHierarchy) continue;
                bool split = false;
                foreach (var e in expand) if (root.name == e) split = true;
                if (split) foreach (Transform c in root.transform) { if (c.gameObject.activeInHierarchy) groups.Add(c); }
                else groups.Add(root.transform);
            }
            var sb = new StringBuilder();
            float baseline = 0f;
            yield return Sample(ms => baseline = ms);
            sb.AppendLine(string.Format("[GPU-G] baseline {0:0.0} ms", baseline));
            foreach (var g in groups)
            {
                var off = new List<Renderer>();
                foreach (var r in g.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; off.Add(r); }
                if (off.Count == 0) continue;
                float ms = 0f;
                yield return Sample(v => ms = v);
                foreach (var r in off) if (r != null) r.enabled = true;
                sb.AppendLine(string.Format("[GPU-G] {0,-40} {1,4} renderers  saves {2,5:0.0} ms", Path(g), off.Count, baseline - ms));
            }
            Report = sb.ToString();
            Debug.Log(Report + "[GPU-G] done");
            Destroy(gameObject);
        }

        static string Path(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
        void OnDestroy() { Busy = false; }

        IEnumerator Probe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[GPU] " + Screen.width + "x" + Screen.height);
            float baseline = 0f;
            var steps = new List<(string name, System.Action off, System.Action on)>
            {
                ("baseline", null, null),
                ("no ambient occlusion", () => Feature("ScreenSpaceAmbientOcclusion", false), () => Feature("ScreenSpaceAmbientOcclusion", true)),
                ("no toon outline", () => Feature("Toon Outline", false), () => Feature("Toon Outline", true)),
                ("no shadows", () => Shadows(false), () => Shadows(true)),
                ("no post / AA", () => Post(false), () => Post(true)),
                ("no particles", () => Particles(false), () => Particles(true)),
                ("no UI canvases", () => Canvases(false), () => Canvases(true)),
                ("half resolution", () => Scale(0.5f), () => Scale(1f)),
            };
            foreach (var step in steps)
            {
                step.off?.Invoke();
                yield return Sample(ms =>
                {
                    if (step.name == "baseline") baseline = ms;
                    sb.AppendLine(string.Format("[GPU] {0,-22} {1,6:0.0} ms   saves {2,5:0.0}", step.name, ms, baseline - ms));
                });
                step.on?.Invoke();
            }
            Report = sb.ToString();
            Debug.Log(Report);
            Destroy(gameObject);
        }

        static IEnumerator Sample(System.Action<float> done)
        {
            for (int i = 0; i < 30; i++) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
            var timings = new FrameTiming[1];
            float total = 0f; int n = 0;
            for (int i = 0; i < 120; i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) { total += (float)timings[0].gpuFrameTime; n++; }
                yield return null;
            }
            done(n > 0 ? total / n : -1f);
        }

        static UniversalRenderPipelineAsset Asset => QualitySettings.renderPipeline as UniversalRenderPipelineAsset ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

        static readonly Dictionary<ScriptableRendererFeature, bool> featureWas = new Dictionary<ScriptableRendererFeature, bool>();
        static void Feature(string name, bool on)
        {
            var cam = Camera.main; if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            var renderer = data != null ? data.scriptableRenderer as UniversalRenderer : null;
            if (renderer == null) return;
            var list = typeof(ScriptableRenderer).GetProperty("rendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(renderer) as List<ScriptableRendererFeature>;
            if (list == null) return;
            foreach (var f in list)
            {
                if (f == null || f.name != name) continue;
                if (!on) { featureWas[f] = f.isActive; f.SetActive(false); }
                else if (featureWas.TryGetValue(f, out var was)) f.SetActive(was);
            }
        }

        static readonly Dictionary<Light, LightShadows> shadowWas = new Dictionary<Light, LightShadows>();
        static void Shadows(bool on)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!on) { shadowWas[l] = l.shadows; l.shadows = LightShadows.None; }
                else if (shadowWas.TryGetValue(l, out var was)) l.shadows = was;
            }
        }

        static bool postWas; static AntialiasingMode aaWas;
        static void Post(bool on)
        {
            var cam = Camera.main; if (cam == null) return;
            var d = cam.GetUniversalAdditionalCameraData();
            if (!on) { postWas = d.renderPostProcessing; aaWas = d.antialiasing; d.renderPostProcessing = false; d.antialiasing = AntialiasingMode.None; }
            else { d.renderPostProcessing = postWas; d.antialiasing = aaWas; }
        }

        static readonly List<ParticleSystem> particlesOff = new List<ParticleSystem>();
        static void Particles(bool on)
        {
            if (!on) { particlesOff.Clear(); foreach (var p in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) { var r = p.GetComponent<ParticleSystemRenderer>(); if (r != null && r.enabled) { r.enabled = false; particlesOff.Add(p); } } }
            else foreach (var p in particlesOff) if (p != null) p.GetComponent<ParticleSystemRenderer>().enabled = true;
        }

        static readonly List<Canvas> canvasesOff = new List<Canvas>();
        static void Canvases(bool on)
        {
            if (!on) { canvasesOff.Clear(); foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.enabled && c.isRootCanvas && c.GetComponent<GpuProbe>() == null) { c.enabled = false; canvasesOff.Add(c); } }
            else foreach (var c in canvasesOff) if (c != null) c.enabled = true;
        }

        static float scaleWas = 1f;
        static void Scale(float s)
        {
            var a = Asset; if (a == null) return;
            if (s < 1f) { scaleWas = a.renderScale; a.renderScale = s; } else a.renderScale = scaleWas;
        }
    }
}
