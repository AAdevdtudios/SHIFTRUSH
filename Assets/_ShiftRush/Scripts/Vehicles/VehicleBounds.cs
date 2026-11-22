using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>A car's body in its own space, measured from its meshes (wheels and all).</summary>
    public static class VehicleBounds
    {
        public static Bounds Local(Transform car)
        {
            var b = new Bounds(Vector3.up * 0.6f, new Vector3(1.9f, 1.2f, 4.4f));
            bool any = false;
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = car.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}
