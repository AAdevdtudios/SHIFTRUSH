using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace ShiftRush.UI
{
    /// <summary>
    /// The title screen's menu, laid out like a gear gate: each entry is a gear ("1", "2", "3", "R").
    /// Up/Down (or W/S, d-pad) shifts between them with a clunk and a kick of the selector bar;
    /// the gear's own key jumps straight to it; Enter/Space/A engages it. Mouse hover and click work too.
    /// </summary>
    public class GearMenu : MonoBehaviour
    {
        [System.Serializable]
        public class Gear
        {
            public string key = "1";
            public string label = "RACE";
            public string caption = "";
            public RectTransform row;
            public TMP_Text keyText;
            public TMP_Text labelText;
            public TMP_Text captionText;
            [Tooltip("Hidden in browser builds (e.g. Quit).")]
            public bool hideOnWeb;
            public UnityEvent onEngage = new UnityEvent();
        }

        [SerializeField] List<Gear> gears = new List<Gear>();
        [Tooltip("The slanted highlight bar that slides to the selected gear.")]
        [SerializeField] RectTransform selector;
        [Tooltip("Only takes input while this group is visible (the Home panel).")]
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] Color idleLabel = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] Color activeLabel = Color.white;
        [SerializeField] Color idleKey = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] Color activeKey = new Color(0.04f, 0.04f, 0.06f, 1f);
        [SerializeField] float activeIndent = 34f;
        [SerializeField] Showroom showroom;

        int index;
        float kick;
        readonly List<float> indents = new List<float>();
        readonly List<Vector2> homes = new List<Vector2>();

        public int Index { get { return index; } }

        void Start()
        {
            for (int i = 0; i < gears.Count; i++)
            {
                var g = gears[i];
#if UNITY_WEBGL
                if (g.hideOnWeb && g.row != null) g.row.gameObject.SetActive(false);
#endif
                homes.Add(g.row != null ? g.row.anchoredPosition : Vector2.zero);
                indents.Add(0f);
                if (g.keyText) g.keyText.text = g.key;
                if (g.labelText) g.labelText.text = g.label;
                if (g.captionText) g.captionText.text = g.caption;

                var hook = g.row != null ? g.row.GetComponent<GearMenuRow>() : null;
                if (hook == null && g.row != null) hook = g.row.gameObject.AddComponent<GearMenuRow>();
                if (hook != null) { hook.menu = this; hook.gearIndex = i; }
            }
            index = 0;
            SnapSelector();
        }

        bool Accepting
        {
            get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); }
        }

        void Update()
        {
            if (Accepting)
            {
                if (MenuInput.Up) Shift(-1);
                else if (MenuInput.Down) Shift(1);
                else if (MenuInput.Submit) Engage(index);
                else
                {
                    for (int i = 0; i < gears.Count; i++)
                        if (Usable(i) && MenuInput.KeyPressed(gears[i].key)) { if (i == index) Engage(i); else SetIndex(i, true); break; }
                }
            }
            Animate(Time.unscaledDeltaTime);
        }

        bool Usable(int i) { return gears[i].row != null && gears[i].row.gameObject.activeInHierarchy; }

        void Shift(int dir)
        {
            int i = index;
            for (int n = 0; n < gears.Count; n++)
            {
                i = (i + dir + gears.Count) % gears.Count;
                if (Usable(i)) break;
            }
            SetIndex(i, true);
        }

        public void SetIndex(int i, bool withSound)
        {
            if (i == index || !Usable(i)) return;
            index = i;
            kick = 1f;
            if (withSound) MenuSounds.Shift();
            if (showroom != null) showroom.Kick(0.6f);
        }

        public void Engage(int i)
        {
            if (!Accepting || !Usable(i)) return;
            if (i != index) SetIndex(i, false);
            MenuSounds.Select();
            if (showroom != null) showroom.Kick(1f);
            gears[i].onEngage.Invoke();
        }

        void SnapSelector()
        {
            if (selector == null || gears.Count == 0 || gears[index].row == null) return;
            selector.anchoredPosition = new Vector2(selector.anchoredPosition.x, gears[index].row.anchoredPosition.y);
        }

        void Animate(float dt)
        {
            kick = Mathf.MoveTowards(kick, 0f, dt * 4f);
            for (int i = 0; i < gears.Count; i++)
            {
                var g = gears[i];
                if (g.row == null) continue;
                bool on = i == index;
                indents[i] = Mathf.Lerp(indents[i], on ? activeIndent : 0f, 1f - Mathf.Exp(-dt * 16f));
                g.row.anchoredPosition = homes[i] + new Vector2(indents[i], 0f);
                if (g.labelText) g.labelText.color = Color.Lerp(g.labelText.color, on ? activeLabel : idleLabel, 1f - Mathf.Exp(-dt * 14f));
                if (g.keyText) g.keyText.color = Color.Lerp(g.keyText.color, on ? activeKey : idleKey, 1f - Mathf.Exp(-dt * 14f));
                if (g.captionText) g.captionText.alpha = Mathf.Lerp(g.captionText.alpha, on ? 0.75f : 0f, 1f - Mathf.Exp(-dt * 12f));
            }

            if (selector != null && gears.Count > 0 && gears[index].row != null)
            {
                var p = selector.anchoredPosition;
                p.y = Mathf.Lerp(p.y, homes[index].y, 1f - Mathf.Exp(-dt * 22f));
                selector.anchoredPosition = p;
                // A shift snaps the bar: it stretches, then settles.
                float s = 1f + kick * kick * 0.18f;
                selector.localScale = new Vector3(s, 1f + kick * 0.08f, 1f);
            }
        }
    }
}
