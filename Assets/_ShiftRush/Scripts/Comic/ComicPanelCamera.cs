using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.Comic
{
    /// <summary>
    /// The camera for a comic panel. It draws through the Comic renderer (cel bands, ink, halftone), which only this
    /// camera uses, so the game's look is untouched. "Capture Panel" (the component's menu) saves a PNG of the panel at
    /// the set size into docs/comic.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public class ComicPanelCamera : MonoBehaviour
    {
        [Tooltip("Index of Comic_Renderer in the pipeline asset's renderer list. Set by SHIFT RUSH → Comic → Build Comic Stage.")]
        public int rendererIndex = 1;
        public Vector2Int panelSize = new Vector2Int(1800, 1100);
        [Tooltip("File name for Capture Panel, without .png.")]
        public string panelName = "ep01_panel_01";

        void OnEnable() { Apply(); }

        void Apply()
        {
            var data = GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(rendererIndex);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        /// <summary>
        /// Renders the panel off screen at the set size and saves it. Returns the path. The lettering is drawn in its own
        /// render with a clear background and laid over the panel, because a manual render doesn't draw the camera stack.
        /// </summary>
        [ContextMenu("Capture Panel")]
        public string Capture()
        {
            Apply();
            var cam = GetComponent<Camera>();
            int w = panelSize.x, h = panelSize.y;
            float aspect = (float)w / h;
            foreach (var a in FindObjectsByType<ComicActor>(FindObjectsSortMode.None)) a.Refresh();

            var letters = GetComponentsInChildren<ComicLettering>();
            // Other panels' lettering nearby would show up in this one's lettering render, so hide it for the capture.
            var hidden = new System.Collections.Generic.List<Canvas>();
            foreach (var other in FindObjectsByType<ComicLettering>(FindObjectsSortMode.None))
            {
                if (System.Array.IndexOf(letters, other) >= 0 || other.canvas == null || !other.canvas.gameObject.activeSelf) continue;
                other.canvas.gameObject.SetActive(false);
                hidden.Add(other.canvas);
            }
            var data = GetComponent<UniversalAdditionalCameraData>();
            var stack = data.cameraStack;
            var keptStack = stack != null ? new System.Collections.Generic.List<Camera>(stack) : null;
            if (stack != null) stack.Clear();

            float keepAspect = cam.aspect;
            cam.aspect = aspect;
            foreach (var l in letters) { if (l.letteringCamera != null) l.letteringCamera.aspect = aspect; l.Refresh(); }
            var panel = Grab(cam, w, h, false);
            cam.aspect = keepAspect;
            cam.ResetAspect();

            foreach (var l in letters)
            {
                var lc = l.letteringCamera;
                if (lc == null) continue;
                var ld = lc.GetComponent<UniversalAdditionalCameraData>();
                var keepType = ld.renderType;
                var keepClear = lc.clearFlags;
                var keepBg = lc.backgroundColor;
                ld.renderType = CameraRenderType.Base;
                lc.clearFlags = CameraClearFlags.SolidColor;
                lc.backgroundColor = new Color(0, 0, 0, 0);
                lc.aspect = aspect;
                var over = Grab(lc, w, h, true);
                ld.renderType = keepType;
                lc.clearFlags = keepClear;
                lc.backgroundColor = keepBg;
                lc.ResetAspect();
                // The lettering render is premultiplied (drawn over a clear black): over + panel * (1 - alpha).
                for (int i = 0; i < panel.Length; i++)
                {
                    var o = over[i];
                    if (o.a == 0) continue;
                    var p = panel[i];
                    int k = 255 - o.a;
                    panel[i] = new Color32((byte)Mathf.Min(255, o.r + p.r * k / 255), (byte)Mathf.Min(255, o.g + p.g * k / 255), (byte)Mathf.Min(255, o.b + p.b * k / 255), 255);
                }
            }
            if (stack != null) stack.AddRange(keptStack);
            foreach (var c in hidden) c.gameObject.SetActive(true);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(panel);
            tex.Apply();
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../docs/comic"));
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, panelName + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex);
            Debug.Log("[COMIC] Panel saved: " + path);
            return path;
        }

        static Color32[] Grab(Camera c, int w, int h, bool alpha)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var keep = c.targetTexture;
            bool keepHdr = c.allowHDR;
            if (alpha) c.allowHDR = false;
            c.targetTexture = rt;
            c.Render();
            c.targetTexture = keep;
            c.allowHDR = keepHdr;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            if (Application.isPlaying) { Destroy(tex); Destroy(rt); } else { DestroyImmediate(tex); DestroyImmediate(rt); }
            return px;
        }
    }
}
