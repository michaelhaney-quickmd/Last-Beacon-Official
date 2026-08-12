using System.IO;
using LastBeacon.Blockout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Phase 1 — generates the first-pass ProBuilder blockout of the vertical-slice
    /// compound (GDD Sections 6-8, 36, 37; workflow doc Phase 1).
    ///
    /// This produces a starting point to be adjusted by hand in the editor, not a
    /// finished layout. Everything it makes is a real ProBuilder mesh, so faces can
    /// be pushed around normally. Re-running it overwrites the scene wholesale —
    /// once you start hand-editing, stop re-running it.
    ///
    /// Layout intent (GDD Section 7):
    ///   - compound footprint 56 x 56 m, crossable in ~12 s at 4.5 m/s
    ///   - courtyard kept clear in the middle so players can see each other
    ///   - lighthouse visible from every exterior point including the dock
    ///   - dock ~17 s from courtyard centre
    /// </summary>
    public static class CompoundBlockoutGenerator
    {
        const string ScenePath = "Assets/_Project/Scenes/Compound_Blockout.unity";
        const string MaterialFolder = "Assets/_Project/Art/Materials/Blockout";

        // --- Layout constants. Adjust these rather than the geometry calls below. ---
        const float CompoundHalfExtent = 28f;   // 56 m across (GDD: 50-70 m)
        const float WallHeight = 2.6f;
        const float WallThickness = 0.6f;
        const float GateWidth = 6f;
        const float GroundY = 0f;

        static Material _rock, _concrete, _wood, _metal, _plank, _ground, _water;

        [MenuItem("Last Beacon/Blockout/Generate Compound Blockout")]
        public static void Generate()
        {
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("--- COMPOUND BLOCKOUT ---").transform;
            var terrain = NewGroup("Terrain", root);
            var compound = NewGroup("Compound", root);
            var lighthouse = NewGroup("Lighthouse", root);
            var buildings = NewGroup("Buildings", root);
            var dock = NewGroup("Dock", root);
            var markers = NewGroup("Markers", root);

            BuildTerrain(terrain);
            BuildPerimeter(compound);
            BuildLighthouse(lighthouse);
            BuildBuildings(buildings);
            BuildDock(dock);
            BuildMarkers(markers);
            BuildLighting(root);
            BuildPlayer(root);

            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterSceneInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Last Beacon] Compound blockout generated at {ScenePath}");
        }

        // ------------------------------------------------------------------ terrain

        static void BuildTerrain(Transform parent)
        {
            // Island slab. Top face sits at y = 0.
            Cube("Island", parent, new Vector3(0f, -2f, -4f), new Vector3(104f, 4f, 116f), _ground);

            // Chunky faceted rock mass around the compound edge (GDD Section 4:
            // broad faceted rocks, not photoreal microdetail).
            Cube("Rock_NW", parent, new Vector3(-38f, 1.5f, 34f), new Vector3(26f, 7f, 18f), _rock);
            Cube("Rock_N", parent, new Vector3(-2f, 1f, 42f), new Vector3(52f, 6f, 14f), _rock);
            Cube("Rock_NE", parent, new Vector3(36f, 1.5f, 32f), new Vector3(24f, 7f, 20f), _rock);
            Cube("Rock_E", parent, new Vector3(42f, 0.5f, 2f), new Vector3(16f, 5f, 44f), _rock);
            Cube("Rock_W", parent, new Vector3(-42f, 0.5f, 0f), new Vector3(16f, 5f, 48f), _rock);
            Cube("Rock_SW", parent, new Vector3(-34f, 0f, -34f), new Vector3(24f, 4f, 22f), _rock);
            Cube("Rock_SE", parent, new Vector3(34f, 0f, -34f), new Vector3(24f, 4f, 22f), _rock);

            // Lighthouse sits on a raised rock shelf so it reads from the dock.
            Cube("Rock_LighthouseShelf", parent, new Vector3(-16f, 0.4f, 18f), new Vector3(18f, 1.6f, 18f), _rock);

            // Water. Flat plane, no simulation (GDD Section 39: no dynamic tides).
            var water = ShapeGenerator.GeneratePlane(PivotLocation.Center, 240f, 240f, 0, 0, Axis.Up);
            water.gameObject.name = "Water";
            water.transform.SetParent(parent, false);
            water.transform.position = new Vector3(0f, -1.6f, 0f);
            Finish(water, _water, addCollider: false, isStatic: true);
        }

        // ---------------------------------------------------------------- perimeter

        static void BuildPerimeter(Transform parent)
        {
            const float e = CompoundHalfExtent;
            const float h = WallHeight;
            const float t = WallThickness;

            // North, east and west runs are solid. South run has the main gate gap.
            Cube("Wall_N", parent, new Vector3(0f, h / 2f, e), new Vector3(e * 2f + t, h, t), _concrete);
            Cube("Wall_E", parent, new Vector3(e, h / 2f, 0f), new Vector3(t, h, e * 2f), _concrete);
            Cube("Wall_W", parent, new Vector3(-e, h / 2f, 0f), new Vector3(t, h, e * 2f), _concrete);

            float sideRun = (e * 2f - GateWidth) / 2f;
            float sideCentre = GateWidth / 2f + sideRun / 2f;
            Cube("Wall_S_West", parent, new Vector3(-sideCentre, h / 2f, -e), new Vector3(sideRun, h, t), _concrete);
            Cube("Wall_S_East", parent, new Vector3(sideCentre, h / 2f, -e), new Vector3(sideRun, h, t), _concrete);

            // Main gate: two posts and a lintel. The gate leaf itself is a separate
            // object so it can be animated/damaged later (GDD Section 35).
            var gate = NewGroup("MainGate", parent);
            Cube("GatePost_W", gate, new Vector3(-GateWidth / 2f - 0.4f, 1.8f, -e), new Vector3(0.8f, 3.6f, 1.0f), _concrete);
            Cube("GatePost_E", gate, new Vector3(GateWidth / 2f + 0.4f, 1.8f, -e), new Vector3(0.8f, 3.6f, 1.0f), _concrete);
            Cube("GateLintel", gate, new Vector3(0f, 3.8f, -e), new Vector3(GateWidth + 1.6f, 0.4f, 0.8f), _metal);
            Cube("GateLeaf", gate, new Vector3(0f, 1.3f, -e), new Vector3(GateWidth - 0.2f, 2.6f, 0.2f), _metal);

            // Fence run that Phase 3's "repair fence" task will target.
            var fence = NewGroup("Fence_Repairable", parent);
            for (int i = 0; i < 5; i++)
            {
                float x = -e + 4f + i * 4f;
                Cube($"FencePost_{i}", fence, new Vector3(x, 1.1f, -e + 0.9f), new Vector3(0.25f, 2.2f, 0.25f), _wood);
            }
        }

        // ---------------------------------------------------------------- lighthouse

        static void BuildLighthouse(Transform parent)
        {
            // Three functional layers (GDD Section 8) stacked as simple volumes.
            // Ground/Operations -> Mechanical -> Lantern Room.
            var pos = new Vector3(-16f, 0f, 18f);
            float y = 0.8f; // top of the rock shelf

            Cylinder("Lighthouse_Plinth", parent, pos + Vector3.up * (y + 0.5f), 5.2f, 1.0f, _rock);
            y += 1.0f;

            Cylinder("Lighthouse_L1_Operations", parent, pos + Vector3.up * (y + 2.4f), 4.2f, 4.8f, _concrete);
            y += 4.8f;

            Cylinder("Lighthouse_Shaft", parent, pos + Vector3.up * (y + 3.0f), 3.6f, 6.0f, _concrete);
            y += 6.0f;

            Cylinder("Lighthouse_L2_Mechanical", parent, pos + Vector3.up * (y + 2.0f), 3.8f, 4.0f, _concrete);
            y += 4.0f;

            Cube("Lighthouse_Balcony", parent, pos + Vector3.up * (y + 0.1f), new Vector3(9.4f, 0.3f, 9.4f), _metal);

            Cylinder("Lighthouse_L3_LanternRoom", parent, pos + Vector3.up * (y + 1.9f), 3.0f, 3.6f, _metal);
            y += 3.6f;

            Cylinder("Lighthouse_Cap", parent, pos + Vector3.up * (y + 0.7f), 3.2f, 1.4f, _metal);

            // ~21 m total. Tall enough to stay visible from the dock (GDD Section 36).
        }

        // ----------------------------------------------------------------- buildings

        static void BuildBuildings(Transform parent)
        {
            // Keeper's House — exterior only for the vertical slice (GDD Section 37).
            var house = NewGroup("KeepersHouse", parent);
            Building("House_Body", house, new Vector2(13f, 13f), new Vector2(13f, 9f), 5.5f, _wood);
            Roof("House_Roof", house, new Vector2(13f, 13f), new Vector2(13.6f, 9.6f), 5.5f, 2.6f, _plank);
            Cube("House_Door", house, new Vector3(13f, 1.1f, 8.4f), new Vector3(1.2f, 2.2f, 0.3f), _plank);
            // Boardable windows (GDD Section 27: "boards Keeper's House window").
            Cube("House_Window_W", house, new Vector3(8.5f, 2.0f, 8.4f), new Vector3(1.4f, 1.4f, 0.3f), _metal);
            Cube("House_Window_E", house, new Vector3(17.5f, 2.0f, 8.4f), new Vector3(1.4f, 1.4f, 0.3f), _metal);

            // Generator shed — separate shed, not a deep basement (GDD Section 8).
            var shed = NewGroup("GeneratorShed", parent);
            Building("Shed_Body", shed, new Vector2(15f, -6f), new Vector2(8f, 6f), 4.0f, _concrete);
            Roof("Shed_Roof", shed, new Vector2(15f, -6f), new Vector2(8.6f, 6.6f), 4.0f, 1.4f, _metal);
            Cube("Shed_Door", shed, new Vector3(11.0f, 1.1f, -6f), new Vector3(0.3f, 2.2f, 1.4f), _metal);
            // Oversized, readable generator (GDD Section 23).
            Cube("Generator_Body", shed, new Vector3(15.5f, 0.9f, -6f), new Vector3(3.2f, 1.8f, 2.0f), _metal);
            Cube("Generator_FuelCap", shed, new Vector3(14.3f, 1.95f, -6f), new Vector3(0.7f, 0.3f, 0.7f), _plank);
            Cube("Generator_Gauge", shed, new Vector3(16.9f, 1.4f, -5.1f), new Vector3(0.5f, 0.5f, 0.15f), _plank);

            // Workshop.
            var workshop = NewGroup("Workshop", parent);
            Building("Workshop_Body", workshop, new Vector2(-13f, -12f), new Vector2(11f, 8f), 4.5f, _wood);
            Roof("Workshop_Roof", workshop, new Vector2(-13f, -12f), new Vector2(11.6f, 8.6f), 4.5f, 1.8f, _metal);
            Cube("Workshop_Door", workshop, new Vector3(-13f, 1.1f, -7.7f), new Vector3(1.4f, 2.2f, 0.3f), _plank);
            Cube("Workshop_Bench", workshop, new Vector3(-16f, 0.5f, -14.5f), new Vector3(4f, 1.0f, 1.2f), _plank);

            // Electrical / control station — switchboard, fuse cabinet (GDD Section 7).
            var electrical = NewGroup("ElectricalStation", parent);
            Building("Electrical_Body", electrical, new Vector2(-4f, 12f), new Vector2(5f, 4f), 3.5f, _concrete);
            Roof("Electrical_Roof", electrical, new Vector2(-4f, 12f), new Vector2(5.4f, 4.4f), 3.5f, 1.0f, _metal);
            Cube("Switchboard", electrical, new Vector3(-4f, 1.6f, 9.9f), new Vector3(2.6f, 2.0f, 0.35f), _metal);

            // Storage — where restocked supplies land (GDD Section 11).
            var storage = NewGroup("Storage", parent);
            Building("Storage_Body", storage, new Vector2(3f, -17f), new Vector2(8f, 6f), 4.0f, _wood);
            Roof("Storage_Roof", storage, new Vector2(3f, -17f), new Vector2(8.6f, 6.6f), 4.0f, 1.4f, _metal);
            Cube("Storage_Door", storage, new Vector3(3f, 1.1f, -13.9f), new Vector3(1.6f, 2.2f, 0.3f), _plank);
            Cube("Shelf_Fuel", storage, new Vector3(0.5f, 0.8f, -19f), new Vector3(2.0f, 1.6f, 0.8f), _plank);
            Cube("Cabinet_Ammunition", storage, new Vector3(5.5f, 0.8f, -19f), new Vector3(2.0f, 1.6f, 0.8f), _metal);
        }

        // ---------------------------------------------------------------------- dock

        static void BuildDock(Transform parent)
        {
            // Path from the gate down to the water, then a jetty over it.
            Cube("Path_ToDock", parent, new Vector3(0f, 0.05f, -43f), new Vector3(5f, 0.2f, 32f), _ground);

            var jetty = NewGroup("Jetty", parent);
            Cube("Jetty_Deck", jetty, new Vector3(0f, 0.2f, -70f), new Vector3(5f, 0.4f, 26f), _plank);
            for (int i = 0; i < 6; i++)
            {
                float z = -59f - i * 4.4f;
                Cube($"Piling_W_{i}", jetty, new Vector3(-2.2f, -0.9f, z), new Vector3(0.4f, 2.4f, 0.4f), _wood);
                Cube($"Piling_E_{i}", jetty, new Vector3(2.2f, -0.9f, z), new Vector3(0.4f, 2.4f, 0.4f), _wood);
            }
            Cube("Dock_Lamp_Post", jetty, new Vector3(-2.4f, 1.8f, -66f), new Vector3(0.2f, 3.2f, 0.2f), _metal);
            Cube("Dock_CrateDropoff", jetty, new Vector3(1.4f, 0.85f, -74f), new Vector3(1.6f, 0.9f, 1.6f), _plank);
        }

        // ------------------------------------------------------------------- markers

        static void BuildMarkers(Transform parent)
        {
            Marker(parent, "Courtyard_Centre", new Vector3(0f, 0.2f, 0f),
                BlockoutMarker.MarkerKind.Landmark,
                "Keep clear. Players should see each other across this space.");

            Marker(parent, "Spawn_Player", new Vector3(0f, 0.2f, -22f),
                BlockoutMarker.MarkerKind.SpawnPoint, "Blockout walk start.");

            Marker(parent, "Entrance_MainGate", new Vector3(0f, 0.2f, -28f),
                BlockoutMarker.MarkerKind.Entrance, "Inspection events arrive here.");

            Marker(parent, "Entrance_Lighthouse", new Vector3(-16f, 0.9f, 13.5f),
                BlockoutMarker.MarkerKind.Entrance, "To Operations floor.");

            Marker(parent, "Task_FuelGenerator", new Vector3(14.3f, 2.2f, -6f),
                BlockoutMarker.MarkerKind.TaskStation, "Phase 3 - fuel generator.");

            Marker(parent, "Task_ReplaceFuse", new Vector3(-4f, 1.8f, 9.7f),
                BlockoutMarker.MarkerKind.TaskStation, "Phase 3 - switchboard fuse.");

            Marker(parent, "Task_RepairFence", new Vector3(-18f, 1.2f, -27.1f),
                BlockoutMarker.MarkerKind.TaskStation, "Phase 3 - repair fence.");

            Marker(parent, "Task_RestockAmmunition", new Vector3(5.5f, 1.8f, -19f),
                BlockoutMarker.MarkerKind.TaskStation, "Phase 3 - ammunition cabinet.");

            Marker(parent, "Task_DockDelivery", new Vector3(1.4f, 1.9f, -74f),
                BlockoutMarker.MarkerKind.TaskStation, "Supply drop-off. Carry loop starts here.");

            Marker(parent, "Defense_GateSocket", new Vector3(0f, 0.2f, -25f),
                BlockoutMarker.MarkerKind.DefenseSocket, "Phase 6 - barricade / shock trap.");

            Marker(parent, "Defense_PathSocket", new Vector3(0f, 0.2f, -36f),
                BlockoutMarker.MarkerKind.DefenseSocket, "Phase 6 - dock path approach.");
        }

        static void Marker(Transform parent, string name, Vector3 position,
            BlockoutMarker.MarkerKind kind, string note)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var marker = go.AddComponent<BlockoutMarker>();

            var so = new SerializedObject(marker);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("note").stringValue = note;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ lighting

        static void BuildLighting(Transform parent)
        {
            var group = NewGroup("Lighting", parent);

            // Cold blue-grey night ambient (GDD Section 4).
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.13f, 0.19f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.09f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.07f);
            RenderSettings.skybox = null;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.09f, 0.11f, 0.15f);
            // Light enough to still read the compound from the dock ~74 m out.
            RenderSettings.fogDensity = 0.008f;

            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(group, false);
            moonGo.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.66f, 0.88f);
            // Temporary blockout lighting: bright enough that the space reads at
            // night (workflow Phase 1 step 4). Pull this down once real art lands.
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.sun = moon;

            // Warm practical lamps (GDD Section 4).
            WarmLamp(group, "Lamp_Courtyard", new Vector3(0f, 6f, 0f), 30f, 9f);
            WarmLamp(group, "Lamp_GeneratorShed", new Vector3(15f, 3.6f, -6f), 16f, 6f);
            WarmLamp(group, "Lamp_Workshop", new Vector3(-13f, 4.0f, -12f), 16f, 6f);
            WarmLamp(group, "Lamp_KeepersHouse", new Vector3(13f, 4.4f, 8f), 16f, 5.5f);
            WarmLamp(group, "Lamp_Gate", new Vector3(0f, 3.8f, -27f), 18f, 7f);
            WarmLamp(group, "Lamp_Storage", new Vector3(3f, 4.0f, -14f), 14f, 5f);
            WarmLamp(group, "Lamp_Electrical", new Vector3(-4f, 3.8f, 10f), 14f, 5f);
            WarmLamp(group, "Lamp_Dock", new Vector3(-2.4f, 3.6f, -66f), 20f, 6f);

            // Placeholder rotating beacon (workflow Phase 1 step 4).
            var pivot = new GameObject("Beacon_Pivot");
            pivot.transform.SetParent(group, false);
            pivot.transform.position = new Vector3(-16f, 18.9f, 18f);
            pivot.AddComponent<BlockoutBeaconSpinner>();

            var beamGo = new GameObject("Beacon_Beam");
            beamGo.transform.SetParent(pivot.transform, false);
            beamGo.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.94f, 0.80f);
            beam.intensity = 240f;
            beam.range = 160f;
            beam.spotAngle = 26f;
            beam.innerSpotAngle = 12f;
            beam.shadows = LightShadows.Soft;
        }

        static void WarmLamp(Transform parent, string name, Vector3 position, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.48f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // -------------------------------------------------------------------- player

        static void BuildPlayer(Transform parent)
        {
            var go = new GameObject("BlockoutPlayer (PLACEHOLDER - delete in Phase 2)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 1.2f, -22f);

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            camGo.AddComponent<AudioListener>();

            go.AddComponent<BlockoutWalker>();
        }

        // ------------------------------------------------------------------- helpers

        static Transform NewGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static ProBuilderMesh Cube(string name, Transform parent, Vector3 centre, Vector3 size, Material material)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = centre;
            Finish(pb, material);
            return pb;
        }

        static ProBuilderMesh Cylinder(string name, Transform parent, Vector3 centre, float radius, float height, Material material)
        {
            var pb = ShapeGenerator.GenerateCylinder(PivotLocation.Center, 16, radius, height, 0);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = centre;
            Finish(pb, material);
            return pb;
        }

        /// <summary>Footprint-and-height helper: base sits on the ground plane.</summary>
        static ProBuilderMesh Building(string name, Transform parent, Vector2 centreXZ, Vector2 footprint, float height, Material material)
        {
            return Cube(name, parent,
                new Vector3(centreXZ.x, GroundY + height / 2f, centreXZ.y),
                new Vector3(footprint.x, height, footprint.y),
                material);
        }

        static ProBuilderMesh Roof(string name, Transform parent, Vector2 centreXZ, Vector2 footprint, float wallHeight, float roofHeight, Material material)
        {
            var pb = ShapeGenerator.GeneratePrism(PivotLocation.Center,
                new Vector3(footprint.x, roofHeight, footprint.y));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(centreXZ.x, GroundY + wallHeight + roofHeight / 2f, centreXZ.y);
            Finish(pb, material);
            return pb;
        }

        static void Finish(ProBuilderMesh pb, Material material, bool addCollider = true, bool isStatic = true)
        {
            pb.ToMesh();
            pb.Refresh();

            var renderer = pb.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            if (addCollider)
                pb.gameObject.AddComponent<MeshCollider>();

            if (isStatic)
                GameObjectUtility.SetStaticEditorFlags(pb.gameObject, StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
        }

        // ----------------------------------------------------------------- materials

        static void CreateMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            _ground = MakeMaterial("Blockout_Ground", new Color(0.19f, 0.20f, 0.18f), 0.95f);
            _rock = MakeMaterial("Blockout_Rock", new Color(0.24f, 0.25f, 0.27f), 0.85f);
            _concrete = MakeMaterial("Blockout_Concrete", new Color(0.42f, 0.42f, 0.41f), 0.80f);
            _wood = MakeMaterial("Blockout_Wood", new Color(0.34f, 0.26f, 0.19f), 0.85f);
            _plank = MakeMaterial("Blockout_Plank", new Color(0.45f, 0.35f, 0.24f), 0.80f);
            _metal = MakeMaterial("Blockout_Metal", new Color(0.30f, 0.32f, 0.34f), 0.45f, 0.7f);
            _water = MakeMaterial("Blockout_Water", new Color(0.05f, 0.09f, 0.12f), 0.10f, 0.2f);
            AssetDatabase.SaveAssets();
        }

        static Material MakeMaterial(string name, Color colour, float smoothnessInverted, float metallic = 0f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 1f - smoothnessInverted);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void RegisterSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == ScenePath)
                    return;
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
