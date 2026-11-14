using System.Collections.Generic;
using ShiftRush.Race;
using ShiftRush.Shift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Puts 2 SHIFT ramps on each lap: the two straightest, flattest stretches of the race line (with room to land),
    /// at least a third of a lap apart, clear of the start line and of anything overhead (the avalanche gallery).
    /// Each ramp gets a return point on the road past its landing zone, where SHIFT worlds hand the car back.
    /// </summary>
    public static partial class TrackBuilder
    {
        const int RampsPerLap = 2;

        [MenuItem("SHIFT RUSH/Tracks/Add SHIFT Ramps To Tracks")]
        static void AddShiftRampsToTracks()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShiftKit.RampPath) == null) ShiftKit.Build();
            foreach (var s in new[] { AlpinePass(), GlacierValley() })
            {
                var scene = EditorSceneManager.OpenScene(s.scene, OpenSceneMode.Single);
                AddShiftRamps(s);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        public static void AddShiftRamps(Spec s)
        {
            var oldHolder = GameObject.Find("SHIFT Ramps");
            if (oldHolder != null) Object.DestroyImmediate(oldHolder);
            foreach (var old in Object.FindObjectsByType<ShiftRamp>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);

            var line = Object.FindFirstObjectByType<RaceLine>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShiftKit.RampPath);
            if (line == null || prefab == null) { Debug.LogWarning("Needs a Race Line and the SHIFT kit."); return; }

            var pts = line.points;
            int n = pts.Length;
            const int back = 6, ahead = 26; // judge 24 m before to ~100 m after the ramp foot
            var score = new float[n];
            for (int k = 0; k < n; k++)
            {
                float d = line.distances[k];
                if (d < 220f || d > line.length - 160f) { score[k] = float.MaxValue; continue; }
                bool nearCut = false;
                foreach (var cut in Object.FindObjectsByType<ShortcutPath>(FindObjectsSortMode.None))
                    if (Mathf.Repeat(d - cut.entryDistance + 80f, line.length) < Mathf.Repeat(cut.exitDistance - cut.entryDistance, line.length) + 160f) nearCut = true;
                if (nearCut) { score[k] = float.MaxValue; continue; }
                float turn = 0f, climb = 0f;
                for (int j = -back; j < ahead; j++)
                {
                    turn += Vector3.Angle(line.Direction((k + j + n) % n), line.Direction((k + j + 1 + n) % n));
                    climb += Mathf.Abs(pts[(k + j + 1 + n) % n].y - pts[(k + j + n) % n].y);
                }
                // Nothing overhead (gallery roof) over the ramp or landing.
                bool covered = false;
                for (int j = 0; j < ahead && !covered; j += 3)
                    covered = Physics.Raycast(pts[(k + j) % n] + Vector3.up * 1.5f, Vector3.up, 30f);
                score[k] = covered ? float.MaxValue : turn + climb * 4f;
            }

            var chosen = new List<int>();
            for (int r = 0; r < RampsPerLap; r++)
            {
                int best = -1; float bestS = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    if (score[k] >= bestS) continue;
                    bool far = true;
                    foreach (var c in chosen)
                    {
                        float gap = Mathf.Abs(line.distances[k] - line.distances[c]);
                        gap = Mathf.Min(gap, line.length - gap);
                        if (gap < line.length * 0.33f) far = false;
                    }
                    if (far) { best = k; bestS = score[k]; }
                }
                if (best >= 0) chosen.Add(best);
            }

            var holder = new GameObject("SHIFT Ramps").transform;
            int count = 0;
            foreach (var k in chosen)
            {
                Vector3 foot = pts[k];
                Vector3 dir = (pts[(k + 3) % n] - foot).normalized;
                var ramp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                ramp.name = "SHIFT Ramp " + (++count);
                ramp.transform.SetPositionAndRotation(foot + Vector3.up * 0.02f, Quaternion.LookRotation(dir, Vector3.up));

                int ret = (k + Mathf.RoundToInt((ShiftKit.RampLengthM + 48f) / 4f)) % n;
                var flat = line.Direction(ret);
                var rp = new GameObject("Return Point").transform;
                rp.SetParent(ramp.transform, true);
                rp.SetPositionAndRotation(pts[ret] + Vector3.up * 0.8f, Quaternion.LookRotation(flat, Vector3.up));
                ramp.GetComponent<ShiftRamp>().returnPoint = rp;
            }
            Debug.Log("[SHIFT//RUSH] " + s.name + ": " + count + " SHIFT ramps at " +
                string.Join(", ", chosen.ConvertAll(c => line.distances[c].ToString("0") + " m").ToArray()));
        }
    }
}
