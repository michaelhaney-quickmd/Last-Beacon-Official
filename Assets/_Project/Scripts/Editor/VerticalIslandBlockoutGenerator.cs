using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastBeacon.Blockout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Phase 1 — vertical compact lighthouse island blockout.
    ///
    /// Replaces the earlier flat-compound blockout. The island is a compact tiered
    /// rock formation: horizontally tight, vertically dramatic, with the lighthouse
    /// dominating from every working surface.
    ///
    /// NOTE ON THE GDD: Section 6 scrapped the previous multi-tier island. This
    /// blockout reinstates elevation deliberately, at compact scale (80 x 62 m of
    /// land, compound 55 m across, no switchbacks, no distant holdout zones).
    /// Treat it as a revision of Section 6, not an oversight.
    ///
    ///   TIER 0  Dock            y  0   jetty z -56..-44, apron z -44..-36
    ///   TIER 1  Lower Landing   y  5   x  -8..8    z -36..-14   16 x 22
    ///   TIER 2  Gate Terrace    y 11   x -11..11   z -14..2     22 x 16
    ///   TIER 3  Main Compound   y 17   x -27.5..27.5  z 2..32   55 x 30
    ///   TIER 4  Lighthouse Base y 21   x -12..12   z 32..44     24 x 12
    ///
    /// The lower tiers are notches cut into the south face of one rock mass rather
    /// than stacked platforms, so the mass never overhangs itself. Each cliff band
    /// is split around its notch, leaving the shelf flanked by rock walls.
    ///
    /// Everything is a separate editable ProBuilder mesh — terraces, cliff bands,
    /// stairs, ramps, retaining walls, buildings and dock are never merged.
    /// </summary>
    public static class VerticalIslandBlockoutGenerator
    {
        public const string RootName = "LB_VerticalIsland_Blockout";

        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string MaterialFolder = "Assets/_Project/Art/Materials/Blockout";

        // --- Elevation bands -----------------------------------------------------
        public const float TierDock = 0f;
        public const float TierLanding = 5f;
        public const float TierGate = 11f;
        public const float TierCompound = 17f;
        public const float TierLighthouse = 21f;

        // --- Island extents ------------------------------------------------------
        const float IslandSouth = -36f;
        const float IslandNorth = 44f;
        const float IslandHalfWidth = 31f;

        // --- Tier extents --------------------------------------------------------
        const float LandingHalfWidth = 8f;
        const float LandingNorth = -20f;
        const float GateHalfWidth = 11f;
        const float GateNorth = 2f;
        const float CompoundHalfWidth = 27.5f;
        const float CompoundNorth = 32f;
        const float KnollHalfWidth = 12f;

        const float GateOpening = 4.5f;
        public const float EyeHeight = 1.7f;

        /// <summary>Lighthouse tower centre on the Tier 4 knoll.</summary>
        public static readonly Vector2 LighthouseXZ = new Vector2(0f, 38f);

        static Material _rock, _cliff, _concrete, _wood, _metal, _plank, _ground, _water;

        [MenuItem("Tools/Last Beacon/Generate Vertical Island")]
        public static void Generate()
        {
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ClearExistingRoots();

            var root = new GameObject(RootName).transform;
            var cliffs = NewGroup("CliffMasses", root);
            var terraces = NewGroup("Terraces", root);
            var paths = NewGroup("PathsAndStairs", root);
            var walls = NewGroup("RetainingWalls", root);
            var dock = NewGroup("Tier0_Dock", root);
            var landing = NewGroup("Tier1_LowerLanding", root);
            var gate = NewGroup("Tier2_GateTerrace", root);
            var compound = NewGroup("Tier3_MainCompound", root);
            var lighthouse = NewGroup("Tier4_Lighthouse", root);
            var markers = NewGroup("Markers", root);
            var cameras = NewGroup("ReviewCameras", root);

            BuildCliffMasses(cliffs);
            BuildSea(terraces);
            BuildDock(dock);
            BuildLowerLanding(landing);
            BuildGateTerrace(gate);
            BuildCompound(compound);
            BuildLighthouse(lighthouse);
            BuildPaths(paths);
            BuildRetainingWalls(walls);
            BuildMarkers(markers);
            BuildReviewCameras(cameras);
            BuildLighting(root);
            BuildPlayer(root);

            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterSceneInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Last Beacon] Vertical island blockout generated at {ScenePath}");
        }

        /// <summary>
        /// Removes any previously generated root so re-running cannot stack two
        /// blockouts in one scene. Public so the validation tests can prove it.
        /// </summary>
        public static int ClearExistingRoots()
        {
            var existing = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t != null && t.parent == null && t.name == RootName)
                .Select(t => t.gameObject)
                .ToArray();

            foreach (var go in existing)
                Object.DestroyImmediate(go);

            return existing.Length;
        }

        // ------------------------------------------------------------ cliff masses

        static void BuildCliffMasses(Transform parent)
        {
            // Band A — sea-level plinth, full width. Its exposed top is Tier 1.
            Slab("Cliff_BandA_Base", parent,
                -IslandHalfWidth, IslandHalfWidth, IslandSouth, IslandNorth, 0f, TierLanding, _cliff);

            // Band B — y 5..11, split around the Tier 1 notch (x -8..8, z -36..-14).
            Slab("Cliff_BandB_West", parent,
                -IslandHalfWidth, -LandingHalfWidth, IslandSouth, IslandNorth, TierLanding, TierGate, _cliff);
            Slab("Cliff_BandB_East", parent,
                LandingHalfWidth, IslandHalfWidth, IslandSouth, IslandNorth, TierLanding, TierGate, _cliff);
            Slab("Cliff_BandB_Centre", parent,
                -LandingHalfWidth, LandingHalfWidth, LandingNorth, IslandNorth, TierLanding, TierGate, _cliff);

            // Band C — y 11..17, split around the Tier 2 notch (x -11..11, z -14..2).
            Slab("Cliff_BandC_West", parent,
                -IslandHalfWidth, -GateHalfWidth, LandingNorth, IslandNorth, TierGate, TierCompound, _cliff);
            Slab("Cliff_BandC_East", parent,
                GateHalfWidth, IslandHalfWidth, LandingNorth, IslandNorth, TierGate, TierCompound, _cliff);
            Slab("Cliff_BandC_Centre", parent,
                -GateHalfWidth, GateHalfWidth, GateNorth, IslandNorth, TierGate, TierCompound, _cliff);

            // Band D — the lighthouse knoll above the compound.
            Slab("Cliff_BandD_Knoll", parent,
                -KnollHalfWidth, KnollHalfWidth, CompoundNorth, IslandNorth, TierCompound, TierLighthouse, _cliff);

            // Broad faceted outcrops breaking the silhouette. Deliberately few and
            // chunky — final art replaces these forms.
            Cube("Rock_Outcrop_SW", parent, new Vector3(-26f, 6f, -30f), new Vector3(12f, 12f, 10f), _rock);
            Cube("Rock_Outcrop_SE", parent, new Vector3(26f, 5f, -28f), new Vector3(12f, 10f, 12f), _rock);
            Cube("Rock_Outcrop_W", parent, new Vector3(-33f, 9f, 6f), new Vector3(10f, 18f, 16f), _rock);
            Cube("Rock_Outcrop_E", parent, new Vector3(33f, 9f, 10f), new Vector3(10f, 18f, 18f), _rock);
            Cube("Rock_Outcrop_N", parent, new Vector3(0f, 12f, 46f), new Vector3(34f, 24f, 10f), _rock);
            Cube("Rock_SeaStack_W", parent, new Vector3(-40f, 3f, -44f), new Vector3(8f, 14f, 8f), _rock);
            Cube("Rock_SeaStack_E", parent, new Vector3(38f, 2f, -50f), new Vector3(7f, 12f, 7f), _rock);

            // Compound rim, so Tier 3 reads as enclosed without walling off views.
            Slab("Rock_Rim_West", parent, -IslandHalfWidth, -CompoundHalfWidth, GateNorth, CompoundNorth,
                TierCompound, TierCompound + 2.6f, _rock);
            Slab("Rock_Rim_East", parent, CompoundHalfWidth, IslandHalfWidth, GateNorth, CompoundNorth,
                TierCompound, TierCompound + 2.6f, _rock);
        }

        static void BuildSea(Transform parent)
        {
            var water = ShapeGenerator.GeneratePlane(PivotLocation.Center, 300f, 300f, 0, 0, Axis.Up);
            water.gameObject.name = "Sea";
            water.transform.SetParent(parent, false);
            water.transform.position = new Vector3(0f, -1.2f, 0f);
            Finish(water, _water, addCollider: false);
        }

        // ------------------------------------------------------------- tier 0 dock

        static void BuildDock(Transform parent)
        {
            // Shore apron at sea level, tucked against the island's south face.
            Slab("Dock_Apron", parent, -12f, 12f, -44f, IslandSouth, -0.4f, TierDock, _ground);

            Cube("Dock_Deck", parent, new Vector3(0f, 0.2f, -50f), new Vector3(5f, 0.4f, 12f), _plank);
            for (int i = 0; i < 6; i++)
            {
                float z = -45f - i * 2.2f;
                Cube($"Dock_Piling_W_{i}", parent, new Vector3(-2.2f, -1.2f, z), new Vector3(0.4f, 3f, 0.4f), _wood);
                Cube($"Dock_Piling_E_{i}", parent, new Vector3(2.2f, -1.2f, z), new Vector3(0.4f, 3f, 0.4f), _wood);
            }

            // Boat arrival point and supply landing.
            Cube("Dock_BoatCleat", parent, new Vector3(-2.6f, 0.6f, -52f), new Vector3(0.5f, 0.5f, 0.5f), _metal);
            Cube("Dock_SupplyLanding", parent, new Vector3(3.6f, 0.3f, -46f), new Vector3(4f, 0.6f, 6f), _plank);

            // Small crane placeholder.
            Cube("Dock_Crane_Base", parent, new Vector3(4.6f, 0.9f, -44.5f), new Vector3(1.6f, 1.2f, 1.6f), _metal);
            Cube("Dock_Crane_Mast", parent, new Vector3(4.6f, 3.6f, -44.5f), new Vector3(0.5f, 4.2f, 0.5f), _metal);
            Cube("Dock_Crane_Jib", parent, new Vector3(2.6f, 5.5f, -46f), new Vector3(0.4f, 0.4f, 4.5f), _metal);

            // Crate storage.
            Cube("Dock_Crate_A", parent, new Vector3(-3.4f, 0.9f, -45.5f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
            Cube("Dock_Crate_B", parent, new Vector3(-3.4f, 0.9f, -47.4f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
            Cube("Dock_Crate_C", parent, new Vector3(-3.4f, 2.3f, -45.5f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
        }

        // ---------------------------------------------------- tier 1 lower landing

        static void BuildLowerLanding(Transform parent)
        {
            // Defensible shelf: crates, low cover, room for four to pass.
            Cube("Landing_Cover_West", parent, new Vector3(-6f, TierLanding + 0.6f, -26f),
                new Vector3(3.5f, 1.2f, 0.6f), _concrete);
            Cube("Landing_Cover_East", parent, new Vector3(6f, TierLanding + 0.6f, -26f),
                new Vector3(3.5f, 1.2f, 0.6f), _concrete);

            for (int i = 0; i < 3; i++)
                Cube($"Landing_Crate_{i}", parent,
                    new Vector3(5.4f, TierLanding + 0.7f, -30f + i * 1.6f),
                    new Vector3(1.3f, 1.4f, 1.3f), _plank);

            // Short fence placeholders along the seaward lip.
            for (int i = 0; i < 5; i++)
                Cube($"Landing_FencePost_{i}", parent,
                    new Vector3(-7f + i * 3.5f, TierLanding + 0.7f, -35.4f),
                    new Vector3(0.25f, 1.4f, 0.25f), _wood);
        }

        // ---------------------------------------------------- tier 2 gate terrace

        static void BuildGateTerrace(Transform parent)
        {
            // Gate sits at z -6, not -8: further north, the 3 m wall stops
            // clipping the lighthouse sightline for players on the approach.
            const float z = -4f;
            const float h = 3f;
            float sideRun = (GateHalfWidth * 2f - GateOpening) / 2f;
            float sideCentre = GateOpening / 2f + sideRun / 2f;

            Cube("GateWall_West", parent, new Vector3(-sideCentre, TierGate + h / 2f, z),
                new Vector3(sideRun, h, 0.6f), _concrete);
            Cube("GateWall_East", parent, new Vector3(sideCentre, TierGate + h / 2f, z),
                new Vector3(sideRun, h, 0.6f), _concrete);
            Cube("GatePost_West", parent, new Vector3(-GateOpening / 2f - 0.4f, TierGate + 2f, z),
                new Vector3(0.8f, 4f, 1f), _concrete);
            Cube("GatePost_East", parent, new Vector3(GateOpening / 2f + 0.4f, TierGate + 2f, z),
                new Vector3(0.8f, 4f, 1f), _concrete);
            Cube("GateLintel", parent, new Vector3(0f, TierGate + 4.2f, z),
                new Vector3(GateOpening + 1.6f, 0.4f, 0.8f), _metal);
            Cube("GateLeaf", parent, new Vector3(0f, TierGate + 1.3f, z),
                new Vector3(GateOpening - 0.2f, 2.6f, 0.2f), _metal);

            // Electric fence run flanking the gate (GDD Section 21).
            for (int i = 0; i < 4; i++)
            {
                Cube($"Fence_Post_W_{i}", parent, new Vector3(-10f + i * 1.4f, TierGate + 0.9f, -3f),
                    new Vector3(0.2f, 1.8f, 0.2f), _metal);
                Cube($"Fence_Post_E_{i}", parent, new Vector3(4.6f + i * 1.4f, TierGate + 0.9f, -3f),
                    new Vector3(0.2f, 1.8f, 0.2f), _metal);
            }

            Cube("Terrace_Barricade_Stack", parent, new Vector3(-8.5f, TierGate + 0.5f, -16f),
                new Vector3(2.4f, 1.0f, 1.2f), _plank);
        }

        // -------------------------------------------------- tier 3 main compound

        static void BuildCompound(Transform parent)
        {
            // Main Yard — a named space, kept clear for sightlines.
            Slab("MainYard", parent, -8f, 8f, 10f, 24f, TierCompound, TierCompound + 0.04f, _ground);

            // Generator Shed 10 x 8.
            var shed = NewGroup("GeneratorShed", parent);
            Building("Shed_Body", shed, new Vector2(-18f, 15.5f), new Vector2(10f, 8f), 4f, _concrete);
            Roof("Shed_Roof", shed, new Vector2(-18f, 15.5f), new Vector2(10.6f, 8.6f), 4f, 1.4f, _metal);
            Cube("Shed_Door", shed, new Vector3(-12.9f, TierCompound + 1.1f, 15.5f),
                new Vector3(0.3f, 2.2f, 1.6f), _metal);
            Cube("Generator_Body", shed, new Vector3(-17.5f, TierCompound + 0.9f, 15.5f),
                new Vector3(3.2f, 1.8f, 2f), _metal);
            Cube("Generator_FuelCap", shed, new Vector3(-18.7f, TierCompound + 1.95f, 15.5f),
                new Vector3(0.7f, 0.3f, 0.7f), _plank);

            // Workshop 12 x 9, backed into the Tier 4 riser.
            var workshop = NewGroup("Workshop", parent);
            Building("Workshop_Body", workshop, new Vector2(-18f, 27f), new Vector2(12f, 9f), 4.5f, _wood);
            Roof("Workshop_Roof", workshop, new Vector2(-18f, 27f), new Vector2(12.6f, 9.6f), 4.5f, 1.8f, _metal);
            // Door faces east onto the yard. A south-facing door would put the
            // workshop's own mass between the doorway and the lighthouse, which sits
            // north-east of it on the knoll (GDD Section 36).
            Cube("Workshop_Door", workshop, new Vector3(-11.85f, TierCompound + 1.1f, 27f),
                new Vector3(0.3f, 2.2f, 1.6f), _plank);
            Cube("Workshop_BenchProp", workshop, new Vector3(-20f, TierCompound + 0.5f, 26f),
                new Vector3(4f, 1f, 1.2f), _plank);

            // Storage 10 x 8.
            var storage = NewGroup("StorageArea", parent);
            Building("Storage_Body", storage, new Vector2(-18f, 4f), new Vector2(10f, 8f), 4f, _wood);
            Roof("Storage_Roof", storage, new Vector2(-18f, 4f), new Vector2(10.6f, 8.6f), 4f, 1.4f, _metal);
            Cube("Storage_Door", storage, new Vector3(-12.9f, TierCompound + 1.1f, 4f),
                new Vector3(0.3f, 2.2f, 1.6f), _plank);
            Cube("Cabinet_Ammunition", storage, new Vector3(-15.5f, TierCompound + 0.8f, 4f),
                new Vector3(1.8f, 1.6f, 0.8f), _metal);

            // Keeper's House 12 x 9 — slightly apart, east side.
            var house = NewGroup("KeepersHouse", parent);
            Building("House_Body", house, new Vector2(18f, 20f), new Vector2(12f, 9f), 5.5f, _wood);
            Roof("House_Roof", house, new Vector2(18f, 20f), new Vector2(12.6f, 9.6f), 5.5f, 2.6f, _plank);
            Cube("House_Door", house, new Vector3(12.1f, TierCompound + 1.1f, 20f),
                new Vector3(0.3f, 2.2f, 1.4f), _plank);
            Cube("House_Window_S", house, new Vector3(15f, TierCompound + 2.4f, 15.4f),
                new Vector3(1.4f, 1.4f, 0.3f), _metal);
            Cube("Cabinet_Medical", house, new Vector3(12.6f, TierCompound + 0.8f, 18f),
                new Vector3(0.8f, 1.6f, 1.6f), _metal);

            // Electrical / Control Station 8 x 7.
            var electrical = NewGroup("ElectricalStation", parent);
            Building("Electrical_Body", electrical, new Vector2(17f, 8f), new Vector2(8f, 7f), 3.5f, _concrete);
            Roof("Electrical_Roof", electrical, new Vector2(17f, 8f), new Vector2(8.4f, 7.4f), 3.5f, 1.2f, _metal);
            Cube("Electrical_Door", electrical, new Vector3(13.1f, TierCompound + 1.1f, 8f),
                new Vector3(0.3f, 2.2f, 1.4f), _metal);
            Cube("Switchboard", electrical, new Vector3(13.4f, TierCompound + 1.6f, 9.8f),
                new Vector3(0.35f, 2f, 2.2f), _metal);
        }

        // ----------------------------------------------------- tier 4 lighthouse

        static void BuildLighthouse(Transform parent)
        {
            var pos = new Vector3(LighthouseXZ.x, 0f, LighthouseXZ.y);
            float y = TierLighthouse;

            Cylinder("Lighthouse_Plinth", parent, pos + Vector3.up * (y + 0.5f), 6.2f, 1.0f, _rock);
            y += 1.0f;

            Cylinder("Lighthouse_L1_Operations", parent, pos + Vector3.up * (y + 2.4f), 5.5f, 4.8f, _concrete);
            y += 4.8f;

            Cylinder("Lighthouse_Shaft", parent, pos + Vector3.up * (y + 3.0f), 4.6f, 6.0f, _concrete);
            y += 6.0f;

            Cylinder("Lighthouse_L2_Mechanical", parent, pos + Vector3.up * (y + 2.0f), 5.0f, 4.0f, _concrete);
            y += 4.0f;

            Cube("Lighthouse_Balcony", parent, pos + Vector3.up * (y + 0.15f),
                new Vector3(12.4f, 0.3f, 12.4f), _metal);

            Cylinder("Lighthouse_L3_LanternRoom", parent, pos + Vector3.up * (y + 1.9f), 4.0f, 3.6f, _metal);
            y += 3.6f;

            Cylinder("Lighthouse_Cap", parent, pos + Vector3.up * (y + 0.7f), 4.2f, 1.4f, _metal);
        }

        /// <summary>World-space centre of the lantern room. Used by the LOS tests.</summary>
        public static Vector3 LanternCentre =>
            new Vector3(LighthouseXZ.x, TierLighthouse + 1f + 4.8f + 6f + 4f + 1.8f, LighthouseXZ.y);

        // ----------------------------------------------------------------- paths

        static void BuildPaths(Transform parent)
        {
            // Dock -> Tier 1. Ramp, 4 m wide, 32 degrees.
            Ramp("Ramp_DockToLanding", parent,
                new Vector3(-4f, TierDock, -44f), new Vector3(-4f, TierLanding, IslandSouth), 4f, _ground);

            // Tier 1 -> Tier 2. Primary stair, 4 m wide, 33.7 degrees.
            Stair("Stair_LandingToGate", parent,
                new Vector3(1f, TierLanding, -29f), new Vector3(1f, TierGate, LandingNorth), 4f);

            // Tier 2 -> Tier 3. Primary stair, 4 m wide.
            Stair("Stair_GateToCompound", parent,
                new Vector3(-3f, TierGate, -7f), new Vector3(-3f, TierCompound, GateNorth), 4f);

            // Tier 2 -> Tier 3. Secondary maintenance ramp, 2.5 m wide, 26.6 degrees.
            Ramp("Ramp_MaintenanceRoute", parent,
                new Vector3(9f, TierGate, -10f), new Vector3(9f, TierCompound, GateNorth), 2.5f, _ground);

            // Tier 3 -> Tier 4. Short stair to the lighthouse, 5 m wide.
            Stair("Stair_CompoundToLighthouse", parent,
                new Vector3(0f, TierCompound, 26f), new Vector3(0f, TierLighthouse, CompoundNorth), 5f);

            // Walkable surface strips, so the intended routes read at blockout stage.
            Slab("Path_LandingSpine", parent, -6f, 6f, -34f, LandingNorth,
                TierLanding, TierLanding + 0.03f, _ground);
            Slab("Path_TerraceSpine", parent, -9f, 9f, -19f, GateNorth,
                TierGate, TierGate + 0.03f, _ground);
            Slab("Path_CompoundApproach", parent, -6f, 6f, GateNorth, 10f,
                TierCompound, TierCompound + 0.03f, _ground);
            Slab("Path_LighthouseApproach", parent, -4f, 4f, 24f, CompoundNorth,
                TierCompound, TierCompound + 0.03f, _ground);
        }

        static void BuildRetainingWalls(Transform parent)
        {
            const float h = 1.1f;
            // The Tier 2 lip is a kerb, not a parapet: at 1.1 m it clipped the
            // lighthouse sightline from the back half of the lower landing.
            const float kerb = 0.6f;

            // Tier 2 south lip, split around the arriving stair (x -1..3).
            Cube("Retain_Tier2_Lip_West", parent,
                new Vector3(-6f, TierGate + kerb / 2f, LandingNorth), new Vector3(10f, kerb, 0.5f), _concrete);
            Cube("Retain_Tier2_Lip_East", parent,
                new Vector3(7f, TierGate + kerb / 2f, LandingNorth), new Vector3(8f, kerb, 0.5f), _concrete);

            // Tier 3 south lip — FLANKS ONLY. Nothing crosses the central corridor:
            // the gate-to-lantern sightline clears this lip by only ~1.2 m.
            Cube("Retain_Tier3_Lip_West", parent,
                new Vector3(-19.25f, TierCompound + h / 2f, GateNorth), new Vector3(16.5f, h, 0.5f), _concrete);
            Cube("Retain_Tier3_Lip_East", parent,
                new Vector3(19.25f, TierCompound + h / 2f, GateNorth), new Vector3(16.5f, h, 0.5f), _concrete);

            // Tier 4 lip, split around the stair.
            Cube("Retain_Tier4_Lip_West", parent,
                new Vector3(-7.75f, TierLighthouse + h / 2f, CompoundNorth), new Vector3(8.5f, h, 0.5f), _concrete);
            Cube("Retain_Tier4_Lip_East", parent,
                new Vector3(7.75f, TierLighthouse + h / 2f, CompoundNorth), new Vector3(8.5f, h, 0.5f), _concrete);
        }

        // --------------------------------------------------------------- markers

        static void BuildMarkers(Transform parent)
        {
            const BlockoutMarker.MarkerKind task = BlockoutMarker.MarkerKind.TaskStation;
            const BlockoutMarker.MarkerKind defense = BlockoutMarker.MarkerKind.DefenseSocket;
            const BlockoutMarker.MarkerKind control = BlockoutMarker.MarkerKind.SystemControl;

            Marker(parent, "Generator_FuelPoint", new Vector3(-18.7f, TierCompound + 2.0f, 15.5f), task,
                "Pour fuel can here.");
            Marker(parent, "Generator_StartPoint", new Vector3(-16.1f, TierCompound + 1.4f, 16.4f), task,
                "Prime and start.");
            Marker(parent, "Generator_RepairPoint", new Vector3(-17.5f, TierCompound + 1.0f, 14.2f), task,
                "Damage repair panel.");
            Marker(parent, "Workshop_Bench", new Vector3(-20f, TierCompound + 1.1f, 26f), task,
                "Trap repair, ammo crafting (GDD 24).");
            Marker(parent, "Ammo_Storage", new Vector3(-15.5f, TierCompound + 1.7f, 4f), task,
                "Ammunition cabinet.");
            Marker(parent, "Fuse_Storage", new Vector3(13.4f, TierCompound + 1.7f, 9.8f), task,
                "Fuse cabinet at the switchboard.");
            Marker(parent, "Medical_Storage", new Vector3(12.6f, TierCompound + 1.7f, 18f), task,
                "Medical cabinet, Keeper's House.");
            Marker(parent, "MainGate_InspectionPoint", new Vector3(0f, TierGate + 1.0f, -5.8f),
                BlockoutMarker.MarkerKind.Inspection, "Visitors questioned here (GDD 12-13).");
            Marker(parent, "MainGate_TrapSocket", new Vector3(0f, TierGate + 0.2f, -7.5f), defense,
                "Shock trap socket, outside the gate.");
            Marker(parent, "MainGate_BarricadeSocket", new Vector3(0f, TierGate + 0.2f, -4f), defense,
                "Barricade socket in the gate opening.");
            Marker(parent, "ShiftBell_Point", new Vector3(3f, TierCompound + 1.4f, 24f), control,
                "Ring to end the shift (GDD 15).");
            Marker(parent, "BeaconControl_Point", new Vector3(2.5f, TierLighthouse + 1.4f, 32.8f), control,
                "Remote beacon control, Operations floor.");
            Marker(parent, "Radio_Point", new Vector3(-2.5f, TierLighthouse + 1.4f, 32.8f), control,
                "Radio, Operations floor.");

            Marker(parent, "Courtyard_Centre", new Vector3(0f, TierCompound + 0.2f, 17f),
                BlockoutMarker.MarkerKind.Landmark, "Main Yard 16 x 14. Keep clear for sightlines.");
            Marker(parent, "Spawn_Player", new Vector3(0f, TierDock + 0.6f, -52f),
                BlockoutMarker.MarkerKind.SpawnPoint, "Blockout walk starts at the dock.");
            Marker(parent, "Entrance_MainGate", new Vector3(0f, TierGate + 0.2f, -4f),
                BlockoutMarker.MarkerKind.Entrance, "Primary enemy approach.");
            Marker(parent, "Task_DockDelivery", new Vector3(3.6f, TierDock + 0.9f, -46f), task,
                "Supply drop-off. Carry loop starts here.");
            Marker(parent, "Defense_LandingSocket", new Vector3(0f, TierLanding + 0.2f, -26f), defense,
                "First fallback, lower landing.");
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

        // -------------------------------------------------------- review cameras

        /// <summary>
        /// Composition review points, at first-person eye height above their tier.
        /// Named exactly as the task spec requires so they can be checked by hand.
        /// </summary>
        public static readonly (string Name, Vector3 Eye, Vector3 LookAt)[] ReviewCameras =
        {
            ("CAM_DockToLighthouse",       new Vector3(0f, TierDock + 0.4f + EyeHeight, -52f), LanternCentre),
            // Sits at the landing's arrival end, west of the stair. Further north the
            // Tier 2 lip subtends too much angle to see the tower at all.
            ("CAM_LowerPathToLighthouse",  new Vector3(-4f, TierLanding + EyeHeight, -33f),    LanternCentre),
            ("CAM_GateToLighthouse",       new Vector3(0f, TierGate + EyeHeight, -5.8f),       LanternCentre),
            ("CAM_MainYard",               new Vector3(0f, TierCompound + EyeHeight, 12f),     LanternCentre),
            ("CAM_GeneratorCourtyard",     new Vector3(-12f, TierCompound + EyeHeight, 15.5f), LanternCentre),
            // Stands on the lighthouse plinth (top y 22), not the bare knoll.
            ("CAM_LighthouseLookingDown",  new Vector3(0f, TierLighthouse + 1f + EyeHeight, 33f),
                new Vector3(0f, TierCompound, 8f))
        };

        static void BuildReviewCameras(Transform parent)
        {
            foreach (var (name, eye, lookAt) in ReviewCameras)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = eye;
                go.transform.rotation = Quaternion.LookRotation((lookAt - eye).normalized, Vector3.up);
            }
        }

        // ------------------------------------------------------------------ lighting

        static void BuildLighting(Transform parent)
        {
            var group = NewGroup("Lighting", parent);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.13f, 0.19f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.09f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.07f);
            RenderSettings.skybox = null;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.09f, 0.11f, 0.15f);
            RenderSettings.fogDensity = 0.007f;

            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(group, false);
            moonGo.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.66f, 0.88f);
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.sun = moon;

            WarmLamp(group, "Lamp_Yard", new Vector3(0f, TierCompound + 6f, 17f), 32f, 9f);
            WarmLamp(group, "Lamp_GeneratorShed", new Vector3(-13f, TierCompound + 3.6f, 15.5f), 16f, 6f);
            WarmLamp(group, "Lamp_Workshop", new Vector3(-18f, TierCompound + 4f, 22f), 16f, 6f);
            WarmLamp(group, "Lamp_KeepersHouse", new Vector3(12f, TierCompound + 4.4f, 20f), 16f, 5.5f);
            WarmLamp(group, "Lamp_Storage", new Vector3(-13f, TierCompound + 3.6f, 4f), 14f, 5f);
            WarmLamp(group, "Lamp_Electrical", new Vector3(13f, TierCompound + 3.8f, 8f), 14f, 5f);
            WarmLamp(group, "Lamp_Gate", new Vector3(0f, TierGate + 3.8f, -4f), 20f, 8f);
            WarmLamp(group, "Lamp_Landing", new Vector3(0f, TierLanding + 3.4f, -26f), 18f, 6f);
            WarmLamp(group, "Lamp_Dock", new Vector3(-2.6f, TierDock + 3.4f, -46f), 20f, 6f);

            var pivot = new GameObject("Beacon_Pivot");
            pivot.transform.SetParent(group, false);
            pivot.transform.position = LanternCentre;
            pivot.AddComponent<BlockoutBeaconSpinner>();

            var beamGo = new GameObject("Beacon_Beam");
            beamGo.transform.SetParent(pivot.transform, false);
            beamGo.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.94f, 0.80f);
            beam.intensity = 260f;
            beam.range = 200f;
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
            go.transform.position = new Vector3(0f, TierDock + 1.2f, -52f);

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.45f;
            controller.slopeLimit = 50f;

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
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

        /// <summary>Axis-aligned box given by its min/max on each axis.</summary>
        static ProBuilderMesh Slab(string name, Transform parent,
            float xMin, float xMax, float zMin, float zMax, float yMin, float yMax, Material material)
        {
            return Cube(name, parent,
                new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, (zMin + zMax) / 2f),
                new Vector3(xMax - xMin, yMax - yMin, zMax - zMin),
                material);
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

        static ProBuilderMesh Building(string name, Transform parent, Vector2 centreXZ, Vector2 footprint, float height, Material material)
        {
            return Cube(name, parent,
                new Vector3(centreXZ.x, TierCompound + height / 2f, centreXZ.y),
                new Vector3(footprint.x, height, footprint.y),
                material);
        }

        static ProBuilderMesh Roof(string name, Transform parent, Vector2 centreXZ, Vector2 footprint,
            float wallHeight, float roofHeight, Material material)
        {
            var pb = ShapeGenerator.GeneratePrism(PivotLocation.Center,
                new Vector3(footprint.x, roofHeight, footprint.y));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(centreXZ.x, TierCompound + wallHeight + roofHeight / 2f, centreXZ.y);
            Finish(pb, material);
            return pb;
        }

        /// <summary>Stair rising from <paramref name="from"/> to <paramref name="to"/> along +Z.</summary>
        static ProBuilderMesh Stair(string name, Transform parent, Vector3 from, Vector3 to, float width)
        {
            float rise = to.y - from.y;
            float run = to.z - from.z;
            int steps = Mathf.Max(4, Mathf.RoundToInt(rise / 0.35f));

            var pb = ShapeGenerator.GenerateStair(PivotLocation.Center,
                new Vector3(width, rise, run), steps, true);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(from.x, (from.y + to.y) / 2f, (from.z + to.z) / 2f);
            Finish(pb, _concrete);
            return pb;
        }

        /// <summary>Sloped slab rising from <paramref name="from"/> to <paramref name="to"/>.</summary>
        static ProBuilderMesh Ramp(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
        {
            float rise = to.y - from.y;
            float run = to.z - from.z;
            float length = Mathf.Sqrt(rise * rise + run * run);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(width, 0.4f, length));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(from.x, (from.y + to.y) / 2f, (from.z + to.z) / 2f);
            pb.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            Finish(pb, material);
            return pb;
        }

        static void Finish(ProBuilderMesh pb, Material material, bool addCollider = true)
        {
            pb.ToMesh();
            pb.Refresh();

            var renderer = pb.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            if (addCollider)
                pb.gameObject.AddComponent<MeshCollider>();

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
            _cliff = MakeMaterial("Blockout_Cliff", new Color(0.20f, 0.21f, 0.23f), 0.90f);
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
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(s => s.path == ScenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
