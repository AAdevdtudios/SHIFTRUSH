using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ShiftRush.Comic
{
    /// <summary>The origin comic's pages in order, for <see cref="ComicIntro"/>. Lives at Resources/SR_ComicIntro.</summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Comic Intro", fileName = "SR_ComicIntro")]
    public class ComicIntroData : ScriptableObject
    {
        public List<Texture2D> pages = new List<Texture2D>();
        public TMP_FontAsset mono;
    }
}
