using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Saves a PNG of what the player sees, including Screen Space - Overlay canvases (which a plain camera render
    /// leaves out). Overlay canvases are switched to camera space for one render, then switched back.
    /// </summary>
    public static class UICapture
    {
        public static string Capture(string path, int width = 1600, int height = 900)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return "No camera";

            var switched = new List<Canvas>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f;
                switched.Add(c);
            }
            Canvas.ForceUpdateCanvases();

            var rt = RenderTexture.GetTemporary(width, height, 24);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            foreach (var c in switched) c.renderMode = RenderMode.ScreenSpaceOverlay;

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path + " (camera " + cam.name + ", " + switched.Count + " overlay canvases)";
        }
    }
}
