using System.Collections.Generic;
using ShiftRush.Core;
using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Puts a car's fitted parts on it. Tyres sit on each wheel model (so they turn and steer with it) in place of
    /// the stock wheel, sized to it. A spoiler replaces the car's own "Spoiler" mesh, fitted to its width and seated
    /// where it sat, whatever units the model was made in. Handling: the drift character moves one step, a wing
    /// adds downforce and narrows the slide. Used by the showroom (looks only) and on the grid (looks and handling).
    /// </summary>
    public static class CarLoadout
    {
        const string TyreTag = "Part Tyre", SpoilerTag = "Part Spoiler";

        /// <summary>The parts fitted to this car in the player's garage.</summary>
        public static void ApplyFitted(GameObject root, RaceCatalog.Car car, bool handling)
        {
            var parts = PartCatalog.Instance;
            if (root == null || car == null || parts == null) return;
            var tyre = parts.Find(PartCatalog.Slot.Tyres, PlayerProfile.Fitted(car, PartCatalog.Slot.Tyres));
            var wing = parts.Find(PartCatalog.Slot.Spoiler, PlayerProfile.Fitted(car, PartCatalog.Slot.Spoiler));
            Apply(root, car, tyre, wing, handling);
        }

        public static void Apply(GameObject root, RaceCatalog.Car car, PartCatalog.Part tyre, PartCatalog.Part wing, bool handling)
        {
            if (root == null || car == null) return;
            var fit = car.fit ?? new PartFit();
            bool tyresAllowed = tyre != null && System.Array.IndexOf(tyre.notFor, car.type) < 0 && fit.takesTyres;
            if (wing != null && !fit.takesSpoiler) wing = null;
            FitTyres(root.transform, tyresAllowed ? tyre.prefab : null, fit);
            FitSpoiler(root.transform, wing != null ? wing.prefab : null, fit);
            if (handling) ApplyHandling(root, car, tyresAllowed ? tyre : null, wing);
        }

        // ------------------------------------------------------------------ tyres

        static void FitTyres(Transform root, GameObject prefab, PartFit fit)
        {
            foreach (var old in Tagged(root, TyreTag)) Kill(old);
            foreach (var wheel in Wheels(root))
            {
                var r = wheel.GetComponent<MeshRenderer>();
                r.enabled = prefab == null;
                if (prefab == null) continue;

                var mesh = wheel.GetComponent<MeshFilter>().sharedMesh;
                var b = mesh.bounds;
                // The car's own wheel: its axle is the thinnest axis, its height the largest of the other two.
                Vector3 size = b.size; // in the wheel's own units: the tyre is its child, so it inherits any scale
                Vector3 axle = size.x <= size.y && size.x <= size.z ? Vector3.right : (size.y <= size.z ? Vector3.up : Vector3.forward);
                float width = Vector3.Dot(size, Abs(axle));
                Vector3 across = Vector3.Scale(size, Vector3.one - Abs(axle));
                float diameter = Mathf.Max(across.x, across.y, across.z);
                // The tyre's face (its +Z, where the nuts are) points away from the middle of the car.
                Vector3 outward = wheel.TransformPoint(b.center) - root.position;
                if (Vector3.Dot(wheel.TransformDirection(axle), outward) < 0f) axle = -axle;

                var tyre = Object.Instantiate(prefab);
                tyre.name = TyreTag;
                tyre.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                tyre.transform.localScale = Vector3.one;
                var tb = WorldBounds(tyre);                 // the pack's wheel: axle along its Z
                float tyreDiameter = Mathf.Max(tb.size.x, tb.size.y);
                float tyreWidth = Mathf.Max(0.0001f, tb.size.z);
                Vector3 tyreCentre = tb.center;
                StripPhysics(tyre);

                // Stretch it to the car's wheel in both height and width, so it fills the arch on a hatchback or a truck.
                float sd = tyreDiameter > 0.0001f ? diameter / tyreDiameter : 1f;
                float sw = width / tyreWidth;
                // Then the car's own fit from Parts Fitting: X/Y = height, Z = width.
                var scale = Vector3.Scale(new Vector3(sd, sd, sw), fit.tyreScale);
                tyre.transform.SetParent(wheel, false);
                tyre.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, axle);
                tyre.transform.localScale = scale;
                // Seat it on the wheel's centre (in the wheel's own units), then nudge by the car's offset:
                // X forward, Y up, Z outwards (mirrored, so both sides move out together).
                Vector3 nudge = wheel.InverseTransformDirection(root.forward) * fit.tyreOffset.x
                              + wheel.InverseTransformDirection(root.up) * fit.tyreOffset.y
                              + axle * fit.tyreOffset.z;
                tyre.transform.localPosition = b.center - tyre.transform.localRotation * Vector3.Scale(tyreCentre, scale) + nudge;
            }
        }

        static IEnumerable<Transform> Wheels(Transform root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.name.ToLowerInvariant();
                if (!n.Contains("wheel") || n.Contains("collider") || n.Contains("steering")) continue;
                if (mf.GetComponentInParent<LoadoutPart>() != null) continue;
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                yield return mf.transform;
            }
        }

        // ------------------------------------------------------------------ spoiler

        static void FitSpoiler(Transform root, GameObject prefab, PartFit fit)
        {
            foreach (var old in Tagged(root, SpoilerTag)) Kill(old);
            Transform stock = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains("spoiler") && t.GetComponent<Renderer>() != null && t.GetComponentInParent<LoadoutPart>() == null) { stock = t; break; }
            if (stock != null) foreach (var r in stock.GetComponentsInChildren<Renderer>(true)) r.enabled = prefab == null;
            if (prefab == null) return;

            // Where the stock spoiler sits, in the car's own space (or the rear of the body if it has none).
            Bounds body = LocalBounds(root, root, null);
            Bounds seat;
            if (stock != null) seat = LocalBounds(root, stock, null);
            else
            {
                bool longX = body.size.x > body.size.z;
                float back = longX ? body.min.x + body.size.x * 0.08f : body.min.z + body.size.z * 0.08f;
                seat = new Bounds(longX ? new Vector3(back, body.max.y - 0.25f, body.center.z) : new Vector3(body.center.x, body.max.y - 0.25f, back),
                                  longX ? new Vector3(0.4f, 0.3f, body.size.z * 0.8f) : new Vector3(body.size.x * 0.8f, 0.3f, 0.4f));
            }
            bool lateralZ = seat.size.z >= seat.size.x;               // which horizontal axis is across the car
            Vector3 lateral = lateralZ ? Vector3.forward : Vector3.right;
            float bodyWidth = lateralZ ? body.size.z : body.size.x;
            float width = Mathf.Clamp(lateralZ ? seat.size.z : seat.size.x, bodyWidth * 0.72f, bodyWidth * 0.92f);
            // Which way is the back of the car: from the body's middle towards the seat.
            Vector3 rear = lateralZ ? new Vector3(Mathf.Sign(seat.center.x - body.center.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(seat.center.z - body.center.z));

            var wing = Object.Instantiate(prefab);
            wing.name = SpoilerTag;
            wing.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            wing.transform.localScale = Vector3.one;
            var wb = WorldBounds(wing);                                  // the model's own units (often millimetres)
            StripPhysics(wing);

            // Model X across the car, Y up, and its +Z (the leading edge) towards the front.
            var rot = Quaternion.LookRotation(-rear, Vector3.up);
            if (Mathf.Abs(Vector3.Dot(rot * Vector3.right, lateral)) < 0.5f) rot = Quaternion.LookRotation(-rear, Vector3.up) * Quaternion.Euler(0, 90, 0);
            // The car's own fit: tilt about the wing's span, size, and a nudge (X forward, Y up, Z sideways).
            rot = rot * Quaternion.Euler(fit.spoilerTilt, 0f, 0f);
            float s = (wb.size.x > 0.0001f ? width / wb.size.x : 1f) * Mathf.Max(0.05f, fit.spoilerScale);
            wing.transform.SetParent(root, false);
            wing.transform.localRotation = rot;
            wing.transform.localScale = Vector3.one * s;
            Vector3 pos = seat.center - rot * (wb.center * s);
            pos.y = seat.min.y - wb.min.y * s;
            pos += -rear * fit.spoilerOffset.x + Vector3.up * fit.spoilerOffset.y + lateral * fit.spoilerOffset.z;
            wing.transform.localPosition = pos;
        }

        // ------------------------------------------------------------------ handling

        static void ApplyHandling(GameObject root, RaceCatalog.Car car, PartCatalog.Part tyre, PartCatalog.Part wing)
        {
            int step = Mathf.Clamp((tyre != null ? tyre.driftStep : 0) + (wing != null ? wing.driftStep : 0), -1, 1);
            float downforce = (tyre != null ? tyre.downforce : 0f) + (wing != null ? wing.downforce : 0f);
            var dm = root.GetComponent<DriftMoves>();
            if (dm != null)
            {
                dm.character = Shifted(CarTypes.Drift(car.type), step);
                dm.customTuning = false;
                dm.partAngleScale = 1f - 0.07f * downforce;
                dm.ApplyCharacter();
            }
            var df = root.GetComponent<PartDownforce>();
            if (downforce > 0f) { if (df == null) df = root.AddComponent<PartDownforce>(); df.level = downforce; }
            else if (df != null) Kill(df);
        }

        /// <summary>The drift character after a part's step: +1 looser (towards Drifty), -1 tidier (towards Grippy).</summary>
        public static DriftCharacter Shifted(DriftCharacter baseChar, int step)
        {
            int i = baseChar == DriftCharacter.Drifty ? 0 : baseChar == DriftCharacter.Grippy ? 2 : 1;
            i = Mathf.Clamp(i - step, 0, 2);
            return i == 0 ? DriftCharacter.Drifty : i == 2 ? DriftCharacter.Grippy : DriftCharacter.Balanced;
        }

        // ------------------------------------------------------------------ helpers

        static List<GameObject> Tagged(Transform root, string tag)
        {
            var list = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == tag) list.Add(t.gameObject);
            return list;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Bounds of a part's meshes in another transform's space (corners transformed, so rotation-safe).</summary>
        static Bounds LocalBounds(Transform space, Transform of, Transform skip)
        {
            bool first = true; var b = new Bounds();
            foreach (var mf in of.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponentInParent<LoadoutPart>() != null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

        /// <summary>Destroy that also works in the editor's Parts Fitting preview (outside Play mode).</summary>
        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        static void StripPhysics(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Kill(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Kill(rb);
            go.AddComponent<LoadoutPart>();
        }
    }
}
