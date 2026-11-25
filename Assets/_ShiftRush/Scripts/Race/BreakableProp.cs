using System.Collections;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// Lightweight track dressing that yields to a vehicle instead of behaving like an immovable obstacle.
    /// Initial contact is a trigger, then the prop becomes physical after it has cleared the vehicle.
    /// </summary>
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public class BreakableProp : MonoBehaviour
    {
        [SerializeField] float breakSpeed = 2f;
        [SerializeField] float mass = 4f;
        [SerializeField] float lifetime = 8f;
        [SerializeField] float throwSpeedScale = 0.55f;
        [SerializeField] float maxThrowSpeed = 18f;

        Rigidbody body;
        Collider[] propColliders;
        bool broken;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            propColliders = GetComponentsInChildren<Collider>(true);
            body.isKinematic = true;
            body.useGravity = true;
            body.mass = mass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            foreach (var propCollider in propColliders) propCollider.isTrigger = true;
            // RCC's prop layer: cars still hit it, but the chase camera doesn't treat it (or the debris once it's
            // smashed) as a wall to dodge, so it stops snapping in close every time you plough through something.
            int propLayer = LayerMask.NameToLayer("RCCP_Prop");
            if (propLayer >= 0) foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = propLayer;
        }

        void OnTriggerEnter(Collider other)
        {
            var vehicleBody = other.attachedRigidbody;
            if (broken || vehicleBody == null || !IsVehicle(vehicleBody)) return;
            if (vehicleBody.linearVelocity.magnitude < breakSpeed) return;

            broken = true;
            transform.SetParent(null, true);
            var vehicleColliders = vehicleBody.GetComponentsInChildren<Collider>(true);
            foreach (var propCollider in propColliders)
                foreach (var vehicleCollider in vehicleColliders)
                    Physics.IgnoreCollision(propCollider, vehicleCollider, true);

            foreach (var propCollider in propColliders) propCollider.isTrigger = false;
            body.isKinematic = false;
            body.WakeUp();
            body.linearVelocity = Vector3.ClampMagnitude(vehicleBody.linearVelocity * throwSpeedScale + Vector3.up * 1.5f, maxThrowSpeed);
            body.angularVelocity = Random.insideUnitSphere * 5f;
            StartCoroutine(RestoreVehicleCollisions(vehicleColliders));
            Destroy(gameObject, lifetime);
        }

        IEnumerator RestoreVehicleCollisions(Collider[] vehicleColliders)
        {
            yield return new WaitForSeconds(0.4f);
            foreach (var propCollider in propColliders)
                foreach (var vehicleCollider in vehicleColliders)
                    if (propCollider != null && vehicleCollider != null)
                        Physics.IgnoreCollision(propCollider, vehicleCollider, false);
        }

        static bool IsVehicle(Rigidbody other)
        {
            var behaviours = other.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
                if (behaviour is Vehicles.IVehicle) return true;
            return false;
        }
    }
}
