using System.Collections.Generic;
using System.IO;
using ShiftRush.UI;
using ShiftRush.VFX;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// The main menu's backdrop, "your car under the plane at night": a wet airstrip after rain, the crew's cargo
    /// plane parked behind the car with its bay lit and ramp down, floodlight towers, runway lights running into
    /// the fog, police lights far off, rain, ground mist and a moonlit cloud sky. Builds into the open MainMenu
    /// under Showroom/Showcase Set and replaces the old studio floor, ring and lights. Run it again to rebuild.
    /// </summary>
    public static class ShowcaseSetBuilder
    {
        const string Dir = "Assets/_ShiftRush/Art/Showcase/";
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);
        static readonly Color Warm = new Color(1f, 0.76f, 0.48f);
        static readonly Color Moon = new Color(0.62f, 0.72f, 1f);

        static Transform root;
        const float planeYaw = 50f;
        static readonly List<ShowcaseLights.Blinker> blinkers = new List<ShowcaseLights.Blinker>();
        static readonly List<Transform> props = new List<Transform>();
        static readonly List<Transform> rotors = new List<Transform>();
        static readonly List<Light> breathers = new List<Light>();

        [MenuItem("SHIFT RUSH/Build Menu Showcase")]
        public static void Build()
        {
            var showroom = Object.FindFirstObjectByType<Showroom>();
            if (showroom == null) { Debug.LogError("[Showcase] Open the MainMenu scene first."); return; }
            if (!AssetDatabase.IsValidFolder("Assets/_ShiftRush/Art/Showcase")) AssetDatabase.CreateFolder("Assets/_ShiftRush/Art", "Showcase");
            blinkers.Clear(); props.Clear(); rotors.Clear(); breathers.Clear();

            var old = showroom.transform.Find("Showcase Set");
            // Keep the stream screen where it was placed by hand in the scene (or where a full menu rebuild found it).
            var oldScreen = old != null ? old.Find("Stream Screen") : null;
            bool keepScreen = oldScreen != null || remembered;
            Vector3 screenPos = oldScreen != null ? oldScreen.localPosition : rememberedPos;
            Quaternion screenRot = oldScreen != null ? oldScreen.localRotation : rememberedRot;
            Vector3 screenScale = oldScreen != null ? oldScreen.localScale : rememberedScale;
            remembered = false;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            root = new GameObject("Showcase Set").transform;
            root.SetParent(showroom.transform, false);

            // The old studio goes: floor, ring, speed lines and its three lights.
            foreach (var n in new[] { "Floor", "Rush Lines", "Key", "Rim Magenta", "Rim Cyan" })
            {
                var t = showroom.transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }

            Ground();
            Runway();
            if (!Lifter()) Plane(); // the pack helicopter, or the built-in plane if the pack isn't installed
            Towers();
            CarLights();
            Police();
            Skyline();
            Pad(showroom);
            Screen(showroom);
            if (keepScreen)
            {
                var screen = root.Find("Stream Screen");
                screen.localPosition = screenPos; screen.localRotation = screenRot; screen.localScale = screenScale;
            }
            Atmosphere();
            Sky();
            Look(showroom);

            var lights = root.gameObject.AddComponent<ShowcaseLights>();
            lights.blinkers = blinkers.ToArray();
            lights.props = props.ToArray();
            lights.rotors = rotors.ToArray();
            lights.breathers = breathers.ToArray();

            // Track select hides the set too (it shows a picture of the car on its own backdrop).
            var so = new SerializedObject(showroom);
            var hide = so.FindProperty("hideWhenPosed");
            var hidden = new List<GameObject> { root.gameObject };
            foreach (var n in new[] { "Turntable/Plinth", "Glow Ring" })
            {
                var t = showroom.transform.Find(n);
                if (t != null) hidden.Add(t.gameObject);
            }
            hide.arraySize = hidden.Count;
            for (int i = 0; i < hidden.Count; i++) hide.GetArrayElementAtIndex(i).objectReferenceValue = hidden[i];
            so.FindProperty("homeOffset").vector3Value = new Vector3(1.3f, -0.55f, 0f);
            so.FindProperty("orbitSway").floatValue = 6f; // gentle: the player can orbit with the mouse now
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(showroom.gameObject.scene);
            AssetDatabase.SaveAssets();
            FixEmission();
            Probe(); // after the lights glow, so the wet ground reflects them
            TimeOfDay(lights);
            AssetDatabase.SaveAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("[Showcase] Built the night airstrip.");
        }

        // ---------------------------------------------------------------- ground and markings

        static void Ground()
        {
            var tex = MakeTex(Dir + "T_WetTarmac.png", 512, (u, v) =>
            {
                // Tiling value noise: fine grit, plus big irregular puddles where the tarmac dips.
                float grit = Fbm(u, v, 64, 3);
                float puddle = Fbm(u, v, 4, 4);
                float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.6f, puddle));
                float g = Mathf.Lerp(0.045f, 0.085f, grit) * Mathf.Lerp(1f, 0.55f, wet);
                return new Color(g, g * 1.03f, g * 1.12f, Mathf.Lerp(0.5f + grit * 0.15f, 0.97f, wet));
            }, false);
            var m = Lit("M_SC_WetTarmac", Color.white, 1f, 0f);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(34, 34));
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            var g0 = Prim(PrimitiveType.Plane, "Wet Tarmac", root, new Vector3(0, -0.07f, -200), Vector3.one * 120f, m);
            g0.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Runway()
        {
            var t = Group("Runway");
            var paint = Lit("M_SC_Paint", new Color(0.82f, 0.82f, 0.8f), 0.75f, 0f);
            var paintAmber = Lit("M_SC_PaintAmber", new Color(0.9f, 0.62f, 0.12f), 0.75f, 0f);
            // Centre line dashes, starting just behind the plane so none run under the car.
            for (float z = -46f; z > -400f; z -= 54f)
                Prim(PrimitiveType.Cube, "Dash", t, new Vector3(0, -0.06f, z), new Vector3(0.9f, 0.02f, 30f), paint);
            // Edge stripes and the taxi line the car sits on.
            Prim(PrimitiveType.Cube, "Edge L", t, new Vector3(-11.5f, -0.06f, -150f), new Vector3(0.9f, 0.02f, 360f), paint);
            Prim(PrimitiveType.Cube, "Edge R", t, new Vector3(11.5f, -0.06f, -150f), new Vector3(0.9f, 0.02f, 360f), paint);
            Prim(PrimitiveType.Cube, "Taxi Line", t, new Vector3(-3.4f, -0.06f, 0f), new Vector3(0.35f, 0.02f, 40f), paintAmber);

            // The runway number, the channel's number.
            var label = new GameObject("Runway 13");
            label.transform.SetParent(t, false);
            label.transform.localPosition = new Vector3(-9.5f, -0.045f, 3f);
            label.transform.localRotation = Quaternion.Euler(90f, 205f, 0f);
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.text = "13";
            tmp.fontSize = 60f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.82f, 0.82f, 0.8f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(14f, 9f);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Motion Titles Pack/Fonts/Roboto-Black SDF.asset");
            if (font != null) tmp.font = font;

            // Edge lights running off into the fog, red at the far end, blue on the taxiway.
            var white = Emissive("M_SC_BulbWhite", new Color(1f, 0.93f, 0.8f) * 5f);
            var blue = Emissive("M_SC_BulbBlue", new Color(0.2f, 0.4f, 1f) * 3f);
            var red = Emissive("M_SC_BulbRed", new Color(1f, 0.12f, 0.08f) * 7f);
            for (float z = 18f; z > -360f; z -= 13f)
            {
                Bulb(t, new Vector3(-13f, 0.2f, z), 0.38f, white);
                Bulb(t, new Vector3(13f, 0.2f, z), 0.38f, white);
            }
            for (float x = -12f; x <= 12f; x += 3f) Bulb(t, new Vector3(x, 0.2f, -362f), 0.6f, red);
            for (float z = 10f; z > -120f; z -= 9f)
            {
                Bulb(t, new Vector3(-22f, 0.15f, z), 0.26f, blue);
                Bulb(t, new Vector3(22f, 0.15f, z), 0.26f, blue);
            }
        }

        // ---------------------------------------------------------------- the crew's cargo plane

        static void Plane()
        {
            var t = Group("Cargo Plane");
            var hull = Lit("M_SC_PlaneHull", new Color(0.065f, 0.07f, 0.09f), 0.5f, 0.45f);
            var dark = Lit("M_SC_PlaneDark", new Color(0.05f, 0.05f, 0.07f), 0.3f, 0.2f);
            var stripe = Emissive("M_SC_PlaneStripe", Magenta * 2.2f);

            // Fuselage along Z, open tail facing the camera, nose away into the dark.
            Prim(PrimitiveType.Cylinder, "Fuselage", t, new Vector3(0, 3.7f, -26f), new Vector3(6.6f, 15f, 6.6f), hull, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Sphere, "Nose", t, new Vector3(0, 3.7f, -41f), new Vector3(6.6f, 6.6f, 7.5f), hull);
            Prim(PrimitiveType.Cube, "Livery Stripe L", t, new Vector3(-3.32f, 4.2f, -27f), new Vector3(0.05f, 0.35f, 24f), stripe);
            Prim(PrimitiveType.Cube, "Livery Stripe R", t, new Vector3(3.32f, 4.2f, -27f), new Vector3(0.05f, 0.35f, 24f), stripe);

            // High wing, four engines with turning props and hot exhausts.
            Prim(PrimitiveType.Cube, "Wing", t, new Vector3(0, 7.35f, -25f), new Vector3(46f, 0.55f, 5f), hull);
            var exhaust = Emissive("M_SC_Exhaust", new Color(1f, 0.4f, 0.1f) * 2.2f);
            var disc = PropDisc();
            foreach (float x in new[] { -14f, -7.5f, 7.5f, 14f })
            {
                Prim(PrimitiveType.Cylinder, "Nacelle", t, new Vector3(x, 6.5f, -23.5f), new Vector3(1.5f, 2.7f, 1.5f), hull, new Vector3(90, 0, 0));
                Prim(PrimitiveType.Cylinder, "Exhaust", t, new Vector3(x, 6.5f, -20.75f), new Vector3(0.75f, 0.05f, 0.75f), exhaust, new Vector3(90, 0, 0));
                var p = new GameObject("Prop").transform;
                p.SetParent(t, false);
                p.localPosition = new Vector3(x, 6.5f, -26.35f);
                for (int i = 0; i < 4; i++)
                    Prim(PrimitiveType.Cube, "Blade", p, Quaternion.Euler(0, 0, i * 90f) * new Vector3(0, 1.1f, 0), new Vector3(0.22f, 2.2f, 0.06f), dark, new Vector3(0, 0, i * 90f));
                var d = Prim(PrimitiveType.Quad, "Blur", p, Vector3.zero, Vector3.one * 4.6f, disc, new Vector3(0, 180, 0));
                d.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                props.Add(p);
            }

            // Tail.
            Prim(PrimitiveType.Cube, "Fin", t, new Vector3(0, 10.2f, -15.5f), new Vector3(0.45f, 7.5f, 5.5f), dark, new Vector3(-14, 0, 0));
            Prim(PrimitiveType.Cube, "Stabiliser", t, new Vector3(0, 7.5f, -13.8f), new Vector3(16f, 0.35f, 3.4f), hull);

            // Open cargo bay: the lit interior, its frame, and the ramp down to the tarmac.
            var bayTex = MakeTex(Dir + "T_CargoBay.png", 512, BayPixel, true);
            var bay = Emissive("M_SC_CargoBay", Color.white * 2.4f, bayTex);
            Prim(PrimitiveType.Quad, "Bay Interior", t, new Vector3(0, 3.0f, -10.94f), new Vector3(5.6f, 5.2f, 1f), bay, new Vector3(0, 180, 0));
            Prim(PrimitiveType.Cube, "Frame Top", t, new Vector3(0, 5.7f, -10.9f), new Vector3(6.2f, 0.3f, 0.2f), dark);
            Prim(PrimitiveType.Cube, "Frame L", t, new Vector3(-2.95f, 3.0f, -10.9f), new Vector3(0.3f, 5.4f, 0.2f), dark);
            Prim(PrimitiveType.Cube, "Frame R", t, new Vector3(2.95f, 3.0f, -10.9f), new Vector3(0.3f, 5.4f, 0.2f), dark);

            // Light pouring out of the bay onto the ramp and the car.
            var spill = AddLight(t, "Bay Spill", LightType.Spot, new Vector3(0, 3.6f, -12.5f), new Vector3(14, 0, 0), Warm, 38f, 40f, 80f);
            spill.shadows = LightShadows.Soft;
            breathers.Add(spill);
            AddLight(t, "Bay Glow", LightType.Point, new Vector3(0, 2.2f, -10.2f), Vector3.zero, Warm, 12f, 9f, 0f);

            // Nav lights, strobes and the red beacons.
            Blink(t, new Vector3(-23.1f, 7.35f, -25f), new Color(1f, 0.1f, 0.08f) * 8f, 1f, 1f, 0f, false, 0.4f, 6f);
            Blink(t, new Vector3(23.1f, 7.35f, -25f), new Color(0.1f, 1f, 0.3f) * 8f, 1f, 1f, 0f, false, 0.4f, 6f);
            Blink(t, new Vector3(-23.1f, 7.5f, -26f), Color.white * 30f, 1.3f, 0.06f, 0f, false, 0.3f, 18f);
            Blink(t, new Vector3(23.1f, 7.5f, -26f), Color.white * 30f, 1.3f, 0.06f, 0.08f, false, 0.3f, 18f);
            Blink(t, new Vector3(0, 7.05f, -12.6f), new Color(1f, 0.08f, 0.05f) * 14f, 1.1f, 1f, 0f, true, 0.45f, 10f);
            Blink(t, new Vector3(0, 13.7f, -16.5f), new Color(1f, 0.08f, 0.05f) * 14f, 1.1f, 1f, 0.5f, true, 0.4f, 0f);

            // Parked at an angle: from behind the camera you see the body, wing and tail in perspective, not a flat wall.
            t.RotateAround(new Vector3(0f, 0f, -4.5f), Vector3.up, planeYaw);
        }


        const string LifterPath = "Assets/ARCADE - Ultimate Vehicles Pack/Prefabs (Meshes Only)/Air/Aglaea.prefab";
        const float lifterScale = 1.8f;
        // Parked side-on further back, nose to the left, in the open part of the shot between the logo and The Feed,
        // so its whole outline (two rotors, long body, tail ramp) reads at a glance.
        static readonly Vector3 lifterPos = new Vector3(-11f, 0f, -15f);
        const float lifterHeading = 235f; // nose away to the right of the shot, tail ramp turned toward the car

        /// <summary>
        /// The crew's ride: the Aglaea heavy-lift helicopter from the ARCADE pack, parked behind the car at an angle
        /// with its rotors idling and warm light spilling from the tail. False if the pack isn't installed.
        /// </summary>
        static bool Lifter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LifterPath);
            if (prefab == null) return false;
            var t = Group("Heavy Lifter");
            var heli = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t);
            heli.name = "Aglaea";
            // Nose away from the camera, tail and loading ramp toward the car. Wheels sit on the tarmac.
            heli.transform.localScale = Vector3.one * lifterScale;
            heli.transform.localRotation = Quaternion.Euler(0f, lifterHeading, 0f);
            heli.transform.localPosition = lifterPos + new Vector3(0f, 1.41f * lifterScale, 0f);
            // The pack's paint is very dark at night: a brighter copy for this scene only (the pack stays untouched).
            var paint = AssetDatabase.LoadAssetAtPath<Material>(Dir + "M_SC_AglaeaPaint.mat");
            var source = heli.GetComponentInChildren<Renderer>().sharedMaterial;
            if (paint == null && source != null) { paint = new Material(source); AssetDatabase.CreateAsset(paint, Dir + "M_SC_AglaeaPaint.mat"); }
            if (paint != null)
            {
                paint.SetColor("_BaseColor", new Color(2.3f, 2.3f, 2.5f));
                paint.SetFloat("_Smoothness", 0.55f);
                paint.SetFloat("_Metallic", 0.3f);
                EditorUtility.SetDirty(paint);
            }
            foreach (var r in heli.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                if (paint != null) { var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = paint; r.sharedMaterials = mats; }
            }
            foreach (var n in new[] { "FrontPropeller", "RearPropeller" })
                foreach (var tr in heli.GetComponentsInChildren<Transform>()) if (tr.name == n) rotors.Add(tr);

            var h = heli.transform;
            // Light pouring out of the open tail onto the car.
            Vector3 tail = h.TransformPoint(new Vector3(0f, 0.4f, -5.6f));
            Vector3 tailDir = Vector3.ProjectOnPlane(h.TransformDirection(Vector3.back), Vector3.up).normalized;
            var spill = AddLight(t, "Tail Spill", LightType.Spot, tail + Vector3.up * 1.2f, Quaternion.LookRotation(tailDir + Vector3.down * 0.3f).eulerAngles, Warm, 34f, 34f, 82f);
            spill.shadows = LightShadows.Soft;
            breathers.Add(spill);
            AddLight(t, "Tail Glow", LightType.Point, tail, Vector3.zero, Warm, 10f, 9f, 0f);
            // A cool wash on the hull from the camera side so the helicopter reads against the night.
            Vector3 centre = h.TransformPoint(new Vector3(0f, 0.8f, 0f));
            Vector3 from = centre + new Vector3(-2f, 10f, 24f);
            var wash = AddLight(t, "Hull Wash", LightType.Spot, from, Quaternion.LookRotation(centre - from).eulerAngles, new Color(0.66f, 0.76f, 1f), 160f, 60f, 44f);
            wash.renderMode = LightRenderMode.ForcePixel; // never dropped by the per-object light limit
            var rim = AddLight(t, "Hull Rim", LightType.Spot, centre + new Vector3(-4f, 9f, -18f), Quaternion.LookRotation(centre - (centre + new Vector3(-4f, 9f, -18f))).eulerAngles, Warm, 90f, 50f, 50f);
            rim.renderMode = LightRenderMode.ForcePixel;

            // Beacons and strobes.
            Blink(t, h.TransformPoint(new Vector3(0f, 2.15f, 0.3f)), new Color(1f, 0.08f, 0.05f) * 14f, 1.1f, 1f, 0f, true, 0.35f, 10f);
            Blink(t, h.TransformPoint(new Vector3(0f, -1.3f, 1.5f)), new Color(1f, 0.08f, 0.05f) * 14f, 1.1f, 1f, 0.5f, true, 0.3f, 6f);
            Blink(t, h.TransformPoint(new Vector3(0f, 2.95f, -4.2f)), Color.white * 30f, 1.3f, 0.06f, 0f, false, 0.25f, 14f);
            Blink(t, h.TransformPoint(new Vector3(-1.5f, 0.6f, 3.6f)), new Color(1f, 0.1f, 0.08f) * 8f, 1f, 1f, 0f, false, 0.2f, 0f);
            Blink(t, h.TransformPoint(new Vector3(1.5f, 0.6f, 3.6f)), new Color(0.1f, 1f, 0.3f) * 8f, 1f, 1f, 0f, false, 0.2f, 0f);

            return true;
        }

        static Color BayPixel(float u, float v)
        {
            // Looking down a lit hold in perspective: concentric frame ribs, a glowing far wall,
            // a strip light along the ceiling and a bright deck.
            float dx = (u - 0.5f) * 2f, dy = (v - 0.4f) / 0.6f;
            float r = Mathf.Clamp01(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)));   // 0 far wall .. 1 at the door
            float z = 1f / (r + 0.18f);                                         // depth into the hold
            float glow = Mathf.Lerp(1.15f, 0.22f, Mathf.Pow(r, 0.8f));
            if (r < 0.22f) glow = 1.6f;                                         // far wall
            bool rib = r > 0.24f && Mathf.Repeat(z * 1.6f, 1f) < 0.16f;
            if (rib) glow *= 0.35f;
            bool ceilingLight = dy > 0f && Mathf.Abs(dy) >= Mathf.Abs(dx) && Mathf.Abs(dx) < 0.025f * r + 0.004f;
            if (ceilingLight && !rib) glow = Mathf.Max(glow, 1.05f);
            bool deck = dy < 0f && Mathf.Abs(dy) >= Mathf.Abs(dx);
            if (deck) glow *= 1.25f;
            if (deck && Mathf.Abs(Mathf.Abs(dx) - 0.55f * r) < 0.012f * r + 0.004f) glow = 1.8f;   // deck guide lines
            return new Color(1f * glow, 0.78f * glow, 0.52f * glow, 1f) * 0.62f;
        }

        static Material PropDisc()
        {
            var tex = MakeTex(Dir + "T_PropBlur.png", 128, (u, v) =>
            {
                float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float a = r > 1f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.82f, r)) * Mathf.Lerp(0.1f, 0.4f, r);
                return new Color(0.6f, 0.62f, 0.7f, a);
            }, true);
            var m = FxMat("M_SC_PropBlur", "Assets/_ShiftRush/Art/VFX/M_FX_SoftAlpha.mat", tex, new Color(0.7f, 0.72f, 0.8f, 0.55f));
            return m;
        }

        // ---------------------------------------------------------------- floodlights and beams

        static void Towers()
        {
            var t = Group("Floodlights");
            var steel = Lit("M_SC_Steel", new Color(0.1f, 0.1f, 0.12f), 0.4f, 0.6f);
            var lamp = Emissive("M_SC_Lamp", new Color(0.85f, 0.92f, 1f) * 9f);
            var beam = BeamMaterial("M_SC_BeamCool", new Color(0.55f, 0.65f, 1f, 0.2f));
            var beamWarm = BeamMaterial("M_SC_BeamWarm", new Color(1f, 0.7f, 0.4f, 0.26f));

            Tower(t, new Vector3(-12f, 0, -17f), steel, lamp, beam, new Color(0.8f, 0.88f, 1f), 120f);
            Tower(t, new Vector3(14f, 0, -21f), steel, lamp, beam, new Color(0.8f, 0.88f, 1f), 100f);

            // Sodium lamps on the far apron.
            var sodium = Emissive("M_SC_Sodium", new Color(1f, 0.55f, 0.15f) * 8f);
            foreach (var p in new[] { new Vector3(-48f, 0, -70f), new Vector3(-30f, 0, -110f), new Vector3(46f, 0, -85f), new Vector3(60f, 0, -140f) })
            {
                Prim(PrimitiveType.Cylinder, "Pole", t, p + Vector3.up * 5f, new Vector3(0.3f, 5f, 0.3f), steel);
                Bulb(t, p + Vector3.up * 10.2f, 0.9f, sodium);
                AddLight(t, "Sodium", LightType.Point, p + Vector3.up * 9.5f, Vector3.zero, new Color(1f, 0.6f, 0.25f), 18f, 30f, 0f);
            }
        }

        static void Tower(Transform t, Vector3 at, Material steel, Material lamp, Material beam, Color color, float intensity)
        {
            const float h = 15f;
            Prim(PrimitiveType.Cylinder, "Mast", t, at + Vector3.up * h * 0.5f, new Vector3(0.45f, h * 0.5f, 0.45f), steel);
            Vector3 head = at + Vector3.up * h;
            Vector3 aim = new Vector3(0f, 0.6f, 0f) - head;
            var rot = Quaternion.LookRotation(aim);
            var box = Prim(PrimitiveType.Cube, "Lamp Head", t, head, new Vector3(2.6f, 1.3f, 0.5f), steel);
            box.transform.rotation = rot;
            var face = Prim(PrimitiveType.Quad, "Lamp Face", box.transform, new Vector3(0, 0, 0.51f), new Vector3(0.9f, 0.8f, 1f), lamp, new Vector3(0, 180, 0));
            face.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var l = AddLight(t, "Flood", LightType.Spot, head, rot.eulerAngles, color, intensity, 60f, 34f);
            l.shadows = LightShadows.Soft;
            breathers.Add(l);
            Beam(t, "Flood Beam", head, aim.normalized, 0.9f, Mathf.Tan(17f * Mathf.Deg2Rad) * aim.magnitude * 2f, beam, aim.magnitude * 1.05f);
        }

        static Material BeamMaterial(string name, Color tint)
        {
            var tex = MakeTex(Dir + "T_LightShaft.png", 256, (u, v) =>
            {
                float across = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2.2f);
                float along = Mathf.Pow(1f - v, 1.4f) * Mathf.SmoothStep(0f, 1f, v * 12f);
                float a = across * along;
                return new Color(1f, 1f, 1f, a);
            }, true);
            return FxMat(name, "Assets/_ShiftRush/Art/VFX/M_FX_SoftAdd.mat", tex, tint);
        }

        /// <summary>Three crossed trapezoid cards from <paramref name="from"/> along <paramref name="dir"/>.</summary>
        static void Beam(Transform t, string name, Vector3 from, Vector3 dir, float startWidth, float endWidth, Material m, float length = 9f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.transform.localPosition = from;
            go.transform.localRotation = Quaternion.LookRotation(dir.normalized);
            go.AddComponent<MeshFilter>().sharedMesh = BeamMesh(startWidth, endWidth, length, name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static Mesh BeamMesh(float w0, float w1, float len, string name)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();
            for (int k = 0; k < 3; k++)
            {
                var q = Quaternion.Euler(0, 0, k * 60f);
                int b = verts.Count;
                verts.Add(q * new Vector3(-w0 * 0.5f, 0, 0)); uvs.Add(new Vector2(0, 0));
                verts.Add(q * new Vector3(w0 * 0.5f, 0, 0)); uvs.Add(new Vector2(1, 0));
                verts.Add(q * new Vector3(-w1 * 0.5f, 0, len)); uvs.Add(new Vector2(0, 1));
                verts.Add(q * new Vector3(w1 * 0.5f, 0, len)); uvs.Add(new Vector2(1, 1));
                for (int i = 0; i < 4; i++) cols.Add(Color.white);
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3, b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            string path = Dir + "Mesh_" + name.Replace(" ", "") + "_" + Mathf.RoundToInt(len * 10) + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ---------------------------------------------------------------- lights on the car, police

        static void CarLights()
        {
            var t = Group("Car Lights");
            AddLight(t, "Rim Magenta", LightType.Point, new Vector3(-4.8f, 1.3f, -3.2f), Vector3.zero, Magenta, 22f, 10f, 0f);
            AddLight(t, "Rim Cyan", LightType.Point, new Vector3(4.8f, 1.1f, -2.6f), Vector3.zero, Cyan, 16f, 10f, 0f);
            AddLight(t, "Camera Fill", LightType.Spot, new Vector3(2.5f, 3.5f, 9f), new Vector3(16, 195, 0), new Color(0.75f, 0.82f, 1f), 18f, 25f, 40f);

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                {
                    l.color = Moon;
                    l.intensity = 0.45f;
                    l.transform.rotation = Quaternion.Euler(24f, 18f, 0f); // from behind the plane: a moonlit rim
                    l.shadows = LightShadows.Soft;
                }
        }

        static void Police()
        {
            var t = Group("Police Far Off");
            var redM = Emissive("M_SC_PoliceRed", new Color(1f, 0.05f, 0.08f) * 14f);
            var blueM = Emissive("M_SC_PoliceBlue", new Color(0.1f, 0.25f, 1f) * 14f);
            Vector3 c = new Vector3(-52f, 0.9f, -120f);
            Blink(t, c, new Color(1f, 0.05f, 0.08f) * 14f, 0.5f, 0.45f, 0f, false, 0.5f, 60f, redM);
            Blink(t, c + new Vector3(1.4f, 0, 0), new Color(0.1f, 0.25f, 1f) * 14f, 0.5f, 0.45f, 0.5f, false, 0.5f, 60f, blueM);
            Vector3 c2 = new Vector3(-61f, 0.9f, -134f);
            Blink(t, c2, new Color(1f, 0.05f, 0.08f) * 14f, 0.44f, 0.45f, 0.25f, false, 0.5f, 40f, redM);
            Blink(t, c2 + new Vector3(1.4f, 0, 0), new Color(0.1f, 0.25f, 1f) * 14f, 0.44f, 0.45f, 0.75f, false, 0.5f, 40f, blueM);
        }

        /// <summary>Hangars either side of the apron with lit doors, and a low city skyline breaking the horizon.</summary>
        static void Skyline()
        {
            var t = Group("Skyline");
            var shed = Lit("M_SC_Hangar", new Color(0.07f, 0.075f, 0.09f), 0.3f, 0.2f);
            var door = Emissive("M_SC_HangarDoor", new Color(1f, 0.78f, 0.55f) * 0.55f);
            var window = Emissive("M_SC_Window", new Color(1f, 0.75f, 0.45f) * 3f);
            var red = Emissive("M_SC_Aviation", new Color(1f, 0.1f, 0.06f) * 10f);

            foreach (var h in new[] { new Vector4(-46f, 0, -60f, 1), new Vector4(-58f, 0, -105f, 0), new Vector4(52f, 0, -72f, 1), new Vector4(66f, 0, -125f, 0) })
            {
                var at = new Vector3(h.x, 0, h.z);
                Prim(PrimitiveType.Cube, "Hangar", t, at + Vector3.up * 7f, new Vector3(26f, 14f, 30f), shed);
                Prim(PrimitiveType.Cylinder, "Roof", t, at + Vector3.up * 14f, new Vector3(26f, 15f, 9f), shed, new Vector3(90, 0, 0));
                if (h.w > 0.5f)
                {
                    float face = h.x < 0 ? 13.05f : -13.05f;
                    Prim(PrimitiveType.Quad, "Lit Door", t, at + new Vector3(face, 5f, 0f), new Vector3(16f, 9f, 1f), door, new Vector3(0, h.x < 0 ? -90f : 90f, 0));
                    AddLight(t, "Door Spill", LightType.Point, at + new Vector3(face * 1.25f, 4f, 0f), Vector3.zero, new Color(1f, 0.78f, 0.55f), 10f, 26f, 0f);
                }
            }

            // City: a painted skyline card far beyond the runway. Sprite shaders skip fog, so the silhouette
            // stays readable through the haze while everything nearer fades into it.
            var cityTex = CityTexture();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            string cityPath = Dir + "M_SC_City.mat";
            var city = AssetDatabase.LoadAssetAtPath<Material>(cityPath);
            if (city == null) { city = new Material(shader); AssetDatabase.CreateAsset(city, cityPath); }
            city.shader = shader;
            city.mainTexture = cityTex;
            city.color = Color.white;
            EditorUtility.SetDirty(city);
            foreach (var card in new[] { new Vector4(0f, -430f, 1300f, 0f), new Vector4(-420f, -330f, 700f, 50f), new Vector4(420f, -330f, 700f, -50f) })
            {
                var g = Prim(PrimitiveType.Quad, "City Skyline", t, new Vector3(card.x, card.z * 0.0625f - 4f, card.y), new Vector3(card.z, card.z * 0.125f, 1f), city, new Vector3(0, 180f + card.w, 0));
                var r = g.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static Texture2D CityTexture()
        {
            const int W = 2048, H = 256;
            var rnd = new System.Random(13);
            var heights = new float[W];
            var lit = new bool[W / 4, H / 4];
            int x = 0;
            while (x < W)
            {
                int w = 18 + rnd.Next(46);
                bool centre = Mathf.Abs(x - W * 0.5f) < W * 0.22f;
                float h = 0.08f + (float)rnd.NextDouble() * (centre ? 0.4f : 0.22f);
                if (rnd.NextDouble() < 0.12) h += 0.2f;
                for (int i = x; i < Mathf.Min(W, x + w); i++) heights[i] = h;
                x += w + (rnd.NextDouble() < 0.3 ? rnd.Next(6) : 0);
            }
            for (int i = 0; i < W / 4; i++) for (int j = 0; j < H / 4; j++) lit[i, j] = rnd.NextDouble() < 0.1;

            var px = new Color[W * H];
            var body = new Color(0.085f, 0.1f, 0.18f, 1f);
            var glow = new Color(1f, 0.55f, 0.35f);
            for (int yy = 0; yy < H; yy++)
                for (int xx = 0; xx < W; xx++)
                {
                    float v = (yy + 0.5f) / H;
                    Color c = new Color(0, 0, 0, 0);
                    if (v < heights[xx])
                    {
                        c = body;
                        bool window = (xx % 4) < 2 && (yy % 4) < 2 && lit[xx / 4, yy / 4] && v < heights[xx] - 0.02f;
                        if (window) c = Color.Lerp(body, new Color(1f, 0.78f, 0.5f), 0.45f);
                        c.a = 1f;
                    }
                    // Warm light pollution hugging the bottom of the skyline.
                    float haze = Mathf.Pow(Mathf.Clamp01(1f - v * 2.4f), 2f) * 0.35f;
                    if (haze > 0f) { c = Color.Lerp(c, glow, haze * (c.a > 0f ? 0.4f : 1f)); c.a = Mathf.Max(c.a, haze); }
                    px[yy * W + xx] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = Dir + "T_CitySkyline.png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The turning pad: dark brushed metal so the bay light doesn't wash it out, ringed in pink.</summary>
        static void Pad(Showroom showroom)
        {
            var plinth = showroom.transform.Find("Turntable/Plinth");
            if (plinth != null) plinth.GetComponent<Renderer>().sharedMaterial = Lit("M_SC_Pad", new Color(0.045f, 0.045f, 0.055f), 0.55f, 0.4f);
            var ring = showroom.transform.Find("Glow Ring");
            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                ring.localScale = new Vector3(6.75f, 0.02f, 6.75f);
                ring.GetComponent<Renderer>().sharedMaterial = Emissive("M_SC_PadRing", Magenta * 3f);
            }
        }

        // ---------------------------------------------------------------- rain, mist, sky, grade

        static void Atmosphere()
        {
            var t = Group("Atmosphere");
            Spawn("Assets/FayLabs/Kroma/Content/Prefabs/Kroma_Particle_Rain_V2_Prefab.prefab", t, new Vector3(0, 14f, -2f));
            Spawn("Assets/FayLabs/Kroma/Content/Prefabs/Kroma_Particle_GroundMovingFog_Blue_Prefab.prefab", t, new Vector3(0, 0f, -14f));
            Spawn("Assets/FayLabs/Kroma/Content/Prefabs/Kroma_Particle_GroundMovingFog_Blue_Prefab.prefab", t, new Vector3(-14f, 0f, -30f));
            var motes = Spawn("Assets/_ShiftRush/Art/VFX/P_Motes.prefab", t, new Vector3(0, 2.5f, -7f));
            if (motes != null)
                foreach (var ps in motes.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startSizeMultiplier *= 0.22f; // fine dust drifting in the bay light, not snow
                }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.045f, 0.055f, 0.105f);
            RenderSettings.fogDensity = 0.014f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.12f);
        }

        static void Sky()
        {
            const string skyPath = Dir + "M_SC_NightSky.mat";
            AssetDatabase.DeleteAsset(skyPath);
            if (!AssetDatabase.CopyAsset("Assets/FayLabs/Kroma/Content/Skybox/Kroma_Skybox_Day.mat", skyPath)) return;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            foreach (var preset in new[] { "Master/02_Cloudy_Moonlight", "Skybox Colors/02_Night" })
                ApplyKromaPreset(sky, "Assets/FayLabs/Kroma/Content/Shaders/Presets/" + preset + ".asset");
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 600f; }
        }

        /// <summary>Kroma presets are lists of shader property values; copy each one the material has.</summary>
        static void ApplyKromaPreset(Material m, string path)
        {
            var preset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (preset == null) { Debug.LogWarning("[Showcase] Missing Kroma preset " + path); return; }
            var list = new SerializedObject(preset).FindProperty("properties");
            if (list == null) return;
            var shader = m.shader;
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                string name = e.FindPropertyRelative("name").stringValue;
                int idx = shader.FindPropertyIndex(name);
                if (idx < 0) continue;
                switch (shader.GetPropertyType(idx))
                {
                    case ShaderPropertyType.Color: m.SetColor(name, e.FindPropertyRelative("colorValue").colorValue); break;
                    case ShaderPropertyType.Vector: m.SetVector(name, e.FindPropertyRelative("vectorValue").vector4Value); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        m.SetFloat(name, e.FindPropertyRelative("floatValue").floatValue);
                        break;
                    case ShaderPropertyType.Texture:
                        var tex = e.FindPropertyRelative("textureValue").objectReferenceValue as Texture;
                        if (tex != null) m.SetTexture(name, tex);
                        break;
                }
            }
        }

        static void Look(Showroom showroom)
        {
            var vol = showroom.GetComponentInChildren<Volume>(true);
            if (vol == null || vol.sharedProfile == null) return;
            var p = vol.sharedProfile;
            Get<Bloom>(p, b => { b.intensity.Override(1.25f); b.threshold.Override(1.05f); b.scatter.Override(0.72f); });
            Get<Vignette>(p, v => { v.intensity.Override(0.42f); v.smoothness.Override(0.45f); });
            Get<FilmGrain>(p, g => { g.type.Override(FilmGrainLookup.Medium3); g.intensity.Override(0.28f); g.response.Override(0.75f); });
            Get<ChromaticAberration>(p, c => c.intensity.Override(0.12f));
            Get<ColorAdjustments>(p, c => { c.contrast.Override(18f); c.saturation.Override(8f); c.postExposure.Override(0.15f); });
            Get<SplitToning>(p, s => { s.shadows.Override(new Color(0.32f, 0.38f, 0.62f)); s.highlights.Override(new Color(0.72f, 0.56f, 0.42f)); s.balance.Override(-15f); });
            Get<WhiteBalance>(p, w => w.temperature.Override(-6f));
            EditorUtility.SetDirty(p);
        }

        static bool remembered;
        static Vector3 rememberedPos, rememberedScale = Vector3.one;
        static Quaternion rememberedRot = Quaternion.identity;

        /// <summary>
        /// Before Build Main Menu replaces the scene: note where the stream screen stands in the saved menu scene, so
        /// the rebuilt set puts it back in the same place.
        /// </summary>
        public static void RememberScreen(string scenePath)
        {
            remembered = false;
            var active = EditorSceneManager.GetActiveScene();
            UnityEngine.SceneManagement.Scene scene = active;
            bool opened = false;
            if (active.path != scenePath)
            {
                if (!System.IO.File.Exists(scenePath)) return;
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                opened = true;
            }
            foreach (var go in scene.GetRootGameObjects())
            {
                var t = go.GetComponentInChildren<Showroom>(true);
                var screen = t != null ? t.transform.Find("Showcase Set/Stream Screen") : null;
                if (screen == null) continue;
                rememberedPos = screen.localPosition; rememberedRot = screen.localRotation; rememberedScale = screen.localScale;
                remembered = true;
                break;
            }
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        // ---------------------------------------------------------------- time of day and the stream screen

        /// <summary>Night (as built), Dusk and Day looks the garage can switch between (ShowroomTime).</summary>
        static void TimeOfDay(ShowcaseLights animated)
        {
            var time = root.gameObject.AddComponent<ShowroomTime>();
            var night = RenderSettings.skybox;
            var dusk = KromaSky("M_SC_DuskSky", "Master/04_Sunrise", "Skybox Colors/04_Sunset");
            var day = KromaSky("M_SC_DaySky", "Master/01_Daylight", "Skybox Colors/01_Day");
            time.looks = new[]
            {
                new ShowroomTime.Look { name = "NIGHT", sky = night, sun = Moon, sunIntensity = 0.45f, sunAngles = new Vector3(24f, 18f, 0f),
                    ambient = new Color(0.07f, 0.08f, 0.12f), fog = new Color(0.045f, 0.055f, 0.105f), fogDensity = 0.014f, lamps = 1f, nightPieces = true, bakedReflections = true, groundLift = 1f, exposure = 0.15f },
                new ShowroomTime.Look { name = "DUSK", sky = dusk != null ? dusk : night, sun = new Color(1f, 0.6f, 0.38f), sunIntensity = 1.2f, sunAngles = new Vector3(9f, 225f, 0f),
                    ambient = new Color(0.3f, 0.22f, 0.27f), fog = new Color(0.42f, 0.29f, 0.3f), fogDensity = 0.007f, lamps = 0.55f, nightPieces = false, bakedReflections = false, groundLift = 2.6f, exposure = 0.35f },
                new ShowroomTime.Look { name = "DAY", sky = day != null ? day : night, sun = new Color(1f, 0.96f, 0.9f), sunIntensity = 1.5f, sunAngles = new Vector3(48f, 150f, 0f),
                    ambient = new Color(0.5f, 0.56f, 0.64f), fog = new Color(0.63f, 0.72f, 0.82f), fogDensity = 0.0035f, lamps = 0.12f, nightPieces = false, bakedReflections = false, groundLift = 5f, exposure = 0.55f },
            };
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { time.sun = l; break; }
            if (time.sun == null)
            {
                // The menu scene has no sun of its own: moonlight at night, the sun by day.
                time.sun = AddLight(root, "Sun", LightType.Directional, Vector3.zero, new Vector3(24f, 18f, 0f), Moon, 0.45f, 10f, 0f);
                time.sun.shadows = LightShadows.Soft;
            }

            // Lamps the time dims: every set light except the sun and the ones ShowcaseLights animates.
            var animatedLights = new HashSet<Light>();
            foreach (var b in animated.blinkers) if (b.light != null) animatedLights.Add(b.light);
            foreach (var l in animated.breathers) if (l != null) animatedLights.Add(l);
            var lamps = new List<Light>();
            foreach (var l in root.GetComponentsInChildren<Light>(true)) if (l != time.sun && !animatedLights.Contains(l)) lamps.Add(l);
            time.lamps = lamps.ToArray();
            time.animated = animated;

            // Night-only pieces: rain, the floodlight beams.
            var nightOnly = new List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("M_SC_Beam")) nightOnly.Add(r.gameObject);
            var atmo = root.Find("Atmosphere");
            if (atmo != null) foreach (Transform c in atmo) if (c.name.Contains("Rain")) nightOnly.Add(c.gameObject);
            time.nightPieces = nightOnly.ToArray();
            time.probe = root.GetComponentInChildren<ReflectionProbe>(true);
            var tarmac = root.Find("Wet Tarmac");
            if (tarmac != null) time.ground = tarmac.GetComponent<Renderer>();
            time.volume = root.parent.GetComponentInChildren<Volume>(true);
        }

        static Material KromaSky(string name, string master, string colours)
        {
            string path = Dir + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset("Assets/FayLabs/Kroma/Content/Skybox/Kroma_Skybox_Day.mat", path)) return null;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            foreach (var preset in new[] { master, colours }) ApplyKromaPreset(sky, "Assets/FayLabs/Kroma/Content/Shaders/Presets/" + preset + ".asset");
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>A big Channel 13 screen on legs behind the car, where the ramp used to glow, running its own broadcast.</summary>
        static void Screen(Showroom showroom)
        {
            var t = Group("Stream Screen");
            t.localPosition = new Vector3(-6.2f, 0f, -8.6f);
            t.localRotation = Quaternion.Euler(0f, 214f, 0f); // its face (-Z) turned back toward the garage camera
            var steel = Lit("M_SC_Steel", new Color(0.1f, 0.1f, 0.12f), 0.4f, 0.6f);
            Prim(PrimitiveType.Cube, "Leg L", t, new Vector3(-2.2f, 1.1f, 0.12f), new Vector3(0.18f, 2.2f, 0.18f), steel);
            Prim(PrimitiveType.Cube, "Leg R", t, new Vector3(2.2f, 1.1f, 0.12f), new Vector3(0.18f, 2.2f, 0.18f), steel);
            Prim(PrimitiveType.Cube, "Frame", t, new Vector3(0f, 3.75f, 0.1f), new Vector3(6.1f, 3.5f, 0.16f), steel);
            var glass = Emissive("M_SC_ScreenGlass", new Color(0.03f, 0.02f, 0.06f));
            Prim(PrimitiveType.Quad, "Screen", t, new Vector3(0f, 3.75f, -0.002f), new Vector3(5.8f, 3.26f, 1f), glass, new Vector3(0f, 0f, 0f));
            AddLight(t, "Screen Glow", LightType.Point, new Vector3(0f, 3.4f, -1.6f), Vector3.zero, new Color(0.9f, 0.4f, 1f), 3f, 7f, 0f);

            // World-space canvas on the glass (faces the same way as the quad's visible side).
            var cgo = new GameObject("Broadcast", typeof(RectTransform), typeof(Canvas));
            cgo.transform.SetParent(t, false);
            var rt = (RectTransform)cgo.transform;
            rt.sizeDelta = new Vector2(1600, 900);
            rt.localPosition = new Vector3(0f, 3.75f, -0.01f);
            rt.localScale = Vector3.one * (5.8f / 1600f);
            cgo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var fBlack = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ShiftRush/Art/Fonts/BigShouldersDisplay-Black SDF.asset");
            var fBarlow = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ShiftRush/Art/Fonts/BarlowCondensed-SemiBold SDF.asset");
            var fMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ShiftRush/Art/Fonts/JetBrainsMono-Bold SDF.asset");

            var accent = UiImage(rt, "Accent Bar", new Color32(255, 46, 154, 255), new Vector2(0, 0), new Vector2(0, 1), new Vector2(70, 0), new Vector2(24, -120));
            var live = UiImage(rt, "Live", new Color32(232, 35, 58, 255), new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -70), new Vector2(170, 56));
            UiText(live.rectTransform, "Label", "LIVE", fMono, 34, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tagT = UiText(rt, "Tag", "CHANNEL 13", fMono, 40, new Color32(189, 183, 209, 255), TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(1, 1), new Vector2(150, -200), new Vector2(-220, 60));
            tagT.characterSpacing = 10;
            var titleT = UiText(rt, "Title", "GRIP PRO", fBlack, 230, Color.white, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(1, 1), new Vector2(150, -460), new Vector2(-220, 260));
            titleT.enableAutoSizing = true; titleT.fontSizeMin = 90; titleT.fontSizeMax = 230; titleT.textWrappingMode = TextWrappingModes.NoWrap;
            var lineT = UiText(rt, "Line", "", fBarlow, 54, new Color32(220, 214, 236, 255), TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(150, -560), new Vector2(-220, 140));
            var band = UiImage(rt, "Ticker Band", new Color32(11, 10, 18, 230), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 50), new Vector2(0, 100));
            var tick = UiText(band.rectTransform, "Ticker", "", fMono, 32, new Color32(46, 242, 255, 255), TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(40, 0), new Vector2(-60, 0));
            tick.textWrappingMode = TextWrappingModes.NoWrap; tick.overflowMode = TextOverflowModes.Ellipsis;
            var flash = UiImage(rt, "Cut Flash", new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var screen = t.gameObject.AddComponent<StreamScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("showroom").objectReferenceValue = showroom;
            so.FindProperty("tag").objectReferenceValue = tagT;
            so.FindProperty("title").objectReferenceValue = titleT;
            so.FindProperty("line").objectReferenceValue = lineT;
            so.FindProperty("ticker").objectReferenceValue = tick;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("accent").objectReferenceValue = accent;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static UnityEngine.UI.Image UiImage(RectTransform parent, string name, Color c, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = new Vector2(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI UiText(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Color c, TextAlignmentOptions align, Vector2 min, Vector2 max, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; if (font != null) t.font = font; t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = new Vector2(0f, min.y == max.y ? min.y : 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        static void Get<T>(VolumeProfile p, System.Action<T> set) where T : VolumeComponent
        {
            if (!p.TryGet(out T c))
            {
                c = p.Add<T>(true);
                AssetDatabase.AddObjectToAsset(c, p);
            }
            c.active = true;
            set(c);
            EditorUtility.SetDirty(c);
        }

        static void Probe()
        {
            var go = new GameObject("Wet Reflections");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, 1.2f, -4f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(160f, 60f, 220f);
            probe.resolution = 256;
            probe.hdr = true;
            probe.farClipPlane = 600f;
            probe.mode = ReflectionProbeMode.Baked;
            string path = Dir + "Cube_WetReflections.exr";
            // Hide the pad and its ring while baking: right next to the probe they would tint every reflection pink.
            var hidden = new List<GameObject>();
            var showroom = root.parent;
            foreach (var n in new[] { "Glow Ring", "Turntable" })
            {
                var h = showroom.Find(n);
                if (h != null && h.gameObject.activeSelf) { h.gameObject.SetActive(false); hidden.Add(h.gameObject); }
            }
            bool baked = Lightmapping.BakeReflectionProbe(probe, path);
            foreach (var h in hidden) h.SetActive(true);
            if (baked)
            {
                AssetDatabase.ImportAsset(path);
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            }
        }

        /// <summary>URP clears the emission keyword when a new material is first saved; put it back on every glowing one.</summary>
        static void FixEmission()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_ShiftRush/Art/Showcase" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                var c = m.GetColor("_EmissionColor");
                if (c.maxColorComponent <= 0f) continue;
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- helpers

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default(Vector3))
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Bulb(Transform t, Vector3 pos, float size, Material m)
        {
            var g = Prim(PrimitiveType.Sphere, "Bulb", t, pos, Vector3.one * size, m);
            var r = g.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }

        static void Blink(Transform t, Vector3 pos, Color color, float period, float duty, float phase, bool smooth, float size, float lightIntensity, Material shared = null)
        {
            var m = shared != null ? shared : Emissive("M_SC_Blink", Color.white * 8f);
            var g = Bulb(t, pos, size, m);
            g.name = "Blinker";
            Light l = null;
            if (lightIntensity > 0f) l = AddLight(g.transform.parent, "Blink Light", LightType.Point, pos, Vector3.zero, color / Mathf.Max(1f, color.maxColorComponent), lightIntensity, Mathf.Clamp(lightIntensity * 0.6f, 6f, 26f), 0f);
            blinkers.Add(new ShowcaseLights.Blinker { bulb = g.GetComponent<Renderer>(), light = l, color = color, period = period, duty = duty, phase = phase, smooth = smooth });
        }

        static Light AddLight(Transform parent, string name, LightType type, Vector3 pos, Vector3 euler, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            if (type == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = angle * 0.55f; }
            l.shadows = LightShadows.None;
            return l;
        }

        static GameObject Spawn(string path, Transform parent, Vector3 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[Showcase] Missing " + path); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.transform.localPosition = pos;
            return g;
        }

        static Material Lit(string name, Color color, float smooth, float metal)
        {
            string path = Dir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Emissive(string name, Color hdr, Texture tex = null)
        {
            // Unlit with an HDR colour: glows through bloom without relying on the emission keyword,
            // which this project's material import keeps clearing.
            string path = Dir + name + ".mat";
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(unlit); AssetDatabase.CreateAsset(m, path); }
            m.shader = unlit;
            m.SetColor("_BaseColor", hdr);
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material FxMat(string name, string templatePath, Texture tex, Color tint)
        {
            string path = Dir + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(templatePath, path);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Value noise on a lattice that wraps every <paramref name="period"/> cells, so it tiles.</summary>
        static float ValueNoise(float u, float v, int period)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period), b = Hash(x0 + 1, y0, period), c = Hash(x0, y0 + 1, period), d = Hash(x0 + 1, y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int period)
        {
            x = ((x % period) + period) % period; y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + period * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        static float Fbm(float u, float v, int period, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++) { sum += ValueNoise(u, v, period << i) * amp; norm += amp; amp *= 0.5f; }
            return sum / norm;
        }

        static float Periodic(float u, float v, int fu, int fv)
        {
            float s = Mathf.Sin((u * fu + v * fv) * Mathf.PI * 2f + fu * 1.7f) * 0.5f
                    + Mathf.Sin((u * fv - v * fu) * Mathf.PI * 2f + fv * 2.3f) * 0.3f
                    + Mathf.Sin((u * (fu + fv)) * Mathf.PI * 2f + v * fu * Mathf.PI * 2f) * 0.2f;
            return s * 0.5f + 0.5f;
        }

        static Texture2D MakeTex(string path, int size, System.Func<float, float, Color> pixel, bool clamp)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.alphaIsTransparency = clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
