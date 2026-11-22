using System.Collections.Generic;
using ShiftRush.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Lab
{
    /// <summary>
    /// Runs the MovesLab test scene: spawns parked cars and two cruisers to roll over and around, and draws a
    /// tuning overlay (F1 hides it). Built by SHIFT RUSH > Grip Pro > Build MovesLab.
    /// </summary>
    public class MovesLab : MonoBehaviour
    {
        [System.Serializable]
        public struct Cruiser { public GameObject prefab; public float lane; public float kmh; }

        public GameObject[] parkedPrefabs;
        public Transform[] parkedSpots;
        public Cruiser[] cruisers;
        public Vector3[] cruisePath;

        GripMoves player;
        DriftMoves playerDrift;
        IVehicle playerCar;
        bool showOverlay = true;
        float lineTurn;
        Vector3 lastVel;
        readonly List<string> log = new List<string>();
        GUIStyle box, title, line, small;

        void Start()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IVehicle v) { playerCar = v; player = mb.GetComponent<GripMoves>(); if (player == null) player = mb.gameObject.AddComponent<GripMoves>(); if (mb.GetComponent<VFX.SpeedAura>() == null) mb.gameObject.AddComponent<VFX.SpeedAura>(); playerDrift = mb.GetComponent<DriftMoves>(); if (playerDrift == null) playerDrift = mb.gameObject.AddComponent<DriftMoves>(); break; }

            for (int i = 0; i < parkedSpots.Length && parkedPrefabs.Length > 0; i++)
            {
                var s = parkedSpots[i];
                var v = VehicleSpawn.Rival(parkedPrefabs[i % parkedPrefabs.Length], s.position, s.rotation);
                if (v == null) continue;
                v.Transform.name = "Target " + (i + 1);
                var r = v.Transform.GetComponent<VehicleReset>();
                if (r != null) r.listenForInput = false;
            }

            int n = 0;
            foreach (var c in cruisers)
            {
                if (c.prefab == null || cruisePath == null || cruisePath.Length < 3) continue;
                int start = (cruisePath.Length / 2 + n * 25) % cruisePath.Length;
                Vector3 dir = (cruisePath[(start + 1) % cruisePath.Length] - cruisePath[start]).normalized;
                Vector3 pos = cruisePath[start] + Vector3.Cross(Vector3.up, dir) * c.lane + Vector3.up * 0.6f;
                var v = VehicleSpawn.Rival(c.prefab, pos, Quaternion.LookRotation(dir));
                if (v == null) continue;
                v.Transform.name = "Cruiser " + (++n);
                var cr = v.Transform.gameObject.AddComponent<LabCruiser>();
                cr.path = cruisePath; cr.lane = c.lane; cr.targetKmh = c.kmh;
            }

            // In the lab 1 / 2 / 3 pick the drift type, so RCC's manual gear keys come off them here (restored on leaving).
            FreeGearKeys(true);
            if (playerDrift != null && GetComponent<DriftTuningHud>() == null) gameObject.AddComponent<DriftTuningHud>().drift = playerDrift;

            GripMoves.Leapt += OnLeap;
            GripMoves.Landed += OnLand;
            KnockTumble.Knocked += OnKnock;
            DriftMoves.TierUp += OnTier;
            DriftMoves.Ended += OnDriftEnd;
        }

        void OnTier(DriftMoves d, int tier) { if (d == playerDrift) Log("Drift tier " + tier); }
        void OnDriftEnd(DriftMoves d, int tier, float held) { if (d == playerDrift) Log(d.LastResult + "   " + held.ToString("F1") + " s"); }

        static void FreeGearKeys(bool free)
        {
            var asset = ControlBindings.Asset;
            if (asset == null) return;
            foreach (var name in new[] { "Gear_1", "Gear_2", "Gear_3" })
            {
                var action = asset.FindAction(name);
                if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].path.StartsWith("<Keyboard>"))
                    {
                        if (free) action.ApplyBindingOverride(i, string.Empty);
                        else action.RemoveBindingOverride(i);
                    }
            }
        }

        void OnDestroy()
        {
            FreeGearKeys(false);
            GripMoves.Leapt -= OnLeap;
            GripMoves.Landed -= OnLand;
            KnockTumble.Knocked -= OnKnock;
            DriftMoves.TierUp -= OnTier;
            DriftMoves.Ended -= OnDriftEnd;
        }

        void OnKnock(GameObject attacker, GameObject victim, float damage, int chain)
        {
            if (player == null || attacker != player.gameObject) return;
            Log((chain > 1 ? "  Chain " + chain + ": " : "Knocked ") + victim.name + "   " + damage.ToString("F0") + " dmg");
        }

        void OnLeap(GripMoves m, int dir) { if (m == player) Log(m.LastMove); }
        void OnLand(GripMoves m, bool clean, float air) { if (m == player) Log(m.LastMove + "   " + air.ToString("F2") + " s air"); }

        void Log(string s)
        {
            log.Insert(0, Time.time.ToString("F1") + "  " + s);
            if (log.Count > 6) log.RemoveAt(log.Count - 1);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) showOverlay = !showOverlay;

            // Drift tuning: 1 drifty, 2 balanced, 3 grippy on the player's car (numbers from Resources/SR_DriftTuning).
            if (kb != null && playerDrift != null)
            {
                DriftCharacter pick = DriftCharacter.Auto;
                if (kb.digit1Key.wasPressedThisFrame) pick = DriftCharacter.Drifty;
                if (kb.digit2Key.wasPressedThisFrame) pick = DriftCharacter.Balanced;
                if (kb.digit3Key.wasPressedThisFrame) pick = DriftCharacter.Grippy;
                if (pick != DriftCharacter.Auto) { playerDrift.character = pick; playerDrift.customTuning = false; playerDrift.ApplyCharacter(); Log("Drift car: " + pick); }
            }

            // How fast the car's line is bending, degrees a second (smoothed), for the drift readout.
            if (playerCar != null && playerCar.Body != null && Time.deltaTime > 0f)
            {
                Vector3 v = Vector3.ProjectOnPlane(playerCar.Body.linearVelocity, Vector3.up);
                if (v.sqrMagnitude > 1f && lastVel.sqrMagnitude > 1f)
                    lineTurn = Mathf.Lerp(lineTurn, Mathf.Abs(Vector3.SignedAngle(lastVel, v, Vector3.up)) / Time.deltaTime, Time.deltaTime * 6f);
                lastVel = v;
            }
        }

        void OnGUI()
        {
            if (!showOverlay || player == null) return;
            if (box == null)
            {
                var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.05f, 0.04f, 0.09f, 0.82f)); bg.Apply();
                box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
                title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.35f, 0.75f) } };
                line = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
                small = new GUIStyle(line) { fontSize = 11, normal = { textColor = new Color(0.75f, 0.75f, 0.85f) } };
            }
            float top = Mathf.Max(84f, Screen.height * 0.16f); // below the HUD's FREE ROAM tag, which scales with the screen
            const float height = 336f;
            float x = 16f, y = top, w = 330f;
            GUI.Box(new Rect(x, y, w, height), GUIContent.none, box);
            x += 12f; y += 8f;
            GUI.Label(new Rect(x, y, w, 22f), "MOVES LAB", title); y += 24f;
            GUI.Label(new Rect(x, y, w, 18f), "Speed  " + (playerCar.Body.linearVelocity.magnitude * 3.6f).ToString("F0") + " km/h", line); y += 18f;
            Bar(x, ref y, "Leap", player.LeapReady01, player.Leaping ? "IN AIR" : null);
            Bar(x, ref y, "Wall grip", player.GripFactor, player.WallRiding ? "ON WALL" : null);
            if (playerDrift != null)
            {
                Bar(x, ref y, "Drift " + playerDrift.Tier, playerDrift.Drifting ? playerDrift.NextTier01 : 0f,
                    playerDrift.Drifting ? playerDrift.SlipAngle.ToString("F0") + "°" : null);
                Vector3 vel = Vector3.ProjectOnPlane(playerCar.Body.linearVelocity, Vector3.up);
                Vector3 fwd = Vector3.ProjectOnPlane(playerCar.Transform.forward, Vector3.up);
                float slide = vel.sqrMagnitude > 1f ? Mathf.Abs(Vector3.SignedAngle(vel, fwd, Vector3.up)) : 0f;
                GUI.Label(new Rect(x, y, w, 18f), "Car: " + playerDrift.Character + "   1 drifty  2 balanced  3 grippy", small); y += 15f;
                GUI.Label(new Rect(x, y, w, 18f), "Slide " + slide.ToString("F0") + "°   line " + lineTurn.ToString("F0") + "°/s   " + (vel.magnitude * 3.6f).ToString("F0") + " km/h true", small); y += 17f;
            }
            GUI.Label(new Rect(x, y, w, 18f), "Clean landings  " + player.CleanLandings, line); y += 22f;
            foreach (var s in log) { GUI.Label(new Rect(x, y, w, 16f), s, small); y += 15f; }
            y = top + height - 70f;
            GUI.Label(new Rect(x, y, w - 20f, 64f),
                "Q / E  spiral roll left / right      Q + E  forward spiral roll\nRoll into the pink wall to ride it, Q / E to roll off\nPink pipe: drive up it above " + player.gripMinKmh.ToString("F0") + " km/h\nSPACE + steer  drift, let go to boost      F1  hide", small);
        }

        void Bar(float x, ref float y, string label, float v, string tag)
        {
            GUI.Label(new Rect(x, y, 80f, 18f), label, line);
            GUI.Box(new Rect(x + 80f, y + 4f, 160f, 10f), GUIContent.none);
            var old = GUI.color;
            GUI.color = v >= 1f ? new Color(0.3f, 1f, 0.75f) : new Color(1f, 0.35f, 0.75f);
            GUI.DrawTexture(new Rect(x + 80f, y + 4f, 160f * Mathf.Clamp01(v), 10f), Texture2D.whiteTexture);
            GUI.color = old;
            if (tag != null) GUI.Label(new Rect(x + 248f, y, 80f, 18f), tag, line);
            y += 20f;
        }
    }
}
