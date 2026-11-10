using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>Keyboard/gamepad navigation for a column of <see cref="OptionRow"/>s: W/S picks, A/D changes.</summary>
    public class OptionsList : MonoBehaviour
    {
        [SerializeField] OptionRow[] rows = new OptionRow[0];
        [SerializeField] CanvasGroup visibleGroup;

        int index;

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++) { rows[i].list = this; rows[i].rowIndex = i; }
        }

        void OnEnable()
        {
            foreach (var r in rows) r.Refresh();
            Select(0, false);
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void Update()
        {
            if (!Accepting || rows.Length == 0) return;
            if (MenuInput.Up) Select(index - 1, true);
            else if (MenuInput.Down) Select(index + 1, true);
            else if (MenuInput.Left) rows[index].Step(-1);
            else if (MenuInput.Right || MenuInput.Submit) rows[index].Step(1);
        }

        public void Select(int i, bool sound)
        {
            if (rows.Length == 0) return;
            i = (i + rows.Length) % rows.Length;
            if (sound && i != index) MenuSounds.Shift();
            index = i;
            for (int r = 0; r < rows.Length; r++) rows[r].SetSelected(r == index);
        }
    }
}
