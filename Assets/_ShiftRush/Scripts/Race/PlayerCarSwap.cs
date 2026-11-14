using ShiftRush.Core;
using ShiftRush.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftRush.Race
{
    /// <summary>
    /// Puts the car picked in the garage on the track. Each circuit scene is built with a Zephyr on the grid; as the
    /// race HUD sets the scene up (before the race registers its racers) that car is swapped for the player's choice in the same
    /// spot. Nothing happens when the choice already is the car in the scene.
    /// </summary>
    public static class PlayerCarSwap
    {
        /// <summary>Called by <see cref="RaceHUD"/> as it sets up a scene, before anything looks for the player's car.</summary>
        public static void Apply(Scene scene)
        {
            if (Object.FindFirstObjectByType<RaceLine>() == null) return;
            var choice = RaceSession.Car;
            var cat = RaceCatalog.Instance;
            if (choice == null || choice.prefab == null || cat == null) return;

            GameObject placed = null;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IVehicle && mb.gameObject.scene == scene) { placed = mb.gameObject; break; }
            if (placed == null || cat.FindCar(placed) == choice) return;

            var pos = placed.transform.position;
            var rot = placed.transform.rotation;
            placed.SetActive(false); // out of every search from here on
            Object.Destroy(placed);
            var v = VehicleSpawn.Player(choice.prefab, pos, rot);
            if (v != null)
            {
                v.Transform.name = choice.prefab.name;
                SceneManager.MoveGameObjectToScene(v.Transform.gameObject, scene);
            }
        }
    }
}
