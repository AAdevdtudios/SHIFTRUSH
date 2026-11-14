using ShiftRush.Race;
using ShiftRush.Shift;
using ShiftRush.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftRush.VFX
{
    /// <summary>
    /// Wires the effects into the game: gives every car a <see cref="CarFX"/> (the player in magenta, rivals in
    /// their own colours) and turns the full-screen overlays on and off around the SHIFT moment.
    /// Spawns itself in every scene with a car.
    /// </summary>
    public class FXDirector : MonoBehaviour
    {
        static FXDirector instance;
        static bool hooked;
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (!hooked) SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Spawn(s); };
            Spawn(SceneManager.GetActiveScene());
            if (hooked) return; // events are static: subscribe once even without a domain reload
            hooked = true;
            ShiftDirector.TimingStarted += () => { ScreenFX.Set(ScreenFX.Overlay.SlowMo, true); Core.MusicPlayer.SetSlowMo(true); };
            ShiftDirector.TimingEnded += () => { ScreenFX.Set(ScreenFX.Overlay.SlowMo, false); Core.MusicPlayer.SetSlowMo(false); };
            ShiftDirector.Warped += () => ScreenFX.Pulse(ScreenFX.Overlay.Warp);
            ShiftDirector.Returned += v =>
            {
                var fx = v != null ? v.Transform.GetComponent<CarFX>() : null;
                if (fx != null) fx.Arrive();
                ScreenFX.Pulse(ScreenFX.Overlay.Warp);
            };
        }

        static void Spawn(Scene scene)
        {
            if (instance != null) return;
            var go = new GameObject("FX Director");
            SceneManager.MoveGameObjectToScene(go, scene);
            instance = go.AddComponent<FXDirector>();
        }

        void OnDestroy() { if (instance == this) instance = null; }

        void Update()
        {
            // Rivals arrive after the scene loads: look for cars without effects now and then.
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            var kit = VFXKit.Instance;
            var race = RaceManager.Current;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(mb is IVehicle v) || mb.GetComponent<CarFX>() != null) continue;
                bool player = race == null ? true : (race.Player != null && race.Player.vehicle == v);
                if (race != null && race.Player == null) continue; // wait until the race has registered cars
                Color colour = kit != null ? kit.magenta : Color.magenta;
                if (!player && race != null)
                    foreach (var r in race.Racers) if (r.vehicle == v) colour = r.color;
                var cat = Core.RaceCatalog.Instance;
                var spec = cat != null ? cat.FindCar(mb.gameObject) : null;
                if (spec == null && player) spec = Core.RaceSession.Car;
                mb.gameObject.AddComponent<CarFX>().Init(v, colour, player, spec);
            }
        }
    }
}
