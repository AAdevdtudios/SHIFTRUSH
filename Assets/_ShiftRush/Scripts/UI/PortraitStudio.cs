using ShiftRush.Core;
using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>
    /// A live head-and-shoulders portrait of anyone in the story, filmed instead of drawn, so every face is in the
    /// game's own style: a dressed driver body stood far below the map, with its own camera, key light and two
    /// coloured rims, rendering into a texture the UI shows in a slanted comic panel.
    /// - "YOU": the player's own driver, dressed exactly as in the Driver tab (a new jacket shows in the next call).
    /// - A cast member or anyone else: the same pack, dressed once in an outfit picked from their name, so it never
    ///   changes between calls, in their colour.
    /// - Slim: a black silhouette, rims only. His face is never shown.
    /// Expressions are comic-book camera work, so they work on any outfit: smug is a low, tilted angle, angry a
    /// tight push-in with a shake, shocked a quick pull-back.
    /// </summary>
    public class PortraitStudio : MonoBehaviour
    {
        public enum Mood { Neutral, Smug, Angry, Shocked }

        const int Size = 512;
        static int count;

        public RenderTexture Texture { get; private set; }
        public Color Colour { get; private set; }
        public bool Silhouette { get; private set; }

        Camera cam;
        Transform head;
        Mood mood = Mood.Neutral;
        float moodSince, fov = 21f, height, dist = 1.9f, roll;
        Vector3 facing = Vector3.forward;

        /// <summary>Films "YOU", a cast id ("kaito"), "SLIM", or any other name.</summary>
        public static PortraitStudio Film(string who, Color colour)
        {
            var go = new GameObject("Portrait " + who);
            // Far below the map, each in its own spot: no scene lights reach, and studios don't see each other.
            go.transform.position = new Vector3(40f * count++, -3000f, 0f);
            var s = go.AddComponent<PortraitStudio>();
            s.Build(who, colour);
            return s;
        }

        public void SetMood(Mood m) { if (m != mood) { mood = m; moodSince = Time.unscaledTime; } }

        /// <summary>Only portraits on screen render.</summary>
        public bool Live { set { if (cam != null) cam.enabled = value; } }

        void Build(string who, Color colour)
        {
            Colour = colour;
            var cat = DriverCatalog.Instance;
            string id = who.ToLowerInvariant();
            Silhouette = id == "slim";
            bool player = id == "you";
            int hash = Hash(id);
            DriverBody body = player ? DriverProfile.Body : (hash & 1) == 0 ? DriverBody.BodyA : DriverBody.BodyB;

            var prefab = cat != null ? cat.Body(body) : null;
            GameObject model = null;
            if (prefab != null)
            {
                // Instantiated under an inactive holder: the pack's own scripts never run (they'd reset the outfit).
                var holder = new GameObject("Body"); holder.SetActive(false);
                holder.transform.SetParent(transform, false);
                model = Instantiate(prefab, holder.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
                if (player) DriverLook.Apply(model, body);
                else DriverLook.Apply(model, body, wornOf: s => Outfit(cat, body, s, hash));
                model.AddComponent<DriverIdle>().portrait = true; // breathing only, eyes on the camera
                holder.SetActive(true);
                if (Silhouette) Blackout(model);
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") { head = t; break; }
                facing = model.transform.forward;
            }

            Texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "Portrait " + who, antiAliasing = 4 };
            cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.targetTexture = Texture;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // the panel behind shows through
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 12f;
            cam.fieldOfView = fov;
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; // keeps the alpha, so the comic panel is the background

            // A warm key from the front, and the two broadcast rims behind: the speaker's colour and cyan.
            Lamp(new Vector3(0.7f, 1.75f, 1.2f), Silhouette ? 0f : 2.2f, new Color(1f, 0.95f, 0.9f), 3.5f);
            Lamp(new Vector3(-0.8f, 1.9f, -0.7f), 5f, colour, 3f);
            Lamp(new Vector3(0.8f, 1.8f, -0.7f), Silhouette ? 5f : 3f, new Color32(46, 242, 255, 255), 3f);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            // Aim at the middle of the face (the head bone sits at the base of the skull), head and shoulders in frame.
            Vector3 target = head != null ? head.position + Vector3.up * 0.07f : transform.position + Vector3.up * 1.68f;
            float t = Time.unscaledTime - moodSince;
            float wantFov = 21f, wantHeight = 0f, wantDist = 1.9f, wantRoll = 0f, shake = 0f;
            switch (mood)
            {
                case Mood.Smug: wantHeight = -0.22f; wantRoll = -7f; wantDist = 2f; break;          // looking down on you
                case Mood.Angry: wantFov = 16f; wantDist = 1.75f; wantRoll = 4f; shake = Mathf.Max(0f, 0.6f - t) * 0.012f + 0.0015f; break;
                case Mood.Shocked: wantFov = 25f; wantHeight = 0.05f; wantDist = 2.1f; break;
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * (mood == Mood.Angry ? 14f : 7f));
            fov = Mathf.Lerp(fov, wantFov, k); height = Mathf.Lerp(height, wantHeight, k);
            dist = Mathf.Lerp(dist, wantDist, k); roll = Mathf.Lerp(roll, wantRoll, k);
            Vector3 pos = target + facing * dist + Vector3.up * height;
            if (shake > 0f) pos += new Vector3(Mathf.Sin(t * 71f), Mathf.Sin(t * 53f), 0f) * shake;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(target - pos, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            cam.fieldOfView = fov;
        }

        void OnDestroy() { if (Texture != null) { Texture.Release(); Destroy(Texture); } }

        void Lamp(Vector3 local, float intensity, Color colour, float range)
        {
            if (intensity <= 0f) return;
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = local;
            l.type = LightType.Point; l.intensity = intensity; l.color = colour; l.range = range;
            l.shadows = LightShadows.None;
        }

        /// <summary>Slim: a flat black shape against his panel. No light reaches him, so no face ever reads.</summary>
        static void Blackout(GameObject model)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return;
            var black = new Material(unlit) { color = new Color(0.012f, 0.008f, 0.02f) };
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = black;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>A fixed outfit for a non-player character, picked from their name: same every time. No helmet
        /// or mask, so the face shows.</summary>
        internal static string Outfit(DriverCatalog cat, DriverBody body, DriverSlot slot, int hash)
        {
            if (slot == DriverSlot.Helmet || slot == DriverSlot.Mask) return null;
            var styles = cat.Styles(body, slot);
            if (styles == null || styles.Count == 0) return null;
            int h = Mathf.Abs(hash * 31 + (int)slot * 977);
            // Optional pieces (glasses, hats, earrings) only on some characters.
            if (DriverCatalog.Optional(slot) && h % 3 != 0) return null;
            var style = styles[h % styles.Count];
            if (style == null || style.Count == 0) return null;
            return style[(h / 7) % style.Count].id;
        }

        internal static int Hash(string s) { unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h; } }
    }
}
