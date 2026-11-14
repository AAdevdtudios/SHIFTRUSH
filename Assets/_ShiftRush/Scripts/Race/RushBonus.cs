using System.Collections.Generic;
using ShiftRush.Audio;
using ShiftRush.Shift;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftRush.Race
{
    /// <summary>
    /// Nitro you earn by driving on the edge, between SHIFT ramps:
    /// <list type="bullet">
    /// <item>SLIPSTREAM: tuck in close behind a rival at speed. The air tows you (a gentle push) and fills the draft
    /// meter; when it fills you're paid nitro and it starts filling again.</item>
    /// <item>CLOSE CALL: overtake a rival with only a sliver of air between you, without touching.</item>
    /// <item>WALL SKIM: run along a wall or rail within a hand's width, without touching.</item>
    /// </list>
    /// Bonuses landed within a few seconds of each other chain (x2, x3), each paying more.
    /// Resources/SR_RushHUD (built by SHIFT RUSH → Build Main Menu) spawns itself in every race scene.
    /// </summary>
    public class RushBonus : MonoBehaviour
    {
        [Header("Slipstream")]
        [Tooltip("The rival must be this far ahead (metres): closer than the max, not inside the min.")]
        [SerializeField] Vector2 draftRange = new Vector2(2.5f, 22f);
        [Tooltip("How far off the rival's line still counts (metres, centre to centre).")]
        [SerializeField] float draftWidth = 1.7f;
        [SerializeField] float draftMinKmh = 80f;
        [Tooltip("Seconds in the slipstream to fill the meter.")]
        [SerializeField] float draftFillSeconds = 1.6f;
        [Tooltip("Tow while in the slipstream (m/s² forward, at the meter's full strength).")]
        [SerializeField] float draftTow = 2.2f;
        [SerializeField] float draftNitro = 0.12f;

        [Header("Close call")]
        [Tooltip("Centre-to-centre gap that counts as a close pass (about 0.6 m of air for these cars).")]
        [SerializeField] float closeGap = 2.5f;
        [SerializeField] float closeMinKmh = 90f;
        [SerializeField] float closeNitro = 0.06f;

        [Header("Wall skim")]
        [Tooltip("Air between the car's side and a wall that counts as a skim (metres).")]
        [SerializeField] float skimGap = 0.6f;
        [Tooltip("Seconds it must be held.")]
        [SerializeField] float skimHold = 0.35f;
        [SerializeField] float skimMinKmh = 110f;
        [SerializeField] float skimNitro = 0.05f;

        [Header("Chain")]
        [Tooltip("Seconds after a bonus in which the next one chains.")]
        [SerializeField] float chainWindow = 4f;
        [Tooltip("Pay multiplier per link in the chain (x2 pays this much more, x3 twice that...).")]
        [SerializeField] float chainStep = 0.5f;
        [SerializeField] int chainMax = 5;

        [Header("HUD")]
        [SerializeField] CanvasGroup popGroup;
        [SerializeField] TMP_Text popHeadline;
        [SerializeField] TMP_Text popDetail;
        [SerializeField] TMP_Text popChain;
        [SerializeField] CanvasGroup draftGroup;
        [SerializeField] RectTransform draftFill;
        [SerializeField] Image draftFillImage;
        [SerializeField] Color accent = new Color32(255, 64, 217, 255);
        [SerializeField] Color cyan = new Color32(56, 225, 255, 255);

        /// <summary>A bonus paid: its name and the nitro (0..1 of a tank).</summary>
        public static event System.Action<string, float> Awarded;

        class Watch { public float along, minLateral = 99f; public bool alongSet, paid; }
        readonly Dictionary<RaceManager.Racer, Watch> watches = new Dictionary<RaceManager.Racer, Watch>();

        RaceManager race;
        float draft, drafting, skimTime, lastContact = -10f, lastBonus = -10f, popUntil, popPunch;
        bool skimArmed = true;
        int chain;
        Rigidbody contactBody;
        readonly RaycastHit[] hits = new RaycastHit[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Spawn(s); };
            Spawn(SceneManager.GetActiveScene());
        }

        static void Spawn(Scene scene)
        {
            if (FindFirstObjectByType<RushBonus>() != null || FindFirstObjectByType<RaceLine>() == null) return;
            var prefab = Resources.Load<RushBonus>("SR_RushHUD");
            if (prefab == null) return;
            var rb = Instantiate(prefab);
            rb.name = "SR_RushHUD";
            SceneManager.MoveGameObjectToScene(rb.gameObject, scene);
        }

        void Awake()
        {
            if (popGroup != null) popGroup.alpha = 0f;
            if (draftGroup != null) draftGroup.alpha = 0f;
        }

        bool Live(out RaceManager.Racer p)
        {
            p = null;
            race = RaceManager.Current;
            if (race == null || race.CurrentState != RaceManager.State.Racing) return false;
            p = race.Player;
            if (p == null || p.vehicle == null || p.suspended || p.finished || Time.timeScale < 0.5f) return false;
            var sd = ShiftDirector.Instance;
            return sd == null || sd.CurrentState == ShiftDirector.State.Idle;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            RaceManager.Racer p;
            bool live = Live(out p);
            drafting = 0f;
            if (live)
            {
                WatchContacts(p.vehicle);
                Rivals(p, dt);
                Walls(p.vehicle, dt);
            }
            else { watches.Clear(); skimTime = 0f; }

            // Draft meter: fills in the slipstream, drains out of it.
            if (drafting > 0f) draft += dt / draftFillSeconds * drafting;
            else draft = Mathf.MoveTowards(draft, 0f, dt * 0.8f);
            if (draft >= 1f)
            {
                draft = 0f;
                Award(p, "SLIPSTREAM", draftNitro);
            }
            if (Time.time - lastBonus > chainWindow) chain = 0;
            Hud(Time.unscaledDeltaTime);
        }

        void FixedUpdate()
        {
            // The tow: a light push forward while tucked in.
            RaceManager.Racer p;
            if (drafting <= 0f || !Live(out p) || p.vehicle.Body == null) return;
            p.vehicle.Body.AddForce(p.vehicle.Transform.forward * draftTow * drafting, ForceMode.Acceleration);
        }

        void Rivals(RaceManager.Racer p, float dt)
        {
            var me = p.vehicle.Transform;
            float speed = p.vehicle.SpeedKmh;
            Vector3 fwd = me.forward, right = me.right;
            foreach (var r in race.Racers)
            {
                if (r == p || r.vehicle == null || r.suspended) continue;
                Watch w;
                if (!watches.TryGetValue(r, out w)) { w = new Watch(); watches[r] = w; }
                Vector3 d = r.vehicle.Transform.position - me.position;
                float along = Vector3.Dot(d, fwd), lateral = Mathf.Abs(Vector3.Dot(d, right));
                bool sameWay = Vector3.Dot(r.vehicle.Transform.forward, fwd) > 0.8f;

                // Slipstream: close behind, on its line, both going the same way at speed.
                if (sameWay && speed > draftMinKmh && along > draftRange.x && along < draftRange.y && lateral < draftWidth)
                {
                    float k = 1f - Mathf.InverseLerp(draftRange.x, draftRange.y, along) * 0.6f; // stronger closer in
                    drafting = Mathf.Max(drafting, k * (1f - lateral / draftWidth * 0.5f));
                }

                // Close call: we go from behind it to past it with a sliver of air between, untouched.
                if (Mathf.Abs(along) < 6f) w.minLateral = Mathf.Min(w.minLateral, lateral);
                if (w.alongSet && !w.paid && w.along > 0f && along <= 0f && sameWay && speed > closeMinKmh
                    && w.minLateral < closeGap && Time.time - lastContact > 1f)
                {
                    w.paid = true; // once per pass: clear of it again before the next
                    Award(p, "CLOSE CALL", closeNitro);
                }
                if (Mathf.Abs(along) >= 6f) { w.minLateral = 99f; w.paid = false; }
                w.along = along; w.alongSet = true;
            }
        }

        void Walls(IVehicle v, float dt)
        {
            var me = v.Transform;
            float speed = v.SpeedKmh;
            float half = 0.95f;
            var col = v.Body != null ? v.Body.GetComponentInChildren<Collider>() : null;
            if (col != null) half = Mathf.Max(0.6f, Vector3.Dot(col.bounds.extents, new Vector3(Mathf.Abs(me.right.x), Mathf.Abs(me.right.y), Mathf.Abs(me.right.z))));
            Vector3 origin = me.position + me.up * 0.55f;
            float gap = Mathf.Min(SideGap(origin, me.right, half, me), SideGap(origin, -me.right, half, me));
            bool close = speed > skimMinKmh && gap < skimGap && v.AllWheelsGrounded && Time.time - lastContact > 0.6f;
            if (close && skimArmed)
            {
                skimTime += dt;
                if (skimTime >= skimHold) { Award(race.Player, "WALL SKIM", skimNitro); skimArmed = false; }
            }
            else if (!close) skimTime = 0f;
            if (gap > skimGap * 2.5f) skimArmed = true; // pull away from the wall to earn the next one
        }

        // Air between the car's side and the nearest solid thing that side (not a car), or a large number.
        float SideGap(Vector3 origin, Vector3 dir, float half, Transform me)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hits, half + skimGap * 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = 99f;
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].transform;
                if (t.IsChildOf(me) || IsCar(t)) continue;
                if (Mathf.Abs(Vector3.Dot(hits[i].normal, Vector3.up)) > 0.6f) continue; // the road, not a wall
                best = Mathf.Min(best, hits[i].distance - half);
            }
            return best;
        }

        bool IsCar(Transform t)
        {
            foreach (var r in race.Racers) if (r.vehicle != null && t.IsChildOf(r.vehicle.Transform)) return true;
            return false;
        }

        // Any knock cancels close calls and skims for a moment: they're for clean driving.
        void WatchContacts(IVehicle v)
        {
            if (v.Body == null || contactBody == v.Body) return;
            contactBody = v.Body;
            var c = contactBody.GetComponent<ContactTap>();
            if (c == null) c = contactBody.gameObject.AddComponent<ContactTap>();
            c.owner = this;
        }

        internal void Touched() { lastContact = Time.time; skimTime = 0f; }

        void Award(RaceManager.Racer p, string label, float nitro)
        {
            if (p == null) return;
            chain = Time.time - lastBonus <= chainWindow ? Mathf.Min(chain + 1, chainMax) : 1;
            lastBonus = Time.time;
            float pay = nitro * (1f + (chain - 1) * chainStep) * Core.Difficulty.Current.bonusNitro;
            if (p.vehicle is INitroTank tank && tank.Nitro01 >= 0f) tank.AddNitro(pay);
            Awarded?.Invoke(label, pay);

            if (popHeadline != null) popHeadline.text = label;
            if (popDetail != null) popDetail.text = "+" + Mathf.RoundToInt(pay * 100f) + "% NITRO";
            if (popChain != null) popChain.text = chain > 1 ? "x" + chain : "";
            popUntil = Time.unscaledTime + 1.6f;
            popPunch = 1f;
            // A bell that climbs with the chain, over the nitro shimmer.
            GameSfx.Play(Sfx.Overtake, 1.2599f * (1f + 0.12f * (chain - 1)), 2.2f);
            GameSfx.Play(Sfx.NitroGain, 1f + 0.06f * (chain - 1), 0.7f);
        }

        void Hud(float dt)
        {
            popPunch = Mathf.MoveTowards(popPunch, 0f, dt * 4f);
            bool showPop = Time.unscaledTime < popUntil;
            if (popGroup != null)
            {
                popGroup.alpha = Mathf.MoveTowards(popGroup.alpha, showPop ? 1f : 0f, dt * (showPop ? 10f : 3f));
                float e = popPunch * popPunch;
                popGroup.transform.localScale = Vector3.one * (1f + 0.22f * e);
            }
            if (popHeadline != null) popHeadline.color = Color.Lerp(Color.white, accent, popPunch);
            if (draftGroup != null)
                draftGroup.alpha = Mathf.MoveTowards(draftGroup.alpha, draft > 0.02f ? 1f : 0f, dt * 5f);
            if (draftFill != null) draftFill.anchorMax = new Vector2(Mathf.Clamp01(draft), 1f);
            if (draftFillImage != null) draftFillImage.color = Color.Lerp(cyan, Color.white, drafting > 0f ? Mathf.Repeat(Time.unscaledTime * 3f, 1f) * 0.35f : 0f);
        }
    }

    /// <summary>Tells <see cref="RushBonus"/> when the player's car body hits something (not the road).</summary>
    public class ContactTap : MonoBehaviour
    {
        internal RushBonus owner;

        void OnCollisionEnter(Collision c) { Report(c); }
        void OnCollisionStay(Collision c) { Report(c); }

        void Report(Collision c)
        {
            if (owner == null || c.contactCount == 0) return;
            if (c.GetContact(0).normal.y > 0.6f) return; // landing on the road
            owner.Touched();
        }
    }
}
