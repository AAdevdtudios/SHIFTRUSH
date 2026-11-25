using System.Collections.Generic;
using ShiftRush.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShiftRush.Comic
{
    /// <summary>
    /// A driver standing in a comic panel. It builds the body itself, so nothing about the outfit is baked into the
    /// scene: <see cref="Outfit.PlayersSaved"/> wears whatever the player has on in the wardrobe right now (it
    /// redresses when they change), <see cref="Outfit.Fixed"/> wears a set outfit (story characters like Slim).
    /// The pose comes from any humanoid clip (Kawaii Cool Action, MoCap Central) frozen at one moment, so the same
    /// pose works on both bodies and every outfit, because the clothes are rigged to the same skeleton. On top of the clip:
    /// head and spine sliders, and a hand shape per hand (fist, point, thumbs up and the rest) set through the
    /// humanoid finger muscles, so no posing asset is needed. Works in the editor, so panels are staged in the Scene view.
    /// </summary>
    [ExecuteAlways]
    public class ComicActor : MonoBehaviour
    {
        public enum Outfit { PlayersSaved, Fixed }
        public enum Shape { FromClip, Relaxed, Open, Fist, Point, ThumbsUp, Peace, Grip }

        [System.Serializable]
        public class Piece
        {
            public DriverSlot slot;
            [Tooltip("Catalog item id. Empty = nothing in this slot.")]
            public string itemId;
        }

        [System.Serializable]
        public class Hand
        {
            public Shape shape = Shape.FromClip;
            [Tooltip("How much of the shape replaces the clip's fingers.")]
            [Range(0, 1)] public float amount = 1f;
            [Tooltip("Curls every finger further (negative) or opens them (positive).")]
            [Range(-1, 1)] public float curl;
            [Range(-1, 1)] public float spread;
            [Tooltip("Thumb open (positive) or tucked (negative), on top of the shape.")]
            [Range(-1, 1)] public float thumb;
        }

        [Header("Who")]
        public Outfit outfit = Outfit.PlayersSaved;
        [Tooltip("Fixed outfit only: which body.")]
        public DriverBody body = DriverBody.BodyA;
        [Tooltip("Fixed outfit only. A slot not listed wears the catalog default.")]
        public List<Piece> pieces = new List<Piece>();

        [Header("Pose")]
        [Tooltip("Any humanoid clip. Empty = the body's T-pose.")]
        public AnimationClip pose;
        [Range(0, 1)] public float poseTime = 0.3f;
        [Tooltip("In Play mode, run the clip instead of freezing it (motion comic shots).")]
        public bool animateInPlay;

        [Header("Head and body (added to the clip)")]
        [Range(-1, 1)] public float headTurn;
        [Range(-1, 1)] public float headNod;
        [Range(-1, 1)] public float headTilt;
        [Range(-1, 1)] public float spineLean;
        [Range(-1, 1)] public float spineTwist;

        [Header("Hands")]
        public Hand leftHand = new Hand();
        public Hand rightHand = new Hand();

        GameObject model;
        Animator animator;
        PlayableGraph graph;
        AnimationClipPlayable clipPlayable;
        AnimationClip graphClip;
        HumanPoseHandler poser;
        HumanPose human;
        float[] restMuscles; // the body's own pose, start point when there's no clip
        Vector3 restBodyPos; Quaternion restBodyRot;
        string builtKey;
        float playStart;

        static Dictionary<string, int> muscle;
        static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

        /// <summary>The spawned body (null until built).</summary>
        public GameObject Model { get { return model; } }

        void OnEnable()
        {
            DriverProfile.Changed += Redress;
            playStart = Time.time;
            Build();
        }

        void OnDisable()
        {
            DriverProfile.Changed -= Redress;
            Teardown();
        }

        void Redress() { builtKey = null; }

        void OnValidate()
        {
#if UNITY_EDITOR
            // Slider moves in the Inspector re-pose straight away in the Scene view.
            if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        DriverBody BodyNow { get { return outfit == Outfit.PlayersSaved ? DriverProfile.Body : body; } }

        string WornOf(DriverSlot s)
        {
            foreach (var p in pieces) if (p.slot == s) return p.itemId ?? "";
            var cat = DriverCatalog.Instance;
            return cat != null ? cat.DefaultFor(body, s) : "";
        }

        /// <summary>Everything the look depends on, so the body is rebuilt only when it changes.</summary>
        string Key()
        {
            var b = BodyNow;
            var sb = new System.Text.StringBuilder(outfit + "|" + b);
            foreach (DriverSlot s in System.Enum.GetValues(typeof(DriverSlot)))
                sb.Append('|').Append(outfit == Outfit.PlayersSaved ? DriverProfile.Worn(b, s) : WornOf(s));
            return sb.ToString();
        }

        void Build()
        {
            Teardown();
            var cat = DriverCatalog.Instance;
            if (cat == null) return;
            var b = BodyNow;
            var prefab = cat.Body(b);
            if (prefab == null) return;

            // Built under an inactive holder so the pack's own scripts never wake up, then stripped.
            var holder = new GameObject("Comic Actor Body");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            model = Instantiate(prefab, holder.transform);
            model.name = prefab.name; // DriverIdle-style female check reads the F_ prefix
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
            if (outfit == Outfit.PlayersSaved) DriverLook.Apply(model, b);
            else DriverLook.Apply(model, b, null, null, WornOf);
            // Not saved with the scene: the outfit is rebuilt from the save every time it opens.
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            holder.SetActive(true);
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.updateWhenOffscreen = true; // posed bounds
                // A capture renders straight after posing, in the same editor frame: skin from the bones as they are now.
                r.forceMatrixRecalculationPerRender = true;
            }

            animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.avatar != null && animator.avatar.isHuman)
                {
                    poser = new HumanPoseHandler(animator.avatar, animator.transform);
                    poser.GetHumanPose(ref human);
                    restMuscles = (float[])human.muscles.Clone();
                    restBodyPos = human.bodyPosition; restBodyRot = human.bodyRotation;
                }
            }
            builtKey = Key();
        }

        void Teardown()
        {
            if (graph.IsValid()) graph.Destroy();
            graphClip = null;
            if (poser != null) { poser.Dispose(); poser = null; }
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (c.name != "Comic Actor Body") continue;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            model = null; animator = null;
        }

        /// <summary>Builds and poses now, for a capture that can't wait for the next frame.</summary>
        public void Refresh() { LateUpdate(); }

        void LateUpdate()
        {
            if (builtKey == null || builtKey != Key() || model == null) Build();
            if (animator == null) return;
            SamplePose();
            ApplyMuscles();
        }

        void SamplePose()
        {
            if (pose == null || !animator.isHuman)
            {
                if (graph.IsValid()) { graph.Destroy(); graphClip = null; }
                return;
            }
            // A fresh graph every sample: a kept one can go quiet after a script reload and leave the body in T-pose.
            // It's one clip on one body, so it costs next to nothing.
            {
                if (graph.IsValid()) graph.Destroy();
                graph = PlayableGraph.Create("Comic Pose " + name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                clipPlayable = AnimationClipPlayable.Create(graph, pose);
                clipPlayable.SetApplyFootIK(true);
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                output.SetSourcePlayable(clipPlayable);
                graphClip = pose;
            }
            double t = Application.isPlaying && animateInPlay
                ? (Time.time - playStart + poseTime * pose.length) % Mathf.Max(0.01f, pose.length)
                : poseTime * pose.length;
            clipPlayable.SetTime(t);
            graph.Evaluate(0f);
        }

        static void LoadMuscles()
        {
            if (muscle != null) return;
            muscle = new Dictionary<string, int>();
            var names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++) muscle[names[i]] = i;
        }

        void Add(string name, float value)
        {
            int i;
            if (value != 0f && muscle.TryGetValue(name, out i)) human.muscles[i] = Mathf.Clamp(human.muscles[i] + value, -1f, 1f);
        }

        void ApplyMuscles()
        {
            if (poser == null) return;
            bool hands = leftHand.shape != Shape.FromClip || rightHand.shape != Shape.FromClip
                || leftHand.curl != 0 || rightHand.curl != 0 || leftHand.spread != 0 || rightHand.spread != 0
                || leftHand.thumb != 0 || rightHand.thumb != 0;
            bool body = headTurn != 0 || headNod != 0 || headTilt != 0 || spineLean != 0 || spineTwist != 0;
            bool clip = graph.IsValid();
            if (!hands && !body && clip) return;
            LoadMuscles();
            poser.GetHumanPose(ref human);
            if (!clip && restMuscles != null)
            {
                // No clip resets the bones each frame, so start from the rest pose or the sliders would pile up.
                System.Array.Copy(restMuscles, human.muscles, restMuscles.Length);
                human.bodyPosition = restBodyPos; human.bodyRotation = restBodyRot;
            }
            // Head split over neck and head so it turns like a person, not a doll.
            Add("Neck Turn Left-Right", headTurn * 0.4f); Add("Head Turn Left-Right", headTurn * 0.8f);
            Add("Neck Nod Down-Up", headNod * 0.4f); Add("Head Nod Down-Up", headNod * 0.8f);
            Add("Neck Tilt Left-Right", headTilt * 0.4f); Add("Head Tilt Left-Right", headTilt * 0.8f);
            Add("Spine Front-Back", spineLean * 0.5f); Add("Chest Front-Back", spineLean * 0.5f);
            Add("Spine Twist Left-Right", spineTwist * 0.5f); Add("Chest Twist Left-Right", spineTwist * 0.5f);
            DoHand("Left", leftHand);
            DoHand("Right", rightHand);
            poser.SetHumanPose(ref human);
        }

        // Openness per finger (thumb, index, middle, ring, little): 1 straight, -1 curled shut. Then the spread.
        static float[] ShapeOf(Shape s, out float spread)
        {
            spread = 0f;
            switch (s)
            {
                case Shape.Relaxed: return new[] { 0.3f, 0.25f, 0.1f, 0f, -0.1f };
                case Shape.Open: spread = 0.7f; return new[] { 1f, 1f, 1f, 1f, 1f };
                case Shape.Fist: return new[] { -0.4f, -1f, -1f, -1f, -1f };
                case Shape.Point: return new[] { -0.5f, 1f, -1f, -1f, -1f };
                case Shape.ThumbsUp: return new[] { 1f, -1f, -1f, -1f, -1f };
                case Shape.Peace: spread = 0.9f; return new[] { -0.6f, 1f, 1f, -1f, -1f };
                case Shape.Grip: return new[] { 0.2f, -0.45f, -0.5f, -0.55f, -0.6f };
            }
            return null;
        }

        void DoHand(string side, Hand h)
        {
            float shapeSpread;
            var open = ShapeOf(h.shape, out shapeSpread);
            for (int f = 0; f < Fingers.Length; f++)
            {
                string finger = side + " " + Fingers[f];
                for (int j = 1; j <= 3; j++)
                {
                    int i;
                    if (!muscle.TryGetValue(finger + " " + j + " Stretched", out i)) continue;
                    float v = human.muscles[i];
                    if (open != null) v = Mathf.Lerp(v, open[f], h.amount);
                    v += h.curl + (f == 0 ? h.thumb : 0f);
                    human.muscles[i] = Mathf.Clamp(v, -1f, 1f);
                }
                int si;
                if (muscle.TryGetValue(finger + " Spread", out si))
                {
                    float v = human.muscles[si];
                    if (open != null) v = Mathf.Lerp(v, shapeSpread, h.amount);
                    human.muscles[si] = Mathf.Clamp(v + h.spread, -1f, 1f);
                }
            }
        }
    }
}
