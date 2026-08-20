using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// TEMPORARY exterior art-direction pass. Broad flat colour only — no textures,
    /// no noise, no microdetail. Assigns materials by object name, sets a cold
    /// exterior mood with warm practicals, and renders review stills.
    ///
    /// Geometry is never touched. Re-running Generate reverts the whole pass.
    /// </summary>
    public static class ExteriorArtPass
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string Folder = "Assets/_Project/Art/Materials/ArtPass";
        const int W = 1600, H = 900;

        static Material _terrainGround, _rock, _path, _wet, _moss, _ocean, _dockPlank, _dockWet;

        [MenuItem("Tools/Last Beacon/Exterior Art Pass (temporary)")]
        public static void Run()
        {
            string output = GetArg("-artOutput") ?? Path.Combine(Path.GetTempPath(), "lb-artpass");
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(Folder);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            MakePalette();
            AssignMaterials();
            SetAtmosphere();
            Capture(output);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Art] pass applied and scene saved. Re-run Generate to revert.");
        }

        // ------------------------------------------------------------------ palette

        static void MakePalette()
        {
            // Subdued and close in value. Cliffs sit darkest and coolest, paths read
            // lightest so circulation stays legible, wet is darker and a little
            // smoother, moss is barely green.
            _rock         = Mat("Art_Rock_Cliff",     new Color(0.150f, 0.170f, 0.200f), 0.04f);
            _terrainGround= Mat("Art_Ground_Terrain", new Color(0.255f, 0.240f, 0.215f), 0.03f);
            _path         = Mat("Art_Path_Compacted", new Color(0.330f, 0.315f, 0.285f), 0.05f);
            _wet          = Mat("Art_Wet_Dark",       new Color(0.095f, 0.110f, 0.125f), 0.24f);
            _moss         = Mat("Art_Moss_Accent",    new Color(0.175f, 0.205f, 0.155f), 0.03f);
            _ocean        = Mat("Art_Ocean_Temp",     new Color(0.030f, 0.055f, 0.080f), 0.52f, 0.05f);
            // Dock timber was reading as the brightest thing on the island at 0.45.
            // Pulled down and given a little sheen so the tidal zone feels wet and
            // heavy. The cliffs are untouched; only the planks move.
            _dockPlank    = Mat("Art_Plank_Dock",     new Color(0.285f, 0.235f, 0.180f), 0.12f);
            // The two dock aprons only. Darker than the inland path so the waterline
            // settles into the tidal zone, but well short of Art_Wet_Dark in both
            // value and sheen, and still legible as somewhere you walk.
            _dockWet      = Mat("Art_Path_DockWet",   new Color(0.225f, 0.215f, 0.200f), 0.15f);
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name, Color c, float smoothness, float metallic = 0f)
        {
            string path = $"{Folder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        // --------------------------------------------------------------- assignment

        static void AssignMaterials()
        {
            int rock = 0, path = 0, wet = 0, moss = 0, dock = 0, dockWet = 0;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                string n = r.name;
                Material pick = null;

                // The dock aprons take the wet-path variant before the general
                // walkable rule can claim them; every inland path is untouched.
                if (n == "Dock_Apron" || n == "Dock_SupplyApron")
                {
                    pick = _dockWet; dockWet++;
                }
                // Walkable surfaces first, so a path never inherits the cliff colour.
                else if (n.StartsWith("Path_") || n.StartsWith("Stair_") || n.StartsWith("Shelf_") ||
                    n == "MainYard" || n == "Terrace_Deck" || n == "Terrace_Throat" ||
                    n == "Ascent_Landing" || n == "Ascent_StairTopPad" ||
                    n == "Dock_Apron" || n == "Dock_SupplyApron" || n.StartsWith("Yard_Kerb") ||
                    n == "Yard_UtilityCornerNW" || n == "Yard_ServiceStripSE")
                {
                    pick = _path; path++;
                }
                else if (n.StartsWith("Cliff_") || n.StartsWith("Rock_"))
                {
                    // Tidal zone reads wet; high rims and the knoll take the moss accent.
                    float top = r.bounds.max.y;
                    if (top <= 3f) { pick = _wet; wet++; }
                    else if (n == "Rock_Rim_West" || n == "Rock_Rim_East" ||
                             n == "Cliff_BandD_Knoll" || n == "Rock_Outcrop_N")
                    { pick = _moss; moss++; }
                    else { pick = _rock; rock++; }
                }
                else if (n.StartsWith("Dock_"))
                {
                    // Reached only after the path rule, so the aprons keep their
                    // walkable-surface value and stay readable.
                    pick = _dockPlank; dock++;
                }
                else if (n == "Lighthouse_Plinth")
                {
                    pick = _rock; rock++;
                }
                else if (n == "Sea")
                {
                    pick = _ocean;
                }

                if (pick != null)
                    r.sharedMaterial = pick;
            }

            Debug.Log($"[Art] assigned — rock {rock}, path {path}, wet {wet}, moss {moss}, dock {dock}, dockApron {dockWet}");

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault();
            if (terrain != null)
            {
                string tp = $"{Folder}/Art_Terrain_Surface.mat";
                var tm = AssetDatabase.LoadAssetAtPath<Material>(tp);
                if (tm == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                    tm = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit"))
                        { name = "Art_Terrain_Surface" };
                    AssetDatabase.CreateAsset(tm, tp);
                }
                tm.SetColor("_BaseColor", new Color(0.225f, 0.225f, 0.215f));
                EditorUtility.SetDirty(tm);
                terrain.materialTemplate = tm;

                // A URP terrain with no TerrainLayers renders as a checkerboard no
                // matter what _BaseColor says: terrain colour comes from a layer's
                // diffuse texture, not from the material's base colour.
                {
                    // Always rewritten: the layer's diffuse is what is actually seen,
                    // so a colour change here has to reach the texture, not just the
                    // material's base colour.
                    string texPath = $"{Folder}/Art_TerrainBase.png";
                    var flat = new Texture2D(16, 16);
                    var px = Enumerable.Repeat(new Color(0.225f, 0.225f, 0.215f), 16 * 16).ToArray();
                    flat.SetPixels(px);
                    flat.Apply();
                    File.WriteAllBytes(texPath, flat.EncodeToPNG());
                    Object.DestroyImmediate(flat);
                    AssetDatabase.ImportAsset(texPath);

                    string layerPath = $"{Folder}/Art_TerrainLayer.terrainlayer";
                    var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                    if (layer == null)
                    {
                        layer = new TerrainLayer();
                        AssetDatabase.CreateAsset(layer, layerPath);
                    }
                    layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    layer.tileSize = new Vector2(16f, 16f);
                    layer.specular = Color.black;
                    layer.metallic = 0f;
                    layer.smoothness = 0.02f;
                    EditorUtility.SetDirty(layer);

                    terrain.terrainData.terrainLayers = new[] { layer };
                    EditorUtility.SetDirty(terrain.terrainData);
                    Debug.Log("[Art] terrain layer created — checkerboard was 0 layers");
                }
                Debug.Log($"[Art] terrain material assigned, layers={terrain.terrainData.terrainLayers.Length}");
            }
        }

        // --------------------------------------------------------------- atmosphere

        static void SetAtmosphere()
        {
            // Ambient pulled DOWN and the key pushed UP. High ambient plus a weak
            // directional is what flattens faceted rock: every plane receives the
            // same light regardless of which way it points, so the facets vanish.
            // The reference sheet is key-dominant, and that is what separates its
            // planes into distinct values.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.055f, 0.070f, 0.100f);
            RenderSettings.ambientEquatorColor = new Color(0.038f, 0.048f, 0.066f);
            RenderSettings.ambientGroundColor = new Color(0.020f, 0.025f, 0.034f);
            RenderSettings.skybox = null;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.075f, 0.095f, 0.130f);
            RenderSettings.fogDensity = 0.0065f;

            // --- key light: rake the west-facing cliffs -------------------------
            // The terrace east wall faces WEST, so the key has to travel eastward
            // to strike it. Elevated ~30 degrees and swung slightly south so
            // south-west facets read brighter than north-west ones.
            var moon = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Moonlight");
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.Euler(30f, 105f, 0f);
                moon.intensity = 1.15f;
                moon.color = new Color(0.62f, 0.72f, 0.92f);
                moon.shadows = LightShadows.Soft;
                moon.shadowStrength = 0.85f;
                Debug.Log($"[Art] key raked to {moon.transform.rotation.eulerAngles}, intensity {moon.intensity}");
            }

            // A weak opposing fill so unlit planes read as dark rock rather than
            // pure black. Deliberately a third of the key.
            var fillGo = GameObject.Find("Fill_Cold");
            if (fillGo == null)
            {
                fillGo = new GameObject("Fill_Cold");
                if (moon != null) fillGo.transform.SetParent(moon.transform.parent, false);
            }
            // Not ??: GetComponent returns a fake-null Unity object that ?? reads as
            // non-null, so AddComponent never ran and the next line threw. Only
            // Unity's overloaded == recognises the fake null.
            var fillLight = fillGo.GetComponent<Light>();
            if (fillLight == null) fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.transform.rotation = Quaternion.Euler(18f, 292f, 0f);
            fillLight.color = new Color(0.42f, 0.52f, 0.72f);
            fillLight.intensity = 0.38f;
            fillLight.shadows = LightShadows.None;

            // Warm practicals on colour temperature rather than a hand-picked tint.
            int lamps = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point) continue;
                l.useColorTemperature = true;
                l.colorTemperature = l.name.Contains("Gate") || l.name.Contains("Dock") ? 2700f : 2400f;
                l.color = Color.white;
                lamps++;
            }
            Debug.Log($"[Art] {lamps} practicals set to 2400-2700K");

            var beam = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Beacon_Beam");
            if (beam != null)
            {
                beam.useColorTemperature = true;
                beam.colorTemperature = 3200f;
                beam.color = Color.white;
                beam.intensity = 420f;
                beam.spotAngle = 22f;
                beam.innerSpotAngle = 9f;

                // A visible shaft: URP has no volumetrics, so the beam gets a thin
                // unlit cone. No collider, and it is not gameplay geometry.
                var existing = GameObject.Find("Lighthouse_BeamShaft");
                if (existing != null) Object.DestroyImmediate(existing);

                var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                // Named as lighthouse apparatus: it is the beam, not a structure that
                // competes with the tower for height.
                cone.name = "Lighthouse_BeamShaft";
                Object.DestroyImmediate(cone.GetComponent<Collider>());
                cone.transform.SetParent(beam.transform, false);
                cone.transform.localPosition = new Vector3(0f, 0f, 55f);
                cone.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cone.transform.localScale = new Vector3(9f, 55f, 9f);

                string bp = $"{Folder}/Art_BeaconBeam.mat";
                var bm = AssetDatabase.LoadAssetAtPath<Material>(bp);
                if (bm == null)
                {
                    bm = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Art_BeaconBeam" };
                    AssetDatabase.CreateAsset(bm, bp);
                }
                bm.SetColor("_BaseColor", new Color(1f, 0.93f, 0.78f, 0.055f));
                bm.SetFloat("_Surface", 1f);           // transparent
                bm.SetFloat("_Blend", 1f);             // additive
                bm.SetFloat("_ZWrite", 0f);
                bm.renderQueue = 3000;
                bm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                EditorUtility.SetDirty(bm);
                cone.GetComponent<MeshRenderer>().sharedMaterial = bm;
                Debug.Log("[Art] beacon beam tuned and shaft added");
            }
        }

        // ----------------------------------------------------------------- captures

        static void Capture(string dir)
        {
            var shots = new (string Name, Vector3 Eye, Vector3 Look, float Fov)[]
            {
                ("Art_Dock",      new Vector3(0f, 2.1f, -47f),      new Vector3(0f, 24f, 38f), 68f),
                ("Art_MainGate",  new Vector3(15.5f, 10.7f, -17.5f), new Vector3(2f, 12f, -14f), 70f),
                ("Art_InnerGate", new Vector3(-5f, 18.7f, 4f),      new Vector3(2f, 21f, 26f), 70f),
                ("Art_Courtyard", new Vector3(0f, 18.7f, 17f),      new Vector3(0f, 27f, 38f), 70f),
                ("Art_Aerial",    new Vector3(-2f, 44f, -30f),      new Vector3(0f, 21f, 24f), 52f)
            };

            var camGo = new GameObject("__ArtCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.020f, 0.030f, 0.045f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;

            try
            {
                foreach (var s in shots)
                {
                    cam.transform.position = s.Eye;
                    cam.transform.rotation = Quaternion.LookRotation((s.Look - s.Eye).normalized, Vector3.up);
                    cam.fieldOfView = s.Fov;

                    var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    cam.targetTexture = rt;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    File.WriteAllBytes(Path.Combine(dir, s.Name + ".png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    rt.Release(); Object.DestroyImmediate(rt);
                }
            }
            finally { Object.DestroyImmediate(camGo); }
            Debug.Log($"[Art] rendered 5 stills to {dir}");
        }

        static string GetArg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
