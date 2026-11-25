using Michsky.UI.Heat;
using ShiftRush.Core;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftRush.UI
{
    /// <summary>
    /// Sits on Settings → Visuals. Whenever one of its options changes (or the tab opens and Heat re-sends the saved
    /// values), re-applies everything through <see cref="DisplaySettings"/> at the end of the frame, after Heat has
    /// saved the new value. Heat's own VSync handler (every second refresh) is switched off in favour of ours.
    /// Hooked up at runtime: listeners added in the editor don't survive on these prefab rows.
    /// </summary>
    public class DisplaySettingsBinder : MonoBehaviour
    {
        bool dirty;

        // Heat's VSync switch fires its saved value when the menu loads, even with the Visuals tab closed, and its
        // handler syncs every second refresh (30 fps on a 60 Hz screen). Mute it in every scene as it loads and
        // put our settings back.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            MuteHeatVSync();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => MuteHeatVSync();
        }

        static void MuteHeatVSync()
        {
            foreach (var sw in Object.FindObjectsByType<SwitchManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (sw.saveKey != "VSync") continue;
                for (int i = 0; i < sw.onValueChanged.GetPersistentEventCount(); i++)
                    sw.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            DisplaySettings.Apply();
        }

        void Awake()
        {
            foreach (var sw in GetComponentsInChildren<SwitchManager>(true))
            {
                if (sw.saveKey != "VSync") continue;
                for (int i = 0; i < sw.onValueChanged.GetPersistentEventCount(); i++)
                    sw.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
                sw.onValueChanged.AddListener(_ => dirty = true);
            }
            foreach (var hs in GetComponentsInChildren<HorizontalSelector>(true)) hs.onValueChanged.AddListener(_ => dirty = true);
        }

        void OnEnable() { dirty = true; }

        void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            DisplaySettings.Apply();
        }
    }
}
