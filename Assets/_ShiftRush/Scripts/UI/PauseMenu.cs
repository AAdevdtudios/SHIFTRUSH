using ShiftRush.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ShiftRush.UI
{
    /// <summary>
    /// In-race pause: Esc (or Start) freezes the race and opens RESUME / RESTART / OPTIONS / MENU.
    /// Spawns itself from Resources/SR_PauseMenu in every scene except the main menu, so race scenes need no setup.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";

        [SerializeField] CanvasGroup root;
        [SerializeField] CanvasGroup mainGroup;
        [SerializeField] CanvasGroup optionsGroup;
        [SerializeField] GearMenu gears;
        [SerializeField] float fadeSpeed = 10f;

        public static bool IsPaused { get; private set; }
        /// <summary>Set while something owns the time scale (the SHIFT selector) so Esc can't pause over it.</summary>
        public static bool Blocked { get; set; }

        bool open, inOptions;
        float savedTimeScale = 1f;
        CursorLockMode savedLock;
        bool savedCursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Spawn(s); };
            Spawn(SceneManager.GetActiveScene());
        }

        static void Spawn(Scene scene)
        {
            IsPaused = false;
            if (scene.name == MenuScene || FindFirstObjectByType<PauseMenu>() != null) return;
            var prefab = Resources.Load<PauseMenu>("SR_PauseMenu");
            if (prefab == null) return;
            var pm = Instantiate(prefab);
            pm.name = "SR_PauseMenu";
            SceneManager.MoveGameObjectToScene(pm.gameObject, scene);
        }

        void Awake()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            Show(root, false, true);
            Show(optionsGroup, false, true);
        }

        void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                     || (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || (open && Gamepad.current.buttonEast.wasPressedThisFrame)));
            if (back && !SceneLoader.IsLoading && !(Blocked && !open))
            {
                if (!open) Pause();
                else if (inOptions) CloseOptions();
                else Resume();
            }

            float dt = Time.unscaledDeltaTime * fadeSpeed;
            Fade(root, open, dt);
            Fade(mainGroup, open && !inOptions, dt);
            Fade(optionsGroup, open && inOptions, dt);
        }

        public void Pause()
        {
            if (open) return;
            open = true; inOptions = false; IsPaused = true;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            savedLock = Cursor.lockState; savedCursor = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            MenuSounds.Back();
        }

        public void Resume()
        {
            if (!open) return;
            Unfreeze();
            Cursor.lockState = savedLock; Cursor.visible = savedCursor;
            MenuSounds.Select();
        }

        void Unfreeze()
        {
            open = false; inOptions = false; IsPaused = false;
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }

        public void Restart()
        {
            Unfreeze();
            var scene = SceneManager.GetActiveScene().name;
            var track = RaceSession.Track;
            if (track != null && track.scene == scene) SceneLoader.LoadTrack(track);
            else SceneLoader.Load(scene, scene, "RESTART", "");
        }

        public void BackToMenu()
        {
            Unfreeze();
            SceneLoader.Load(MenuScene, "SHIFT//RUSH", "BACK TO THE GARAGE", "");
        }

        public void OpenOptions() { inOptions = true; }
        public void CloseOptions() { inOptions = false; MenuSounds.Back(); }

        void OnDestroy()
        {
            if (open) Unfreeze();
        }

        static void Fade(CanvasGroup g, bool on, float dt)
        {
            if (g == null) return;
            g.alpha = Mathf.MoveTowards(g.alpha, on ? 1f : 0f, dt);
            g.interactable = on && g.alpha > 0.9f;
            g.blocksRaycasts = on;
        }

        static void Show(CanvasGroup g, bool on, bool instant)
        {
            if (g == null) return;
            if (instant) g.alpha = on ? 1f : 0f;
            g.interactable = on; g.blocksRaycasts = on;
        }
    }
}
