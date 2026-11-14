using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Spawns extra cars (rivals) through whichever vehicle package is installed. The RCC Pro adapter registers
    /// its own spawner so a rival never steals the player's camera or controls; the fallback is a plain Instantiate.
    /// </summary>
    public static class VehicleSpawn
    {
        public static System.Func<GameObject, Vector3, Quaternion, IVehicle> Rival = (prefab, pos, rot) =>
        {
            var go = Object.Instantiate(prefab, pos, rot);
            return go.GetComponent<IVehicle>();
        };
    }
}
