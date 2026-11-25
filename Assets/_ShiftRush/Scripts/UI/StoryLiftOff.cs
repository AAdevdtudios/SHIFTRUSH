using System.Collections;
using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// The story mode's ride to the race, played on the garage set before the loading screen. The Heavy Lifter's
    /// rotors spin up and it lifts off, your car launches down the runway flat out, the chopper drops a magnet on a
    /// cable, the car catches up under it and CLANG, it is snatched off the tarmac and carried off into the night.
    /// Everything is built from what the set already has (the chopper, the runway, the car on the plinth), plus a
    /// magnet made of primitives and synthesised sound. Any key skips it.
    /// </summary>
    public class StoryLiftOff : MonoBehaviour
    {
        // Shot timings, in seconds from the start.
        const float LaunchAt = 3.0f, ChaseAt = 5.6f, HookAt = 8.0f, AirborneAt = 9.0f, End = 11.6f;
        const float Lane = 6.5f;        // runs down the right of the runway, clear of the stream screen on the centreline
        const float TopSpeed = 52f;     // m/s, about 190 km/h
        const float Accel = 19f;

        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);

        StoryCatalog.Episode episode;
        System.Func<bool> start;
        Showroom showroom;
        Camera cam;
        Transform parked, chopper, frontRotor, rearRotor, car, magnet, cable;
        Vector3 frontHub, rearHub, hookLocal;
        Pose chopperHome, camHome;
        bool parkedWasOn;
        Transform camParentHome;
        float camFovHome;
        CameraClearFlags clearHome;
        Color bgHome;
        readonly System.Collections.Generic.List<Behaviour> paused = new System.Collections.Generic.List<Behaviour>();
        readonly System.Collections.Generic.List<Transform> wheels = new System.Collections.Generic.List<Transform>();
        CanvasGroup overlay, card;
        Image black, flash;
        RectTransform barTop, barBottom;
        AudioSource rotorAudio, engineAudio;
        Light hookFlash;
        float t, rotorSpin, carZ, carSpeed, shake, rotorAngle, wheelAngle;
        bool skipping, finished;
        Vector3 carStart;
        float roofY, hookZ, hangLen;
        GameObject plinthCar;

        /// <summary>Plays the lift-off, then calls done (which starts the race). False if the set isn't here: start straight away.</summary>
        public static bool Running { get; private set; }

        public static bool Run(StoryCatalog.Episode e, System.Func<bool> start)
        {
            if (Running) return true;
            var room = FindFirstObjectByType<Showroom>();
            if (room == null || room.CurrentCar == null || Camera.main == null) return false;
            var lifter = room.transform.Find("Showcase Set/Heavy Lifter");
            var parked = lifter != null ? lifter.Find("Aglaea") : null;
            var story = StoryCatalog.Instance;
            if (parked == null || story == null || story.liftChopper == null) return false;
            var go = new GameObject("Story Lift-Off");
            var s = go.AddComponent<StoryLiftOff>();
            s.episode = e; s.start = start; s.showroom = room; s.parked = parked;
            // The parked one is static-batched (moving it moves nothing on screen): fly a copy in its place.
            s.chopper = Instantiate(story.liftChopper).transform;
            s.chopper.name = "Lift-Off Heavy Lifter";
            s.chopper.SetPositionAndRotation(parked.position, parked.rotation);
            s.chopper.localScale = parked.lossyScale;
            // Same paint as the parked one (the set uses a brighter copy of the pack's).
            foreach (var r in s.chopper.GetComponentsInChildren<Renderer>(true))
            {
                var twin = FindDeep(parked, r.name);
                var tr = twin != null ? twin.GetComponent<Renderer>() : null;
                if (tr != null) r.sharedMaterials = tr.sharedMaterials;
            }
            Running = true;
            return s.Setup();
        }

        void OnDestroy() { Running = false; }

        bool Setup()
        {
            cam = Camera.main;
            camParentHome = cam.transform.parent;
            camHome = new Pose(cam.transform.position, cam.transform.rotation);
            camFovHome = cam.fieldOfView;
            clearHome = cam.clearFlags; bgHome = cam.backgroundColor;
            // The set's skybox is a day sky the garage never looks up at: these shots do, so it's night above.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(9, 7, 20, 255);
            chopperHome = new Pose(chopper.position, chopper.rotation);
            parkedWasOn = parked.gameObject.activeSelf;
            parked.gameObject.SetActive(false);
            frontRotor = FindDeep(chopper, "FrontPropeller");
            rearRotor = FindDeep(chopper, "RearPropeller");
            frontHub = frontRotor != null ? chopper.InverseTransformPoint(frontRotor.GetComponent<Renderer>().bounds.center) : Vector3.zero;
            rearHub = rearRotor != null ? chopper.InverseTransformPoint(rearRotor.GetComponent<Renderer>().bounds.center) : Vector3.zero;
            var body = FindDeep(chopper, "Body");
            var bodyR = body != null ? body.GetComponent<Renderer>() : null;
            Vector3 belly = bodyR != null ? bodyR.bounds.center - Vector3.up * bodyR.bounds.extents.y * 0.85f : chopper.position;
            hookLocal = chopper.InverseTransformPoint(belly);

            // The garage stops: no orbiting camera, no menu on screen.
            Pause(showroom);
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && c.enabled && c.gameObject.scene == gameObject.scene) Pause(c);
            cam.transform.SetParent(null, true);

            BuildCar();
            BuildMagnet();
            BuildOverlay();
            BuildAudio();
            StartCoroutine(Play());
            return true;
        }

        void Pause(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; paused.Add(b); } }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ building

        void BuildCar()
        {
            var src = showroom.CurrentCar;
            var copy = Instantiate(src);
            copy.name = "Lift-Off Car";
            copy.SetActive(true);
            car = copy.transform;
            car.SetParent(null, false);
            car.localScale = src.transform.lossyScale;
            // The holder faces the car's nose: put it on the runway pointing down it (-Z).
            carStart = new Vector3(Lane, 0f, 4f);
            car.SetPositionAndRotation(carStart, Quaternion.LookRotation(Vector3.back));
            var rs = copy.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                car.position += new Vector3(carStart.x - b.center.x, -b.min.y, carStart.z - b.center.z);
            }
            carStart = car.position;
            roofY = carStart.y + 1.4f;
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); roofY = b.max.y; }
            // The car on the plinth is the one driving off.
            plinthCar = src;
            if (plinthCar.activeSelf) plinthCar.SetActive(false); else plinthCar = null;
            carZ = carStart.z;
            foreach (var tr in copy.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name.ToLowerInvariant();
                if (n.Contains("wheel") && !n.Contains("brake") && !n.Contains("caliper") && !n.Contains("arch") && tr.GetComponent<Renderer>() != null) wheels.Add(tr);
            }
        }

        void BuildMagnet()
        {
            // Dark gunmetal of its own (the chopper's material is a colour palette that smears on a plain cylinder).
            Material metal = null;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                metal = new Material(lit) { name = "Lift Magnet Metal" };
                metal.SetColor("_BaseColor", new Color32(38, 40, 48, 255));
                metal.SetFloat("_Metallic", 0.85f);
                metal.SetFloat("_Smoothness", 0.62f);
            }
            var ring = showroom.transform.Find("Glow Ring");
            var glow = ring != null && ring.GetComponent<Renderer>() != null ? ring.GetComponent<Renderer>().sharedMaterial : metal;

            magnet = new GameObject("Lift Magnet").transform;
            var disc = Primitive(PrimitiveType.Cylinder, magnet, metal);
            disc.localScale = new Vector3(2.2f, 0.18f, 2.2f);
            var rim = Primitive(PrimitiveType.Cylinder, magnet, glow);
            rim.localScale = new Vector3(2.35f, 0.05f, 2.35f);
            rim.localPosition = new Vector3(0f, -0.16f, 0f);
            var cap = Primitive(PrimitiveType.Cylinder, magnet, metal);
            cap.localScale = new Vector3(0.6f, 0.25f, 0.6f);
            cap.localPosition = new Vector3(0f, 0.35f, 0f);
            cable = Primitive(PrimitiveType.Cylinder, null, metal);
            cable.name = "Lift Cable";
            hookFlash = new GameObject("Hook Flash").AddComponent<Light>();
            hookFlash.transform.SetParent(magnet, false);
            hookFlash.type = LightType.Point;
            hookFlash.color = Cyan;
            hookFlash.range = 9f;
            hookFlash.intensity = 0f;
            magnet.gameObject.SetActive(false);
            cable.gameObject.SetActive(false);
        }

        static Transform Primitive(PrimitiveType type, Transform parent, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            if (col != null) Destroy(col);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (parent != null) g.transform.SetParent(parent, false);
            return g.transform;
        }

        void BuildOverlay()
        {
            var go = new GameObject("Lift-Off Overlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400; // under the loading screen (500), so it fades in over our black
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            overlay = go.AddComponent<CanvasGroup>();
            overlay.blocksRaycasts = false;

            // Fonts from the menu so the card matches it.
            TMP_FontAsset big = null, mono = null;
            foreach (var txt in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (big == null && txt.name == "Label" && txt.transform.parent != null && txt.transform.parent.name == "STORY Panel") big = txt.font;
                if (mono == null && txt.name == "Hint" && txt.transform.parent != null && txt.transform.parent.name == "Broadcast Menu") mono = txt.font;
            }

            barTop = Bar(go.transform, "Bar Top", true);
            barBottom = Bar(go.transform, "Bar Bottom", false);

            var cardGo = new GameObject("Card", typeof(RectTransform));
            cardGo.transform.SetParent(go.transform, false);
            card = cardGo.AddComponent<CanvasGroup>();
            card.alpha = 0f;
            var cr = (RectTransform)cardGo.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 0f); cr.pivot = new Vector2(0f, 0f);
            cr.anchoredPosition = new Vector2(72f, 150f); cr.sizeDelta = new Vector2(1200f, 220f);
            string ep = episode != null ? "EP " + episode.number.ToString("00") + "  //  " + episode.eventType : "STORY";
            var track = TrackName();
            Text(cr, "Kicker", ep, mono, 22, Cyan, new Vector2(0, 150), 10);
            var echo = Text(cr, "Title Echo", episode != null ? episode.title : "LIFT OFF", big, 96, Cyan, new Vector2(5, 42), 0);
            Text(cr, "Title", episode != null ? episode.title : "LIFT OFF", big, 96, Color.white, new Vector2(0, 48), 0);
            echo.alpha = 0.9f;
            Text(cr, "Where", "HEAVY LIFTER  //  NEXT STOP " + track, mono, 22, Magenta, new Vector2(0, 0), 8);

            var skip = Text((RectTransform)go.transform, "Skip", "ANY KEY  SKIP", mono, 18, new Color(1, 1, 1, 0.55f), Vector2.zero, 8);
            var sr = skip.rectTransform;
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(1f, 0f);
            sr.anchoredPosition = new Vector2(-60f, 40f); sr.sizeDelta = new Vector2(400f, 30f);
            skip.alignment = TextAlignmentOptions.BottomRight;

            flash = Fill(go.transform, "Flash", new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
            black = Fill(go.transform, "Black", new Color(0, 0, 0, 0f));
        }

        string TrackName()
        {
            var race = RaceCatalog.Instance;
            if (episode == null || race == null) return "THE TRACK";
            var tr = race.tracks.Find(x => x.id == episode.trackId);
            return tr != null ? tr.displayName.ToUpperInvariant() : "THE TRACK";
        }

        static RectTransform Bar(Transform parent, string name, bool top)
        {
            var img = Fill(parent, name, Color.black);
            var r = img.rectTransform;
            r.anchorMin = new Vector2(0f, top ? 1f : 0f); r.anchorMax = new Vector2(1f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, top ? 1f : 0f);
            r.sizeDelta = new Vector2(0f, 0f);
            return r;
        }

        static Image Fill(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return img;
        }

        static TMP_Text Text(RectTransform parent, string name, string s, TMP_FontAsset font, float size, Color c, Vector2 pos, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = size; t.color = c; t.characterSpacing = spacing;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.raycastTarget = false;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = pos; r.sizeDelta = new Vector2(1200f, size * 1.3f);
            return t;
        }

        // ------------------------------------------------------------------ sound

        void BuildAudio()
        {
            var group = AudioBoot.Group("SFX");
            rotorAudio = Source("Rotor", group, RotorLoop(), true);
            engineAudio = Source("Engine", group, EngineLoop(), true);
            rotorAudio.volume = 0f; engineAudio.volume = 0f;
            rotorAudio.Play(); engineAudio.Play();
        }

        AudioSource Source(string name, UnityEngine.Audio.AudioMixerGroup group, AudioClip clip, bool loop)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = clip; a.loop = loop; a.playOnAwake = false; a.spatialBlend = 0f;
            a.outputAudioMixerGroup = group;
            return a;
        }

        const int Rate = 44100;

        /// <summary>Twin rotors: a low blade thump (pitch follows the spin-up) over filtered wind.</summary>
        static AudioClip RotorLoop()
        {
            int n = Rate; var d = new float[n];
            var rng = new System.Random(13);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float time = (float)i / Rate;
                float beat = time * 9f % 1f;                  // 9 thumps a second, loops cleanly over 1 s
                float thump = Mathf.Exp(-beat * 9f) * Mathf.Sin(2f * Mathf.PI * 52f * time);
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.05f;
                d[i] = thump * 0.8f + lp * 0.9f;
            }
            return Clip("rotor", d);
        }

        /// <summary>An engine note: stacked saw harmonics with a little rasp, pitched up and down live.</summary>
        static AudioClip EngineLoop()
        {
            int n = Rate / 2; var d = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float time = (float)i / Rate;
                float f = 110f; // loops cleanly over 0.5 s
                float saw = 0f;
                for (int h = 1; h <= 6; h++) saw += Mathf.Sin(2f * Mathf.PI * f * h * time) / h;
                float rasp = ((float)rng.NextDouble() * 2f - 1f) * 0.15f;
                d[i] = saw * 0.5f + rasp * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 27.5f * time));
            }
            return Clip("engine", d);
        }

        /// <summary>The magnet hitting the roof: a hard thud and a ringing, out-of-tune metal clang.</summary>
        static AudioClip Clang()
        {
            int n = Rate * 2; var d = new float[n];
            float[] partials = { 220f, 341f, 587f, 803f, 1190f, 1764f };
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float time = (float)i / Rate;
                float s = 0f;
                for (int p = 0; p < partials.Length; p++) s += Mathf.Sin(2f * Mathf.PI * partials[p] * time) * Mathf.Exp(-time * (2.2f + p * 1.1f)) / (1f + p * 0.4f);
                float thud = Mathf.Sin(2f * Mathf.PI * 60f * time) * Mathf.Exp(-time * 14f) * 1.6f;
                float crack = time < 0.03f ? ((float)rng.NextDouble() * 2f - 1f) * (1f - time / 0.03f) : 0f;
                d[i] = s * 0.6f + thud + crack;
            }
            return Clip("clang", d);
        }

        static AudioClip Clip(string name, float[] d)
        {
            float peak = 0.0001f; foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.85f;
            var c = AudioClip.Create("LiftOff_" + name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        // ------------------------------------------------------------------ the scene

        IEnumerator Play()
        {
            while (t < End && !skipping)
            {
                float dt = Time.unscaledDeltaTime;
                // Slow motion round the hook.
                float speed = t > HookAt - 0.15f && t < HookAt + 0.55f ? 0.28f : 1f;
                t += dt * speed;
                if (SkipPressed()) skipping = true;
                Step(dt * speed, dt);
                yield return null;
            }
            // Fade to black, then the race takes over (its loading screen fades in over the black).
            float a = black.color.a;
            while (a < 1f)
            {
                a = Mathf.MoveTowards(a, 1f, Time.unscaledDeltaTime / (skipping ? 0.25f : 0.6f));
                black.color = new Color(0, 0, 0, a);
                rotorAudio.volume *= 0.92f; engineAudio.volume *= 0.92f;
                yield return null;
            }
            Finish();
        }

        static bool SkipPressed()
        {
            var k = Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var p = Gamepad.current;
            return p != null && (p.buttonSouth.wasPressedThisFrame || p.buttonEast.wasPressedThisFrame || p.startButton.wasPressedThisFrame);
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            var go = start;
            start = null;
            bool started = false;
            try { started = go != null && go(); }
            finally { if (!started) Restore(); }
        }

        /// <summary>If the race didn't start, put the garage back as it was.</summary>
        void Restore()
        {
            if (parked != null) parked.gameObject.SetActive(parkedWasOn);
            if (chopper != null) Destroy(chopper.gameObject);
            cam.transform.SetParent(camParentHome, true);
            cam.transform.SetPositionAndRotation(camHome.position, camHome.rotation);
            cam.fieldOfView = camFovHome;
            cam.clearFlags = clearHome; cam.backgroundColor = bgHome;
            foreach (var b in paused) if (b != null) b.enabled = true;
            if (car != null) Destroy(car.gameObject);
            if (plinthCar != null) plinthCar.SetActive(true);
            if (magnet != null) Destroy(magnet.gameObject);
            if (cable != null) Destroy(cable.gameObject);
            Destroy(gameObject);
        }

        void Step(float dt, float realDt)
        {
            // The menu can switch set pieces back on when its screen changes: the parked one stays hidden.
            if (parked != null && parked.gameObject.activeSelf) parked.gameObject.SetActive(false);
            // Letterbox in, card up during the spin-up.
            float bars = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f)) * 118f;
            barTop.sizeDelta = new Vector2(0f, bars); barBottom.sizeDelta = new Vector2(0f, bars);
            card.alpha = Mathf.Clamp01((t - 0.5f) / 0.5f) * Mathf.Clamp01((LaunchAt + 0.4f - t) / 0.5f);

            // Rotors spin up over the first two seconds.
            rotorSpin = Mathf.Lerp(rotorSpin, t < 0.2f ? 0.15f : 1f, 1f - Mathf.Exp(-dt * 1.6f));
            rotorAngle += rotorSpin * 1300f * dt;

            // The car: revs on the line, then flat out.
            if (t >= LaunchAt) carSpeed = Mathf.Min(TopSpeed, carSpeed + Accel * dt);
            carZ -= carSpeed * dt;
            wheelAngle += carSpeed / 0.34f * Mathf.Rad2Deg * dt;

            stepDt = dt;
            PoseChopperAndCar();
            PoseCamera(dt, realDt);
            Spin();
            Sound();

            // The hook: flash, shake.
            float since = t - HookAt;
            float f = since >= 0f ? Mathf.Exp(-since * 9f) : 0f;
            flash.color = new Color(Cyan.r, Cyan.g, Cyan.b, f * 0.55f);
            hookFlash.intensity = f * 60f;
            black.color = new Color(0, 0, 0, Mathf.Clamp01((t - (End - 0.6f)) / 0.6f));
        }

        Vector3 Hook(Vector3 chopperPos, Quaternion chopperRot) { return chopperPos + chopperRot * Vector3.Scale(hookLocal, chopper.lossyScale); }

        float stepDt;

        void PoseChopperAndCar()
        {
            Vector3 home = chopperHome.position;
            Quaternion homeRot = chopperHome.rotation;
            // Heading down the runway (-Z), nose first.
            Quaternion runRot = Quaternion.LookRotation(Vector3.back);

            Vector3 pos; Quaternion rot; float cableLen;
            if (t < LaunchAt)
            {
                // Lift off the pad and turn to face down the runway.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.0f) / 2.0f));
                pos = home + Vector3.up * k * 9f + new Vector3(Lane - home.x, 0f, 0f) * k * 0.35f;
                rot = Quaternion.Slerp(homeRot, runRot, k) * Quaternion.Euler(k * 4f, 0f, Mathf.Sin(t * 1.3f) * 1.5f);
                cableLen = 0f;
            }
            else
            {
                // Away down the runway ahead of the car, then easing back over it for the snatch.
                Vector3 start = home + Vector3.up * 9f + new Vector3(Lane - home.x, 0f, 0f) * 0.35f;
                float run = t - LaunchAt;
                Vector3 ahead = new Vector3(Lane, 16f, start.z - run * 34f - 4f * run * run * 0.5f);
                float meet = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (ChaseAt + 0.4f)) / (HookAt - ChaseAt - 0.4f)));
                float drop = Mathf.Clamp01((t - ChaseAt) / (HookAt - ChaseAt));
                // Hold the hook straight above the roof at the moment of the hit.
                Vector3 over = new Vector3(Lane, roofY + 9f, carZ);
                pos = Vector3.Lerp(Vector3.Lerp(start, ahead, Mathf.Clamp01(run / 1.5f)), over, meet);
                float pitch = Mathf.Lerp(12f, 6f, meet);
                rot = runRot * Quaternion.Euler(pitch, 0f, Mathf.Sin(t * 1.7f) * 2f);
                cableLen = Mathf.SmoothStep(0f, 1f, drop) * 1f;
                if (t >= HookAt)
                {
                    // Snatched: the chopper climbs away with the car swinging under it.
                    float up = t - HookAt;
                    pos = new Vector3(Lane, roofY + 9f + up * up * 3.2f + up * 2f, carZ);
                    rot = runRot * Quaternion.Euler(14f, 0f, Mathf.Sin(t * 1.4f) * 3f);
                }
            }
            chopper.SetPositionAndRotation(pos, rot);

            Vector3 hook = Hook(pos, rot);
            bool hooked = t >= HookAt;
            if (!hooked) { hookZ = carZ; hangLen = Mathf.Max(0.3f, hook.y - (roofY + 0.14f)); }
            Vector3 magnetPos;
            if (!hooked)
            {
                float hang = Mathf.Max(0.3f, (hook.y - (roofY + 0.14f)) * cableLen);
                magnetPos = hook - Vector3.up * hang;
            }
            else
            {
                // Swinging back under the chopper as it pulls away.
                float up = t - HookAt;
                float swing = -Mathf.Sin(up * 2.6f) * Mathf.Exp(-up * 0.4f) * 14f;
                magnetPos = hook + Quaternion.Euler(swing, 0f, swing * 0.25f) * (Vector3.down * hangLen);
            }
            magnet.gameObject.SetActive(t >= ChaseAt - 0.3f);
            cable.gameObject.SetActive(magnet.gameObject.activeSelf);
            magnet.position = magnetPos;
            magnet.rotation = hooked ? Quaternion.LookRotation(Vector3.back, (hook - magnetPos).normalized) * Quaternion.Euler(0f, 0f, 0f) : Quaternion.LookRotation(Vector3.back);
            // Cable from the belly to the magnet's cap.
            Vector3 top = hook, bottom = magnetPos + magnet.up * 0.45f;
            cable.position = (top + bottom) * 0.5f;
            cable.up = (top - bottom).normalized;
            cable.localScale = new Vector3(0.09f, Mathf.Max(0.01f, (top - bottom).magnitude * 0.5f), 0.09f);

            // The car: on the tarmac until the hit, then hanging off the magnet.
            if (!hooked) car.position = new Vector3(car.position.x, carStart.y, carZ);
            else
            {
                if (car.parent != magnet) car.SetParent(magnet, true);
                carSpeed = Mathf.Max(16f, carSpeed - 24f * stepDt);
            }
        }

        void PoseCamera(float dt, float realDt)
        {
            Vector3 carPos = car.position;
            Vector3 chopPos = chopper.position;
            Vector3 pos; Vector3 look; float fov;
            if (t < LaunchAt)
            {
                // Low on the wet tarmac, the chopper waking up across the runway.
                pos = new Vector3(9f, 0.6f, 18f);
                look = Vector3.Lerp(chopPos, carPos, 0.3f) + Vector3.up * 1f;
                fov = 42f;
            }
            else if (t < ChaseAt)
            {
                // Down the runway ahead of the car: it launches at the lens and blasts past.
                pos = new Vector3(Lane + 3.4f, 0.45f, carStart.z - 22f); // outside the car: the stream screen is on the other side
                look = carPos + Vector3.up * 0.6f + Vector3.back * 2f;
                fov = Mathf.Lerp(30f, 50f, Mathf.Clamp01((t - LaunchAt) / (ChaseAt - LaunchAt)));
            }
            else if (t < HookAt - 0.15f)
            {
                // Chasing low behind the car, looking up at the chopper and the magnet coming down.
                pos = carPos + new Vector3(1.6f, 1.1f, 7.5f);
                look = Vector3.Lerp(carPos + Vector3.up * 1f, magnet.position, 0.45f) + Vector3.back * 6f;
                fov = 52f;
            }
            else if (t < AirborneAt)
            {
                // Side-on to the roof for the hit, in slow motion.
                pos = carPos + new Vector3(-5.5f, 1.6f, -1.2f);
                look = carPos + Vector3.up * 1.6f;
                fov = 38f;
            }
            else
            {
                // Wide and still: the chopper hauls the car off into the city.
                pos = new Vector3(Lane - 20f, 2.5f, hookZ - 16f);
                look = Vector3.Lerp(carPos, chopPos, 0.35f);
                fov = 46f;
            }
            if (t >= HookAt) shake = Mathf.Max(shake, t < HookAt + 0.08f ? 1f : 0f);
            shake = Mathf.MoveTowards(shake, 0f, realDt * 2.2f);
            Vector3 jitter = new Vector3(Mathf.PerlinNoise(Time.unscaledTime * 31f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.unscaledTime * 29f) - 0.5f, 0f) * shake * 0.5f;
            // A touch of handheld drift on every shot.
            Vector3 drift = new Vector3(Mathf.Sin(Time.unscaledTime * 0.7f), Mathf.Sin(Time.unscaledTime * 0.9f + 1f), 0f) * 0.05f;
            cam.transform.position = pos + jitter + drift;
            cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position);
            cam.fieldOfView = fov;
        }

        void Spin()
        {
            if (frontRotor != null) frontRotor.RotateAround(chopper.TransformPoint(frontHub), chopper.up, rotorSpin * 1300f * Time.unscaledDeltaTime);
            if (rearRotor != null) rearRotor.RotateAround(chopper.TransformPoint(rearHub), chopper.up, -rotorSpin * 1300f * Time.unscaledDeltaTime);
            foreach (var w in wheels)
            {
                var r = w.GetComponent<Renderer>();
                if (r != null) w.RotateAround(r.bounds.center, car.right, carSpeed / 0.34f * Mathf.Rad2Deg * Time.unscaledDeltaTime);
            }
        }

        void Sound()
        {
            rotorAudio.volume = Mathf.Lerp(rotorAudio.volume, 0.15f + rotorSpin * 0.5f, 0.1f);
            rotorAudio.pitch = 0.55f + rotorSpin * 0.5f;
            float rev = t < LaunchAt ? 0.35f + Mathf.Max(0f, Mathf.Sin(t * 5f)) * 0.25f * Mathf.Clamp01(t - 1.5f) : carSpeed / TopSpeed;
            engineAudio.volume = Mathf.Lerp(engineAudio.volume, t < 1.2f ? 0f : 0.25f + rev * 0.45f, 0.1f);
            engineAudio.pitch = 0.6f + rev * 1.4f;
            if (t >= HookAt && !clanged)
            {
                clanged = true;
                var a = gameObject.AddComponent<AudioSource>();
                a.outputAudioMixerGroup = rotorAudio.outputAudioMixerGroup;
                a.spatialBlend = 0f;
                a.PlayOneShot(Clang(), 0.9f);
            }
        }

        bool clanged;
    }
}
