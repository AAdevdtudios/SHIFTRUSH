using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Anything dropped into Resources/Audio is set up for its job automatically: music streams from disk
    /// (so long songs cost almost no memory), game sounds decompress on load (so they fire instantly).
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        const string Music = "/Resources/Audio/Music/", Sfx = "/Resources/Audio/SFX/";

        void OnPreprocessAudio()
        {
            bool music = assetPath.Contains(Music), sfx = assetPath.Contains(Sfx);
            if (!music && !sfx) return;
            var imp = (AudioImporter)assetImporter;
            var s = imp.defaultSampleSettings;
            if (music)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                imp.loadInBackground = true;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.8f;
            }
            s.preloadAudioData = !music;
            imp.defaultSampleSettings = s;
        }
    }
}
