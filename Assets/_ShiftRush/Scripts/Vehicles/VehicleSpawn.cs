using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Spawns cars (the player's chosen car, rivals) through whichever vehicle package is installed. The RCC Pro adapter registers
    /// its own spawner so a rival never steals the player's camera or controls; the fallback is a plain Instantiate.
    /// </summary>
    public static class VehicleSpawn
    {
        public static System.Func<GameObject, Vector3, Quaternion, IVehicle> Rival = (prefab, pos, rot) =>
        {
            var go = Object.Instantiate(prefab, pos, rot);
            return go.GetComponent<IVehicle>();
        };

        /// <summary>Makes a car stronger (or weaker): engine torque, top speed and tyre grip, × the factor.</summary>
        public static System.Action<GameObject, float> Boost = (car, factor) => { };

        /// <summary>Spawns the player's car: it takes the camera, dashboard and controls.</summary>
        public static System.Func<GameObject, Vector3, Quaternion, IVehicle> Player = (prefab, pos, rot) =>
        {
            var go = Object.Instantiate(prefab, pos, rot);
            return go.GetComponent<IVehicle>();
        };
    }
}
