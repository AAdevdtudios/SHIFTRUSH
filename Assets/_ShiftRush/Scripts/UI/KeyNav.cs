using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// Arrow-key and d-pad navigation for a row of buttons that has none of its own, with a focus ring so you always
    /// see which one Enter will press: a magenta frame with cyan corner ticks that sits just outside the button and
    /// breathes a little. Hovering a button with the mouse moves the focus to it too. The owner calls
    /// <see cref="Press"/> on submit (or reads <see cref="Current"/>).
    /// </summary>
    public class KeyNav : MonoBehaviour
    {
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);

        readonly List<Button> buttons = new List<Button>();
        int index;
        RectTransform ring;
        readonly List<Image> ringParts = new List<Image>();
        Vector2 lastMouse;

        /// <summary>Input is only read while this returns true (null: always).</summary>
        public System.Func<bool> Gate;

        public Button Current { get { return index >= 0 && index < buttons.Count ? buttons[index] : null; } }

        /// <summary>Adds navigation over these buttons, in on-screen order (left to right or top to bottom).</summary>
        public static KeyNav Attach(GameObject host, params Button[] row)
        {
            var nav = host.GetComponent<KeyNav>();
            if (nav == null) nav = host.AddComponent<KeyNav>();
            nav.buttons.Clear();
            foreach (var b in row) if (b != null) nav.buttons.Add(b);
            nav.index = 0;
            nav.Place();
            return nav;
        }

        public void Focus(int i) { if (buttons.Count == 0) return; index = (i + buttons.Count) % buttons.Count; Place(); }

        public void Press()
        {
            var b = Current;
            if (b != null && b.interactable && b.gameObject.activeInHierarchy) b.onClick.Invoke();
        }

        void OnEnable() { index = 0; Place(); }
        void OnDisable() { if (ring != null) ring.gameObject.SetActive(false); }

        void Update()
        {
            if (buttons.Count == 0 || (Gate != null && !Gate())) return;
            int before = index;
            if (MenuInput.Left || MenuInput.Up) Step(-1);
            else if (MenuInput.Right || MenuInput.Down) Step(1);

            // The mouse moved onto a button: focus follows it.
            var m = Mouse.current;
            if (m != null)
            {
                Vector2 pos = m.position.ReadValue();
                if ((pos - lastMouse).sqrMagnitude > 4f)
                {
                    lastMouse = pos;
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        var rt = buttons[i] != null ? (RectTransform)buttons[i].transform : null;
                        if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                        var canvas = rt.GetComponentInParent<Canvas>();
                        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                        if (RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) { index = i; break; }
                    }
                }
            }
            if (index != before) { Place(); MenuSounds.Tick(); }

            if (ring != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                float pad = 8f + k * 3f;
                ring.offsetMin = new Vector2(-pad, -pad); ring.offsetMax = new Vector2(pad, pad);
            }
        }

        void Step(int dir)
        {
            for (int n = 0; n < buttons.Count; n++)
            {
                index = (index + dir + buttons.Count) % buttons.Count;
                var b = buttons[index];
                if (b != null && b.interactable && b.gameObject.activeInHierarchy) return;
            }
        }

        void Place()
        {
            var b = Current;
            if (b == null) return;
            if (ring == null) Build();
            ring.gameObject.SetActive(true);
            ring.SetParent(b.transform, false);
            ring.anchorMin = Vector2.zero; ring.anchorMax = Vector2.one;
            ring.offsetMin = new Vector2(-9, -9); ring.offsetMax = new Vector2(9, 9);
            ring.SetAsLastSibling();
        }

        void Build()
        {
            ring = new GameObject("Focus Ring", typeof(RectTransform)).GetComponent<RectTransform>();
            // Four edges in magenta.
            Edge(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -4), new Vector2(0, 0), Magenta);
            Edge(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 4), Magenta);
            Edge(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(4, 0), Magenta);
            Edge(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-4, 0), new Vector2(0, 0), Magenta);
            // Cyan corner ticks, offset like the game's drop shadows.
            Tick(new Vector2(0, 1), new Vector2(-6, 6)); Tick(new Vector2(1, 1), new Vector2(6, 6));
            Tick(new Vector2(0, 0), new Vector2(-6, -6)); Tick(new Vector2(1, 0), new Vector2(6, -6));
        }

        void Edge(Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color c)
        {
            var rt = new GameObject("Edge", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(ring, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var img = rt.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            ringParts.Add(img);
        }

        void Tick(Vector2 corner, Vector2 offset)
        {
            var rt = new GameObject("Tick", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(ring, false);
            rt.anchorMin = rt.anchorMax = corner; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset; rt.sizeDelta = new Vector2(10, 10);
            var img = rt.gameObject.AddComponent<Image>(); img.color = Cyan; img.raycastTarget = false;
            ringParts.Add(img);
        }
    }
}
