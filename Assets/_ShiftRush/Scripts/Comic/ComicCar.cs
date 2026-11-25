using ShiftRush.Core;
using UnityEngine;

namespace ShiftRush.Comic
{
    /// <summary>
    /// A car in a comic panel, built like the showroom builds it: model only, no physics, with the parts fitted in the
    /// garage. Empty id = the car the player last picked, so the panel shows their car the same way it shows their driver.
    /// Not saved with the scene, rebuilt when it opens.
    /// </summary>
    [ExecuteAlways]
    public class ComicCar : MonoBehaviour
    {
        [Tooltip("Race catalog car id (zephyr, ...). Empty = the player's current car.")]
        public string carId = "";

        GameObject built;
        string builtFor;

        void OnEnable() { Build(); }
        void OnDisable() { Clear(); }

        RaceCatalog.Car Car()
        {
            var cat = RaceCatalog.Instance;
            if (cat == null) return null;
            if (string.IsNullOrEmpty(carId)) return RaceSession.Car;
            foreach (var c in cat.cars) if (c.id == carId) return c;
            return null;
        }

        void Update()
        {
            var car = Car();
            string want = car != null ? car.id : "";
            if (want != builtFor || built == null) Build();
        }

        void Build()
        {
            Clear();
            var car = Car();
            builtFor = car != null ? car.id : "";
            if (car == null) return;
            var source = car.showroomPrefab != null ? car.showroomPrefab : car.prefab;
            if (source == null) return;
            var holder = new GameObject("Comic Car Body");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            var model = Instantiate(source, holder.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            if (car.showroomPrefab == null) UI.Showroom.Strip(model);
            Vehicles.CarLoadout.ApplyFitted(model, car, false);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            holder.SetActive(true);
            // Wheels on the ground at this object's position.
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                model.transform.position += new Vector3(transform.position.x - b.center.x, transform.position.y - b.min.y, transform.position.z - b.center.z);
            }
            built = holder;
        }

        void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (c.name != "Comic Car Body") continue;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            built = null;
        }
    }
}
