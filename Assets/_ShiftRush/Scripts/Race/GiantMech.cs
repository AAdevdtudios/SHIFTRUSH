using ShiftRush.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShiftRush.Race
{
    /// <summary>
    /// A 36 m mech standing off the circuit (the city's harbour side, wherever the ground is lowest), turned towards
    /// the race and watching it: a slow, heavy look-around borrowed from a humanoid idle and played at half speed.
    /// On the player's final lap it taunts, and the ground shakes.
    /// Kept cheap: one instance, no colliders, no shadows, and its animator stops completely whenever it's off
    /// screen. Only in the scenes the kit lists.
    /// </summary>
    public class GiantMech : MonoBehaviour
    {
        const float Height = 36f, Distance = 330f, PlaySpeed = 0.5f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idle, taunt;
        float tauntUntil = -1f;
        RaceManager race;
        Transform body;

        public static GiantMech Build(SpectacleKit kit, RaceLine line, Transform parent)
        {
            if (kit == null || kit.mech == null || line == null || line.points == null) return null;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (kit.mechScenes == null || System.Array.IndexOf(kit.mechScenes, scene) < 0) return null;

            // Where a giant can be seen: candidates off both sides of the road at a few distances, each scored by how
            // much of the road around it has a clear line of sight to the mech's chest (buildings and hills block
            // it). Lower ground breaks ties (a harbour, a valley).
            Vector3 best = Vector3.zero, faceTo = Vector3.zero; float bestScore = float.MinValue;
            int n = line.points.Length;
            for (int s = 0; s < 24; s++)
            {
                int i = s * n / 24;
                foreach (float dist in new[] { 260f, Distance, 430f })
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 p = line.points[i] + line.Right(i) * side * dist;
                        if (!Physics.Raycast(p + Vector3.up * 400f, Vector3.down, out var hit, 800f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (hit.point.y > line.points[i].y + 6f) continue; // a roof or a hilltop: it stands on the ground
                        Vector3 chest = hit.point + Vector3.up * Height * 0.6f;
                        int clear = 0;
                        for (int k = -6; k <= 6; k++)
                        {
                            Vector3 eye = line.points[(i + k * n / 60 + n) % n] + Vector3.up * 3f;
                            Vector3 to = chest - eye;
                            if (!Physics.Raycast(eye, to.normalized, to.magnitude - 5f, ~0, QueryTriggerInteraction.Ignore)) clear++;
                        }
                        float score = clear * 10f - hit.point.y * 0.05f;
                        if (score > bestScore) { bestScore = score; best = hit.point; faceTo = line.points[i]; }
                    }
            }
            if (bestScore == float.MinValue) return null;

            var go = Instantiate(kit.mech, best, Quaternion.LookRotation(Vector3.ProjectOnPlane(faceTo - best, Vector3.up).normalized, Vector3.up), parent);
            go.name = "Giant Mech";
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                go.transform.localScale *= Height / Mathf.Max(0.5f, b.size.y);
            }
            var gm = go.AddComponent<GiantMech>();
            gm.Animate(kit);
            return gm;
        }

        void Animate(SpectacleKit kit)
        {
            body = transform;
            var anim = GetComponentInChildren<Animator>(true);
            if (anim == null || kit.mechIdle == null) return;
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullCompletely; // nothing ticks while it's off screen
            graph = PlayableGraph.Create("Giant Mech");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            idle = AnimationClipPlayable.Create(graph, kit.mechIdle);
            idle.SetSpeed(PlaySpeed);
            graph.Connect(idle, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            if (kit.mechTaunt != null)
            {
                taunt = AnimationClipPlayable.Create(graph, kit.mechTaunt);
                taunt.SetSpeed(PlaySpeed);
                graph.Connect(taunt, 0, mixer, 1);
                mixer.SetInputWeight(1, 0f);
            }
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        void Start()
        {
            race = RaceManager.Current;
            if (race != null) race.FinalLap += OnFinalLap;
        }

        void OnDestroy()
        {
            if (race != null) race.FinalLap -= OnFinalLap;
            if (graph.IsValid()) graph.Destroy();
        }

        void OnFinalLap(RaceManager.Racer r)
        {
            if (!r.isPlayer || !taunt.IsValid()) return;
            taunt.SetTime(0);
            tauntUntil = Time.time + (float)taunt.GetAnimationClip().length / PlaySpeed;
            // The stomp: felt everywhere on the circuit, harder the closer you are.
            if (r.vehicle != null)
            {
                float d = (r.vehicle.Transform.position - body.position).magnitude;
                CameraFX.SpeedFeel.Jolt(Mathf.Lerp(0.7f, 0.25f, Mathf.InverseLerp(150f, 800f, d)));
            }
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            // The idle clips don't loop on import: wrap them by hand.
            var clip = idle.GetAnimationClip();
            if (clip != null && idle.GetTime() >= clip.length) idle.SetTime(0);
            float want = Time.time < tauntUntil ? 1f : 0f;
            float w = Mathf.MoveTowards(mixer.GetInputWeight(1), want, Time.deltaTime * 0.8f);
            if (taunt.IsValid()) { mixer.SetInputWeight(1, w); mixer.SetInputWeight(0, 1f - w); }
        }
    }
}
