using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftRush.Core
{
    /// <summary>
    /// Loading screen between the menu and a race. Lives on the Resources/SR_LoadingScreen prefab, which
    /// spawns itself on first use and survives scene changes. Shows the track name, a slanted progress
    /// bar and a tip, holds for a moment so the screen is readable, then fades out over the new scene.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text tip;
        [SerializeField] TMP_Text percent;
        [SerializeField] Image fill;
        [SerializeField] RectTransform streaks;
        [Tooltip("Soft-masked shot of the track behind the title.")]
        [SerializeField] Image hero;
        [Tooltip("Minimum time on screen, so the track name can be read even on a fast load.")]
        [SerializeField] float minimumShow = 0.7f;
        [SerializeField] float fade = 0.25f;

        static SceneLoader instance;
        public static bool IsLoading { get; private set; }

        static SceneLoader Instance
        {
            get
            {
                if (instance == null)
                {
                    var prefab = Resources.Load<SceneLoader>("SR_LoadingScreen");
                    if (prefab == null) return null;
                    instance = Instantiate(prefab);
                    instance.name = "SR_LoadingScreen";
                    DontDestroyOnLoad(instance.gameObject);
                    instance.group.alpha = 0f;
                    instance.group.blocksRaycasts = false;
                }
                return instance;
            }
        }

        /// <summary>Loads a scene behind the loading screen. Falls back to a plain load if the prefab is missing.</summary>
        public static void Load(string scene, string heading, string subheading, string tipText, Sprite heroSprite = null)
        {
            if (IsLoading) return;
            var loader = Instance;
            if (loader == null) { SceneManager.LoadScene(scene); return; }
            if (loader.hero != null) { loader.hero.sprite = heroSprite; loader.hero.enabled = heroSprite != null; }
            loader.StartCoroutine(loader.Run(scene, heading, subheading, tipText));
        }

        public static void LoadTrack(RaceCatalog.Track track)
        {
            string tipText = track.loadingTips != null && track.loadingTips.Length > 0
                ? track.loadingTips[Random.Range(0, track.loadingTips.Length)] : "";
            var car = RaceSession.Car;
            string sub = (car != null ? car.displayName.ToUpperInvariant() + "  //  " : "") + Difficulty.Label + "  //  " + track.LengthLabel;
            Load(track.scene, track.displayName, sub, tipText, track.thumbnail);
        }

        IEnumerator Run(string scene, string heading, string subheading, string tipText)
        {
            IsLoading = true;
            title.text = heading.ToUpperInvariant();
            subtitle.text = subheading;
            tip.text = tipText;
            SetProgress(0f);
            group.blocksRaycasts = true;

            yield return Fade(0f, 1f);
            float shown = 0f;

            var op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            float shownProgress = 0f;
            while (op.progress < 0.9f || shown < minimumShow)
            {
                // Unity reports 0..0.9 until activation; the bar eases toward the real value.
                float target = Mathf.Min(op.progress / 0.9f, shown / minimumShow);
                shownProgress = Mathf.MoveTowards(shownProgress, target, Time.unscaledDeltaTime * 1.5f);
                SetProgress(shownProgress);
                shown += Time.unscaledDeltaTime;
                yield return null;
            }
            SetProgress(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            yield return null;
            yield return Fade(1f, 0f);
            group.blocksRaycasts = false;
            IsLoading = false;
        }

        void Update()
        {
            if (streaks != null && group.alpha > 0f)
                streaks.anchoredPosition = new Vector2(Mathf.Repeat(Time.unscaledTime * 900f, 400f) - 200f, streaks.anchoredPosition.y);
        }

        void SetProgress(float t)
        {
            // Grow the bar from the left (keeps its slanted ends, unlike a Filled image).
            var r = fill.rectTransform;
            r.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
            percent.text = Mathf.RoundToInt(t * 100f).ToString("00") + "%";
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / fade);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
