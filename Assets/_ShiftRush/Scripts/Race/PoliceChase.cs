using System.Collections;
using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.Race
{
    /// <summary>
    /// The police in a story race that calls for them (episode "police"). A few seconds after the start, cop cars
    /// join from behind with light bars and sirens and hunt the player along the circuit: catching up on the race
    /// line, then going for the car to box it in. The BUST meter fills while a cop is close and the player is
    /// slow, and drains when the player gets away. Full is busted: the race is lost and it's back to the board.
    /// </summary>
    public class PoliceChase : MonoBehaviour
    {
        const float JoinAfter = 7f;          // seconds after the start
        const float BustRadius = 10f;        // a cop this close counts
        const float SlowKmh = 55f;           // and the player this slow
        const float FillSeconds = 3.2f;      // empty to busted, pinned
        const float DrainPerSecond = 0.35f;

        static readonly Color Ink = new Color32(8, 5, 16, 255);
        static readonly Color Paper = new Color32(250, 247, 240, 255);
        static readonly Color Magenta = new Color32(255, 46, 154, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => TryStart();
            TryStart();
        }

        static void TryStart()
        {
            var e = StoryProgress.ActiveEpisode;
            if (e == null || !e.police) return;
            if (FindFirstObjectByType<RaceLine>() == null || FindFirstObjectByType<PoliceChase>() != null) return;
            new GameObject("Police Chase").AddComponent<PoliceChase>().cars = e.policeCars;
        }

        public int cars = 2;
        public float Bust { get; private set; }
        public static PoliceChase Current { get; private set; }

        RaceManager race;
        readonly List<Cop> cops = new List<Cop>();
        Image fill;
        CanvasGroup meter, busted;
        TMP_Text label;
        bool over;
        float joinedAt = float.MaxValue;

        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }

        IEnumerator Start()
        {
            yield return null;
            race = RaceManager.Current;
            if (race == null || race.Line == null) { Destroy(gameObject); yield break; }
            while (race.CurrentState == RaceManager.State.Countdown) yield return null;
            BuildUi();
            yield return new WaitForSeconds(JoinAfter);
            var f = RivalField.Load();
            if (f == null || f.carPrefab == null) yield break;
            for (int i = 0; i < cars; i++)
            {
                var cop = Spawn(f.carPrefab, i);
                if (cop != null) { cops.Add(cop); if (cops.Count == 1) joinedAt = Time.time; }
                yield return new WaitForSeconds(1.2f);
            }
        }

        Cop Spawn(GameObject prefab, int i)
        {
            var line = race.Line;
            var player = race.Player;
            if (player == null) return null;
            float d = line.distances[line.Nearest(player.vehicle.Transform.position)] - 70f - i * 16f;
            int k = line.IndexAt(Mathf.Repeat(d, line.length));
            var pos = line.points[k] + line.Right(k) * (i % 2 == 0 ? -3f : 3f) + Vector3.up * 0.6f;
            var v = VehicleSpawn.Rival(prefab, pos, Quaternion.LookRotation(line.Direction(k), Vector3.up));
            if (v == null) return null;
            var go = v.Transform.gameObject;
            go.name = "Police " + (i + 1);
            Livery(go);
            var reset = go.GetComponent<VehicleReset>();
            if (reset != null) reset.listenForInput = false;
            v.SetControllable(true);
            var cop = go.AddComponent<Cop>();
            cop.Init(race, v, i);
            return cop;
        }

        /// <summary>Black and white, with a red and blue bar on the roof.</summary>
        static void Livery(GameObject car)
        {
            var black = new Material(Shader.Find("Universal Render Pipeline/Lit")); black.color = new Color(0.04f, 0.04f, 0.06f);
            var rs = car.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds(car.transform.position, Vector3.one);
            bool first = true;
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                var mats = r.sharedMaterials;
                // The biggest painted surface goes black; wheels, glass and lights keep their own.
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null && (mats[m].name.ToLowerInvariant().Contains("paint") || mats[m].name.ToLowerInvariant().Contains("body"))) mats[m] = black;
                r.sharedMaterials = mats;
            }
            var bar = new GameObject("Light Bar").transform;
            bar.SetParent(car.transform, false);
            bar.position = new Vector3(b.center.x, b.max.y + 0.08f, b.center.z);
            bar.rotation = car.transform.rotation;
            var lights = bar.gameObject.AddComponent<LightBar>();
            lights.Build();
        }

        void Update()
        {
            if (race == null || over || race.Player == null) return;
            var player = race.Player;
            if (player.finished) { if (meter != null) meter.alpha = 0f; return; }
            if (cops.Count == 0 || Time.time < joinedAt + 4f) return; // a moment to react once they show up
            float kmh = Mathf.Abs(player.vehicle.SpeedKmh);
            bool pinned = false;
            var p = player.vehicle.Transform.position;
            foreach (var c in cops) if (c != null && Vector3.Distance(c.transform.position, p) < BustRadius) { pinned = true; break; }
            if (pinned && kmh < SlowKmh) Bust += Time.deltaTime / FillSeconds * Mathf.Lerp(1.6f, 0.6f, kmh / SlowKmh);
            else Bust -= Time.deltaTime * DrainPerSecond * (kmh > SlowKmh * 1.6f ? 1.6f : 1f);
            Bust = Mathf.Clamp01(Bust);
            if (meter != null)
            {
                meter.alpha = Mathf.MoveTowards(meter.alpha, 1f, Time.deltaTime * 2f);
                fill.fillAmount = Bust;
                fill.color = Color.Lerp(Magenta, new Color(1f, 0.1f, 0.15f), Bust);
                label.text = pinned && kmh < SlowKmh ? "BUST  //  SHAKE THEM" : (Bust > 0.01f ? "BUST" : "COPS ON YOU");
            }
            if (Bust >= 1f) StartCoroutine(Busted());
        }

        IEnumerator Busted()
        {
            over = true;
            race.Player.vehicle.SetControllable(false);
            busted.alpha = 1f;
            StoryProgress.Report(99, 0, 0); // a lost race: back to the case board
            yield return new WaitForSeconds(3.5f);
            SceneLoader.Load("MainMenu", "GRIP PRO", "BUSTED", "The cops got you. Speed shakes them, slowing down feeds them.");
        }

        void BuildUi()
        {
            var data = Resources.Load<Comic.ComicIntroData>("SR_ComicIntro");
            var mono = data != null ? data.mono : null;
            var go = new GameObject("Police UI");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 790;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;

            var m = Rect("Bust Meter", go.transform);
            m.anchorMin = m.anchorMax = new Vector2(0.5f, 1f); m.pivot = new Vector2(0.5f, 1f);
            m.anchoredPosition = new Vector2(0, -110); m.sizeDelta = new Vector2(520, 46);
            meter = m.gameObject.AddComponent<CanvasGroup>(); meter.alpha = 0f;
            var ink = Rect("Ink", m); Stretch(ink); ink.gameObject.AddComponent<Image>().color = Ink;
            var bar = Rect("Fill", m); Stretch(bar); bar.offsetMin = new Vector2(5, 5); bar.offsetMax = new Vector2(-5, -5);
            fill = bar.gameObject.AddComponent<Image>();
            fill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f; fill.color = Magenta;
            label = Label(m, mono, 22, Paper);
            Stretch(label.rectTransform);

            var b = Rect("Busted", go.transform); Stretch(b);
            busted = b.gameObject.AddComponent<CanvasGroup>(); busted.alpha = 0f;
            var dim = Rect("Dim", b); Stretch(dim); dim.gameObject.AddComponent<Image>().color = new Color(0.05f, 0, 0.02f, 0.6f);
            var big = Label(b, null, 190, new Color(1f, 0.1f, 0.15f));
            Stretch(big.rectTransform);
            big.text = "BUSTED"; big.fontStyle = FontStyles.Bold | FontStyles.Italic;
            big.outlineWidth = 0.25f; big.outlineColor = Paper;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
        static TMP_Text Label(Transform parent, TMP_FontAsset font, float size, Color col)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = col; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
        }

        // ------------------------------------------------------------------ a cop car's driving

        /// <summary>Catches up along the race line, then goes for the player's car. Comes back if it falls far behind.</summary>
        public class Cop : MonoBehaviour
        {
            RaceManager race; RaceLine line; IVehicle v; IDriverInput input;
            int nearest = -1, index; float steerSmooth, stuck;

            public void Init(RaceManager race, IVehicle v, int index)
            {
                this.race = race; line = race.Line; this.v = v; input = v as IDriverInput; this.index = index;
            }

            void FixedUpdate()
            {
                if (input == null || race.Player == null) return;
                float dt = Time.fixedDeltaTime;
                var p = transform.position;
                var vel = v.Body != null ? v.Body.linearVelocity : Vector3.zero;
                float speed = new Vector3(vel.x, 0, vel.z).magnitude;
                nearest = line.Nearest(p, nearest);
                var target = race.Player.vehicle.Transform;
                var tp = target.position;
                int tn = line.Nearest(tp);
                float gap = Mathf.Repeat(line.distances[tn] - line.distances[nearest], line.length);
                if (gap > line.length * 0.5f) gap -= line.length; // negative: the cop is ahead

                // Far behind (or lost): put it back on the road behind the player, out of sight.
                if (gap > 260f || stuck > 4f)
                {
                    stuck = 0f;
                    float d = line.distances[tn] - 90f - index * 15f;
                    int k = line.IndexAt(Mathf.Repeat(d, line.length));
                    v.Teleport(line.points[k] + Vector3.up * 0.6f, Quaternion.LookRotation(line.Direction(k)), Mathf.Abs(race.Player.vehicle.SpeedKmh) + 20f);
                    return;
                }

                Vector3 aim; float vLimit;
                float playerSpeed = Mathf.Abs(race.Player.vehicle.SpeedKmh) / 3.6f;
                if (gap > 28f)
                {
                    // Chase on the race line, faster than the player.
                    float look = Mathf.Clamp(8f + speed * 0.5f, 10f, 40f);
                    float td = line.distances[nearest] + look;
                    aim = line.PointAt(td) + line.Right(line.IndexAt(td)) * (index % 2 == 0 ? -2f : 2f);
                    vLimit = Mathf.Max(playerSpeed + 12f, 40f);
                    for (int k = 0; k < 4; k++) vLimit = Mathf.Min(vLimit, line.CornerSpeed(nearest + k) * 1.15f);
                }
                else
                {
                    // Close: go for the car, a little ahead of where it's going. One cop aims for each rear quarter.
                    var pv = race.Player.vehicle.Body != null ? race.Player.vehicle.Body.linearVelocity : Vector3.zero;
                    aim = tp + pv * 0.35f + target.right * (index % 2 == 0 ? -1.4f : 1.4f);
                    vLimit = playerSpeed + 6f;
                }

                var local = transform.InverseTransformPoint(aim);
                float ang = Mathf.Atan2(local.x, Mathf.Max(local.z, 0.1f)) * Mathf.Rad2Deg;
                steerSmooth = Mathf.MoveTowards(steerSmooth, Mathf.Clamp(ang / 26f, -1f, 1f), dt * 6f);
                float err = vLimit - speed, throttle, brake = 0f;
                if (err > 1.5f) throttle = 1f;
                else if (err > -1f) throttle = 0.6f;
                else { throttle = 0f; brake = Mathf.Clamp01(-err * 0.1f); }
                stuck = race.CurrentState == RaceManager.State.Racing && speed < 2f ? stuck + dt : 0f;
                input.SetDriverInput(steerSmooth, throttle, brake, 0f, err > 10f && Mathf.Abs(steerSmooth) < 0.2f);
            }
        }

        /// <summary>Red and blue flashers and a two-tone siren.</summary>
        public class LightBar : MonoBehaviour
        {
            Renderer red, blue; Light glow; AudioSource siren;
            Material redOn, blueOn, off;

            public void Build()
            {
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                redOn = new Material(unlit); redOn.SetColor("_BaseColor", new Color(4f, 0.2f, 0.2f));
                blueOn = new Material(unlit); blueOn.SetColor("_BaseColor", new Color(0.2f, 0.5f, 4f));
                off = new Material(unlit); off.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.12f));
                red = Piece("Red", new Vector3(-0.35f, 0, 0)); blue = Piece("Blue", new Vector3(0.35f, 0, 0));
                var lg = new GameObject("Flash"); lg.transform.SetParent(transform, false); lg.transform.localPosition = Vector3.up * 0.3f;
                glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 14f; glow.intensity = 6f;
                siren = gameObject.AddComponent<AudioSource>();
                siren.clip = SirenClip(); siren.loop = true; siren.spatialBlend = 1f; siren.minDistance = 8f; siren.maxDistance = 120f; siren.volume = 0.55f;
                siren.Play();
            }

            Renderer Piece(string name, Vector3 at)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(c.GetComponent<Collider>());
                c.name = name; c.transform.SetParent(transform, false);
                c.transform.localPosition = at; c.transform.localScale = new Vector3(0.6f, 0.14f, 0.25f);
                return c.GetComponent<Renderer>();
            }

            void Update()
            {
                bool a = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
                red.sharedMaterial = a ? redOn : off;
                blue.sharedMaterial = a ? off : blueOn;
                glow.color = a ? new Color(1f, 0.15f, 0.15f) : new Color(0.2f, 0.4f, 1f);
            }

            static AudioClip clip;

            /// <summary>A wailing two-tone siren, made once.</summary>
            static AudioClip SirenClip()
            {
                if (clip != null) return clip;
                const int rate = 22050; const float seconds = 2f;
                int n = (int)(rate * seconds);
                var data = new float[n];
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float f = Mathf.Lerp(620f, 980f, 0.5f + 0.5f * Mathf.Sin(t / seconds * Mathf.PI * 2f));
                    phase += 2 * Mathf.PI * f / rate;
                    data[i] = (Mathf.Sin((float)phase) > 0 ? 0.25f : -0.25f) * 0.6f + Mathf.Sin((float)phase) * 0.2f;
                }
                clip = AudioClip.Create("Siren", n, 1, rate, false);
                clip.SetData(data, 0);
                return clip;
            }
        }
    }
}
