using ShiftRush.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Lab
{
    /// <summary>
    /// MovesLab's drift tuning panel: a slider for every drift setting of the type the player's car is using
    /// (1 drifty, 2 balanced, 3 grippy). Sliders change the tuning asset (Resources/SR_DriftTuning) directly, so the
    /// next drift feels the change and the numbers are kept after Play stops. "Copy values" puts the numbers on the
    /// clipboard (and the Console) to send on; "Reset" puts that type back to its starting numbers. F1 hides it.
    /// </summary>
    public class DriftTuningHud : MonoBehaviour
    {
        public DriftMoves drift;

        bool show = true, more;
        Vector2 scroll;
        string copied = "";
        float copiedAt = -10f;
        GUIStyle box, title, label, hint, value;

        struct Row
        {
            public string name, about; public float min, max; public bool whole;
            public Row(string name, float min, float max, string about, bool whole = false) { this.name = name; this.min = min; this.max = max; this.about = about; this.whole = whole; }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) show = !show;
        }

        void OnGUI()
        {
            var tuning = DriftTuning.Instance;
            if (!show || drift == null || tuning == null) return;
            if (box == null) Styles();
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var type = drift.Character;
            var p = tuning.For(type);
            float w = 380f, h = Mathf.Min(Screen.height - 40f, 760f);
            float x = Screen.width - w - 16f, y = Mathf.Max(84f, Screen.height * 0.16f);
            h = Mathf.Min(h, Screen.height * 0.72f - y); // stops above the speedometer
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, box);
            GUILayout.BeginArea(new Rect(x + 12f, y + 8f, w - 24f, h - 16f));
            GUILayout.Label("DRIFT TUNING  ·  " + type.ToString().ToUpperInvariant(), title);
            GUILayout.Label("1 drifty   2 balanced   3 grippy   F1 hide.  Changes apply on the next drift and are kept.", hint);
            GUILayout.Space(4f);

            scroll = GUILayout.BeginScrollView(scroll);
            // The handful that set the feel.
            p.openPerSecond = Slider(new Row("Swing out (°/s)", 5f, 150f, "while Space is held the tail swings out this fast", true), p.openPerSecond);
            p.maxAngle = Slider(new Row("Max angle (°)", 10f, 70f, "the furthest the tail can swing out", true), p.maxAngle);
            p.letGoRecovery = Slider(new Row("Let-go recovery (°/s)", 20f, 250f, "after Space is released it straightens this fast", true), p.letGoRecovery);
            p.tyreGripInDrift = Slider(new Row("Slippery", 0.05f, 1f, "tyre grip while sliding: low = ice, high = tyres bite"), p.tyreGripInDrift);
            p.swingSpeed = Slider(new Row("Body weight", 0.5f, 10f, "how fast the body follows: low = heavy and floaty, high = snappy"), p.swingSpeed);

            GUILayout.Space(6f);
            more = GUILayout.Toggle(more, more ? " Fewer settings" : " More settings");
            if (more)
            {
                p.startKmh = Slider(new Row("Start speed (km/h)", 20f, 90f, "slowest speed a drift can start", true), p.startKmh);
                p.steerToStart = Slider(new Row("Steer needed", 0f, 1f, "how hard you must steer for Space to start it"), p.steerToStart);
                p.tapAngle = Slider(new Row("Tap angle (°)", 0f, 30f, "the first kick a quick tap of Space gives", true), p.tapAngle);
                p.cornerGrip = Slider(new Row("Corner grip (m/s²)", 5f, 22f, "how tight the drift can corner"), p.cornerGrip);
                p.speedLoss = Slider(new Row("Speed loss (km/h/s)", 0f, 30f, "lost per second, off the throttle"), p.speedLoss);
                p.speedLossOnThrottle = Slider(new Row("Speed loss on throttle", 0f, 15f, "lost per second, on the throttle"), p.speedLossOnThrottle);
                p.handbrakeSpeedLoss = Slider(new Row("Handbrake speed loss", 0f, 30f, "extra lost per second while Space is held"), p.handbrakeSpeedLoss);
                p.chargeRate = Slider(new Row("Charge rate", 0.3f, 2.5f, "how fast the tiers charge"), p.chargeRate);
                p.boostScale = Slider(new Row("Boost scale", 0.3f, 2.5f, "× the 12 / 24 / 38 km/h tier boosts"), p.boostScale);
                tuning.weightEffect = Slider(new Row("Weight effect (all)", 0f, 1f, "how much a car's weight changes its drift"), tuning.weightEffect);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy values", GUILayout.Height(26f))) Copy(type, p);
            if (GUILayout.Button("Reset " + type, GUILayout.Height(26f))) Reset(tuning, type);
            GUILayout.EndHorizontal();
            if (Time.unscaledTime - copiedAt < 3f) GUILayout.Label(copied, hint);
            GUILayout.EndArea();

            if (GUI.changed) Save(tuning);
        }

        float Slider(Row r, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(r.name, label, GUILayout.Width(150f));
            float nv = GUILayout.HorizontalSlider(v, r.min, r.max, GUILayout.Width(110f));
            if (r.whole) nv = Mathf.Round(nv); else nv = Mathf.Round(nv * 100f) / 100f;
            GUILayout.Label(r.whole ? nv.ToString("F0") : nv.ToString("F2"), value, GUILayout.Width(46f));
            GUILayout.EndHorizontal();
            GUILayout.Label(r.about, hint);
            return nv;
        }

        void Section(string name)
        {
            GUILayout.Space(6f);
            GUILayout.Label(name.ToUpperInvariant(), label);
        }

        void Copy(DriftCharacter type, DriftTuning.Profile p)
        {
            string text = type + ": start " + p.startKmh.ToString("F0") + " km/h, steer " + p.steerToStart.ToString("F2") + ", tap " + p.tapAngle.ToString("F0")
                + "°, max " + p.maxAngle.ToString("F0") + "°, open " + p.openPerSecond.ToString("F0") + "°/s, swing " + p.swingSpeed.ToString("F2")
                + ", let-go recovery " + p.letGoRecovery.ToString("F0") + "°/s"
                + ", tyre grip " + p.tyreGripInDrift.ToString("F2") + ", corner grip " + p.cornerGrip.ToString("F1")
                + ", speed loss " + p.speedLoss.ToString("F1") + "/" + p.speedLossOnThrottle.ToString("F1") + "/" + p.handbrakeSpeedLoss.ToString("F1")
                + ", charge " + p.chargeRate.ToString("F2") + ", boost " + p.boostScale.ToString("F2");
            GUIUtility.systemCopyBuffer = text;
            Debug.Log("[DRIFT TUNING] " + text);
            copied = "Copied " + type + " to the clipboard (and the Console).";
            copiedAt = Time.unscaledTime;
        }

        void Reset(DriftTuning tuning, DriftCharacter type)
        {
            var fresh = ScriptableObject.CreateInstance<DriftTuning>();
            var src = fresh.For(type);
            var json = JsonUtility.ToJson(src);
            JsonUtility.FromJsonOverwrite(json, tuning.For(type));
            Destroy(fresh);
            Save(tuning);
            copied = type + " back to its starting numbers.";
            copiedAt = Time.unscaledTime;
        }

        static void Save(DriftTuning tuning)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tuning); // kept when Play stops
#endif
        }

        void Styles()
        {
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.05f, 0.04f, 0.09f, 0.88f)); bg.Apply();
            box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
            title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.35f, 0.75f) } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            value = new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.95f, 1f) } };
            hint = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = new Color(0.7f, 0.7f, 0.8f) } };
        }
    }
}
