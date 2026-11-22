using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// A car knocked by a spiral roll. Scripted, so it reads the same every time and never leaves a car on its roof:
    /// thrown away from the hit, lifted, barrel rolled toward the side it was pushed, then dropped back on its wheels
    /// with part of its speed gone. Strength comes from the weight of the two cars: a heavy car sends a light one
    /// flying (two rolls), a light car only shoves a heavy one (a lurch, no roll).
    /// While it tumbles it knocks any car it hits in turn, weaker each time (the chain).
    /// Added to the victim on the first hit. Damage is only reported for now (<see cref="Knocked"/>), the damage
    /// system comes later.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(60)]
    public class KnockTumble : MonoBehaviour
    {
        /// <summary>attacker (who rolled), victim, damage, chain step (1 = hit directly by the roll).</summary>
        public static event System.Action<GameObject, GameObject, float, int> Knocked;

        [Header("Tumble at strength 1 (two cars of the same weight)")]
        [Tooltip("How far the victim is thrown sideways, metres.")]
        public float pushMeters = 6f;
        [Tooltip("How high it is lifted, metres.")]
        public float liftMeters = 1.6f;
        [Tooltip("Seconds in the air.")]
        public float tumbleTime = 0.85f;
        [Tooltip("Share of its forward speed the victim keeps.")]
        [Range(0f, 1f)] public float speedKeep = 0.7f;

        [Header("Chain")]
        [Tooltip("Each car further down the chain is hit this much softer.")]
        [Range(0f, 1f)] public float chainFactor = 0.6f;
        [Tooltip("Most cars one roll can knock, the first one included.")]
        public int maxChain = 3;

        [Header("Damage (reported only, not applied yet)")]
        public float baseDamage = 20f;

        public bool Tumbling { get { return t >= 0f; } }

        Rigidbody body;
        IVehicle vehicle;
        AirControl air;
        Bounds local;
        float t = -1f, duration, vy, g, pushSpeed, fwdSpeed, rollAngle, strength;
        int side, chain;
        Vector3 fwdDir, pushDir;
        GameObject attacker;
        HashSet<Rigidbody> hitSet;

        /// <summary>
        /// Knocks target away along pushDir. strength: 1 for cars of equal weight (the roller's mass over the target's,
        /// clamped 0.3..2). hitSet: cars already hit by this roll, shared down the chain so nothing is hit twice.
        /// Returns false if the target is already tumbling.
        /// </summary>
        public static bool Hit(Rigidbody target, Vector3 pushDir, float strength, GameObject attacker, int chain, HashSet<Rigidbody> hitSet)
        {
            if (target == null) return false;
            var k = target.GetComponent<KnockTumble>();
            if (k == null) k = target.gameObject.AddComponent<KnockTumble>();
            if (k.Tumbling) return false;
            k.Begin(pushDir, strength, attacker, chain, hitSet);
            return true;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            vehicle = GetComponent<IVehicle>();
            air = GetComponent<AirControl>();
            local = VehicleBounds.Local(transform);
        }

        void Begin(Vector3 push, float s, GameObject who, int step, HashSet<Rigidbody> set)
        {
            strength = Mathf.Clamp(s, 0.3f, 2f);
            attacker = who; chain = step;
            hitSet = set ?? new HashSet<Rigidbody>();
            hitSet.Add(body);

            fwdDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwdDir.sqrMagnitude < 0.01f) fwdDir = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            fwdDir.Normalize();
            pushDir = Vector3.ProjectOnPlane(push, Vector3.up);
            pushDir = pushDir.sqrMagnitude > 0.01f ? pushDir.normalized : Vector3.Cross(Vector3.up, fwdDir);
            side = Vector3.Dot(pushDir, Vector3.Cross(Vector3.up, fwdDir)) >= 0f ? 1 : -1; // 1 = pushed to its right

            float k01 = Mathf.InverseLerp(0.3f, 2f, strength);
            duration = tumbleTime * Mathf.Lerp(0.75f, 1.2f, k01);
            float h = liftMeters * Mathf.Lerp(0.3f, 1.6f, k01);
            vy = 4f * h / duration;
            g = 8f * h / (duration * duration);
            pushSpeed = pushMeters * strength / duration;
            fwdSpeed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity, fwdDir)) * speedKeep;
            // A light car only shoves a heavy one (a lurch, no roll). Equal weight: one roll. Much heavier: two.
            int turns = strength < 0.6f ? 0 : strength > 1.3f ? 2 : 1;
            rollAngle = -side * 360f * turns; // a positive angle about forward rolls left
            t = 0f;

            float damage = baseDamage * strength * Mathf.Pow(chainFactor, chain - 1);
            if (Knocked != null) Knocked(attacker, gameObject, damage, chain);
        }

        void FixedUpdate()
        {
            if (t < 0f) return;
            float dt = Time.fixedDeltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / duration);
            body.useGravity = false;
            if (air != null) air.enabled = false;

            vy -= g * dt;
            // Thrown hard at first, then drifting (same front-loaded shape as the dodge).
            body.linearVelocity = fwdDir * fwdSpeed + pushDir * (pushSpeed * 2f * (1f - k)) + Vector3.up * vy;

            Quaternion level = Quaternion.LookRotation(fwdDir, Vector3.up);
            float r = Mathf.Clamp01(k / 0.85f);
            float e = r * r * (3f - 2f * r);
            float angle = rollAngle != 0f ? rollAngle * e : -side * 18f * Mathf.Sin(k * Mathf.PI); // no roll: a lurch away
            body.MoveRotation(Quaternion.AngleAxis(angle, fwdDir) * level);
            body.angularVelocity = Vector3.zero;

            if (chain < maxChain && k < 0.75f) Chain();

            bool falling = vy < 0f;
            if ((falling && k > 0.8f && vehicle != null && vehicle.AnyWheelGrounded) || k >= 1f) End(level);
        }

        /// <summary>Knocks the next car this one hits, softer.</summary>
        void Chain()
        {
            Vector3 centre = body.worldCenterOfMass;
            Vector3 half = local.extents + new Vector3(0.4f, 0.4f, 0.3f);
            var hits = Physics.OverlapBox(centre, half, Quaternion.LookRotation(fwdDir, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                var rb = c.attachedRigidbody;
                if (rb == null || hitSet.Contains(rb) || rb.GetComponent<IVehicle>() == null) continue;
                if (attacker != null && rb.gameObject == attacker) continue;
                hitSet.Add(rb);
                Vector3 away = Vector3.ProjectOnPlane(rb.worldCenterOfMass - centre, Vector3.up);
                Vector3 push = (pushDir + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero)).normalized;
                float s = strength * chainFactor * Mathf.Clamp(body.mass / Mathf.Max(1f, rb.mass), 0.5f, 1.5f);
                Hit(rb, push, s, attacker, chain + 1, hitSet);
                if (chain + 1 >= maxChain) return;
            }
        }

        void End(Quaternion level)
        {
            t = -1f;
            body.useGravity = true;
            if (air != null) air.enabled = true;
            body.MoveRotation(level);
            body.angularVelocity = Vector3.zero;
            body.linearVelocity = fwdDir * fwdSpeed - Vector3.up * 3f;
            var s = GetComponent<KnockStabilizer>();
            if (s == null) s = gameObject.AddComponent<KnockStabilizer>();
            s.duration = 0.8f;
            s.Kick();
        }

        void OnDisable()
        {
            if (t >= 0f && body != null) { body.useGravity = true; if (air != null) air.enabled = true; t = -1f; }
        }
    }
}
