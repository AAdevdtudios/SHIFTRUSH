using System.Collections;
using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ShiftRush.Race
{
    /// <summary>
    /// How a story race starts, on any track: a drone sweeps the start while the two masked hosts open the show,
    /// then every racer's chopper flies in along the circuit with their car hanging under it. The player drops
    /// whenever they like (Space, Enter or A) but never past their grid spot, where the drop happens on its own.
    /// Rivals let go at their spots. Landing is the start: no countdown. When the player finishes, a chopper
    /// comes down and scoops the car off the road. Quick races keep the normal grid and 3-2-1.
    /// </summary>
    [DefaultExecutionOrder(100)] // after RaceManager has put the field on the grid, in the same frame
    public class StoryDrop : MonoBehaviour
    {
        const float Height = 13f;        // chopper above the road
        const float Cable = 6.5f;        // car hangs this far under it
        const float Approach = 170f;     // metres flown along the circuit before the grid
        const float Speed = 24f;         // m/s, about 86 km/h
        const float DroneTime = 5.5f;

        static readonly Color Ink = new Color32(8, 5, 16, 255);
        static readonly Color Paper = new Color32(250, 247, 240, 255);
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => TryStart();
            TryStart();
        }

        static void TryStart()
        {
            if (StoryProgress.ActiveEpisode == null) return;
            if (FindFirstObjectByType<RaceLine>() == null || FindFirstObjectByType<StoryDrop>() != null) return;
            new GameObject("Story Drop").AddComponent<StoryDrop>();
        }

        class Drop
        {
            public RaceManager.Racer racer;
            public Transform chopper, rotorA, rotorB, cable;
            public float slot, lateral, at, letGoAt;
            public bool released, landed;
            public float releasedTime;
        }

        readonly List<Drop> drops = new List<Drop>();
        RaceManager race;
        RaceLine line;
        Camera drone;
        TMP_Text speakerText, lineText, promptText, titleText;
        RectTransform lowerThird;
        CanvasGroup prompt;
        Drop mine;
        bool started;

        void Start()
        {
            // Runs after RaceManager.Start in the very first frame, so the cars are never seen sitting on the grid:
            // they're in the air under the choppers from the first picture.
            race = RaceManager.Current;
            line = race != null ? race.Line : null;
            if (race == null || line == null || race.CurrentState != RaceManager.State.Countdown || race.Racers.Count == 0) { Destroy(gameObject); return; }
            race.HoldStart = true;
            BuildUi();
            var story = StoryCatalog.Instance;
            var prefab = story != null ? story.liftChopper : null;

            var rng = new System.Random(7);
            foreach (var r in race.Racers)
            {
                var p = r.vehicle.Transform.position;
                int i = line.Nearest(p);
                var d = new Drop { racer = r, slot = line.distances[i], lateral = line.Lateral(p, i) };
                // Every chopper keeps its car's grid spacing all the way in (a random head start bigger than the
                // gap between grid rows stacked cars on top of each other), and rivals let go a little before their
                // spot, only once the road below them is clear.
                d.at = d.slot - Approach - (float)rng.NextDouble() * 1.5f;
                d.letGoAt = r.isPlayer ? d.slot : d.slot - (float)rng.NextDouble() * 6f;
                d.chopper = MakeChopper(prefab);
                d.rotorA = Deep(d.chopper, "FrontPropeller"); d.rotorB = Deep(d.chopper, "RearPropeller");
                d.cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                Destroy(d.cable.GetComponent<Collider>());
                d.cable.SetParent(d.chopper, false);
                d.cable.localScale = new Vector3(0.08f, Cable * 0.5f, 0.08f);
                d.cable.localPosition = new Vector3(0, -Cable * 0.5f, 0);
                d.cable.GetComponent<Renderer>().material.color = Ink;
                if (r.isPlayer) mine = d;
                drops.Add(d);
                Hang(d, 0f);
            }
            StartCoroutine(Show());
            race.Finished += OnFinished;
        }

        void OnDestroy()
        {
            if (race != null) race.Finished -= OnFinished;
        }

        // ------------------------------------------------------------------ the sequence

        IEnumerator Show()
        {
            var info = FindFirstObjectByType<TrackInfo>();
            string track = info != null ? info.displayName : null;
            if (track == null && RaceCatalog.Instance != null)
            {
                // Older tracks have no TrackInfo: the catalog knows the name by scene.
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                foreach (var t in RaceCatalog.Instance.tracks) if (t.scene == scene) track = t.displayName;
            }
            track = track != null ? track.ToUpperInvariant() : "TONIGHT";
            var ep = StoryProgress.ActiveEpisode;
            drone = MakeDrone();
            HideHud(true);
            titleText.text = "CHANNEL 13  //  LIVE\n<size=150%>" + track + "</size>";

            // The drone: a slow, high orbit over the start line while the hosts open.
            var start = line.points[0];
            var fwd = line.Direction(0);
            float t0 = Time.time;
            Say("MASK ONE", "Good evening, city. Channel 13 is live" + (ep != null ? ", and tonight is " + ep.title.ToLowerInvariant() + "." : "."));
            bool second = false, third = false;
            while (Time.time - t0 < DroneTime)
            {
                float k = (Time.time - t0) / DroneTime;
                float ang = Mathf.Lerp(-70f, 40f, k);
                var around = Quaternion.AngleAxis(ang, Vector3.up) * -fwd;
                drone.transform.position = start + around * Mathf.Lerp(80f, 55f, k) + Vector3.up * Mathf.Lerp(45f, 24f, k);
                drone.transform.LookAt(start + Vector3.up * 2f);
                titleText.alpha = Mathf.Clamp01(1.5f - Mathf.Abs(k - 0.35f) * 3f);
                if (!second && k > 0.45f) { second = true; Say("MASK TWO", Matchup(ep)); }
                Fly(Time.deltaTime);
                yield return null;
            }
            titleText.alpha = 0f;

            // Behind the player's chopper as the formation comes in.
            Say("MASK ONE", "Here come the choppers. Watch the drop.");
            while (!mine.released)
            {
                Fly(Time.deltaTime);
                var c = mine.chopper.position;
                var dir = line.Direction(line.IndexAt(Mathf.Repeat(mine.at, line.length)));
                drone.transform.position = Vector3.Lerp(drone.transform.position, c - dir * 22f + Vector3.up * 6f, 1f - Mathf.Exp(-3f * Time.deltaTime));
                drone.transform.rotation = Quaternion.Slerp(drone.transform.rotation, Quaternion.LookRotation(c - Vector3.up * 4f - drone.transform.position), 1f - Mathf.Exp(-4f * Time.deltaTime));
                float toGo = mine.slot - mine.at;
                prompt.alpha = Mathf.MoveTowards(prompt.alpha, 1f, Time.deltaTime * 3f);
                promptText.text = "<size=60%>" + Mathf.Max(0, Mathf.CeilToInt(toGo)) + " M TO YOUR GRID SPOT</size>\nDROP  <color=#2EF2FF>SPACE / A</color>";
                if (!third && toGo < 60f) { third = true; Say("MASK TWO", "Drop zone coming up..."); }
                if (DropPressed() || toGo <= 0f) Release(mine);
                yield return null;
            }
            prompt.alpha = 0f;
            Say("MASK TWO", "Oh, they're here!");
            // Cut to the car, and the race HUD comes back.
            Destroy(drone.gameObject);
            HideHud(false);

            // Keep flying the rest in until everyone's down; the race starts when the player lands.
            float released = Time.time;
            while (true)
            {
                Fly(Time.deltaTime);
                if (!started && (Landed(mine) || Time.time - released > 2.2f))
                {
                    started = true;
                    race.StartNow(r => { var d = drops.Find(x => x.racer == r); return d == null || d.landed || d.released; });
                }
                bool all = true;
                foreach (var d in drops) if (d.chopper != null) all = false;
                if (started && all) break;
                if (Time.time - released > 4f) HideLowerThird();
                yield return null;
            }
            HideLowerThird();
        }

        readonly List<Canvas> hidden = new List<Canvas>();

        /// <summary>The race HUD stays out of the drone shot: it's television until the drop.</summary>
        void HideHud(bool hide)
        {
            if (hide)
            {
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.isRootCanvas && c.enabled && !c.transform.IsChildOf(transform)) { c.enabled = false; hidden.Add(c); }
            }
            else { foreach (var c in hidden) if (c != null) c.enabled = true; hidden.Clear(); }
        }

        /// <summary>The hosts call tonight's matchup: "Tonight's a BEEF. One on one: you against KAITO."</summary>
        static string Matchup(StoryCatalog.Episode ep)
        {
            var cast = RivalCast.Instance;
            if (ep == null || ep.opponents == null || ep.opponents.Length == 0 || cast == null)
                return "Every racer in the air. Every cop in town looking for them.";
            var names = new List<string>();
            foreach (var id in ep.opponents) { var c = cast.Find(id); if (c != null) names.Add(c.name); }
            string format = ep.eventType.ToLowerInvariant();
            string who = names.Count == 1 ? "One on one: the rookie against " + names[0] + "."
                : names.Count <= 3 ? "The rookie against " + string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1] + "."
                : "The rookie against the whole board, " + names[0] + " out front.";
            string article = format.Length > 0 && "aeiou".IndexOf(format[0]) >= 0 ? "an " : "a ";
            return "Tonight's " + article + format + ". " + who + (ep.police ? " And the cops are listening." : "");
        }

        void Fly(float dt)
        {
            foreach (var d in drops)
            {
                if (d.chopper == null) continue;
                d.at += Speed * dt;
                if (!d.released)
                {
                    if (!d.racer.isPlayer && d.at >= d.letGoAt && (ClearBelow(d) || d.at >= d.slot + 4f)) Release(d);
                    else Hang(d, dt);
                }
                if (d.released)
                {
                    // Climb away and peel off, then go.
                    d.chopper.position += (d.chopper.forward * Speed + Vector3.up * 7f) * dt;
                    d.chopper.rotation = Quaternion.Slerp(d.chopper.rotation, d.chopper.rotation * Quaternion.Euler(-4f, d.lateral >= 0 ? 12f : -12f, 0f), dt * 2f);
                    if (!d.landed && Landed(d)) { d.landed = true; if (started) d.racer.vehicle.SetControllable(true); }
                    if (Time.time - d.releasedTime > 7f) { Destroy(d.chopper.gameObject); d.chopper = null; }
                }
                Spin(d, dt);
            }
            if (started)
                foreach (var d in drops)
                    if (d.released && (d.landed || Time.time - d.releasedTime > 2f)) d.racer.vehicle.SetControllable(true);
        }

        void Hang(Drop d, float dt)
        {
            float s = Mathf.Repeat(d.at, line.length);
            int i = line.IndexAt(s);
            var dir = line.Direction(i);
            var right = line.Right(i);
            var road = line.PointAt(s) + right * d.lateral;
            var pos = road + Vector3.up * Height;
            d.chopper.position = pos;
            d.chopper.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(6f, 0f, Mathf.Sin(Time.time * 1.3f + d.lateral) * 2f);
            var body = d.racer.vehicle.Body;
            if (body != null) { body.isKinematic = true; body.linearVelocity = Vector3.zero; }
            d.racer.vehicle.SetControllable(false);
            AutoReset(d.racer.vehicle, false); // a car on a cable isn't "off the track"
            d.racer.vehicle.Transform.SetPositionAndRotation(pos - Vector3.up * (Cable + 1.2f), Quaternion.LookRotation(dir, Vector3.up));
        }

        void Release(Drop d)
        {
            if (d.released) return;
            d.released = true;
            d.releasedTime = Time.time;
            if (d.cable != null) Destroy(d.cable.gameObject);
            var body = d.racer.vehicle.Body;
            var dir = line.Direction(line.IndexAt(Mathf.Repeat(d.at, line.length)));
            if (body != null) { body.isKinematic = false; body.linearVelocity = dir * Speed * 0.9f; body.angularVelocity = Vector3.zero; }
            StartCoroutine(ResetBackOn(d.racer.vehicle));
        }

        /// <summary>No other car on the road under this one, or about to be: safe to let go.</summary>
        bool ClearBelow(Drop d)
        {
            var me = d.racer.vehicle.Transform.position;
            foreach (var other in drops)
            {
                if (other == d || !other.released) continue;
                var p = other.racer.vehicle.Transform.position;
                var flat = new Vector3(p.x - me.x, 0f, p.z - me.z);
                if (flat.magnitude < 6.5f) return false;
            }
            return true;
        }

        static bool Landed(Drop d) { return d.released && d.racer.vehicle.AnyWheelGrounded && Time.time - d.releasedTime > 0.3f; }

        static bool DropPressed()
        {
            var k = Keyboard.current;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            return g != null && g.buttonSouth.wasPressedThisFrame;
        }

        void Spin(Drop d, float dt)
        {
            if (d.rotorA != null) d.rotorA.Rotate(0f, 1400f * dt, 0f, Space.Self);
            if (d.rotorB != null) d.rotorB.Rotate(0f, -1400f * dt, 0f, Space.Self);
        }

        // ------------------------------------------------------------------ the scoop

        void OnFinished(RaceManager.Racer r)
        {
            if (r == null || !r.isPlayer) return;
            StartCoroutine(Scoop(r));
        }

        IEnumerator Scoop(RaceManager.Racer r)
        {
            yield return new WaitForSeconds(2.5f);
            var story = StoryCatalog.Instance;
            var chopper = MakeChopper(story != null ? story.liftChopper : null);
            var rotorA = Deep(chopper, "FrontPropeller"); var rotorB = Deep(chopper, "RearPropeller");
            var car = r.vehicle.Transform;
            Say("MASK ONE", "And the chopper's already there. Nobody stays on these streets long.");
            float t = 0;
            Vector3 from = car.position + Vector3.up * 45f - car.forward * 30f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                var target = car.position + Vector3.up * (Cable + Height * 0.5f);
                chopper.position = Vector3.Lerp(from, target, Mathf.SmoothStep(0, 1, t / 3f));
                chopper.rotation = Quaternion.LookRotation(Flat(car.forward), Vector3.up);
                if (rotorA) rotorA.Rotate(0, 1400 * Time.deltaTime, 0); if (rotorB) rotorB.Rotate(0, -1400 * Time.deltaTime, 0);
                yield return null;
            }
            var body = r.vehicle.Body;
            r.vehicle.SetControllable(false);
            AutoReset(r.vehicle, false);
            if (body != null) body.isKinematic = true;
            var hold = car.position - chopper.position;
            t = 0;
            // Climb and fly off with it, and keep holding on until the results take us out of the scene:
            // the car never drops back onto the road.
            while (chopper != null && car != null)
            {
                t += Time.deltaTime;
                float climb = t < 8f ? 6f : 1.5f;
                chopper.position += (Vector3.up * climb + chopper.forward * Mathf.Min(t * 4f, 22f)) * Time.deltaTime;
                if (body != null) { body.isKinematic = true; body.linearVelocity = Vector3.zero; }
                r.vehicle.SetControllable(false);
                car.position = chopper.position + hold;
                car.rotation = Quaternion.LookRotation(Flat(chopper.forward), Vector3.up);
                if (rotorA) rotorA.Rotate(0, 1400 * Time.deltaTime, 0); if (rotorB) rotorB.Rotate(0, -1400 * Time.deltaTime, 0);
                if (t > 6f) HideLowerThird();
                yield return null;
            }
        }

        /// <summary>The car's auto-reset (off track, beached) would yank it back to the road from under a chopper.</summary>
        static void AutoReset(IVehicle v, bool on)
        {
            var reset = v != null ? v.Transform.GetComponent<VehicleReset>() : null;
            if (reset != null) reset.enabled = on;
        }

        IEnumerator ResetBackOn(IVehicle v)
        {
            // Back on once it has landed and settled.
            float t = 0f;
            while (t < 4f && !(v.AllWheelsGrounded && t > 0.6f)) { t += Time.deltaTime; yield return null; }
            AutoReset(v, true);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward; }

        // ------------------------------------------------------------------ builders

        Transform MakeChopper(GameObject prefab)
        {
            Transform c;
            if (prefab != null) c = Instantiate(prefab).transform;
            else
            {
                // No chopper model: a stand-in body so the drop still reads.
                c = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
                c.localScale = new Vector3(2.5f, 2.5f, 7f);
            }
            foreach (var col in c.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var mb in c.GetComponentsInChildren<MonoBehaviour>()) Destroy(mb);
            c.name = "Drop Chopper";
            return c;
        }

        static Transform Deep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        Camera MakeDrone()
        {
            var go = new GameObject("Drone Cam");
            var cam = go.AddComponent<Camera>();
            cam.depth = 50;
            cam.fieldOfView = 55;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            return cam;
        }

        void BuildUi()
        {
            var data = Resources.Load<Comic.ComicIntroData>("SR_ComicIntro");
            var mono = data != null ? data.mono : null;
            var go = new GameObject("Story Drop UI");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            // Lower third: the hosts' lines, paper on ink with a magenta hard shadow.
            lowerThird = Rect("Lower Third", go.transform);
            lowerThird.anchorMin = lowerThird.anchorMax = new Vector2(0.5f, 0f);
            lowerThird.pivot = new Vector2(0.5f, 0f);
            // Above the race HUD's bottom row (power-ups, nitro).
            lowerThird.anchoredPosition = new Vector2(0, 200);
            lowerThird.sizeDelta = new Vector2(1100, 150);
            var shadow = Rect("Shadow", lowerThird); Fill(shadow); shadow.offsetMin = new Vector2(10, -10); shadow.offsetMax = new Vector2(10, -10);
            shadow.gameObject.AddComponent<Image>().color = Magenta;
            var ink = Rect("Ink", lowerThird); Fill(ink); ink.gameObject.AddComponent<Image>().color = Ink;
            var paper = Rect("Paper", lowerThird); Fill(paper); paper.offsetMin = new Vector2(6, 6); paper.offsetMax = new Vector2(-6, -6);
            paper.gameObject.AddComponent<Image>().color = Paper;
            speakerText = Label(paper, mono, 24, Magenta, TextAlignmentOptions.TopLeft);
            Fill(speakerText.rectTransform); speakerText.rectTransform.offsetMin = new Vector2(28, 12); speakerText.rectTransform.offsetMax = new Vector2(-28, -12);
            lineText = Label(paper, null, 36, Ink, TextAlignmentOptions.TopLeft);
            Fill(lineText.rectTransform); lineText.rectTransform.offsetMin = new Vector2(28, 12); lineText.rectTransform.offsetMax = new Vector2(-28, -46);
            lineText.enableAutoSizing = true; lineText.fontSizeMin = 24; lineText.fontSizeMax = 36;
            lineText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            lowerThird.gameObject.SetActive(false);

            titleText = Label(go.transform, null, 56, Paper, TextAlignmentOptions.Center);
            var tr = titleText.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.72f); tr.sizeDelta = new Vector2(1600, 300);
            titleText.fontStyle = FontStyles.Bold;
            titleText.outlineWidth = 0.25f; titleText.outlineColor = Ink;
            titleText.alpha = 0f;

            var p = Rect("Drop Prompt", go.transform);
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.42f); p.sizeDelta = new Vector2(900, 160);
            prompt = p.gameObject.AddComponent<CanvasGroup>(); prompt.alpha = 0f;
            promptText = Label(p, null, 64, Paper, TextAlignmentOptions.Center);
            Fill(promptText.rectTransform);
            promptText.fontStyle = FontStyles.Bold;
            promptText.outlineWidth = 0.3f; promptText.outlineColor = Ink;
        }

        void Say(string who, string text)
        {
            if (lowerThird == null) return;
            lowerThird.gameObject.SetActive(true);
            speakerText.text = who + "  //  CH13 LIVE";
            lineText.text = text;
            StopCoroutine("Pop");
            StartCoroutine("Pop");
        }

        IEnumerator Pop()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
            {
                float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.06f;
                lowerThird.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            lowerThird.localScale = Vector3.one;
        }

        void HideLowerThird() { if (lowerThird != null) lowerThird.gameObject.SetActive(false); }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        static TMP_Text Label(Transform parent, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = col; t.alignment = align; t.raycastTarget = false;
            return t;
        }
    }
}
