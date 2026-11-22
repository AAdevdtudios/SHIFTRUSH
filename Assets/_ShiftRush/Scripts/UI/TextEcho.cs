using TMPro;
using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>The cyan copy behind a title: keeps the same words as the title it echoes, whatever sets them.</summary>
    public class TextEcho : MonoBehaviour
    {
        public TMP_Text source;
        TMP_Text self;

        void Awake() { self = GetComponent<TMP_Text>(); }

        void LateUpdate()
        {
            if (source != null && self != null && self.text != source.text) self.text = source.text;
        }
    }
}
