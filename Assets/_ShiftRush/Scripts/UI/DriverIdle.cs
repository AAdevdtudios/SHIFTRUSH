using ShiftRush.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShiftRush.UI
{
    /// <summary>
    /// The driver's idle on the showroom stage. When the catalog has idle clips for this body (Kawaii Cool Action for
    /// the female, MoCap Central for the male) they play on the body's humanoid avatar: the resting idle, then a
    /// random other clip, then back, crossfading between them. With no clips it falls back to an idle made on the bones.
    /// The bone idle has arms down with soft elbows, a slow weight shift from hip to hip (the spine leans back against it so the head
    /// stays level), breathing through the chest and shoulders, the head glancing round and settling back on the
    /// camera, and every so often a shoulder roll, like a driver loosening up before the race.
    /// Rotations are given around the body's own axes, so the rig's bone orientations don't matter.
    /// </summary>
    public class DriverIdle : MonoBehaviour
    {
        [SerializeField] float breathPeriod = 4.2f;
        [SerializeField] float breathDegrees = 1.8f;
        [SerializeField] float swayPeriod = 7f;
        [SerializeField] float swayDegrees = 2.4f;
        [SerializeField] float swayShift = 0.012f;
        [SerializeField] float lookDegrees = 14f;
        [SerializeField] float elbowBend = 12f;
        [SerializeField] Vector2 fidgetEvery = new Vector2(7f, 12f);

        Transform hips, spine, chest, neck, head, shoulderL, shoulderR, armL, armR, foreL, foreR, upLegL, upLegR, legL, legR;
        Transform[] bones;
        Quaternion[] rest;
        Vector3 hipsRest;
        float seed, nextFidget, fidgetStart = -100f;
        float lookYaw, lookPitch, lookTargetYaw, lookTargetPitch, nextGlance;

        [SerializeField] float crossfade = 0.6f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClip[] clips;
        int playing = -1, slot;
        float clipStart, clipLength, fadeStart = -100f;

        void Awake()
        {
            var anim = GetComponentInChildren<Animator>(true);
            if (anim != null && StartClips(anim)) return;
            if (anim != null) anim.enabled = false; // no clips: the animator would only hold the bind pose
            hips = Find("Hips"); spine = Find("Spine"); chest = Find("Spine2") ?? Find("Spine1");
            neck = Find("Neck"); head = Find("Head");
            shoulderL = Find("Shoulder.L"); shoulderR = Find("Shoulder.R");
            armL = Find("Arm.L"); armR = Find("Arm.R"); foreL = Find("ForeArm.L"); foreR = Find("ForeArm.R");
            upLegL = Find("UpLeg.L"); upLegR = Find("UpLeg.R"); legL = Find("Leg.L"); legR = Find("Leg.R");
            RelaxArm(armL, foreL);
            RelaxArm(armR, foreR);
            // Parents before children: each frame starts every bone from rest, then turns it.
            bones = new[] { hips, spine, chest, neck, head, shoulderL, shoulderR, armL, armR, foreL, foreR, upLegL, upLegR, legL, legR };
            rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) if (bones[i]) rest[i] = bones[i].localRotation;
            if (hips) hipsRest = hips.localPosition;
            seed = Random.value * 10f;
            nextFidget = Time.unscaledTime + Random.Range(fidgetEvery.x, fidgetEvery.y);
            nextGlance = Time.unscaledTime + Random.Range(2f, 4f);
        }

        /// <summary>Plays the catalog's idles for this body through a playable graph (no controller asset needed).</summary>
        bool StartClips(Animator anim)
        {
            var cat = DriverCatalog.Instance;
            if (cat == null || anim.avatar == null || !anim.avatar.isHuman) return false;
            bool female = name.StartsWith("F_");
            clips = female ? cat.femaleIdles : cat.maleIdles;
            if (clips == null || clips.Length == 0 || clips[0] == null) return false;
            anim.enabled = true;
            anim.applyRootMotion = false; // stay on the turntable
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Driver Idle");
            graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput.Create(graph, "Driver", anim).SetSourcePlayable(mixer);
            Play(0, true);
            graph.Play();
            return true;
        }

        /// <summary>Puts a clip in the free mixer input and fades it in over the one playing.</summary>
        void Play(int index, bool instant)
        {
            var clip = clips[index];
            slot = playing < 0 ? 0 : 1 - slot;
            var old = mixer.GetInput(slot);
            if (old.IsValid()) { graph.Disconnect(mixer, slot); old.Destroy(); }
            var p = AnimationClipPlayable.Create(graph, clip);
            p.SetApplyFootIK(true);
            graph.Connect(p, 0, mixer, slot);
            playing = index;
            clipStart = Time.unscaledTime;
            // A looping resting idle runs twice before the next pick, so gestures don't come back to back.
            clipLength = clip.length * (index == 0 && clip.length < 8f ? 2f : 1f);
            fadeStart = instant ? -100f : Time.unscaledTime;
            if (instant) { mixer.SetInputWeight(slot, 1f); mixer.SetInputWeight(1 - slot, 0f); }
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            float now = Time.unscaledTime;
            float w = Mathf.Clamp01((now - fadeStart) / Mathf.Max(0.01f, crossfade));
            mixer.SetInputWeight(slot, w);
            mixer.SetInputWeight(1 - slot, 1f - w);
            if (now - clipStart < clipLength - crossfade) return;
            // Resting idle, then a random other clip, then back to resting.
            int next = 0;
            if (playing == 0 && clips.Length > 1)
            {
                next = Random.Range(1, clips.Length);
                if (clips[next] == null) next = 0;
            }
            if (next == 0 && playing == 0) { clipStart = now; return; } // nothing else to play: keep resting
            Play(next, false);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>The pack's bind pose is a T-pose: swing each upper arm down beside the body.</summary>
        void RelaxArm(Transform arm, Transform fore)
        {
            if (arm == null || fore == null) return;
            Vector3 dir = (fore.position - arm.position).normalized;
            float side = Mathf.Sign(Vector3.Dot(dir, transform.right));
            Vector3 want = (-transform.up * 0.95f + transform.right * side * 0.22f).normalized;
            arm.rotation = Quaternion.FromToRotation(dir, want) * arm.rotation;
        }

        Transform Find(string bone)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == bone) return t;
            return null;
        }

        /// <summary>Turns a bone around one of the body's own axes (right, up, forward).</summary>
        void Turn(Transform bone, Vector3 bodyAxis, float degrees)
        {
            if (bone == null || degrees == 0f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, transform.TransformDirection(bodyAxis)) * bone.rotation;
        }

        void LateUpdate()
        {
            if (graph.IsValid() || bones == null) return; // the clips are driving the body
            float now = Time.unscaledTime;
            float t = now + seed;
            for (int i = 0; i < bones.Length; i++) if (bones[i]) bones[i].localRotation = rest[i];

            float breath = Mathf.Sin(t * Mathf.PI * 2f / breathPeriod);
            // Weight shift: a slow sine with a little hold at each side.
            float s = Mathf.Sin(t * Mathf.PI * 2f / swayPeriod);
            float sway = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.6f);

            // Fidget: a shoulder roll with a small head drop, then back.
            if (now >= nextFidget) { fidgetStart = now; nextFidget = now + 1.6f + Random.Range(fidgetEvery.x, fidgetEvery.y); }
            float f = Mathf.Clamp01((now - fidgetStart) / 1.6f);
            float roll = f > 0f && f < 1f ? Mathf.Sin(f * Mathf.PI) : 0f;
            float rollSide = Mathf.Sin(f * Mathf.PI * 2f);

            // Glances: pick somewhere to look, hold it, and mostly come back to the camera.
            if (now >= nextGlance)
            {
                bool away = Random.value < 0.55f;
                lookTargetYaw = away ? Random.Range(-1f, 1f) * lookDegrees : 0f;
                lookTargetPitch = away ? Random.Range(-0.4f, 0.25f) * lookDegrees : 0f;
                nextGlance = now + Random.Range(1.8f, 4.5f);
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.5f);
            lookYaw = Mathf.Lerp(lookYaw, lookTargetYaw, k);
            lookPitch = Mathf.Lerp(lookPitch, lookTargetPitch, k);

            // Hips: shift and roll onto the standing leg; the legs roll back so the feet stay under the body.
            if (hips) hips.localPosition = hipsRest + hips.parent.InverseTransformVector(transform.right * sway * swayShift);
            Turn(hips, Vector3.forward, sway * swayDegrees);
            Turn(hips, Vector3.up, sway * swayDegrees * 0.6f);
            Turn(upLegL, Vector3.forward, -sway * swayDegrees);
            Turn(upLegR, Vector3.forward, -sway * swayDegrees);
            // The leg taking no weight softens at the knee.
            float relaxL = Mathf.Max(0f, -sway), relaxR = Mathf.Max(0f, sway);
            Turn(upLegL, Vector3.right, -relaxL * 4f); Turn(legL, Vector3.right, relaxL * 8f);
            Turn(upLegR, Vector3.right, -relaxR * 4f); Turn(legR, Vector3.right, relaxR * 8f);

            // Spine leans back against the hips, chest breathes.
            Turn(spine, Vector3.forward, -sway * swayDegrees * 0.6f);
            Turn(chest, Vector3.forward, -sway * swayDegrees * 0.5f);
            Turn(chest, Vector3.right, -breath * breathDegrees);
            Turn(chest, Vector3.up, -sway * swayDegrees * 0.4f);

            // Shoulders lift with the breath, and roll during a fidget.
            float lift = breath * 0.8f + roll * 7f;
            Turn(shoulderL, Vector3.forward, -lift);
            Turn(shoulderR, Vector3.forward, lift);
            Turn(shoulderL, Vector3.right, -rollSide * roll * 6f);
            Turn(shoulderR, Vector3.right, -rollSide * roll * 6f);

            // Arms hang with soft elbows and swing a touch with the sway.
            float swing = Mathf.Sin(t * Mathf.PI * 2f / (swayPeriod * 0.5f)) * 1.5f;
            Turn(armL, Vector3.right, swing); Turn(armR, Vector3.right, -swing);
            Turn(armL, Vector3.forward, -breath * 0.6f); Turn(armR, Vector3.forward, breath * 0.6f);
            Turn(foreL, Vector3.right, -elbowBend - breath * 1.5f);
            Turn(foreR, Vector3.right, -elbowBend - breath * 1.5f);

            // Neck and head: keep level against the sway, then the glance.
            Turn(neck, Vector3.forward, sway * swayDegrees * 0.4f);
            Turn(neck, Vector3.up, lookYaw * 0.35f);
            Turn(head, Vector3.up, lookYaw * 0.65f);
            Turn(head, Vector3.right, lookPitch + breath * breathDegrees * 0.4f + roll * 6f);
        }
    }
}
