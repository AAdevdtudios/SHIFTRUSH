using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Shift
{
    /// <summary>
    /// A SHIFT world: a short neon run, driven along local +Z from <see cref="start"/> to <see cref="finishZ"/>.
    /// Ring Rush scores rings driven through; Trap Run scores how far you got, minus a share per trap hit.
    /// Built by SHIFT RUSH → SHIFT → Build SHIFT Kit.
    /// </summary>
    public class ShiftArena : MonoBehaviour
    {
        public enum Kind { RingRush, TrapRun }

        public Kind kind;
        public string title = "RING RUSH";
        public Transform start;
        public float finishZ = 300f;
        public float timeLimit = 10f;
        [Header("Ring Rush")]
        public Transform[] rings;
        public float ringRadius = 3.6f;
        public Color ringColor = new Color(1f, 0.25f, 0.85f), ringPassedColor = Color.white;
        [Header("Trap Run")]
        [Tooltip("Share of the score lost per trap hit.")]
        public float hitPenalty = 0.25f;

        public bool Done { get; private set; }
        /// <summary>Reached the finish arch before the clock ran out.</summary>
        public bool Won { get; private set; }
        public float Score { get; private set; }
        public float TimeLeft { get { return Mathf.Max(0f, timeLimit - time); } }
        public float Progress { get; private set; }
        public int RingsPassed { get; private set; }
        public int Hits { get; private set; }
        public event System.Action Pulse; // ring passed or trap hit, for HUD feedback
        /// <summary>Any arena: a ring passed (true) or a trap hit (false), for sound.</summary>
        public static event System.Action<ShiftArena, bool> Feedback;

        IVehicle vehicle;
        float time, prevZ, lastHit;
        bool[] passed;
        MaterialPropertyBlock block;

        public void Begin(IVehicle v)
        {
            vehicle = v;
            Done = false; Won = false; Score = 0f; time = 0f; Progress = 0f; RingsPassed = 0; Hits = 0; lastHit = -9f;
            prevZ = -1f;
            passed = new bool[rings != null ? rings.Length : 0];
            for (int i = 0; i < passed.Length; i++) Tint(i, ringColor);
        }

        public string Summary
        {
            get
            {
                return kind == Kind.RingRush
                    ? RingsPassed + " / " + passed.Length + " RINGS"
                    : (Progress >= 1f ? "FINISHED" : Mathf.RoundToInt(Progress * 100f) + "% DONE") + (Hits > 0 ? "  //  " + Hits + (Hits == 1 ? " HIT" : " HITS") : "  //  CLEAN");
            }
        }

        public void TrapHit()
        {
            if (Done || Time.time - lastHit < 0.6f) return;
            lastHit = Time.time;
            Hits++;
            Pulse?.Invoke();
            Feedback?.Invoke(this, false);
        }

        void Update()
        {
            if (vehicle == null || Done) return;
            time += Time.deltaTime;
            Vector3 local = transform.InverseTransformPoint(vehicle.Transform.position);
            Progress = Mathf.Clamp01(local.z / finishZ);

            if (kind == Kind.RingRush && rings != null)
            {
                for (int i = 0; i < rings.Length; i++)
                {
                    if (passed[i]) continue;
                    Vector3 rl = transform.InverseTransformPoint(rings[i].position);
                    if (prevZ < rl.z && local.z >= rl.z)
                    {
                        float off = new Vector2(local.x - rl.x, (local.y + 0.6f) - rl.y).magnitude;
                        if (off < ringRadius)
                        {
                            passed[i] = true;
                            RingsPassed++;
                            Tint(i, ringPassedColor);
                            Pulse?.Invoke();
                            Feedback?.Invoke(this, true);
                        }
                    }
                }
            }
            prevZ = local.z;

            if (local.z >= finishZ || time >= timeLimit) Finish(local.z >= finishZ);
        }

        void Finish(bool reachedEnd)
        {
            Done = true;
            Won = reachedEnd;
            if (kind == Kind.RingRush) Score = passed.Length > 0 ? RingsPassed / (float)passed.Length : 0f;
            else
            {
                if (reachedEnd) Progress = 1f;
                Score = Mathf.Clamp01(Progress * Mathf.Max(0f, 1f - hitPenalty * Hits));
            }
        }

        void Tint(int i, Color c)
        {
            if (rings == null || rings[i] == null) return;
            var r = rings[i].GetComponent<Renderer>();
            if (r == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", c * 2.5f);
            r.SetPropertyBlock(block);
        }
    }
}
