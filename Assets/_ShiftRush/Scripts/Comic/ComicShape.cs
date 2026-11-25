using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.Comic
{
    /// <summary>
    /// The drawn shapes of comic lettering, made as UI geometry so they stay sharp at any panel size:
    /// a speech balloon (smooth oval), a radio balloon (jagged oval, the voice comes over the radio), a caption box with
    /// a hard drop shadow, and the panel's ink border. Balloons can have a tail pointing at whoever is talking.
    /// Ink is drawn as a bigger shape behind the paper, so the outline and the tail join with no seam.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ComicShape : MaskableGraphic
    {
        public enum Kind { Balloon, Radio, Box, Frame }

        public Kind kind = Kind.Balloon;
        public Color paper = new Color32(250, 247, 240, 255);
        public Color ink = new Color32(8, 5, 16, 255);
        [Tooltip("Box only: the hard drop shadow behind it.")]
        public Color shadow = new Color32(255, 46, 154, 255);
        public Vector2 shadowOffset = new Vector2(8, -8);
        public float inkWidth = 6f;

        [Header("Balloons")]
        public bool tail = true;
        [Tooltip("Where the tail points, in this shape's local units from its centre.")]
        public Vector2 tailTip = new Vector2(-260, -220);
        public float tailWidth = 70f;
        public int spikes = 22;
        [Range(0, 0.3f)] public float spikeDepth = 0.1f;

        public void SetTail(Vector2 tip)
        {
            if ((tip - tailTip).sqrMagnitude < 0.25f) return;
            tailTip = tip;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            switch (kind)
            {
                case Kind.Box:
                    Quad(vh, Offset(r, shadowOffset), shadow);
                    Quad(vh, r, ink);
                    Quad(vh, Inset(r, inkWidth), paper);
                    break;
                case Kind.Frame:
                    float w = inkWidth;
                    Quad(vh, new Rect(r.xMin, r.yMin, r.width, w), ink);
                    Quad(vh, new Rect(r.xMin, r.yMax - w, r.width, w), ink);
                    Quad(vh, new Rect(r.xMin, r.yMin, w, r.height), ink);
                    Quad(vh, new Rect(r.xMax - w, r.yMin, w, r.height), ink);
                    break;
                default:
                    Balloon(vh, r);
                    break;
            }
        }

        void Balloon(VertexHelper vh, Rect r)
        {
            Vector2 c = r.center;
            float rx = r.width * 0.5f, ry = r.height * 0.5f;
            bool radio = kind == Kind.Radio;
            int n = radio ? Mathf.Max(6, spikes) * 2 : 64;
            var inner = new List<Vector2>(n);
            var outer = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                float k = radio && (i & 1) == 1 ? 1f - spikeDepth : 1f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                inner.Add(c + new Vector2(dir.x * rx * k, dir.y * ry * k));
                // Ink grows the same way at every point, so the line stays an even width round the spikes.
                outer.Add(c + new Vector2(dir.x * (rx * k + inkWidth), dir.y * (ry * k + inkWidth)));
            }

            bool hasTail = tail && tailTip.sqrMagnitude > 1f;
            Vector2 tip = c + tailTip, b1 = c, b2 = c, oTip = tip, ob1 = c, ob2 = c;
            if (hasTail)
            {
                Vector2 d = tailTip.normalized, p = new Vector2(-d.y, d.x);
                // Base sits inside the oval so the paper oval drawn last covers the join.
                Vector2 baseC = c + new Vector2(d.x * rx, d.y * ry) * 0.6f;
                // A little curl: the tip leans to one side like a hand-drawn tail.
                tip += p * tailWidth * 0.15f;
                b1 = baseC + p * tailWidth * 0.5f; b2 = baseC - p * tailWidth * 0.5f;
                oTip = tip + d * inkWidth * 1.6f;
                ob1 = b1 + p * inkWidth; ob2 = b2 - p * inkWidth;
            }

            Fan(vh, c, outer, ink);
            if (hasTail) Tri(vh, ob1, oTip, ob2, ink);
            if (hasTail) Tri(vh, b1, tip, b2, paper);
            Fan(vh, c, inner, paper);
        }

        static Rect Inset(Rect r, float w) { return new Rect(r.xMin + w, r.yMin + w, r.width - 2 * w, r.height - 2 * w); }
        static Rect Offset(Rect r, Vector2 o) { return new Rect(r.position + o, r.size); }

        static void Quad(VertexHelper vh, Rect r, Color col)
        {
            int s = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), col, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), col, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), col, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), col, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
        }

        static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int s = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero);
            vh.AddVert(b, col, Vector2.zero);
            vh.AddVert(c, col, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
        }

        static void Fan(VertexHelper vh, Vector2 centre, List<Vector2> ring, Color col)
        {
            int s = vh.currentVertCount;
            vh.AddVert(centre, col, Vector2.zero);
            foreach (var p in ring) vh.AddVert(p, col, Vector2.zero);
            for (int i = 0; i < ring.Count; i++) vh.AddTriangle(s, s + 1 + i, s + 1 + (i + 1) % ring.Count);
        }
    }
}
