using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.VFX
{
    /// <summary>
    /// The toon look's parts, made by SHIFT RUSH → Build Toon Look: the car shader, and the ink outline renderer
    /// features it added to each URP renderer. Resources/SR_ToonKit.
    /// </summary>
    public class ToonKit : ScriptableObject
    {
        public Shader carShader;
        public Material outline;
        public List<ScriptableRendererFeature> outlineFeatures = new List<ScriptableRendererFeature>();

        static ToonKit instance;
        public static ToonKit Instance { get { if (instance == null) instance = Resources.Load<ToonKit>("SR_ToonKit"); return instance; } }
    }
}
