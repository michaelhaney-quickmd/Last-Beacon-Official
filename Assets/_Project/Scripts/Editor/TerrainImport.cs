using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Imports the broad island heightmap as a Unity Terrain beneath the approved
    /// ProBuilder blockout. The blockout stays authoritative for every gameplay
    /// surface; this is landmass, shoreline and background silhouette only.
    ///
    /// The terrain lives under its own root, so regenerating the blockout does not
    /// touch it and this import does not touch the blockout.
    /// </summary>
    public static class TerrainImport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string RawPath = "Assets/_Project/Art/Terrain/Last_Beacon_Heightmap_513_RAW16_LE.raw";
        const string DataPath = "Assets/_Project/Art/Terrain/LB_IslandTerrain_Heightmap.asset";
        const string RootName = "LB_Terrain";
        const string TerrainName = "LB_IslandTerrain_Heightmap";

        // Approved import settings. Not to be second-guessed.
        const int Resolution = 513;
        const float SizeX = 160f, SizeZ = 160f, Height = 40f;
        static readonly Vector3 WorldPosition = new Vector3(-82f, -12f, -82f);

        /// <summary>
        /// How the file's rows and columns map onto the terrain's Z and X. Set from
        /// the landmark diagnostic below — never guessed.
        /// </summary>
        public enum Orientation { RowZ_ColX, RowZflip_ColX, RowZ_ColXflip, RowZflip_ColXflip }
        public static Orientation Mapping = Orientation.RowZ_ColX;

        [MenuItem("Tools/Last Beacon/Terrain/Import Heightmap")]
        public static void Import()
        {
            var raw = File.ReadAllBytes(RawPath);
            int expected = Resolution * Resolution * 2;
            if (raw.Length != expected)
            {
                Debug.LogError($"[Terrain] {RawPath} is {raw.Length} bytes, expected {expected}.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // heights[z, x], 0..1
            var heights = new float[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
            {
                int row = z, col = x;
                switch (Mapping)
                {
                    case Orientation.RowZflip_ColX: row = Resolution - 1 - z; break;
                    case Orientation.RowZ_ColXflip: col = Resolution - 1 - x; break;
                    case Orientation.RowZflip_ColXflip:
                        row = Resolution - 1 - z; col = Resolution - 1 - x; break;
                }

                int i = (row * Resolution + col) * 2;
                heights[z, x] = (raw[i] | (raw[i + 1] << 8)) / 65535f;   // little endian
            }

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
            if (data == null)
            {
                data = new TerrainData();
                Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
                AssetDatabase.CreateAsset(data, DataPath);
            }

            data.heightmapResolution = Resolution;
            data.size = new Vector3(SizeX, Height, SizeZ);
            data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);

            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            var existing = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == TerrainName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = TerrainName;
            go.transform.SetParent(root.transform, false);
            go.transform.position = WorldPosition;

            var terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            go.GetComponent<TerrainCollider>().terrainData = data;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Terrain] Imported {TerrainName} at {WorldPosition}, size {data.size}, " +
                      $"resolution {Resolution}, mapping {Mapping}");
        }
    }
}
