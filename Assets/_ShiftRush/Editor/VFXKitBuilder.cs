using System.IO;
using ShiftRush.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → VFX → Build VFX Kit: generates the effect textures, particle materials (URP Particles/Unlit)
    /// and particle prefabs in Art/VFX, and Resources/SR_VFXKit that points at them (plus the Hovl Studio
    /// full-screen overlays when that pack is installed). Rerun after changing anything here.
    /// </summary>
    public static class VFXKitBuilder
    {
        const string Dir = "Assets/_ShiftRush/Art/VFX/";
        const string KitPath = "Assets/_ShiftRush/Resources/SR_VFXKit.asset";
        const string Hovl = "Assets/Hovl Studio/Fullscreen effects/Prefabs/";
        static readonly Color Magenta = new Color32(255, 64, 217, 255);
        static readonly Color Cyan = new Color32(56, 225, 255, 255);

        [MenuItem("SHIFT RUSH/VFX/Build VFX Kit")]
        public static VFXKit Build()
        {
            Directory.CreateDirectory(Dir);

            // ---- textures first (materials made before a texture import lose their settings)
            var soft = Tex("T_FX_Soft", 128, 128, (u, v) => { float r = Radius(u, v); float a = Mathf.Clamp01(1f - r); return new Color(1, 1, 1, a * a); });
            var flame = Tex("T_FX_Flame", 128, 128, (u, v) =>
            {
                // Teardrop: round head at the bottom, tapering tail upward, hot core.
                float x = (u - 0.5f) * 2f, y = v;
                float width = Mathf.Lerp(0.95f, 0.15f, Mathf.Pow(y, 0.8f));
                float d = Mathf.Abs(x) / Mathf.Max(width, 0.01f);
                float a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - Mathf.Pow(y, 2f)) * Mathf.Clamp01(y * 6f);
                float core = Mathf.Clamp01(1f - d * 1.8f);
                return new Color(1, 1, 1, Mathf.Clamp01(a * 1.3f + core * 0.4f * a));
            });
            var ring = Tex("T_FX_Ring", 256, 256, (u, v) =>
            {
                float r = Radius(u, v);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.12f);
                return new Color(1, 1, 1, a * a);
            });
            var smoke = Tex("T_FX_Smoke", 128, 128, (u, v) =>
            {
                float r = Radius(u, v);
                float n = Mathf.PerlinNoise(u * 5f + 3.1f, v * 5f + 7.7f) * 0.6f + Mathf.PerlinNoise(u * 11f, v * 11f) * 0.4f;
                float a = Mathf.Clamp01(1f - r * (1.1f - n * 0.5f));
                return new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, a) * 0.9f);
            });
            var beam = Tex("T_FX_Beam", 64, 64, (u, v) => { float a = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f); return new Color(1, 1, 1, a * a); });

            var mSoftAdd = Mat("M_FX_SoftAdd", soft, true);
            var mFlame = Mat("M_FX_Flame", flame, true);
            var mRing = Mat("M_FX_Ring", ring, true);
            var mSmoke = Mat("M_FX_Smoke", smoke, false);
            var mSoftAlpha = Mat("M_FX_SoftAlpha", soft, false);
            var mTrail = Mat("M_FX_Trail", beam, true);

            var kit = AssetDatabase.LoadAssetAtPath<VFXKit>(KitPath);
            if (kit == null) { kit = ScriptableObject.CreateInstance<VFXKit>(); AssetDatabase.CreateAsset(kit, KitPath); }
            kit.nitroFlame = NitroFlame(mFlame);
            kit.nitroTrail = mTrail;
            kit.landingPuff = LandingPuff(mSmoke);
            kit.dirtSpray = DirtSpray(mSmoke);
            kit.arrivalBurst = ArrivalBurst(mRing, mSoftAdd);
            kit.snow = Snow(mSoftAlpha);
            kit.motes = Motes(mSoftAdd);
            kit.slowMoOverlay = AssetDatabase.LoadAssetAtPath<GameObject>(Hovl + "Screen hex.prefab");
            kit.warpOverlay = AssetDatabase.LoadAssetAtPath<GameObject>(Hovl + "Screen microcircuit.prefab");
            kit.nitroOverlay = AssetDatabase.LoadAssetAtPath<GameObject>(Hovl + "Screen wind straight.prefab");
            kit.magenta = Magenta; kit.cyan = Cyan;
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] VFX kit built" + (kit.slowMoOverlay == null ? " (Hovl Studio overlays not found: skipped)" : ""));
            return kit;
        }

        /// <summary>Snow on snowy circuits, pollen motes elsewhere; saves both track scenes.</summary>
        [MenuItem("SHIFT RUSH/VFX/Add Track Ambience To Tracks")]
        static void AddAmbienceToTracks()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var s in new[] { TrackBuilder.AlpinePass(), TrackBuilder.GlacierValley() })
            {
                var scene = EditorSceneManager.OpenScene(s.scene, OpenSceneMode.Single);
                AddAmbience(s.snowy);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        public static void AddAmbience(bool snowy)
        {
            foreach (var old in Object.FindObjectsByType<TrackAmbience>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Track Ambience");
            var a = go.AddComponent<TrackAmbience>();
            a.air = snowy ? TrackAmbience.Air.Snow : TrackAmbience.Air.Motes;
            a.dust = snowy ? new Color(0.95f, 0.97f, 1f, 0.75f) : new Color(0.84f, 0.76f, 0.62f, 0.65f);
            a.density = snowy ? 0.8f : 1f;
        }

        // ------------------------------------------------------------------ prefabs

        static ParticleSystem NitroFlame(Material mat)
        {
            var go = new GameObject("P_NitroFlame");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f; main.loop = true; main.playOnAwake = false;
            // A short hot jet that stays on the pipe (local space), white core shading to the car's colour.
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Magenta);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 120;
            var em = ps.emission; em.rateOverTime = 160f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = 0.04f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f, 0f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.15f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.2f;
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return Save(go);
        }

        static ParticleSystem LandingPuff(Material mat)
        {
            var go = new GameObject("P_LandingPuff");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f; main.loop = true; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(2f, 0.1f, 4f); sh.rotation = new Vector3(-90f, 0, 0); sh.randomDirectionAmount = 0.8f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 0.45f, 0f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 1.7f)));
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.2f; lim.dampen = 0.15f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return Save(go);
        }

        static ParticleSystem DirtSpray(Material mat)
        {
            var go = new GameObject("P_DirtSpray");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f; main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.31f, 0.2f, 0.9f), new Color(0.7f, 0.6f, 0.45f, 0.6f));
            main.gravityModifier = 1.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;
            var em = ps.emission; em.rateOverTime = 40f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 22f; sh.radius = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f, 0f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1.4f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return Save(go);
        }

        /// <summary>Flat magenta shockwave on the road, a white-hot flash and a spray of sparks.</summary>
        static ParticleSystem ArrivalBurst(Material ringMat, Material softMat)
        {
            var go = new GameObject("P_ArrivalBurst");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 0.7f; main.startSpeed = 0f; main.startSize = 2f;
            main.startColor = Magenta;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.12f, 1) });
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f, 0f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(18f, new AnimationCurve(new Keyframe(0, 0.05f, 0, 3f), new Keyframe(1, 1f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = ringMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;

            var flash = Child(go, "Flash", softMat);
            var fm = flash.main; fm.duration = 1f; fm.loop = false; fm.playOnAwake = false; fm.startLifetime = 0.25f; fm.startSpeed = 0f; fm.startSize = 9f; fm.startColor = Color.white;
            var fe = flash.emission; fe.rateOverTime = 0f; fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var fs = flash.shape; fs.enabled = false;
            var fc = flash.colorOverLifetime; fc.enabled = true; fc.color = Grad(Color.white, Magenta, 0.9f, 0f);

            var sparks = Child(go, "Sparks", softMat);
            var sm = sparks.main; sm.duration = 1f; sm.loop = false; sm.playOnAwake = false;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f); sm.startSpeed = new ParticleSystem.MinMaxCurve(9f, 22f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f); sm.gravityModifier = 1.2f;
            sm.startColor = new ParticleSystem.MinMaxGradient(Color.white, Cyan);
            sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.maxParticles = 100;
            var se = sparks.emission; se.rateOverTime = 0f; se.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
            var ss = sparks.shape; ss.shapeType = ParticleSystemShapeType.Hemisphere; ss.radius = 1f; ss.rotation = new Vector3(-90f, 0, 0);
            var sc = sparks.colorOverLifetime; sc.enabled = true; sc.color = Grad(Color.white, Color.white, 1f, 0f);
            var sr = sparks.GetComponent<ParticleSystemRenderer>(); sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.06f; sr.lengthScale = 1f;
            return Save(go);
        }

        static ParticleSystem Snow(Material mat)
        {
            var go = new GameObject("P_Snow");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f; main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.gravityModifier = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            var em = ps.emission; em.rateOverTime = 650f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60f, 1f, 60f); sh.position = new Vector3(0f, 10f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f, 0f, 0.15f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return Save(go);
        }

        static ParticleSystem Motes(Material mat)
        {
            var go = new GameObject("P_Motes");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f; main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.65f, 0.8f), new Color(0.75f, 1f, 0.85f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            var em = ps.emission; em.rateOverTime = 130f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(55f, 8f, 55f); sh.position = new Vector3(0f, 3f, 0f); sh.randomDirectionAmount = 1f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f, 0f, 0.3f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return Save(go);
        }

        // ------------------------------------------------------------------ helpers

        static ParticleSystem Child(GameObject parent, string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem Save(GameObject go)
        {
            string path = Dir + go.name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }

        /// <summary>Colour a→b over life, alpha fading in over `fadeIn` then from `a0` to `a1`.</summary>
        static Gradient Grad(Color a, Color b, float a0, float a1, float fadeIn = 0f)
        {
            var g = new Gradient();
            var alphas = fadeIn > 0f
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a0, fadeIn), new GradientAlphaKey(a1, 1f) }
                : new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a1, 1f) };
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) }, alphas);
            return g;
        }

        static float Radius(float u, float v) { return Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f; }

        static Texture2D Tex(string name, int w, int h, System.Func<float, float, Color> f)
        {
            string path = Dir + name + ".png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels(px); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>URP Particles/Unlit, transparent; additive (glows) or alpha-blended (smoke, dust, snow).</summary>
        static Material Mat(string name, Texture2D tex, bool additive)
        {
            string path = Dir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
