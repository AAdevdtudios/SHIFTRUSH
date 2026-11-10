using UnityEngine;
using UnityEngine.EventSystems;

namespace ShiftRush.UI
{
    /// <summary>Mouse support for a gear row: hover selects, click engages.</summary>
    public class GearMenuRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [HideInInspector] public GearMenu menu;
        [HideInInspector] public int gearIndex;

        public void OnPointerEnter(PointerEventData eventData) { if (menu) menu.SetIndex(gearIndex, true); }
        public void OnPointerClick(PointerEventData eventData) { if (menu) menu.Engage(gearIndex); }
    }
}
