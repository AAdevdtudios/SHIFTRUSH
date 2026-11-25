using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.Comic
{
    /// <summary>
    /// A panel's lettering, written as lists in the Inspector: caption boxes (scene and time), balloons (speech, or
    /// jagged radio balloons for Slim on comms) and sound effects. Positions are in panel space, 0..1 from the top left,
    /// so they hold at any panel shape. A balloon's tail can follow an actor in the scene, so moving the actor keeps
    /// the tail on them. Drawn on a Screen Space Camera canvas through an overlay camera with a clean renderer, so the
    /// comic pass never inks over the letters. The canvas is a world space sheet the exact size of the panel in front of
    /// an orthographic overlay camera, so the lettering lands in the same place in the Game view and in a captured PNG. All lettering is in capitals, in the game's own fonts and colours.
    /// The drawn pieces are rebuilt from the lists, never saved, so the lists are the only thing to edit.
    /// </summary>
    [ExecuteAlways]
    public class ComicLettering : MonoBehaviour
    {
        [System.Serializable]
        public class Caption
        {
            public string title = "NIGHT ONE.";
            [TextArea] public string body = "THE AIRSTRIP OUTSIDE THE CITY.";
            [Tooltip("Top left corner, panel space 0..1 from the top left.")]
            public Vector2 at = new Vector2(0.026f, 0.04f);
            public float width = 610f;
        }

        [System.Serializable]
        public class Bubble
        {
            public string speaker = "SLIM  //  CH13 RADIO";
            [TextArea] public string line = "Nobody expects anything.";
            [Tooltip("Jagged outline: the voice comes over the radio.")]
            public bool radio = true;
            [Tooltip("Centre, panel space 0..1 from the top left.")]
            public Vector2 at = new Vector2(0.22f, 0.33f);
            public Vector2 size = new Vector2(720f, 310f);
            public bool tail = true;
            [Tooltip("The tail points at this actor's head. Empty = point at Tail To.")]
            public Transform speaker3D;
            [Tooltip("Where the tail points when there's no actor, panel space. Off the panel is fine (an off-panel voice).")]
            public Vector2 tailTo = new Vector2(0.02f, 0.58f);
        }

        [System.Serializable]
        public class Sfx
        {
            public string text = "WHUP WHUP";
            [Tooltip("Centre, panel space 0..1 from the top left.")]
            public Vector2 at = new Vector2(0.78f, 0.14f);
            public float angle = 8f;
            public float size = 104f;
            public bool cyan = true;
        }

        [Header("Rig (made by SHIFT RUSH → Comic → Add Lettering)")]
        public Camera panelCamera;
        [Tooltip("The orthographic overlay camera that draws the lettering, stacked on the panel camera.")]
        public Camera letteringCamera;
        public Canvas canvas;

        [Header("Fonts")]
        public TMP_FontAsset titleFont;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset sfxFont;

        [Header("Colours")]
        public Color paper = new Color32(250, 247, 240, 255);
        public Color ink = new Color32(8, 5, 16, 255);
        public Color magenta = new Color32(255, 46, 154, 255);
        public Color cyan = new Color32(46, 242, 255, 255);

        [Header("Lettering")]
        public bool border = true;
        public float borderWidth = 14f;
        public List<Caption> captions = new List<Caption>();
        public List<Bubble> bubbles = new List<Bubble>();
        public List<Sfx> sfx = new List<Sfx>();

        const string BuiltName = "Lettering (built)";
        RectTransform root;
        readonly List<KeyValuePair<ComicShape, Bubble>> tails = new List<KeyValuePair<ComicShape, Bubble>>();
        bool dirty = true;
        readonly List<Material> made = new List<Material>();

        void OnEnable() { dirty = true; }
        void OnDisable() { Clear(); }
        void OnValidate()
        {
            dirty = true;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        const float Scale = 0.01f; // canvas units to metres
        const float SheetHeight = 1100f;

        /// <summary>Sizes the sheet to the panel and the overlay camera to the sheet.</summary>
        void Fit()
        {
            var pc = panelCamera != null ? panelCamera.GetComponent<ComicPanelCamera>() : null;
            // Lettering is laid out on a sheet 1100 units tall, whatever the panel's pixel size, so a small
            // preview and a big print letter the same. Only the width follows the panel's shape.
            Vector2 px = pc != null ? (Vector2)pc.panelSize : new Vector2(1800, 1100);
            Vector2 size = new Vector2(SheetHeight * px.x / Mathf.Max(1f, px.y), SheetHeight);
            var rt = (RectTransform)canvas.transform;
            if (rt.sizeDelta != size) rt.sizeDelta = size;
            if (letteringCamera != null)
            {
                letteringCamera.orthographic = true;
                letteringCamera.orthographicSize = size.y * Scale * 0.5f;
                rt.SetParent(letteringCamera.transform, false);
                rt.localPosition = new Vector3(0, 0, 1f);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one * Scale;
            }
        }

        void LateUpdate()
        {
            if (canvas == null) return;
            Fit();
            if (dirty || root == null) Build();
            AimTails();
        }

        /// <summary>Rebuilds now (Capture calls this so the saved PNG matches the lists).</summary>
        public void Refresh()
        {
            if (canvas == null) return;
            Fit();
            Build();
            Canvas.ForceUpdateCanvases();
            AimTails();
            Canvas.ForceUpdateCanvases();
        }

        void Clear()
        {
            tails.Clear();
            foreach (var m in made) if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
            made.Clear();
            if (canvas == null) return;
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                var c = canvas.transform.GetChild(i).gameObject;
                if (c.name != BuiltName) continue;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            root = null;
        }

        void Build()
        {
            Clear();
            dirty = false;
            root = New(BuiltName, canvas.transform);
            Stretch(root);

            foreach (var s in sfx) BuildSfx(s);
            foreach (var b in bubbles) BuildBubble(b);
            foreach (var c in captions) BuildCaption(c);
            if (border)
            {
                var f = Shape(New("Border", root), ComicShape.Kind.Frame);
                Stretch(f.rectTransform);
                f.inkWidth = borderWidth;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = canvas.gameObject.layer;
                t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            }
        }

        // --- pieces

        void BuildCaption(Caption c)
        {
            var box = New("Caption", root);
            Place(box, c.at, new Vector2(0, 1));
            const float pad = 22f;
            var title = Text(box, "Title", c.title, titleFont, 46f, ink, TextAlignmentOptions.TopLeft);
            var body = Text(box, "Body", c.body, bodyFont, 24f, ink, TextAlignmentOptions.TopLeft);
            float inner = c.width - pad * 2f;
            float th = string.IsNullOrEmpty(c.title) ? 0f : title.GetPreferredValues(title.text, inner, 0).y;
            float bh = string.IsNullOrEmpty(c.body) ? 0f : body.GetPreferredValues(body.text, inner, 0).y;
            float gap = th > 0 && bh > 0 ? 6f : 0f;
            box.sizeDelta = new Vector2(c.width, pad * 2f + th + gap + bh - 8f);
            TopBand(title.rectTransform, pad, pad, th);
            TopBand(body.rectTransform, pad, pad + th + gap, bh);
            var bg = Shape(New("Box", box), ComicShape.Kind.Box);
            Stretch(bg.rectTransform);
            bg.inkWidth = 5f;
            bg.shadow = magenta;
            bg.transform.SetAsFirstSibling();
        }

        void BuildBubble(Bubble b)
        {
            var holder = New("Balloon", root);
            Place(holder, b.at, new Vector2(0.5f, 0.5f));
            holder.sizeDelta = b.size;
            var shape = Shape(New("Shape", holder), b.radio ? ComicShape.Kind.Radio : ComicShape.Kind.Balloon);
            Stretch(shape.rectTransform);
            shape.tail = b.tail;
            if (b.tail) tails.Add(new KeyValuePair<ComicShape, Bubble>(shape, b));

            // The speaker tag and the line sit as one block in the middle of the oval, so text never touches the curve.
            float insetX = b.size.x * 0.15f;
            float textW = b.size.x - insetX * 1.8f;
            var line = Text(holder, "Line", b.line, titleFont, 40f, ink, TextAlignmentOptions.TopLeft);
            line.lineSpacing = -8f;
            float lineH = Mathf.Min(line.GetPreferredValues(line.text, textW, 0).y, b.size.y * 0.62f);
            bool who = !string.IsNullOrEmpty(b.speaker);
            float whoH = who ? 32f : 0f;
            float top = Mathf.Max(b.size.y * 0.12f, (b.size.y - whoH - lineH) * 0.5f);
            if (who)
            {
                var tag = Text(holder, "Speaker", b.speaker, bodyFont, 20f, magenta, TextAlignmentOptions.TopLeft);
                TopBand(tag.rectTransform, insetX, top, 28f);
            }
            // Too long for the balloon: the line shrinks to fit rather than spilling out.
            line.enableAutoSizing = true;
            line.fontSizeMin = 22f; line.fontSizeMax = 40f;
            TopBand(line.rectTransform, insetX, top + whoH, lineH);
            line.rectTransform.offsetMax = new Vector2(-insetX * 0.8f, line.rectTransform.offsetMax.y);
        }

        void BuildSfx(Sfx s)
        {
            var holder = New("SFX", root);
            Place(holder, s.at, new Vector2(0.5f, 0.5f));
            holder.sizeDelta = new Vector2(1200f, s.size * 1.6f);
            holder.localRotation = Quaternion.Euler(0, 0, s.angle);
            var t = Text(holder, "Text", s.text, sfxFont, s.size, s.cyan ? cyan : magenta, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.characterSpacing = 4f;
            if (sfxFont != null)
            {
                // Thick ink outline and a hard offset shadow in the other neon, the printed look.
                var m = new Material(sfxFont.material) { name = "SFX (built)", hideFlags = HideFlags.DontSave };
                m.EnableKeyword("OUTLINE_ON");
                m.SetFloat("_OutlineWidth", 0.32f);
                m.SetColor("_OutlineColor", ink);
                m.SetFloat("_FaceDilate", 0.3f);
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", s.cyan ? magenta : cyan);
                m.SetFloat("_UnderlayOffsetX", 0.9f);
                m.SetFloat("_UnderlayOffsetY", -0.9f);
                m.SetFloat("_UnderlayDilate", 0.6f);
                m.SetFloat("_UnderlaySoftness", 0f);
                t.fontSharedMaterial = m;
                made.Add(m);
            }
        }

        /// <summary>Keeps each balloon's tail on its speaker as the actor or camera moves.</summary>
        void AimTails()
        {
            if (root == null) return;
            Vector2 size = root.rect.size;
            foreach (var pair in tails)
            {
                var shape = pair.Key; var b = pair.Value;
                if (shape == null || b == null) continue;
                Vector2 target = new Vector2(b.tailTo.x, 1f - b.tailTo.y); // viewport, bottom left
                if (b.speaker3D != null && panelCamera != null)
                {
                    var head = HeadOf(b.speaker3D);
                    var vp = panelCamera.WorldToViewportPoint(head);
                    if (vp.z > 0) target = vp;
                }
                Vector2 centre = new Vector2(b.at.x, 1f - b.at.y);
                Vector2 tip = Vector2.Scale(target - centre, size);
                // Stop short of the head, and never longer than a balloon and a half.
                float len = tip.magnitude;
                float max = Mathf.Max(b.size.x, b.size.y) * 0.9f;
                float want = Mathf.Min(len * (b.speaker3D != null ? 0.82f : 1f), max);
                shape.SetTail(len > 1f ? tip / len * want : Vector2.zero);
            }
        }

        static Vector3 HeadOf(Transform actor)
        {
            // The speaker can be the actor or any marker inside it.
            var who = actor.GetComponentInParent<ComicActor>();
            if (who != null) actor = who.transform;
            var anim = actor.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head.position + actor.up * 0.1f;
            }
            return actor.position + actor.up * 1.7f;
        }

        // --- helpers

        static RectTransform New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Puts a piece at a panel-space point (0..1 from the top left) with the given pivot.</summary>
        static void Place(RectTransform rt, Vector2 at, Vector2 pivot)
        {
            var a = new Vector2(at.x, 1f - at.y);
            rt.anchorMin = a; rt.anchorMax = a;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>A strip across the top of the parent: inset from the sides, down from the top, so tall.</summary>
        static void TopBand(RectTransform rt, float side, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(side, -top - height);
            rt.offsetMax = new Vector2(-side, -top);
        }

        ComicShape Shape(RectTransform rt, ComicShape.Kind kind)
        {
            var s = rt.gameObject.AddComponent<ComicShape>();
            s.kind = kind;
            s.paper = paper;
            s.ink = ink;
            s.raycastTarget = false;
            return s;
        }

        TextMeshProUGUI Text(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align)
        {
            var t = New(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = (text ?? "").ToUpperInvariant();
            t.fontSize = size;
            t.color = col;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
    }
}
