using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Shift
{
    /// <summary>
    /// A SHIFT ramp. The trigger box sits at the lip: a car leaving the ramp forwards starts the SHIFT moment.
    /// <see cref="returnPoint"/> is the road spot a car is put back on after a SHIFT world.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ShiftRamp : MonoBehaviour
    {
        public Transform returnPoint;

        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var vehicle = rb.GetComponent<IVehicle>();
            if (vehicle == null) return;
            if (Vector3.Dot(rb.linearVelocity, transform.forward) < 5f) return; // leaving forwards, at speed
            ShiftDirector.Instance?.Launch(this, vehicle);
        }
    }
}
