using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// The home screen's menu: a row of stream panels along the bottom of the shot (Story, Quick Race, Garage,
    /// Free Roam) plus a small stack (Settings, Credits, Quit). Left/Right (A/D, d-pad, bumpers) moves along the
    /// row, Up/Down moves within the stack, Enter/Space/A engages. Mouse hover selects, click engages.
    /// The selected panel turns signal pink and lifts. A locked entry shakes and posts its message to The Feed.
    /// Built by SHIFT RUSH → Build Main Menu.
    /// </summary>
    public class BroadcastMenu : MonoBehaviour
    {
        [System.Serializable]
        public class Item
        {
            public RectTransform root;
            public Image background;
            public TMP_Text label;
            public TMP_Text caption;
            [Tooltip("Small entries stacked on the right of the row (Settings, Credits, Quit).")]
            public bool small;
            [Tooltip("Shown but not usable yet.")]
            public bool locked;
            [Tooltip("Posted to The Feed when a locked entry is engaged.")]
            public string lockedMessage;
            [Tooltip("Hidden in browser builds (Quit).")]
            public bool hideOnWeb;
            public UnityEvent onEngage = new UnityEvent();
            [HideInInspector] public Vector2 home;
            [HideInInspector] public Color captionIdle;
        }

        [SerializeField] List<Item> items = new List<Item>();
        [Tooltip("Only takes input while this group is visible (the Home panel).")]
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] Showroom showroom;
        [SerializeField] TheFeed feed;
        [SerializeField] Color selectedFill = new Color32(255, 46, 154, 255);
        [SerializeField] Color idleFill = new Color32(11, 10, 18, 230);
        [SerializeField] Color selectedText = new Color32(11, 10, 18, 255);
        [SerializeField] Color idleText = new Color32(244, 241, 250, 255);
        [SerializeField] float lift = 14f;

        int index;
        float shake;
        readonly List<float> lifts = new List<float>();

        public int Index { get { return index; } }

        void Start()
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
#if UNITY_WEBGL
                if (it.hideOnWeb && it.root != null) it.root.gameObject.SetActive(false);
#endif
                if (it.root == null) continue;
                it.home = it.root.anchoredPosition;
                it.captionIdle = it.caption != null ? it.caption.color : idleText;
                var hook = it.root.GetComponent<BroadcastMenuItem>();
                if (hook == null) hook = it.root.gameObject.AddComponent<BroadcastMenuItem>();
                hook.menu = this;
                hook.index = i;
                lifts.Add(0f);
            }
            index = 0;
            for (int i = 0; i < items.Count; i++) if (Usable(i) && !items[i].locked) { index = i; break; } // start on something you can play
            Paint(true);
        }

        bool Accepting
        {
            get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); }
        }

        bool Usable(int i) { return i >= 0 && i < items.Count && items[i].root != null && items[i].root.gameObject.activeInHierarchy; }

        void Update()
        {
            if (Accepting)
            {
                bool small = Usable(index) && items[index].small;
                if (MenuInput.Left) Step(-1, false);
                else if (MenuInput.Right) Step(1, false);
                else if (MenuInput.Up && small) Step(-1, true);
                else if (MenuInput.Down && small) Step(1, true);
                else if (MenuInput.Up && !small) JumpToSmall();
                else if (MenuInput.Submit) Engage(index);
            }
            Animate(Time.unscaledDeltaTime);
        }

        /// <summary>Left/Right walks the whole row (the small stack counts as one stop); Up/Down walks the stack.</summary>
        void Step(int dir, bool withinStack)
        {
            var stops = new List<int>();
            if (withinStack)
            {
                for (int i = 0; i < items.Count; i++) if (Usable(i) && items[i].small) stops.Add(i);
            }
            else
            {
                bool stackAdded = false;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!Usable(i)) continue;
                    if (!items[i].small) { stops.Add(i); continue; }
                    if (stackAdded) continue;
                    stops.Add(items[index].small ? index : i); // the stack is one stop: keep where you were in it
                    stackAdded = true;
                }
            }
            if (stops.Count == 0) return;
            int cur = Mathf.Max(0, stops.IndexOf(index));
            Select(stops[(cur + dir + stops.Count) % stops.Count], true);
        }

        void JumpToSmall() { int s = FirstSmall(); if (s >= 0) Select(s, true); }

        int FirstSmall()
        {
            for (int i = 0; i < items.Count; i++) if (Usable(i) && items[i].small) return i;
            return -1;
        }

        public void Select(int i, bool withSound)
        {
            if (i == index || !Usable(i)) return;
            index = i;
            if (withSound) MenuSounds.Shift();
            if (showroom != null) showroom.Kick(0.5f);
            Paint(false);
        }

        public void Engage(int i)
        {
            if (!Accepting || !Usable(i)) return;
            if (i != index) Select(i, false);
            var it = items[i];
            if (it.locked)
            {
                shake = 1f;
                MenuSounds.Back();
                if (feed != null && !string.IsNullOrEmpty(it.lockedMessage)) feed.PostSystem(it.lockedMessage);
                return;
            }
            MenuSounds.Select();
            if (showroom != null) showroom.Kick(1f);
            it.onEngage.Invoke();
        }

        void Paint(bool instant)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                bool on = i == index;
                if (it.background != null) it.background.color = on ? selectedFill : idleFill;
                if (it.label != null) it.label.color = on ? selectedText : (it.locked ? new Color(idleText.r, idleText.g, idleText.b, 0.55f) : idleText);
                if (it.caption != null) it.caption.color = on ? selectedText : it.captionIdle;
            }
        }

        void Animate(float dt)
        {
            shake = Mathf.MoveTowards(shake, 0f, dt * 3f);
            for (int i = 0; i < items.Count && i < lifts.Count; i++)
            {
                var it = items[i];
                if (it.root == null) continue;
                float target = i == index && !it.small ? lift : 0f;
                lifts[i] = Mathf.Lerp(lifts[i], target, 1f - Mathf.Exp(-dt * 14f));
                float jitter = i == index ? Mathf.Sin(Time.unscaledTime * 70f) * 10f * shake * shake : 0f;
                it.root.anchoredPosition = it.home + new Vector2(jitter, lifts[i]);
            }
        }
    }

    /// <summary>Mouse hover selects a menu panel, click engages it.</summary>
    public class BroadcastMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public BroadcastMenu menu;
        public int index;

        public void OnPointerEnter(PointerEventData e) { if (menu != null) menu.Select(index, true); }
        public void OnPointerClick(PointerEventData e) { if (menu != null) menu.Engage(index); }
    }
}
