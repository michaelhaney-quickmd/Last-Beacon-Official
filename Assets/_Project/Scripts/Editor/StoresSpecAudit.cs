using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Measures the Stores/Radio blockout so an external artist can author a
    /// replacement shell that drops in without guesswork. Read-only.
    /// </summary>
    public static class StoresSpecAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const float TierCompound = 17f;

        [MenuItem("Tools/Last Beacon/Audit Stores Spec")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                         FindObjectsSortMode.None);

            var group = all.FirstOrDefault(t => t.name == "StoresRadio");
            if (group == null) { Debug.LogError("[SP] StoresRadio group not found"); return; }

            Debug.Log("[SP] ===== StoresRadio group =====");
            var whole = new Bounds();
            bool first = true;
            foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                if (first) { whole = b; first = false; } else whole.Encapsulate(b);
                var t = r.transform;
                Debug.Log($"[SP] {r.name}\n" +
                          $"      pos   {V(t.position)}  rotY {t.rotation.eulerAngles.y:0.00}\n" +
                          $"      size  {V(b.size)}  (world AABB, includes rotation)\n" +
                          $"      AABB  x {b.min.x:0.00}..{b.max.x:0.00}  y {b.min.y:0.00}..{b.max.y:0.00}  " +
                          $"z {b.min.z:0.00}..{b.max.z:0.00}\n" +
                          $"      mesh  local size {V(r.GetComponent<MeshFilter>().sharedMesh.bounds.size)}  " +
                          $"tris {r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3}");
            }
            Debug.Log($"[SP] GROUP TOTAL world AABB x {whole.min.x:0.00}..{whole.max.x:0.00}  " +
                      $"y {whole.min.y:0.00}..{whole.max.y:0.00}  z {whole.min.z:0.00}..{whole.max.z:0.00}");
            Debug.Log($"[SP] GROUP TOTAL size {V(whole.size)}  centre {V(whole.center)}");
            int gt = group.GetComponentsInChildren<MeshFilter>(true)
                          .Sum(f => f.sharedMesh.triangles.Length / 3);
            Debug.Log($"[SP] blockout group tris: {gt}");

            // The floor the building stands on, and headroom to anything above.
            Debug.Log("[SP] ===== siting =====");
            Debug.Log($"[SP] compound floor Y = {TierCompound:0.00}; group base Y = {whole.min.y:0.00} " +
                      $"(so the shell sits {whole.min.y - TierCompound:0.00} m above the floor)");

            // Doorway: measured from the door recess prop and the task markers.
            var recess = all.FirstOrDefault(t => t.name == "Stores_DoorRecess");
            if (recess != null)
            {
                var b = recess.GetComponent<Renderer>().bounds;
                Debug.Log($"[SP] Stores_DoorRecess pos {V(recess.position)} size {V(b.size)} " +
                          $"y {b.min.y:0.00}..{b.max.y:0.00}");
            }

            Debug.Log("[SP] ===== interior fixtures that must fit =====");
            foreach (var n in new[] { "Stores_RadioSet", "Stores_ManifestDesk", "Cabinet_Ammunition",
                                      "Stores_DeliveryShelf" })
            {
                var t = all.FirstOrDefault(x => x.name == n);
                if (t == null) { Debug.Log($"[SP] {n}: NOT PRESENT"); continue; }
                var b = t.GetComponent<Renderer>().bounds;
                Debug.Log($"[SP] {n}: pos {V(t.position)} size {V(b.size)} top Y {b.max.y:0.00}");
            }

            Debug.Log("[SP] ===== gameplay markers (must stay reachable) =====");
            foreach (var n in new[] { "Ammo_Storage", "Radio_Point", "Manifest_Point", "Delivery_Records" })
            {
                var t = all.FirstOrDefault(x => x.name == n);
                Debug.Log(t == null ? $"[SP] {n}: NOT PRESENT"
                                    : $"[SP] {n}: {V(t.position)}");
            }

            var lamp = all.FirstOrDefault(x => x.name == "Lamp_Stores");
            if (lamp != null) Debug.Log($"[SP] Lamp_Stores (exterior practical): {V(lamp.position)}");

            Debug.Log("[SP] ===== neighbours / clearance =====");
            foreach (var n in new[] { "House_Body", "Shed_Body", "Work_Body", "InnerGate_Leaf" })
            {
                var t = all.FirstOrDefault(x => x.name == n);
                if (t == null) continue;
                var b = t.GetComponent<Renderer>().bounds;
                float gap = Mathf.Sqrt(whole.SqrDistance(b.ClosestPoint(whole.center)));
                Debug.Log($"[SP] {n}: centre {V(t.position)} size {V(b.size)} — nearest gap to Stores {gap:0.00} m");
            }

            // Player metrics, so door and interior clearances can be authored correctly.
            var pc = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None).FirstOrDefault();
            if (pc != null)
                Debug.Log($"[SP] player capsule: radius {pc.radius:0.00} height {pc.height:0.00} " +
                          $"stepOffset {pc.stepOffset:0.00} slopeLimit {pc.slopeLimit:0.0}");
        }

        static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
