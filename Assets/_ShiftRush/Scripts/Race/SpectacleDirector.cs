using System.Collections;
using System.Collections.Generic;
using ShiftRush.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftRush.Race
{
    /// <summary>
    /// The show around the race, timed to race moments so it lands as drama, not noise:
    /// - All race: the Channel 13 news chopper shadows whoever's leading, searchlight cone on them, rotors chopping.
    ///   On police episodes the police chopper joins and sits on the player.
    /// - Final lap: an air-show formation (gold lead, four wingmen) comes in head-on low over the road ahead of the
    ///   player trailing magenta, cyan, white and gold smoke, booms overhead, then bomb-bursts apart. Every story
    ///   race, most quick races.
    /// - Background: hot-air balloons, fan zones at the big corners (<see cref="FanZones"/>), checkpoint arches and
    ///   corner furniture (<see cref="TrackDressing"/>), the driving line (<see cref="DrivingLine"/>) and, on the
    ///   tracks that have one, the giant (<see cref="GiantMech"/>).
    /// Cheap by design: a handful of low-poly models, no colliders, trails instead of particles, two spot lights.
    /// Spawns itself in any race scene; aircraft come from Resources/SR_Spectacle.
    /// </summary>
    public class SpectacleDirector : MonoBehaviour
    {
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);
        static readonly Color Gold = new Color32(255, 196, 46, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Spawn(s); };
            Spawn(SceneManager.GetActiveScene());
        }

        static void Spawn(Scene scene)
        {
            if (FindFirstObjectByType<SpectacleDirector>() != null || FindFirstObjectByType<RaceLine>() == null) return;
            if (SpectacleKit.Instance == null) return;
            var go = new GameObject("Spectacle Director");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<SpectacleDirector>();
        }

#if UNITY_EDITOR
        /// <summary>Editor tuning aid: a file path to screenshot the moment the jets pass over the player.</summary>
        public static string CaptureFlyover;
#endif

        RaceManager race;
        RaceLine line;
        SpectacleKit kit;
        Chopper news, police;
        bool parade;

        IEnumerator Start()
        {
            kit = SpectacleKit.Instance;
            // Wait for the race (and the story drop) to set up.
            while ((race = RaceManager.Current) == null || race.Line == null) yield return null;
            line = race.Line;
            Balloons();
            FanZones.Build(line, kit, transform);
            TrackDressing.Build(line, kit, transform);
            DrivingLine.Build(line, kit, transform);
            GiantMech.Build(kit, line, transform);
            while (race.CurrentState == RaceManager.State.Countdown) yield return null;

            news = Chopper.Make(kit.newsChopper, "Channel 13 Chopper", new Color(1f, 0.95f, 0.85f), transform);
            race.FinalLap += OnFinalLap;

            yield return new WaitForSeconds(6f);
            if (PoliceChase.Current != null) police = Chopper.Make(kit.policeChopper, "Police Chopper", new Color(0.75f, 0.85f, 1f), transform, true);
        }

        void OnDestroy() { if (race != null) race.FinalLap -= OnFinalLap; }

        void Update()
        {
            if (race == null || race.Racers.Count == 0) return;
            float dt = Time.deltaTime;
            // The news chopper follows the story: the leader, or the player when they're in the top two.
            var p = race.Player;
            var star = p != null && race.PositionOf(p) <= 2 && !p.finished ? p : race.Racers[0];
            // Both fly ahead of their car, high enough to clear the road, low enough to sit in the top of the shot,
            // turned to face it like a camera crew.
            if (news != null && star != null && star.vehicle != null) news.Follow(star.vehicle.Transform, new Vector3(17f, 15f, 36f), dt);
            if (police != null && p != null && p.vehicle != null) police.Follow(p.vehicle.Transform, new Vector3(-19f, 13f, 28f), dt);
        }

        void OnFinalLap(RaceManager.Racer r)
        {
            if (!r.isPlayer || parade) return;
            bool story = !string.IsNullOrEmpty(StoryProgress.Active);
            if (!story && Random.value > 0.6f) return;
            parade = true;
            StartCoroutine(Parade(r));
        }

        // ------------------------------------------------------------------ the final-lap jet parade

        IEnumerator Parade(RaceManager.Racer r)
        {
            yield return new WaitForSeconds(Random.Range(5f, 10f));
            if (r.vehicle == null || r.finished || kit.wingJet == null) yield break;

            // The formation flies back down the road towards the player, low over the lane, following its curves
            // (so it never meets a mountain or a tower and always comes out of the fog right over the road), passes
            // overhead, then pulls up and bursts apart.
            var car = r.vehicle.Transform;
            float v = Mathf.Max(20f, r.vehicle.SpeedKmh / 3.6f);
            const float JetSpeed = 95f, Height = 22f, Lead = 6f;
            float playerD = line.distances[line.Nearest(car.position)];
            float meetD = playerD + Mathf.Clamp(v * Lead, 150f, 450f);
            float s = meetD + JetSpeed * Lead; // along the lap, counting down towards the player

            var slots = new[] { new Vector3(0, 0, 0), new Vector3(-9, -1.5f, -11), new Vector3(9, -1.5f, -11), new Vector3(0, -3f, -22), new Vector3(0, 6f, -34) };
            var smoke = new[] { Gold, Magenta, Cyan, Color.white, Magenta };
            var jets = new List<Transform>();
            for (int k = 0; k < slots.Length; k++) jets.Add(Jet(k == 0 && kit.leadJet != null ? kit.leadJet : kit.wingJet, smoke[k]));
            var roar = Roar(jets[0]);

            Vector3 pos = RoadPoint(s, Height), fwd = (RoadPoint(s - 5f, Height) - pos).normalized;
            float bank = 0f, burstT = -1f;
            Vector3 burstVel = Vector3.zero;
            bool boomed = false;
            int hint = line.Nearest(car.position);
            for (float life = 0f; life < 25f; life += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                if (burstT < 0f)
                {
                    s -= JetSpeed * dt;
                    Vector3 next = RoadPoint(s, Height);
                    Vector3 ahead = (RoadPoint(s - 30f, Height) - next).normalized;
                    // Bank into the road's curve: how fast the heading turns.
                    float turn = Vector3.SignedAngle(fwd, ahead, Vector3.up);
                    bank = Mathf.Lerp(bank, Mathf.Clamp(-turn * 2.2f, -60f, 60f), dt * 3f);
                    fwd = Vector3.Slerp(fwd, ahead, dt * 4f);
                    pos = next;
                    // Past the player along the road (not in a straight line: switchbacks double back on
                    // themselves): pull up and break.
                    if (car != null)
                    {
                        hint = line.Nearest(car.position, hint);
                        float L = line.length, gap = Mathf.Repeat(s - line.distances[hint] + L * 0.5f, L) - L * 0.5f;
                        if (gap < -50f) { burstT = 0f; burstVel = fwd * JetSpeed; }
                    }
                }
                else
                {
                    burstT += dt;
                    if (burstT > 6f) break; // gone into the sky
                    burstVel += Vector3.up * 18f * dt;
                    pos += burstVel * dt;
                    fwd = burstVel.normalized;
                }
                var rot = Quaternion.LookRotation(fwd, Vector3.up);
                for (int k = 0; k < jets.Count; k++)
                {
                    if (jets[k] == null) continue;
                    Vector3 off = rot * slots[k];
                    float roll = bank;
                    if (burstT >= 0f && k > 0)
                    {
                        // Bomb burst: each wingman peels away its own side, climbing and rolling.
                        Vector3 side = rot * new Vector3(Mathf.Sign(slots[k].x + (k == 3 ? 0f : 0.01f)) * (k == 3 ? 0f : 1f), k == 3 ? -0.4f : 0.5f, 0f);
                        off += side * (burstT * burstT * 28f);
                        roll += (k % 2 == 0 ? 1f : -1f) * Mathf.Min(1f, burstT) * 180f;
                    }
                    jets[k].SetPositionAndRotation(pos + off, rot * Quaternion.Euler(0f, 0f, roll));
                }
                if (!boomed && car != null && (pos - car.position).magnitude < 45f)
                {
                    boomed = true;
                    CameraFX.SpeedFeel.Jolt(0.85f);
                    Debug.Log("[SPECTACLE] jets over the player, " + (pos - car.position).magnitude.ToString("0") + " m");
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(CaptureFlyover)) { ScreenCapture.CaptureScreenshot(CaptureFlyover, 1); CaptureFlyover = null; }
#endif
                }
                yield return null;
            }
            if (roar != null) Destroy(roar);
            foreach (var j in jets) if (j != null) Destroy(j.gameObject, 6f); // let the smoke fade out
        }

        /// <summary>A point over the road at this distance round the lap, this high above it.</summary>
        Vector3 RoadPoint(float d, float height) { return line.PointAt(Mathf.Repeat(d, line.length)) + Vector3.up * height; }

        Transform Jet(GameObject prefab, Color smoke)
        {
            var go = Instantiate(prefab, transform);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (var r in rs) { b.Encapsulate(r.bounds); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            // Smoke from the tail.
            var tail = new GameObject("Smoke").transform;
            tail.SetParent(go.transform, false);
            tail.localPosition = new Vector3(0f, 0f, -b.extents.z * 0.9f);
            var tr = tail.gameObject.AddComponent<TrailRenderer>();
            tr.time = 5.5f; tr.minVertexDistance = 3f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1.6f), new Keyframe(0.25f, 4f), new Keyframe(1f, 9f));
            tr.material = SmokeMaterial();
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(smoke, 0f), new GradientColorKey(Color.Lerp(smoke, Color.white, 0.5f), 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Material smokeMat;
        static Material SmokeMaterial()
        {
            if (smokeMat != null) return smokeMat;
            if (SpectacleKit.Instance != null && SpectacleKit.Instance.smoke != null) return smokeMat = SpectacleKit.Instance.smoke;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            smokeMat = new Material(sh);
            smokeMat.SetFloat("_Surface", 1f); // transparent
            smokeMat.SetFloat("_Blend", 0f);   // alpha
            smokeMat.SetOverrideTag("RenderType", "Transparent");
            smokeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            smokeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            smokeMat.SetInt("_ZWrite", 0);
            smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            smokeMat.renderQueue = 3000;
            return smokeMat;
        }

        /// <summary>The formation's roar: a low rumble loop on the lead, with doppler as it passes.</summary>
        static AudioSource Roar(Transform lead)
        {
            var src = lead.gameObject.AddComponent<AudioSource>();
            src.clip = Rumble();
            src.loop = true; src.spatialBlend = 1f; src.dopplerLevel = 1.4f;
            src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 30f; src.maxDistance = 700f;
            src.volume = 0.9f; src.priority = 40;
            src.Play();
            return src;
        }

        static AudioClip rumble;
        static AudioClip Rumble()
        {
            if (rumble != null) return rumble;
            const int rate = 44100; int n = rate * 2;
            var data = new float[n];
            float brown = 0f, hiss = 0f;
            var rng = new System.Random(13);
            for (int i = 0; i < n; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                brown = Mathf.Clamp(brown + w * 0.04f, -1f, 1f) * 0.998f;
                hiss = hiss * 0.6f + w * 0.4f;
                data[i] = brown * 0.8f + hiss * 0.12f;
            }
            // Fade the loop seam.
            for (int i = 0; i < 2000; i++) { float k = i / 2000f; data[i] = Mathf.Lerp(data[n - 2000 + i], data[i], k); }
            rumble = AudioClip.Create("Jet Rumble", n, 1, rate, false);
            rumble.SetData(data, 0);
            return rumble;
        }

        // ------------------------------------------------------------------ balloons

        void Balloons()
        {
            if (kit.balloon == null || line.points == null || line.points.Length < 4) return;
            var rng = new System.Random(line.points.Length);
            // A sky full of them: some close enough to read (150-300 m off the road, low), some far off and high.
            for (int k = 0; k < 7; k++)
            {
                bool near = k < 4;
                int i = (int)(rng.NextDouble() * line.points.Length);
                Vector3 p = line.points[i];
                float off = near ? 150f + (float)rng.NextDouble() * 150f : 380f + (float)rng.NextDouble() * 320f;
                Vector3 side = line.Right(i) * ((rng.NextDouble() > 0.5 ? 1f : -1f) * off);
                Vector3 pos = p + side + Vector3.up * (near ? 45f + (float)rng.NextDouble() * 35f : 90f + (float)rng.NextDouble() * 70f);
                var go = Instantiate(kit.balloon, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), transform);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = Vector3.one * 1.6f;
                go.AddComponent<Drift>();
            }
        }

        /// <summary>A balloon's slow drift and bob.</summary>
        class Drift : MonoBehaviour
        {
            Vector3 home, wind; float seed;
            void Start() { home = transform.position; seed = Random.value * 10f; wind = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * 1.2f; }
            void Update()
            {
                float t = Time.time;
                transform.position = home + wind * t + Vector3.up * Mathf.Sin(t * 0.25f + seed) * 3f;
                transform.Rotate(0f, Time.deltaTime * 2f, 0f);
            }
        }

        // ------------------------------------------------------------------ choppers

        /// <summary>A helicopter that shadows a car: smooth, banking into its turns, rotors spinning, a searchlight
        /// with a soft beam cone on the car, and the chop of its blades.</summary>
        class Chopper
        {
            Transform root, main, tail, beamRoot;
            Light spot;
            Vector3 vel;
            float bank;

            public static Chopper Make(GameObject prefab, string name, Color light, Transform parent, bool police = false)
            {
                if (prefab == null) return null;
                var c = new Chopper();
                var go = Instantiate(prefab, parent);
                go.name = name;
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                c.root = go.transform;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Propeller" || t.name == "Propellers") c.main = t;
                    else if (t.name == "SmallPropeller" || t.name == "RearPropeller") c.tail = t;
                }
                // Searchlight under the nose, aimed each frame at the car.
                c.beamRoot = new GameObject("Searchlight").transform;
                c.beamRoot.SetParent(c.root, false);
                c.beamRoot.localPosition = new Vector3(0f, -1.2f, 2f);
                c.spot = c.beamRoot.gameObject.AddComponent<Light>();
                c.spot.type = LightType.Spot; c.spot.color = light; c.spot.intensity = police ? 40f : 30f;
                c.spot.range = 90f; c.spot.spotAngle = 22f; c.spot.innerSpotAngle = 10f; c.spot.shadows = LightShadows.None;
                // The visible beam only on the news chopper: the police one flies so close to the camera that its
                // cone would wash over the screen. Its light still lands on the road.
                if (!police) Cone(light).SetParent(c.beamRoot, false);
                // Start out of shot, behind and above.
                var cam = Camera.main;
                c.root.position = (cam != null ? cam.transform.position : Vector3.zero) + new Vector3(0f, 80f, -120f);
                var src = go.AddComponent<AudioSource>();
                src.clip = Chop(); src.loop = true; src.spatialBlend = 1f; src.dopplerLevel = 0.5f;
                src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 15f; src.maxDistance = 160f; src.volume = 0.55f; src.priority = 90;
                src.Play();
                return c;
            }

            public void Follow(Transform car, Vector3 offset, float dt)
            {
                if (root == null || car == null) return;
                Vector3 fwd = Vector3.ProjectOnPlane(car.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3 want = car.position + right * offset.x + Vector3.up * offset.y + fwd * offset.z;
                Vector3 pos = Vector3.SmoothDamp(root.position, want, ref vel, 1.1f, 160f, dt);
                root.position = pos;
                // Turned to face the car it's filming; leaning the way it's moving (pitch back as it flies away
                // from the camera, roll into sideways moves), so it reads as a real chopper keeping station.
                Vector3 look = Vector3.ProjectOnPlane(car.position - root.position, Vector3.up);
                if (look.sqrMagnitude < 1f) look = fwd;
                var face = Quaternion.LookRotation(look, Vector3.up);
                Vector3 localVel = Quaternion.Inverse(face) * vel;
                bank = Mathf.Lerp(bank, Mathf.Clamp(-localVel.x * 0.5f, -22f, 22f), dt * 2f);
                float pitch = Mathf.Clamp(localVel.z * 0.25f, -16f, 16f);
                root.rotation = Quaternion.Slerp(root.rotation, face * Quaternion.Euler(pitch, 0f, bank), dt * 3f);
                if (main != null) main.Rotate(0f, 1500f * dt, 0f, Space.Self);
                if (tail != null) tail.Rotate(2400f * dt, 0f, 0f, Space.Self);
                if (beamRoot != null) beamRoot.rotation = Quaternion.LookRotation(car.position - beamRoot.position, Vector3.up);
            }

            /// <summary>A soft additive cone along the searchlight beam: reads as light in the air, costs a draw call.</summary>
            static Transform Cone(Color c)
            {
                var go = new GameObject("Beam");
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = ConeMesh();
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(SmokeMaterial()) { name = "Beam" };
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // additive glow
                mat.color = new Color(c.r, c.g, c.b, 0.025f); // a hint in daylight, a beam at dusk
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = new Vector3(1f, 1f, 1f);
                return go.transform;
            }

            static Mesh cone;
            static Mesh ConeMesh()
            {
                if (cone != null) return cone;
                const int seg = 20; const float len = 45f, radius = 8.5f;
                var v = new List<Vector3> { Vector3.zero };
                var tris = new List<int>();
                for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, len)); }
                for (int i = 1; i <= seg; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); tris.Add(0); tris.Add(i + 1); tris.Add(i); }
                cone = new Mesh { name = "Searchlight Beam" };
                cone.SetVertices(v); cone.SetTriangles(tris, 0); cone.RecalculateBounds();
                return cone;
            }

            static AudioClip chop;
            static AudioClip Chop()
            {
                if (chop != null) return chop;
                const int rate = 22050; int n = rate; // one second loop at 11 blade passes a second
                var data = new float[n];
                var rng = new System.Random(5);
                float low = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float phase = Mathf.Repeat(t * 11f, 1f);
                    float pulse = Mathf.Exp(-phase * 9f);
                    float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                    low = low * 0.85f + w * 0.15f;
                    data[i] = low * pulse * 1.6f + Mathf.Sin(t * 2f * Mathf.PI * 55f) * pulse * 0.25f;
                }
                chop = AudioClip.Create("Rotor Chop", n, 1, rate, false);
                chop.SetData(data, 0);
                return chop;
            }
        }
    }
}
