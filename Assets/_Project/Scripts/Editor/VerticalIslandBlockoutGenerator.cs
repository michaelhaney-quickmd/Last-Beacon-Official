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
    /// Phase 1 — compact vertical lighthouse island blockout.
    ///
    /// The dock-to-compound approach is a serpentine that wraps the side of the
    /// cliff mass: LEFT, then RIGHT through an overlook shelf, then LEFT again into
    /// the compound. The lighthouse holds still on the centreline throughout, so the
    /// destination never moves while the foreground swings.
    ///
    /// APPROVED ROUTE. Every waypoint is on the primary path — the overlook is not
    /// a detour: the traverse terminates at its south-west corner and the final
    /// ascent begins at its north-west corner.
    ///
    ///    0  dock, jetty end        (   0, 0.4, -48 )
    ///    1  shore apron            (   0, 0.0, -41 )
    ///    2  ramp base              (  -4, 0.0, -40 )   route breaks LEFT
    ///    3  lower-left ascent top  ( -14, 4.0, -28 )   14.4 deg
    ///    4  traverse midpoint      (   0, 6.5, -22 )   bends RIGHT,  9.3 deg
    ///    5  overlook entry  SW     (  10, 9.0, -19 )   13.5 deg
    ///    6  fence lookout          (15.5, 9.0, -17.5)  look back to dock
    ///    7  overlook exit   NW     (10.5, 9.0, -12.5)  turn LEFT
    ///    8  ascent A top           (   4,11.5, -9  )   18.7 deg
    ///    9  landing                (   0,11.5, -7.5)   flat
    ///   10  broad stairs top       (  -5,16.0, -1  )   28.7 deg
    ///   11  compound entrance      (  -6,17.0,  2  )   17.6 deg
    ///   12  main yard centre       (   0,17.0, 17  )
    ///       lighthouse base        (   0,21.0, 38  )   UNCHANGED
    ///
    /// NOTE ON THE GDD: Section 6 scrapped the previous multi-tier island. This
    /// blockout reinstates elevation deliberately, at compact scale. Treat it as a
    /// revision of Section 6, not an oversight.
    ///
    /// Cliff massing is broad faceted planes and battered faces, not stacked
    /// rectangular terrace walls. The west flank is deliberately shallow so the
    /// lighthouse stays visible from the lower-left leg of the climb — do not
    /// steepen it without re-running the visibility report.
    ///
    /// Everything is a separate editable ProBuilder mesh.
    /// </summary>
    public static class VerticalIslandBlockoutGenerator
    {
        public const string RootName = "LB_VerticalIsland_Blockout";

        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string MaterialFolder = "Assets/_Project/Art/Materials/Blockout";

        // --- Elevation bands -----------------------------------------------------
        public const float TierDock = 0f;
        public const float TierLowerAscent = 4f;
        public const float TierOverlook = 9f;
        public const float TierLanding = 11.5f;
        public const float TierCompound = 17f;
        public const float TierLighthouse = 21f;

        // --- Island extents ------------------------------------------------------
        const float IslandNorth = 44f;
        const float IslandHalfWidth = 31f;
        const float CompoundSouth = 2f;
        const float CompoundNorth = 32f;
        const float CompoundHalfWidth = 27.5f;
        const float KnollHalfWidth = 12f;

        const float GateOpening = 4.5f;
        public const float EyeHeight = 1.7f;

        /// <summary>Lighthouse tower centre. Unchanged, and must stay unchanged.</summary>
        public static readonly Vector2 LighthouseXZ = new Vector2(0f, 38f);

        // --- Approved route waypoints -------------------------------------------
        public static readonly Vector3 WpJettyEnd = new Vector3(0f, 0.4f, -48f);
        public static readonly Vector3 WpShoreApron = new Vector3(0f, 0f, -41f);
        public static readonly Vector3 WpRampBase = new Vector3(-4f, 0f, -40f);
        public static readonly Vector3 WpLowerLeftTop = new Vector3(-14f, 4f, -28f);
        public static readonly Vector3 WpTraverseMid = new Vector3(0f, 6.5f, -22f);
        public static readonly Vector3 WpOverlookEntry = new Vector3(10f, 9f, -19f);
        public static readonly Vector3 WpFenceLookout = new Vector3(15.5f, 9f, -17.5f);
        public static readonly Vector3 WpOverlookExit = new Vector3(10.5f, 9f, -12.5f);
        public static readonly Vector3 WpAscentATop = new Vector3(4f, 11.5f, -9f);
        public static readonly Vector3 WpLanding = new Vector3(0f, 11.5f, -7.5f);
        public static readonly Vector3 WpStairsTop = new Vector3(-5f, 16f, -1f);
        public static readonly Vector3 WpCompoundEntrance = new Vector3(-6f, 17f, 2f);
        public static readonly Vector3 WpYardCentre = new Vector3(0f, 17f, 17f);

        /// <summary>The primary route, in order. Used for distance and timing.</summary>
        public static Vector3[] Route => new[]
        {
            WpJettyEnd, WpShoreApron, WpRampBase, WpLowerLeftTop, WpTraverseMid,
            WpOverlookEntry, WpFenceLookout, WpOverlookExit, WpAscentATop,
            WpLanding, WpStairsTop, WpCompoundEntrance, WpYardCentre
        };

        // --- Overlook shelf, 11 x 8 ----------------------------------------------
        const float OverlookXMin = 7.5f, OverlookXMax = 18.5f;
        const float OverlookZMin = -20f, OverlookZMax = -12f;

        static Material _rock, _cliff, _concrete, _wood, _metal, _plank, _ground, _water;

        [MenuItem("Tools/Last Beacon/Generate Vertical Island")]
        public static void Generate()
        {
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ClearExistingRoots();

            var root = new GameObject(RootName).transform;
            var cliffs = NewGroup("CliffMasses", root);
            var sea = NewGroup("Sea", root);
            var dock = NewGroup("Approach_00_Dock", root);
            var lower = NewGroup("Approach_01_LowerLeftAscent", root);
            var traverse = NewGroup("Approach_02_RisingTraverse", root);
            var overlook = NewGroup("Approach_03_RightOverlook", root);
            var ascent = NewGroup("Approach_04_FinalAscent", root);
            var compound = NewGroup("Tier3_MainCompound", root);
            var lighthouse = NewGroup("Tier4_Lighthouse", root);
            var markers = NewGroup("Markers", root);
            var cameras = NewGroup("ReviewCameras", root);

            BuildCliffMasses(cliffs);
            BuildSea(sea);
            BuildDock(dock);
            BuildLowerLeftAscent(lower);
            BuildRisingTraverse(traverse);
            BuildRightOverlook(overlook);
            BuildFinalAscent(ascent);
            BuildCompound(compound);
            BuildLighthouse(lighthouse);
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
            // Solid cores. These are buried support; every exposed face gets a
            // battered plane over it so nothing reads as a rectangular terrace wall.
            Slab("Cliff_ShorePlinth", parent, -20f, 20f, -44f, -34f, -2f, TierDock, _rock);
            Slab("Cliff_LowerWestBench", parent, -24f, -6f, -36f, -22f, -2f, TierLowerAscent, _cliff);
            Slab("Cliff_OverlookBench", parent, 6f, 22f, -22f, -10f, -2f, TierOverlook, _cliff);
            Slab("Cliff_LandingBench", parent, -4f, 7f, -11f, -6f, -2f, TierLanding, _cliff);
            Slab("Cliff_CompoundPlateau", parent,
                -IslandHalfWidth, IslandHalfWidth, CompoundSouth, CompoundNorth, -2f, TierCompound, _cliff);

            // Battered faces — broad faceted planes. The route is cut into these.
            // South face the traverse climbs: y0 at z -30 up to y9 at z -12.
            BatteredFace("Cliff_SouthFace_Battered", parent,
                new Vector3(0f, 0f, -30f), new Vector3(0f, TierOverlook, -12f), 44f, _cliff);

            // West flank. Deliberately shallow: this is what keeps the lighthouse
            // visible from the lower-left leg.
            BatteredFace("Cliff_WestFlank_Battered", parent,
                new Vector3(-19.5f, TierLowerAscent, -16f),
                new Vector3(-19.5f, TierCompound, CompoundSouth), 23f, _cliff);

            // East flank, above and behind the overlook.
            BatteredFace("Cliff_EastFlank_Battered", parent,
                new Vector3(22f, TierOverlook, -12f),
                new Vector3(22f, TierCompound, CompoundSouth), 16f, _cliff);

            // Centre face the final ascent is carved into.
            BatteredFace("Cliff_CentreFace_Battered", parent,
                new Vector3(0f, TierOverlook, -10f),
                new Vector3(0f, TierCompound, CompoundSouth), 16f, _cliff);

            // Cliff below the overlook. Steep on purpose — this is the drop the
            // fence guards, and the wall you look down when you turn back.
            BatteredFace("Cliff_OverlookFace_Battered", parent,
                new Vector3(14f, 0f, -26f), new Vector3(14f, TierOverlook, -20f), 18f, _cliff);

            // Broad chunky outcrops for silhouette. Few and large, never fragmented.
            Cube("Rock_Outcrop_W", parent, new Vector3(-33f, 9f, 6f), new Vector3(10f, 18f, 16f), _rock);
            Cube("Rock_Outcrop_E", parent, new Vector3(33f, 9f, 10f), new Vector3(12f, 20f, 22f), _rock);
            Cube("Rock_Outcrop_N", parent, new Vector3(0f, 12f, 46f), new Vector3(34f, 24f, 10f), _rock);
            Cube("Rock_Outcrop_SeaW", parent, new Vector3(-30f, 2f, -34f), new Vector3(12f, 12f, 14f), _rock);
            Cube("Rock_Outcrop_SeaE", parent, new Vector3(28f, 1f, -32f), new Vector3(12f, 10f, 16f), _rock);
            Cube("Rock_SeaStack_W", parent, new Vector3(-40f, 3f, -46f), new Vector3(8f, 14f, 8f), _rock);
            Cube("Rock_SeaStack_E", parent, new Vector3(36f, 2f, -50f), new Vector3(7f, 12f, 7f), _rock);

            // Lighthouse knoll — unchanged.
            Slab("Cliff_BandD_Knoll", parent,
                -KnollHalfWidth, KnollHalfWidth, CompoundNorth, IslandNorth, TierCompound, TierLighthouse, _cliff);

            // Compound rim — unchanged.
            Slab("Rock_Rim_West", parent, -IslandHalfWidth, -CompoundHalfWidth, CompoundSouth, CompoundNorth,
                TierCompound, TierCompound + 2.6f, _rock);
            Slab("Rock_Rim_East", parent, CompoundHalfWidth, IslandHalfWidth, CompoundSouth, CompoundNorth,
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

        // ------------------------------------------------------------ 00 — the dock

        static void BuildDock(Transform parent)
        {
            Slab("Dock_Apron", parent, -14f, 10f, -41f, -34f, -0.4f, TierDock, _ground);

            Cube("Dock_Deck", parent, new Vector3(0f, 0.2f, -44.5f), new Vector3(5f, 0.4f, 7f), _plank);
            for (int i = 0; i < 4; i++)
            {
                float z = -42f - i * 2.1f;
                Cube($"Dock_Piling_W_{i}", parent, new Vector3(-2.2f, -1.2f, z), new Vector3(0.4f, 3f, 0.4f), _wood);
                Cube($"Dock_Piling_E_{i}", parent, new Vector3(2.2f, -1.2f, z), new Vector3(0.4f, 3f, 0.4f), _wood);
            }

            Cube("Dock_BoatCleat", parent, new Vector3(-2.6f, 0.6f, -47f), new Vector3(0.5f, 0.5f, 0.5f), _metal);
            Cube("Dock_SupplyLanding", parent, new Vector3(4.2f, 0.3f, -39f), new Vector3(4f, 0.6f, 5f), _plank);

            Cube("Dock_Crane_Base", parent, new Vector3(5.2f, 0.9f, -36.5f), new Vector3(1.6f, 1.2f, 1.6f), _metal);
            Cube("Dock_Crane_Mast", parent, new Vector3(5.2f, 3.6f, -36.5f), new Vector3(0.5f, 4.2f, 0.5f), _metal);
            Cube("Dock_Crane_Jib", parent, new Vector3(3.4f, 5.5f, -38f), new Vector3(0.4f, 0.4f, 4f), _metal);

            Cube("Dock_Crate_A", parent, new Vector3(-4.2f, 0.7f, -37.5f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
            Cube("Dock_Crate_B", parent, new Vector3(-4.2f, 0.7f, -39.4f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
            Cube("Dock_Crate_C", parent, new Vector3(-4.2f, 2.1f, -37.5f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
        }

        // -------------------------------------------------- 01 — lower-left ascent

        static void BuildLowerLeftAscent(Transform parent)
        {
            // Route breaks LEFT off the apron and climbs the west shoulder.
            Ramp("Path_LowerLeftAscent", parent, WpRampBase, WpLowerLeftTop, 4.5f, _ground);

            // Small pivot shelf where the route turns from left-heading to right.
            Slab("Shelf_LowerLeftPivot", parent, -18f, -10f, -32f, -24f,
                TierLowerAscent, TierLowerAscent + 0.04f, _ground);

            Cube("LowerLeft_Kerb", parent, new Vector3(-14f, TierLowerAscent + 0.3f, -32.3f),
                new Vector3(8f, 0.6f, 0.4f), _concrete);
        }

        // ---------------------------------------------------- 02 — rising traverse

        static void BuildRisingTraverse(Transform parent)
        {
            // Two legs sweeping RIGHT across the south face, climbing y4 -> y9.
            Ramp("Path_TraverseLeg1", parent, WpLowerLeftTop, WpTraverseMid, 4f, _ground);
            Ramp("Path_TraverseLeg2", parent, WpTraverseMid, WpOverlookEntry, 4f, _ground);

            // Retaining kerb on the seaward side only, where it is genuinely useful.
            Cube("Traverse_Kerb_A", parent, new Vector3(-7f, 5.6f, -26.6f), new Vector3(12f, 0.5f, 0.4f), _concrete);
            Cube("Traverse_Kerb_B", parent, new Vector3(5f, 8.1f, -21.6f), new Vector3(10f, 0.5f, 0.4f), _concrete);
        }

        // ----------------------------------------------------- 03 — right overlook

        static void BuildRightOverlook(Transform parent)
        {
            // 11 x 8 shelf. A widened cliff path with a fence, not an arena.
            Slab("Overlook_Deck", parent, OverlookXMin, OverlookXMax, OverlookZMin, OverlookZMax,
                TierOverlook, TierOverlook + 0.04f, _ground);

            // Short fence on the exposed south and east lips — the drop to the dock.
            for (int i = 0; i < 6; i++)
            {
                float x = OverlookXMin + 0.75f + i * 2f;
                Cube($"Overlook_FencePost_S_{i}", parent,
                    new Vector3(x, TierOverlook + 0.6f, OverlookZMin + 0.3f),
                    new Vector3(0.22f, 1.2f, 0.22f), _wood);
            }
            Cube("Overlook_FenceRail_S", parent,
                new Vector3(13f, TierOverlook + 1.05f, OverlookZMin + 0.3f),
                new Vector3(10.5f, 0.15f, 0.18f), _wood);

            for (int i = 0; i < 4; i++)
            {
                float z = OverlookZMin + 1.2f + i * 2f;
                Cube($"Overlook_FencePost_E_{i}", parent,
                    new Vector3(OverlookXMax - 0.3f, TierOverlook + 0.6f, z),
                    new Vector3(0.22f, 1.2f, 0.22f), _wood);
            }
            Cube("Overlook_FenceRail_E", parent,
                new Vector3(OverlookXMax - 0.3f, TierOverlook + 1.05f, -16.6f),
                new Vector3(0.18f, 0.15f, 7f), _wood);

            Cube("Overlook_LampPost", parent, new Vector3(11f, TierOverlook + 1.6f, -16f),
                new Vector3(0.2f, 3.2f, 0.2f), _metal);
            Cube("Overlook_LampHead", parent, new Vector3(11f, TierOverlook + 3.3f, -16f),
                new Vector3(0.5f, 0.4f, 0.5f), _metal);

            Cube("Overlook_Crate_A", parent, new Vector3(17f, TierOverlook + 0.7f, -19f),
                new Vector3(1.3f, 1.4f, 1.3f), _plank);
            Cube("Overlook_Crate_B", parent, new Vector3(17f, TierOverlook + 0.7f, -17.4f),
                new Vector3(1.3f, 1.4f, 1.3f), _plank);
            Cube("Overlook_Crate_C", parent, new Vector3(17f, TierOverlook + 2.1f, -19f),
                new Vector3(1.3f, 1.4f, 1.3f), _plank);
        }

        // ------------------------------------------------------- 04 — final ascent

        static void BuildFinalAscent(Transform parent)
        {
            // Four beats, turning LEFT. Deliberately not one monumental staircase.
            Ramp("Path_AscentA_ShortRise", parent, WpOverlookExit, WpAscentATop, 4f, _ground);

            Slab("Ascent_Landing", parent, -2.5f, 5.5f, -10.5f, -6.5f,
                TierLanding, TierLanding + 0.04f, _ground);

            // Chunky broad stairs, short run. The surrounding cliff carries the scale.
            Stair("Stair_AscentBroad", parent, WpLanding, WpStairsTop, 5f);

            Ramp("Path_AscentD_FinalRise", parent, WpStairsTop, WpCompoundEntrance, 4f, _ground);

            // Retaining walls only where the cut genuinely needs holding back.
            Cube("Ascent_Retain_West", parent, new Vector3(-8.6f, 14f, -3.5f),
                new Vector3(0.5f, 5f, 9f), _concrete);
            Cube("Ascent_Retain_East", parent, new Vector3(3.2f, 13f, -3.5f),
                new Vector3(0.5f, 4f, 8f), _concrete);

            // The compound gate now sits at the entrance, its old terrace having gone.
            var gate = NewGroup("MainGate", parent);
            Cube("GatePost_West", gate, new Vector3(-8.6f, TierCompound + 2f, CompoundSouth),
                new Vector3(0.8f, 4f, 1f), _concrete);
            Cube("GatePost_East", gate, new Vector3(-3.4f, TierCompound + 2f, CompoundSouth),
                new Vector3(0.8f, 4f, 1f), _concrete);
            Cube("GateLintel", gate, new Vector3(-6f, TierCompound + 4.2f, CompoundSouth),
                new Vector3(6.1f, 0.4f, 0.8f), _metal);
            Cube("GateLeaf", gate, new Vector3(-6f, TierCompound + 1.3f, CompoundSouth),
                new Vector3(GateOpening - 0.2f, 2.6f, 0.2f), _metal);
        }

        // --------------------------------------------------- tier 3 main compound
        // UNCHANGED except the approved Storage move from (-18, 4) to (-19, 12).

        static void BuildCompound(Transform parent)
        {
            Slab("MainYard", parent, -8f, 8f, 10f, 24f, TierCompound, TierCompound + 0.04f, _ground);

            var shed = NewGroup("GeneratorShed", parent);
            Building("Shed_Body", shed, new Vector2(-18f, 15.5f), new Vector2(10f, 8f), 4f, _concrete);
            Roof("Shed_Roof", shed, new Vector2(-18f, 15.5f), new Vector2(10.6f, 8.6f), 4f, 1.4f, _metal);
            Cube("Shed_Door", shed, new Vector3(-12.9f, TierCompound + 1.1f, 15.5f),
                new Vector3(0.3f, 2.2f, 1.6f), _metal);
            Cube("Generator_Body", shed, new Vector3(-17.5f, TierCompound + 0.9f, 15.5f),
                new Vector3(3.2f, 1.8f, 2f), _metal);
            Cube("Generator_FuelCap", shed, new Vector3(-18.7f, TierCompound + 1.95f, 15.5f),
                new Vector3(0.7f, 0.3f, 0.7f), _plank);

            var workshop = NewGroup("Workshop", parent);
            Building("Workshop_Body", workshop, new Vector2(-18f, 27f), new Vector2(12f, 9f), 4.5f, _wood);
            Roof("Workshop_Roof", workshop, new Vector2(-18f, 27f), new Vector2(12.6f, 9.6f), 4.5f, 1.8f, _metal);
            Cube("Workshop_Door", workshop, new Vector3(-11.85f, TierCompound + 1.1f, 27f),
                new Vector3(0.3f, 2.2f, 1.6f), _plank);
            Cube("Workshop_BenchProp", workshop, new Vector3(-20f, TierCompound + 0.5f, 26f),
                new Vector3(4f, 1f, 1.2f), _plank);

            // Storage moved north so the final left climb enters the compound cleanly.
            var storage = NewGroup("StorageArea", parent);
            Building("Storage_Body", storage, new Vector2(-19f, 12f), new Vector2(10f, 8f), 4f, _wood);
            Roof("Storage_Roof", storage, new Vector2(-19f, 12f), new Vector2(10.6f, 8.6f), 4f, 1.4f, _metal);
            Cube("Storage_Door", storage, new Vector3(-13.9f, TierCompound + 1.1f, 12f),
                new Vector3(0.3f, 2.2f, 1.6f), _plank);
            Cube("Cabinet_Ammunition", storage, new Vector3(-16.5f, TierCompound + 0.8f, 12f),
                new Vector3(1.8f, 1.6f, 0.8f), _metal);

            var house = NewGroup("KeepersHouse", parent);
            Building("House_Body", house, new Vector2(18f, 20f), new Vector2(12f, 9f), 5.5f, _wood);
            Roof("House_Roof", house, new Vector2(18f, 20f), new Vector2(12.6f, 9.6f), 5.5f, 2.6f, _plank);
            Cube("House_Door", house, new Vector3(12.1f, TierCompound + 1.1f, 20f),
                new Vector3(0.3f, 2.2f, 1.4f), _plank);
            Cube("House_Window_S", house, new Vector3(15f, TierCompound + 2.4f, 15.4f),
                new Vector3(1.4f, 1.4f, 0.3f), _metal);
            Cube("Cabinet_Medical", house, new Vector3(12.6f, TierCompound + 0.8f, 18f),
                new Vector3(0.8f, 1.6f, 1.6f), _metal);

            var electrical = NewGroup("ElectricalStation", parent);
            Building("Electrical_Body", electrical, new Vector2(17f, 8f), new Vector2(8f, 7f), 3.5f, _concrete);
            Roof("Electrical_Roof", electrical, new Vector2(17f, 8f), new Vector2(8.4f, 7.4f), 3.5f, 1.2f, _metal);
            Cube("Electrical_Door", electrical, new Vector3(13.1f, TierCompound + 1.1f, 8f),
                new Vector3(0.3f, 2.2f, 1.4f), _metal);
            Cube("Switchboard", electrical, new Vector3(13.4f, TierCompound + 1.6f, 9.8f),
                new Vector3(0.35f, 2f, 2.2f), _metal);
        }

        // ------------------------------------------- tier 4 lighthouse — UNCHANGED

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

            Stair("Stair_CompoundToLighthouse", parent,
                new Vector3(0f, TierCompound, 26f), new Vector3(0f, TierLighthouse, CompoundNorth), 5f);

            Cube("Retain_Knoll_West", parent,
                new Vector3(-7.75f, TierLighthouse + 0.55f, CompoundNorth), new Vector3(8.5f, 1.1f, 0.5f), _concrete);
            Cube("Retain_Knoll_East", parent,
                new Vector3(7.75f, TierLighthouse + 0.55f, CompoundNorth), new Vector3(8.5f, 1.1f, 0.5f), _concrete);
        }

        public static Vector3 LanternCentre =>
            new Vector3(LighthouseXZ.x, TierLighthouse + 1f + 4.8f + 6f + 4f + 1.8f, LighthouseXZ.y);

        // ----------------------------------------------------------------- markers

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
            Marker(parent, "Ammo_Storage", new Vector3(-16.5f, TierCompound + 1.7f, 12f), task,
                "Ammunition cabinet.");
            Marker(parent, "Fuse_Storage", new Vector3(13.4f, TierCompound + 1.7f, 9.8f), task,
                "Fuse cabinet at the switchboard.");
            Marker(parent, "Medical_Storage", new Vector3(12.6f, TierCompound + 1.7f, 18f), task,
                "Medical cabinet, Keeper's House.");

            Marker(parent, "MainGate_InspectionPoint", new Vector3(-6f, TierCompound + 1.0f, 3.6f),
                BlockoutMarker.MarkerKind.Inspection, "Visitors questioned at the compound gate.");
            Marker(parent, "MainGate_BarricadeSocket", new Vector3(-6f, TierCompound + 0.2f, CompoundSouth), defense,
                "Barricade socket in the gate opening.");
            Marker(parent, "MainGate_TrapSocket", new Vector3(-5.4f, 16.2f, -0.6f), defense,
                "Shock trap on the final rise below the gate.");
            Marker(parent, "Overlook_TrapSocket", new Vector3(10.5f, TierOverlook + 0.2f, -18.5f), defense,
                "Shock trap on the overlook entry throat.");

            Marker(parent, "ShiftBell_Point", new Vector3(3f, TierCompound + 1.4f, 24f), control,
                "Ring to end the shift (GDD 15).");
            Marker(parent, "BeaconControl_Point", new Vector3(2.5f, TierLighthouse + 1.4f, 32.8f), control,
                "Remote beacon control, Operations floor.");
            Marker(parent, "Radio_Point", new Vector3(-2.5f, TierLighthouse + 1.4f, 32.8f), control,
                "Radio, Operations floor.");

            Marker(parent, "Courtyard_Centre", WpYardCentre + Vector3.up * 0.2f,
                BlockoutMarker.MarkerKind.Landmark, "Main Yard 16 x 14. Keep clear for sightlines.");
            Marker(parent, "Spawn_Player", new Vector3(0f, TierDock + 0.6f, -46f),
                BlockoutMarker.MarkerKind.SpawnPoint, "Blockout walk starts at the dock.");
            Marker(parent, "Entrance_MainGate", WpCompoundEntrance + Vector3.up * 0.2f,
                BlockoutMarker.MarkerKind.Entrance, "Primary enemy approach, top of the serpentine.");
            Marker(parent, "Task_DockDelivery", new Vector3(4.2f, TierDock + 0.9f, -39f), task,
                "Supply drop-off. Carry loop starts here.");
            Marker(parent, "Overlook_FenceLookout", WpFenceLookout + Vector3.up * 0.2f,
                BlockoutMarker.MarkerKind.Landmark, "Look back down at the dock from here.");
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

        // ---------------------------------------------------------- review cameras

        public static readonly (string Name, Vector3 Eye, Vector3 LookAt)[] ReviewCameras =
        {
            ("CAM_Dock",          new Vector3(0f, TierDock + 0.4f + EyeHeight, -46f),      LanternCentre),
            ("CAM_LowerLeft",     new Vector3(-14f, TierLowerAscent + EyeHeight, -28f),    LanternCentre),
            ("CAM_RightTraverse", new Vector3(0f, 6.5f + EyeHeight, -22f),                 LanternCentre),
            ("CAM_Overlook",      new Vector3(15.5f, TierOverlook + EyeHeight, -17.5f),    LanternCentre),
            ("CAM_FinalAscent",   new Vector3(0f, TierLanding + EyeHeight, -7.5f),         LanternCentre),
            ("CAM_CompoundEntry", new Vector3(-6f, TierCompound + EyeHeight, 2f),          LanternCentre)
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

        // ---------------------------------------------------------------- lighting

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

            // One lamp per route beat, so the serpentine reads at night.
            WarmLamp(group, "Lamp_Dock", new Vector3(0f, 3.4f, -42f), 20f, 6f);
            WarmLamp(group, "Lamp_LowerLeft", new Vector3(-14f, 7f, -28f), 18f, 6f);
            WarmLamp(group, "Lamp_Traverse", new Vector3(0f, 9.5f, -22f), 18f, 6f);
            WarmLamp(group, "Lamp_Overlook", new Vector3(11f, 12f, -16f), 20f, 7f);
            WarmLamp(group, "Lamp_AscentLanding", new Vector3(1.5f, 14.5f, -8.5f), 16f, 6f);
            WarmLamp(group, "Lamp_CompoundGate", new Vector3(-6f, 20f, 2f), 20f, 8f);

            WarmLamp(group, "Lamp_Yard", new Vector3(0f, TierCompound + 6f, 17f), 32f, 9f);
            WarmLamp(group, "Lamp_GeneratorShed", new Vector3(-13f, TierCompound + 3.6f, 15.5f), 16f, 6f);
            WarmLamp(group, "Lamp_Workshop", new Vector3(-18f, TierCompound + 4f, 22f), 16f, 6f);
            WarmLamp(group, "Lamp_KeepersHouse", new Vector3(12f, TierCompound + 4.4f, 20f), 16f, 5.5f);
            WarmLamp(group, "Lamp_Storage", new Vector3(-14f, TierCompound + 3.6f, 12f), 14f, 5f);
            WarmLamp(group, "Lamp_Electrical", new Vector3(13f, TierCompound + 3.8f, 8f), 14f, 5f);

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

        // ------------------------------------------------------------------ player

        static void BuildPlayer(Transform parent)
        {
            var go = new GameObject("BlockoutPlayer (PLACEHOLDER - delete in Phase 2)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, TierDock + 1.2f, -46f);

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

        // ----------------------------------------------------------------- helpers

        static Transform NewGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

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

        /// <summary>
        /// Sloped walkable slab between two points, at any heading. Oriented along
        /// the full 3D direction so diagonal ramps stay flush with the route.
        /// </summary>
        static ProBuilderMesh Ramp(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
        {
            Vector3 delta = to - from;
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(width, 0.4f, delta.magnitude));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = (from + to) / 2f;
            pb.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            Finish(pb, material);
            return pb;
        }

        /// <summary>
        /// Stair between two points. Only the heading is rotated — the steps stay
        /// level, so the rise is taken vertically instead of tilting the flight.
        /// </summary>
        static ProBuilderMesh Stair(string name, Transform parent, Vector3 from, Vector3 to, float width)
        {
            Vector3 delta = to - from;
            var horizontal = new Vector3(delta.x, 0f, delta.z);
            float rise = delta.y;
            int steps = Mathf.Max(4, Mathf.RoundToInt(rise / 0.35f));

            var pb = ShapeGenerator.GenerateStair(PivotLocation.Center,
                new Vector3(width, rise, horizontal.magnitude), steps, true);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = (from + to) / 2f;
            pb.transform.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            Finish(pb, _concrete);
            return pb;
        }

        /// <summary>
        /// Broad faceted cliff plane, battered between a foot edge and a top edge.
        /// Used instead of vertical terrace walls so the route reads as carved rock.
        /// </summary>
        static ProBuilderMesh BatteredFace(string name, Transform parent, Vector3 footEdge, Vector3 topEdge,
            float width, Material material)
        {
            Vector3 delta = topEdge - footEdge;
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(width, 1.2f, delta.magnitude));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = (footEdge + topEdge) / 2f;
            pb.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
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

        // --------------------------------------------------------------- materials

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
