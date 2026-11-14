using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.Shift
{
    /// <summary>
    /// SHIFT overlay (Resources/SR_ShiftHUD, built with the menu): the timing diamond (a diamond closes on a
    /// target diamond, three tries), the warp flash, the in-world readout (rings / hits, time, progress) and the
    /// result panel. Everything runs on unscaled time because the timing bar plays in slow motion.
    /// </summary>
    public class ShiftHUD : MonoBehaviour
    {
        [Header("Timing diamond")]
        [SerializeField] CanvasGroup selectorGroup;
        [Tooltip("The fixed diamond outline to line up with.")]
        [SerializeField] Image targetDiamond;
        [Tooltip("The diamond that closes in (scaled per frame).")]
        [SerializeField] Image closingDiamond;
        [Tooltip("Soft fill inside the target that lights up while the timing is right.")]
        [SerializeField] Image sweetFill;
        [Tooltip("One per try: grey, then green (hit) or magenta (miss).")]
        [SerializeField] Image[] pips;
        [SerializeField] Color green = new Color32(61, 255, 138, 255);

        [Header("Flash")]
        [SerializeField] Image flash;
        [SerializeField] TMP_Text flashTitle;

        [Header("Challenge")]
        [SerializeField] CanvasGroup challengeGroup;
        [SerializeField] TMP_Text challengeTitle, challengeCount, challengeTime;
        [SerializeField] RectTransform progressFill;

        [Header("Reward")]
        [SerializeField] CanvasGroup rewardGroup;
        [SerializeField] TMP_Text rewardHeadline, rewardDetail;
        [SerializeField] RectTransform rewardFill;

        [Header("Sound")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip enterClip, pickClip, pulseClip, rewardClip;

        [SerializeField] Color accent = new Color32(255, 64, 217, 255);

        float flashAlpha, rewardUntil, rewardTarget, pulse;
        ShiftArena arena;
        float zonePunch;
        readonly float[] pipPunch = new float[8];

        void Awake()
        {
            Set(selectorGroup, 0f); Set(challengeGroup, 0f); Set(rewardGroup, 0f);
            if (flash != null) flash.color = new Color(1, 1, 1, 0);
            if (flashTitle != null) flashTitle.alpha = 0f;
        }

        /// <summary>Slow motion starts: the bar appears with every try still to play.</summary>
        public void ShowSelector()
        {
            Set(selectorGroup, 1f);
            if (pips != null) foreach (var p in pips) if (p != null) { p.color = new Color(1, 1, 1, 0.6f); p.transform.localScale = Vector3.one; }
            SetDiamond(2.6f, false);
            Play(enterClip, 1f);
        }

        /// <summary>The closing diamond's size relative to the target (1 = lined up), and whether that's a hit.</summary>
        public void SetDiamond(float scale, bool inside)
        {
            if (closingDiamond != null)
            {
                closingDiamond.rectTransform.localScale = Vector3.one * scale;
                var c = inside ? green : accent;
                c.a = Mathf.Clamp01(1.6f - Mathf.Abs(scale - 1f)); // fades in as it approaches
                closingDiamond.color = c;
            }
            if (targetDiamond != null) targetDiamond.color = inside ? green : Color.white;
            if (sweetFill != null) { var f = green; f.a = inside ? 0.28f : 0f; sweetFill.color = f; }
            zonePunch = inside ? 1f : zonePunch;
        }

        /// <summary>Result of one try.</summary>
        public void MarkPip(int i, bool hit)
        {
            if (pips == null || i < 0 || i >= pips.Length || pips[i] == null) return;
            pips[i].color = hit ? green : accent;
            pipPunch[i] = 1f;
            Play(hit ? pickClip : pulseClip, hit ? 1f : 0.7f);
        }

        public void HideSelector()
        {
            Set(selectorGroup, 0f);
        }

        public void Picked(string title)
        {
            HideSelector();
            flashAlpha = 1f;
            if (flashTitle != null) flashTitle.text = title;
            Play(pickClip, 1f);
        }

        public void ShowChallenge(ShiftArena a)
        {
            arena = a;
            arena.Pulse += OnPulse;
            if (challengeTitle != null) challengeTitle.text = a.title;
            Set(challengeGroup, 1f);
        }

        public void EndChallenge()
        {
            if (arena != null) arena.Pulse -= OnPulse;
            arena = null;
            Set(challengeGroup, 0f);
            flashAlpha = 0.9f;
            if (flashTitle != null) flashTitle.text = "";
        }

        public void ShowReward(float fill, string headline, string detail)
        {
            if (rewardHeadline != null) rewardHeadline.text = headline;
            if (rewardDetail != null) rewardDetail.text = detail;
            rewardTarget = Mathf.Clamp01(fill);
            if (rewardFill != null) rewardFill.anchorMax = new Vector2(0f, 1f);
            rewardUntil = Time.unscaledTime + 2.6f;
            Play(rewardClip, 1f);
        }

        void OnPulse() { pulse = 1f; Play(pulseClip, 0.8f); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            zonePunch = Mathf.MoveTowards(zonePunch, 0f, dt * 5f);
            if (targetDiamond != null) targetDiamond.rectTransform.localScale = Vector3.one * (1f + 0.06f * zonePunch);
            if (pips != null)
                for (int i = 0; i < pips.Length && i < pipPunch.Length; i++)
                {
                    pipPunch[i] = Mathf.MoveTowards(pipPunch[i], 0f, dt * 3f);
                    if (pips[i] != null) pips[i].transform.localScale = Vector3.one * (1f + 0.45f * pipPunch[i]);
                }

            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 2.2f);
            if (flash != null) { var fc = Color.Lerp(accent, Color.white, flashAlpha); fc.a = flashAlpha; flash.color = fc; }
            if (flashTitle != null) { flashTitle.alpha = Mathf.Clamp01(flashAlpha * 1.6f); flashTitle.transform.localScale = Vector3.one * (1.25f - 0.25f * flashAlpha); }

            if (arena != null)
            {
                pulse = Mathf.MoveTowards(pulse, 0f, dt * 3f);
                if (challengeCount != null)
                {
                    challengeCount.text = arena.kind == ShiftArena.Kind.RingRush
                        ? arena.RingsPassed + "<size=55%><color=#9AA0B4>/" + arena.rings.Length + " RINGS</color></size>"
                        : (arena.Hits == 0 ? "CLEAN" : arena.Hits + (arena.Hits == 1 ? " HIT" : " HITS"));
                    challengeCount.transform.localScale = Vector3.one * (1f + 0.25f * pulse);
                    challengeCount.color = arena.kind == ShiftArena.Kind.TrapRun && pulse > 0f ? Color.Lerp(Color.white, accent, pulse) : Color.white;
                }
                if (challengeTime != null)
                {
                    float left = arena.TimeLeft;
                    challengeTime.text = left.ToString("00.0");
                    challengeTime.color = left < 3f && Mathf.Repeat(Time.unscaledTime * 4f, 1f) > 0.5f ? accent : Color.white;
                }
                if (progressFill != null) progressFill.anchorMax = new Vector2(arena.Progress, 1f);
            }

            bool showReward = Time.unscaledTime < rewardUntil;
            if (rewardGroup != null) rewardGroup.alpha = Mathf.MoveTowards(rewardGroup.alpha, showReward ? 1f : 0f, dt * 4f);
            if (rewardFill != null && showReward)
                rewardFill.anchorMax = new Vector2(Mathf.MoveTowards(rewardFill.anchorMax.x, rewardTarget, dt * 1.2f), 1f);
        }

        void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        static void Set(CanvasGroup g, float a)
        {
            if (g == null) return;
            g.alpha = a; g.interactable = false; g.blocksRaycasts = false;
        }
    }
}
