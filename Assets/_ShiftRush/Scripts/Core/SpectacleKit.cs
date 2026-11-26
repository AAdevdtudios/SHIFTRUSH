using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// The aircraft the race's set pieces fly (Resources/SR_Spectacle): the Channel 13 news chopper, the police
    /// chopper, the air-show jets and the far-off balloons. Models only, no colliders: nothing here touches a car.
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Spectacle Kit", fileName = "SR_Spectacle")]
    public class SpectacleKit : ScriptableObject
    {
        [Tooltip("Shadows the race leader all race, searchlight on.")]
        public GameObject newsChopper;
        [Tooltip("Joins on police episodes and sits on the player.")]
        public GameObject policeChopper;
        [Tooltip("Leads the final-lap formation.")]
        public GameObject leadJet;
        [Tooltip("The rest of the formation.")]
        public GameObject wingJet;
        [Tooltip("Drift far off in the background.")]
        public GameObject balloon;
        [Tooltip("Transparent particle material for jet smoke and searchlight beams (an asset, so builds keep its shader).")]
        public Material smoke;
        [Tooltip("Font for the fan zones' Channel 13 banners.")]
        public TMPro.TMP_FontAsset bannerFont;
        [Tooltip("Additive URP particle material: the race effects (built for the old pipeline) are re-skinned with it.")]
        public Material additive;

        [Header("Track dressing")]
        public GameObject checkpointArch;
        public GameObject checkpointSign;
        public GameObject checkpointFlag;
        public GameObject startSign;
        public GameObject finishSign;
        public GameObject chevronBoard;
        public GameObject tyreStack;
        public GameObject crashBarrel;
        public GameObject confettiFx;
        public GameObject arrowsFx;

        [Header("Giant")]
        [Tooltip("The one borrowed mech: stands in the harbour behind the skyline, watching the race.")]
        public GameObject mech;
        public AnimationClip mechIdle;
        public AnimationClip mechTaunt;
        [Tooltip("Scenes (by name) the giant appears in.")]
        public string[] mechScenes = { "Track_DowntownNeon" };

        static SpectacleKit instance;
        public static SpectacleKit Instance { get { if (instance == null) instance = Resources.Load<SpectacleKit>("SR_Spectacle"); return instance; } }
    }
}
