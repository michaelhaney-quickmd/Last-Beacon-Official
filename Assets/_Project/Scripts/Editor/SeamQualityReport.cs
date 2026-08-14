using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Measures the two ramp-to-deck transitions where a diagonal ramp meets a
    /// receiving surface: the step across the full width of the seam, the step when
    /// crossing at oblique angles, and whether any walkable faces are stacked there.
    /// </summary>
    public static class SeamQualityReport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const float StepOffset = 0.45f;

        [MenuItem("Tools/Last Beacon/Report Seam Quality")]
        public static void Report()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            var playerGo = player != null ? player.gameObject : null;
            if (playerGo != null)
                playerGo.SetActive(false);
            Physics.SyncTransforms();

            Measure("terrace deck seam", Gen.WpTraverseMid, Gen.OverlookDeckEdge);
            Measure("ascent landing lip", Gen.WpOverlookExit, Gen.WpAscentATop);

            if (playerGo != null)
                playerGo.SetActive(true);
        }

        static void Measure(string label, Vector3 from, Vector3 to)
        {
            var horiz = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
            var side = new Vector3(horiz.z, 0f, -horiz.x);

            // --- straight-on crossing, sampled across the full 4 m width ----------
            float worstStraight = 0f;
            Vector3 worstAt = Vector3.zero;

            for (float lateral = -1.7f; lateral <= 1.7f; lateral += 0.2f)
            {
                float? before = GroundAt(to + side * lateral - horiz * 0.15f, to.y);
                float? after = GroundAt(to + side * lateral + horiz * 0.15f, to.y);
                if (before == null || after == null)
                    continue;

                float step = Mathf.Abs(after.Value - before.Value);
                if (step > worstStraight)
                {
                    worstStraight = step;
                    worstAt = to + side * lateral;
                }
            }

            Debug.Log($"[Seam] {label}: worst step across the seam {worstStraight:0.000} m at {worstAt}");

            // --- oblique crossings -------------------------------------------------
            float worstOblique = 0f;
            int worstAngle = 0;

            foreach (int angle in new[] { -45, -30, -15, 15, 30, 45 })
            {
                var dir = Quaternion.Euler(0f, angle, 0f) * horiz;
                float worstThis = 0f;

                for (float lateral = -1.6f; lateral <= 1.6f; lateral += 0.4f)
                {
                    var at = to + side * lateral;
                    float? before = GroundAt(at - dir * 0.2f, to.y);
                    float? after = GroundAt(at + dir * 0.2f, to.y);
                    if (before == null || after == null)
                        continue;

                    worstThis = Mathf.Max(worstThis, Mathf.Abs(after.Value - before.Value));
                }

                if (worstThis > worstOblique)
                {
                    worstOblique = worstThis;
                    worstAngle = angle;
                }
            }

            Debug.Log($"[Seam] {label}: worst step crossing obliquely {worstOblique:0.000} m (at {worstAngle} deg)");

            // --- stacked walkable faces at the seam --------------------------------
            var stacked = new List<string>();
            for (float lateral = -1.7f; lateral <= 1.7f; lateral += 0.5f)
            for (float along = -1f; along <= 1f; along += 0.5f)
            {
                var at = to + side * lateral + horiz * along;
                var hits = Physics.RaycastAll(at + Vector3.up * 3f, Vector3.down, 6f)
                    .Where(h => h.point.y > to.y - 1f && h.point.y < to.y + 1.2f)
                    .OrderByDescending(h => h.point.y)
                    .ToArray();

                for (int k = 1; k < hits.Length; k++)
                {
                    float gap = hits[k - 1].point.y - hits[k].point.y;
                    if (gap < 0.12f)
                        stacked.Add($"{hits[k - 1].collider.name} / {hits[k].collider.name} " +
                                    $"{gap:0.000} m apart at {at}");
                }
            }

            Debug.Log(stacked.Count == 0
                ? $"[Seam] {label}: no stacked walkable faces."
                : $"[Seam] {label}: {stacked.Distinct().Count()} stacked pair(s):\n  " +
                  string.Join("\n  ", stacked.Distinct().Take(6)));

            Debug.Log($"[Seam] {label}: {(worstStraight <= 0.2f && worstOblique <= 0.2f ? "within 0.20 m target" : worstStraight <= StepOffset && worstOblique <= StepOffset ? "over 0.20 m but under the step offset" : "OVER THE STEP OFFSET")}");
        }

        static float? GroundAt(Vector3 p, float expectedY)
        {
            float ceiling = expectedY + 1.2f;
            float? best = null;
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, expectedY + 3f, p.z), Vector3.down, 8f))
            {
                if (hit.point.y > ceiling) continue;
                if (best == null || hit.point.y > best.Value) best = hit.point.y;
            }
            return best;
        }
    }
}
