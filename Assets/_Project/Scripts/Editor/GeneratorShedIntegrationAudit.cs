using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Read-only audit for the Phase 2 / interior-shell integration: what owns the
    /// floor inside the shed, what the three assemblies actually measure against
    /// their locked envelopes, and how the current collision compares to the render
    /// geometry. Changes nothing.
    /// </summary>
    public static class GeneratorShedIntegrationAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        static readonly Vector3 ShedC = new Vector3(17f, 17f, 13f);
        const float ShedYaw = -5f;

        /// <summary>Shed-local (x, z) plus a height, out to world.</summary>
        static Vector3 W(float lx, float lz, float h)
        {
            float t = ShedYaw * Mathf.Deg2Rad, c = Mathf.Cos(t), s = Mathf.Sin(t);
            return new Vector3(ShedC.x + lx * c + lz * s, ShedC.y + h, ShedC.z - lx * s + lz * c);
        }

        [MenuItem("Tools/Last Beacon/Audit Generator Shed Integration")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            Debug.Log("===== 1. WHAT OWNS THE INTERIOR FLOOR =====");
            foreach (var (lx, lz) in new[] { (0f, 0f), (-3.5f, 0f), (3.5f, 0f),
                                             (0f, -2.8f), (0f, 2.8f), (4.0f, -2.8f) })
            {
                var from = W(lx, lz, 4.0f);
                if (Physics.Raycast(from, Vector3.down, out var hit, 12f))
                    Debug.Log($"[AUD] floor at local ({lx,5:0.0},{lz,5:0.0}) -> " +
                              $"'{hit.collider.name}' y={hit.point.y:0.000} " +
                              $"(shed datum {ShedC.y:0.000}, delta {hit.point.y - ShedC.y:+0.000;-0.000})");
                else
                    Debug.LogWarning($"[AUD] floor at local ({lx,5:0.0},{lz,5:0.0}) -> NOTHING within 12 m");
            }

            Debug.Log("===== 1b. INTERIOR INSTANCE ORIENTATION =====");
            var interior = GameObject.Find("SM_GeneratorShed_Interior");
            if (interior == null) Debug.LogError("[AUD] SM_GeneratorShed_Interior missing");
            else
            {
                foreach (var t in interior.GetComponentsInChildren<Transform>())
                    Debug.Log($"[AUD] xf {t.name,-26} localPos {t.localPosition} " +
                              $"localEuler {t.localEulerAngles} localScale {t.localScale}");
                foreach (var r in interior.GetComponentsInChildren<MeshRenderer>())
                    Debug.Log($"[AUD] {r.name,-22} world min {r.bounds.min} max {r.bounds.max}");
            }

            Debug.Log("===== 2. ASSEMBLY BOUNDS vs LOCKED ENVELOPES =====");
            var p2 = GameObject.Find("SM_GeneratorShed_P2");
            if (p2 == null) { Debug.LogError("[AUD] SM_GeneratorShed_P2 not in scene"); return; }
            var root = p2.transform;

            LocalBounds(root, "GENERATOR body art", r => r.name.StartsWith("SM_Generator_")
                                                         && !r.name.Contains("Exhaust"));
            LocalBounds(root, "GENERATOR exhaust",   r => r.name.Contains("Exhaust"));
            LocalBounds(root, "BREAKER assembly",    r => r.name.Contains("Breaker"));
            LocalBounds(root, "FUSE assembly",       r => r.name.Contains("Fuse"));

            Debug.Log("===== 3. CURRENT COLLISION =====");
            foreach (var bc in root.GetComponentsInChildren<BoxCollider>())
                Debug.Log($"[AUD] {bc.name,-24} size {bc.size} centre {bc.center}");

            Debug.Log("===== 4. MARKERS vs THEIR SURFACES =====");
            foreach (var name in new[] { "Generator_StartPoint", "Generator_FuelPoint",
                                         "Generator_RepairPoint", "Generator_Breaker", "Fuse_Storage" })
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                              .FirstOrDefault(x => x.name == name && x.GetComponent<Renderer>() == null);
                if (t == null) { Debug.LogWarning($"[AUD] marker {name} missing"); continue; }

                float best = float.MaxValue; string who = "-";
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                {
                    float d = r.bounds.SqrDistance(t.position);
                    if (d < best) { best = d; who = r.name; }
                }
                Debug.Log($"[AUD] {name,-22} at {t.position} -> nearest art {Mathf.Sqrt(best):0.000} m ({who})");
            }

            Debug.Log("===== 5. CLEARANCE SWEEP =====");
            // Walk the room on the player's centre line and report the tightest gap
            // the capsule sees between the machine and each wall.
            var gen = root.GetComponentsInChildren<MeshRenderer>()
                          .Where(r => r.name.StartsWith("SM_Generator_") && !r.name.Contains("Exhaust")).ToArray();
            var lo = Vector3.one * float.MaxValue; var hi = Vector3.one * float.MinValue;
            foreach (var r in gen) { lo = Vector3.Min(lo, LocalMin(root, r)); hi = Vector3.Max(hi, LocalMax(root, r)); }
            Debug.Log($"[AUD] generator local X[{lo.x:0.000},{hi.x:0.000}] Z[{lo.z:0.000},{hi.z:0.000}]");
            Debug.Log($"[AUD] doorway side {(-4.70f - lo.x) * -1:0.000} m   " +
                      $"electrical side {(4.70f - hi.x):0.000} m   " +
                      $"gables {(lo.z + 3.70f):0.000} / {(3.70f - hi.z):0.000} m");
        }

        static Vector3 LocalMin(Transform root, MeshRenderer r) => Corner(root, r, true);
        static Vector3 LocalMax(Transform root, MeshRenderer r) => Corner(root, r, false);

        static Vector3 Corner(Transform root, MeshRenderer r, bool wantMin)
        {
            var mf = r.GetComponent<MeshFilter>();
            var b = mf.sharedMesh.bounds;
            var acc = Vector3.one * (wantMin ? float.MaxValue : float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var lp = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                     (i & 2) == 0 ? b.min.y : b.max.y,
                                     (i & 4) == 0 ? b.min.z : b.max.z);
                var l = root.InverseTransformPoint(r.transform.TransformPoint(lp));
                acc = wantMin ? Vector3.Min(acc, l) : Vector3.Max(acc, l);
            }
            return acc;
        }

        static void LocalBounds(Transform root, string label, System.Func<MeshRenderer, bool> filter)
        {
            var parts = root.GetComponentsInChildren<MeshRenderer>().Where(filter).ToArray();
            if (parts.Length == 0) { Debug.LogWarning($"[AUD] {label}: no parts"); return; }
            var lo = Vector3.one * float.MaxValue; var hi = Vector3.one * float.MinValue;
            foreach (var r in parts) { lo = Vector3.Min(lo, LocalMin(root, r)); hi = Vector3.Max(hi, LocalMax(root, r)); }
            Debug.Log($"[AUD] {label,-22} local X[{lo.x:0.000},{hi.x:0.000}] " +
                      $"Y[{lo.y:0.000},{hi.y:0.000}] Z[{lo.z:0.000},{hi.z:0.000}]  ({parts.Length} parts)");
        }
    }
}
