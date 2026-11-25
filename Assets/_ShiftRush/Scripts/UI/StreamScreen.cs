using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// The big screen on the airstrip: Channel 13 running its own broadcast. Every few seconds it cuts to the next
    /// slide: the channel ident, the car on the turntable, tonight's track, the viewer count, and a fit check when
    /// the driver is on stage in the garage. A short static flash covers each cut.
    /// </summary>
    public class StreamScreen : MonoBehaviour
    {
        [SerializeField] Showroom showroom;
        [SerializeField] TMP_Text tag;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text line;
        [SerializeField] TMP_Text ticker;
        [SerializeField] Image flash;
        [SerializeField] Image accent;
        [SerializeField] float slideSeconds = 5f;

        static readonly Color Pink = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);
        static readonly Color Gold = new Color32(255, 197, 61, 255);
        static readonly Color Orange = new Color32(255, 138, 61, 255);

        int slide = -1;
        float timer, flashT, viewers;

        void OnEnable()
        {
            viewers = 41200f + Random.value * 6000f;
            timer = 0f;
            Next();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            viewers += dt * Random.Range(4f, 22f);
            timer += dt;
            if (timer >= slideSeconds) { timer = 0f; Next(); }
            flashT = Mathf.MoveTowards(flashT, 0f, dt * 5f);
            if (flash) flash.color = new Color(1f, 1f, 1f, flashT * flashT * 0.5f);
            if (ticker) ticker.text = "CH13 // LIVE   " + Mathf.RoundToInt(viewers).ToString("N0") + " WATCHING   //   PRESS L IN THE GARAGE TO CHANGE THE LIGHT";
        }

        void Next()
        {
            bool driverOn = showroom != null && showroom.ShownDriver != null;
            int count = driverOn ? 5 : 4;
            slide = (slide + 1) % count;
            flashT = 1f;
            var car = showroom != null ? showroom.ShownCar : null;
            var track = RaceSession.Track;
            switch (slide)
            {
                case 0:
                    Set("CHANNEL 13", "GRIP PRO", "Pirate feed, live from the airstrip.", Pink);
                    break;
                case 1:
                    Set("TONIGHT'S RIDE", car != null ? car.displayName.ToUpperInvariant() : "THE CREW'S CAR",
                        car != null ? Vehicles.CarTypes.Name(car.type) + "  ·  " + car.tagline : "Fresh off the turntable.", Cyan);
                    break;
                case 2:
                    Set("ON THE SCHEDULE", track != null ? track.displayName.ToUpperInvariant() : "TBA",
                        track != null ? track.LengthLabel + "  ·  " + Short(track.description) : "The crew is still scouting.", Gold);
                    break;
                case 3:
                    Set("THE FEED", Mathf.RoundToInt(viewers / 100f) / 10f + "K", "People watching right now. Give them something.", Pink);
                    break;
                default:
                    DriverProfile.Lean(DriverProfile.Body, out int gloss, out int grit);
                    int total = gloss + grit;
                    int g = total == 0 ? 50 : Mathf.RoundToInt(100f * gloss / total);
                    Set("FIT CHECK", g >= 65 ? "GLOSS" : g <= 35 ? "GRIT" : "MIXED", "GLOSS " + g + "%  ·  GRIT " + (100 - g) + "%", g <= 35 ? Orange : Cyan);
                    break;
            }
        }

        void Set(string t, string big, string sub, Color c)
        {
            if (tag) tag.text = t;
            if (title) title.text = big;
            if (line) line.text = sub;
            if (accent) accent.color = c;
            if (title) title.color = Color.white;
        }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int cut = s.IndexOf('.');
            return cut > 0 && cut < 70 ? s.Substring(0, cut + 1) : (s.Length > 70 ? s.Substring(0, 67) + "..." : s);
        }
    }
}
