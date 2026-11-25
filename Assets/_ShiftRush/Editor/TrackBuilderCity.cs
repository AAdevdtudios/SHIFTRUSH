using System.Collections.Generic;
using SplineArchitect;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>The city's road uses the same Spline Architect road (asphalt, kerbs on the bends) as every other track.</summary>
    public static partial class TrackBuilder
    {
        internal static Spline CityRoad(Transform root, List<Vector3> points)
        {
            return BuildRoad(root, points, new Spec { name = "Downtown Neon" }, TrackKit.Load());
        }
    }
}
