using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>Reports the current state of the east terrace face: which blockout
    /// renderers are off, and which art prototypes are still sitting in the scene.</summary>
    public static class TerraceEastStateAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";

        [MenuItem("Tools/Last Beacon/Audit Terrace East State")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                         FindObjectsSortMode.None);

            Debug.Log("[TE] --- blockout geometry on the east terrace ---");
            foreach (var n in new[] { "Rock_TerraceEast", "Cliff_TerraceEastFace_Battered",
                                      "Rock_TerraceNorth", "Terrace_Deck" })
            {
                var t = all.FirstOrDefault(x => x.name == n);
                if (t == null) { Debug.Log($"[TE] {n}: NOT PRESENT"); continue; }
                var r = t.GetComponent<MeshRenderer>();
                var c = t.GetComponent<Collider>();
                var b = r != null ? r.bounds : new Bounds();
                Debug.Log($"[TE] {n}: active={t.gameObject.activeInHierarchy} " +
                          $"renderer={(r == null ? "none" : r.enabled.ToString())} " +
                          $"collider={(c == null ? "none" : c.enabled.ToString())} " +
                          $"pos={t.position} size={(r == null ? Vector3.zero : b.size)}");
            }

            Debug.Log("[TE] --- art prototypes present ---");
            var root = all.FirstOrDefault(x => x.name == "LB_ArtProto");
            if (root == null) Debug.Log("[TE] LB_ArtProto: NOT PRESENT");
            else
                foreach (Transform child in root)
                {
                    var mf = child.GetComponentInChildren<MeshFilter>();
                    var r = child.GetComponentInChildren<MeshRenderer>();
                    Debug.Log($"[TE] proto '{child.name}': pos={child.position} " +
                              $"rot={child.rotation.eulerAngles} " +
                              $"tris={(mf == null ? 0 : mf.sharedMesh.triangles.Length / 3)} " +
                              $"mat={(r == null ? "none" : r.sharedMaterial?.name)} " +
                              $"size={(r == null ? Vector3.zero : r.bounds.size)}");
                }

            Debug.Log("[TE] --- any other disabled MeshRenderers in the blockout ---");
            int off = 0;
            foreach (var t in all)
            {
                var r = t.GetComponent<MeshRenderer>();
                if (r != null && !r.enabled) { Debug.Log($"[TE] renderer OFF: {t.name}"); off++; }
            }
            Debug.Log($"[TE] total disabled renderers: {off}");
        }
    }
}
