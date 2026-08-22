using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastBeacon.Blockout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
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

        /// <summary>
        /// Where each compound building is actually open. The wall is built around
        /// these, and the tests read the same table, so an opening cannot drift away
        /// from the doorway it is supposed to be.
        /// </summary>
        public readonly struct DoorwaySpec
        {
            public readonly string Building;
            public readonly Vector2 Centre;
            public readonly float Yaw;
            /// <summary>Signed local X of the wall the doorway is cut into.</summary>
            public readonly float LocalX;
            /// <summary>Where along that wall the opening is centred.</summary>
            public readonly float LocalZ;
            public readonly float Width;
            /// <summary>Clear height ABOVE whatever you stand on in the doorway.</summary>
            public readonly float Height;
            /// <summary>Height of the threshold surface above the compound floor.</summary>
            public readonly float Sill;

            public DoorwaySpec(string building, Vector2 centre, float yaw,
                float localX, float localZ, float width, float height, float sill = 0f)
            {
                Building = building; Centre = centre; Yaw = yaw;
                LocalX = localX; LocalZ = localZ; Width = width; Height = height; Sill = sill;
            }

            /// <summary>Local Y of the lintel: the sill is carried up with the opening.</summary>
            public float LintelY => Sill + Height;

            /// <summary>Centre of the opening, at a height above the compound floor.</summary>
            public Vector3 Threshold(float above) =>
                At(Centre, Yaw, new Vector2(LocalX, LocalZ), TierCompound + above);

            /// <summary>Unit vector pointing out through the doorway.</summary>
            public Vector3 Outward =>
                Quaternion.Euler(0f, Yaw, 0f) * new Vector3(Mathf.Sign(LocalX), 0f, 0f);
        }

        public static readonly DoorwaySpec[] Doorways =
        {
            // Mirrored-reference layout. Industrial pair EAST, domestic pair WEST.
            // Generator: broad frontage turned W onto the yard, the readable workspace.
            new DoorwaySpec("Shed",     new Vector2(17f, 13f),     -5f, -5f,   0f,   3.5f, 3.2f),
            // Workshop: side entrance into a sheltered nook, deliberately not a
            // second broad frontage.
            new DoorwaySpec("Workshop", new Vector2(19f, 26.2f),  -17f, -5.5f, -2.5f, 1.6f, 2.2f),
            // Stores: recessed, angled to acknowledge the yard AND the gate arrival.
            new DoorwaySpec("Stores",   new Vector2(-17.5f, 8.5f), 18f,  4.5f, 1.5f, 1.6f, 2.2f),
            // House: porch onto the yard; the sill is carried up with the opening.
            new DoorwaySpec("House",    new Vector2(-18f, 20.5f),  -3f,  6f,   0f,   1.4f, 2.2f, 0.3f)
        };

        /// <summary>Lighthouse tower centre. Unchanged, and must stay unchanged.</summary>
        public static readonly Vector2 LighthouseXZ = new Vector2(0f, 38f);

        // --- Approved route waypoints -------------------------------------------
        public static readonly Vector3 WpJettyEnd = new Vector3(0f, 0.4f, -48f);
        public static readonly Vector3 WpShoreApron = new Vector3(0f, 0.4f, -41f);
        public static readonly Vector3 WpRampBase = new Vector3(-4f, 0.4f, -40f);
        /// <summary>Gentle introductory ramp so the steep ascent never starts at the dock edge.</summary>
        public static readonly Vector3 WpIntroTop = new Vector3(-7f, 1.2f, -36f);
        public static readonly Vector3 WpLowerLeftTop = new Vector3(-14f, 4f, -28f);
        /// <summary>Where the ramp reaches shelf height; the pivot pad carries the bend.</summary>
        public static readonly Vector3 LowerLeftRampTop = new Vector3(-14f, 4f, -30f);
        public static readonly Vector3 WpTraverseMid = new Vector3(0f, 6.5f, -22f);
        public static readonly Vector3 WpOverlookEntry = new Vector3(10f, 9f, -17.2f);
        public static readonly Vector3 WpFenceLookout = new Vector3(15.5f, 9f, -17.5f);
        public static readonly Vector3 WpOverlookExit = new Vector3(11f, 9f, -12f);
        public static readonly Vector3 WpAscentATop = new Vector3(6.5f, 11.5f, -9f);
        public static readonly Vector3 WpLanding = new Vector3(2.5f, 11.5f, -10.2f);
        public static readonly Vector3 WpStairsTop = new Vector3(-5f, 16f, -1f);
        public static readonly Vector3 WpCompoundEntrance = new Vector3(-6f, 17f, 2f);
        public static readonly Vector3 WpYardCentre = new Vector3(0f, 17f, 17f);

        /// <summary>The primary route, in order. Used for distance and timing.</summary>
        public static Vector3[] Route => new[]
        {
            WpJettyEnd, WpShoreApron, WpRampBase, WpIntroTop, WpLowerLeftTop, WpTraverseMid,
            WpOverlookEntry, WpFenceLookout, WpOverlookExit, WpAscentATop,
            WpLanding, WpStairsTop, WpCompoundEntrance, WpYardCentre
        };

        /// <summary>
        /// The route as actually walked, including the points where a ramp meets a
        /// deck. Route is the approved waypoint list; this is the geometry.
        /// </summary>
        public static Vector3[] WalkPath => new[]
        {
            WpJettyEnd, WpShoreApron, WpRampBase, WpIntroTop, LowerLeftRampTop, WpLowerLeftTop, WpTraverseMid,
            OverlookDeckEdge, WpOverlookEntry, WpFenceLookout, WpOverlookExit,
            WpAscentATop, WpLanding, WpStairsTop, WpCompoundEntrance, WpYardCentre
        };

        // --- Overlook shelf, 11 x 8 ----------------------------------------------
        const float OverlookXMin = 6f, OverlookXMax = 17f;
        /// <summary>West lip, set out to receive traverse leg 2's full width.</summary>
        const float TerraceWestEdge = 5.452f;
        const float OverlookZMin = -20.6f, OverlookZMax = -11.8f;

        // --- Main Gate barrier, on the terrace's west edge ------------------------
        public const float MainGateX = 8f;
        const float MainGateOpenZMax = -16.1f;   // opening runs from the cliff lip north

        /// <summary>Where the traverse actually meets the overlook deck, flush.</summary>
        public static readonly Vector3 OverlookDeckEdge = new Vector3(6.5f, TierOverlook, -18f);

        static Material _rock, _cliff, _concrete, _wood, _metal, _plank, _ground, _water;

        [MenuItem("Tools/Last Beacon/Generate Vertical Island")]
        public static void Generate()
        {
            CreateMaterials();

            // Open the existing scene rather than replacing it, so anything that is
            // not the blockout survives a regenerate — the imported island Terrain
            // above all. NewScene() here silently destroyed LB_Terrain.
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ClearExistingRoots();

            var root = new GameObject(RootName).transform;
            var cliffs = NewGroup("CliffMasses", root);
            var sea = NewGroup("Sea", root);
            var dock = NewGroup("Approach_00_Dock", root);
            var lower = NewGroup("Approach_01_LowerLeftAscent", root);
            var traverse = NewGroup("Approach_02_RisingTraverse", root);
            var overlook = NewGroup("Approach_03_MainGateTerrace", root);
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
            BuildMainGateTerrace(overlook);
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
            // Shore bed sits 0.4 BELOW the landing, not level with it. The old
            // Cliff_ShorePlinth topped out at 0.4 across 40 x 10 m and duplicated
            // the apron's walking surface over 192 m2 of shared footprint.
            Slab("Rock_ShoreBed", parent, -22f, 22f, -43f, -33f, -2f, 0f, _rock);
            // Flanking cliff, shaped to leave the landing and ramp corridor open.
            Slab("Rock_ShoreWest", parent, -22f, -11f, -45f, -36f, -2f, 2.6f, _rock);
            Slab("Rock_ShoreEast", parent, 11f, 22f, -45f, -33f, -2f, 2.6f, _rock);
            // Supports the pivot shelf, north of the ramp top. Kept: the reveal test
            // cleared it, but the traversal suite did not — without it the terrain
            // (3.35) becomes the walking surface below the shelf lip (4.04), a 0.69 m
            // step at the ramp-top junction that the controller cannot climb.
            Slab("Cliff_LowerWestBench", parent, -20f, -10f, -30f, -22f, -2f, TierLowerAscent - 0.3f, _cliff);
            // Matches the deck footprint. At x 6 / z -22 it protruded into the
            // traverse corridor and buried the last 4 m of the climb.
            Slab("Cliff_OverlookBench", parent, TerraceWestEdge, OverlookXMax, -21f, -10f, -2f, TierOverlook - 0.5f, _cliff);
            // The terrace was a painted slab on a 16 x 10.6 bench; these bound it to
            // its real 11 x 8.8 footprint. The north-west corner is left open as the
            // exit throat to Ascent A.
            Slab("Rock_TerraceEast", parent, OverlookXMax, 22f, -21f, -10f, -2f, 13f, _rock);
            Slab("Rock_TerraceNorth", parent, 14.5f, OverlookXMax, OverlookZMax, -10f,
                TierOverlook, TierOverlook + 2.2f, _rock);
            // Seals the strip behind the electric fence so the gate is the only way in.
            Slab("Rock_MainGateInfill", parent, TerraceWestEdge, 7.5f, -14.1f, -12.3f,
                TierOverlook, TierOverlook + 3f, _rock);
            // Matches the landing footprint; at x 7 it buried the top of ascent A.
            Slab("Cliff_CompoundPlateau", parent,
                -IslandHalfWidth, IslandHalfWidth, CompoundSouth, CompoundNorth, -2f, TierCompound, _cliff);

            // Battered faces — broad faceted planes. The route is cut into these.
            // South face the traverse climbs: y0 at z -30 up to y9 at z -12.
            BatteredFace("Cliff_SouthFace_Battered", parent,
                new Vector3(0f, 0f, -30f), new Vector3(0f, TierOverlook - 0.5f, -12.5f), 44f, _cliff);

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
                new Vector3(0f, TierOverlook - 1f, -10f),
                new Vector3(0f, TierCompound - 1f, CompoundSouth), 16f, _cliff);

            // The terrace's east wall, seen straight-on from the whole terrace.
            BatteredFace("Cliff_TerraceEastFace_Battered", parent,
                new Vector3(17f, TierOverlook - 1f, -15.5f), new Vector3(22f, 15f, -15.5f), 11f, _cliff);

            // The compound plateau's south wall between the centre and east flanks,
            // which was left as bare vertical face.
            BatteredFace("Cliff_SouthEastFace_Battered", parent,
                new Vector3(11f, TierOverlook, -4f), new Vector3(11f, TierCompound, CompoundSouth), 8f, _cliff);

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
            // Three abutting walkable surfaces, all topping out at exactly 0.4 and
            // sharing clean boundaries rather than overlapping footprints:
            //   deck    z -48   .. -41.5   the wooden jetty
            //   apron   z -41.5 .. -35     the 8 m landing that receives it
            //   cargo   x  3.5  ..   9.5   the delivery shelf beside it
            // Jetty runs 10 m out, with a 12 m berthing arm along its head so a
            // vessel can come alongside instead of nosing into the end.
            Cube("Dock_Deck", parent, new Vector3(0f, 0.2f, -47f), new Vector3(5f, 0.4f, 10f), _plank);
            Cube("Dock_BerthArm", parent, new Vector3(8.5f, 0.2f, -49.5f), new Vector3(12f, 0.4f, 5f), _plank);
            Slab("Dock_Apron", parent, -4.5f, 3.5f, -42f, -35f, -0.4f, 0.4f, _ground);
            Slab("Dock_SupplyApron", parent, 3.5f, 9.5f, -41f, -36f, -0.4f, 0.4f, _ground);

            // Pilings sit south of the shore bed so they read as standing in water.
            for (int i = 0; i < 5; i++)
            {
                float z = -43.5f - i * 2.1f;
                Cube($"Dock_Piling_W_{i}", parent, new Vector3(-2.2f, -1.2f, z), new Vector3(0.4f, 3f, 0.4f), _wood);
                Cube($"Dock_Piling_E_{i}", parent, new Vector3(2.2f, -1.2f, z), new Vector3(0.4f, 3f, 0.4f), _wood);
            }
            for (int i = 0; i < 3; i++)
            {
                float x = 4.5f + i * 4f;
                Cube($"Dock_BerthPiling_{i}", parent, new Vector3(x, -1.2f, -51.6f), new Vector3(0.4f, 3f, 0.4f), _wood);
            }

            // Mooring furniture along the berth face, where a hull would lie.
            Cube("Dock_BoatCleat", parent, new Vector3(-2.6f, 0.6f, -49f), new Vector3(0.5f, 0.5f, 0.5f), _metal);
            for (int i = 0; i < 3; i++)
            {
                float x = 4.5f + i * 4f;
                Cube($"Dock_Bollard_{i}", parent, new Vector3(x, 0.75f, -51.4f), new Vector3(0.5f, 0.7f, 0.5f), _metal);
            }
            Cube("Dock_BerthFender_W", parent, new Vector3(3.4f, 0.2f, -51.7f), new Vector3(0.4f, 0.6f, 0.4f), _wood);
            Cube("Dock_BerthFender_E", parent, new Vector3(13.4f, 0.2f, -51.7f), new Vector3(0.4f, 0.6f, 0.4f), _wood);

            // Props now sit ON the aprons rather than sunk through them.
            Cube("Dock_SupplyLanding", parent, new Vector3(6f, 0.7f, -38.5f), new Vector3(4f, 0.6f, 4f), _plank);
            Cube("Dock_Crane_Base", parent, new Vector3(8.2f, 1f, -40f), new Vector3(1.6f, 1.2f, 1.6f), _metal);
            Cube("Dock_Crane_Mast", parent, new Vector3(8.2f, 3.7f, -40f), new Vector3(0.5f, 4.2f, 0.5f), _metal);
            Cube("Dock_Crane_Jib", parent, new Vector3(6.6f, 5.6f, -39.6f), new Vector3(3.6f, 0.4f, 0.4f), _metal);

            Cube("Dock_Crate_A", parent, new Vector3(8.6f, 1.1f, -36.9f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
            Cube("Dock_Crate_B", parent, new Vector3(8.6f, 1.1f, -38.4f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
            Cube("Dock_Crate_C", parent, new Vector3(8.6f, 2.5f, -36.9f), new Vector3(1.4f, 1.4f, 1.4f), _plank);
        }

        // -------------------------------------------------- 01 — lower-left ascent

        static void BuildLowerLeftAscent(Transform parent)
        {
            // Route breaks LEFT off the apron and climbs the west shoulder.
            // Two segments: a gentle introductory ramp so the steep ascent never
            // begins at the dock edge, then the main climb.
            Ramp("Path_IntroRamp", parent, WpRampBase, WpIntroTop, 4.5f, _ground, surfaceOnLine: true);

            // Tops out flush with the pivot shelf: the ramp's far corner beds into
            // the shelf's thickness, so the shelf stays the walking surface there.
            Ramp("Path_LowerLeftAscent", parent, WpIntroTop, LowerLeftRampTop, 4.5f, _ground, surfaceOnLine: true);
            Shoulder("Cliff_LowerLeftShoulder", parent, WpIntroTop, LowerLeftRampTop, 6f, 10f);

            // Small pivot shelf where the route turns from left-heading to right.
            // Pulled north of the ramp top: at its old extent it overhung the climb.
            // Thickened so it beds into its bench instead of floating above it.
            Slab("Shelf_LowerLeftPivot", parent, -18f, -10f, -29.4f, -22f,
                TierLowerAscent - 0.6f, TierLowerAscent + 0.04f, _ground);

            // On the shelf's west lip. At its old south-lip position it floated
            // over the ramp climbing beneath and blocked the capsule.
            Cube("LowerLeft_Kerb", parent, new Vector3(-18.3f, TierLowerAscent + 0.3f, -25f),
                new Vector3(0.4f, 0.6f, 6f), _concrete);
        }

        // ---------------------------------------------------- 02 — rising traverse

        static void BuildRisingTraverse(Transform parent)
        {
            // Two legs sweeping RIGHT across the south face, climbing y4 -> y9.
            Ramp("Path_TraverseLeg1", parent, WpLowerLeftTop, WpTraverseMid, 4f, _ground, surfaceOnLine: true);
            Shoulder("Cliff_TraverseShoulder1", parent, WpLowerLeftTop, WpTraverseMid, 6f, 10f);

            // Leg 2 tops out at the overlook's south-west deck edge rather than
            // 2.5 m inside it, so it meets the shelf flush instead of tunnelling.
            Ramp("Path_TraverseLeg2", parent, WpTraverseMid, OverlookDeckEdge, 4f, _ground, surfaceOnLine: true);
            Shoulder("Cliff_TraverseShoulder2", parent, WpTraverseMid, OverlookDeckEdge, 6f, 10f);

            // Retaining kerb on the seaward side only, where it is genuinely useful.
            // Kept low and offset: an eye-height kerb on the inside of the bend
            // clipped the lighthouse sightline from the traverse.
            Kerb("Traverse_Kerb_A", parent, WpLowerLeftTop, WpTraverseMid, 2.3f);
            Kerb("Traverse_Kerb_B", parent, WpTraverseMid, WpOverlookEntry, 2.3f);
        }

        // ----------------------------------------------------- 03 — right overlook

        /// <summary>
        /// The MAIN GATE terrace: the controlled chokepoint between the lower island
        /// and the upper station, and the first major defensive fallback.
        ///
        /// 11 x 8.8 m. Bounded west by the gate and electric fence, south by the
        /// cliff and its short overlook fence, east and north-east by rock. The
        /// north-west corner stays open as the exit throat to Ascent A.
        ///
        /// Defences are NOT team-safe by design: the electric fence and shock trap
        /// can injure players and legitimate NPCs. The layout therefore keeps the
        /// gate opening, the control console and the through-route mutually visible,
        /// so a player can read the defence state, make it safe, walk a sailor
        /// through, and re-arm. Inspection itself stays at the dock.
        /// </summary>
        static void BuildMainGateTerrace(Transform parent)
        {
            // The south-west corner is cut parallel to traverse leg 2's iso-height
            // line, so the ramp meets the deck at one constant height across the
            // whole seam instead of a step that varies by 0.6 m across its width.
            PolygonDeck("Terrace_Deck", parent, new[]
            {
                new Vector2(TerraceWestEdge, -16.297f),
                new Vector2(8.1f, OverlookZMin),
                new Vector2(OverlookXMax, OverlookZMin),
                new Vector2(OverlookXMax, OverlookZMax),
                new Vector2(TerraceWestEdge, OverlookZMax)
            }, TierOverlook + 0.04f, 0.5f, _ground);

            // The exit throat, abutting the terrace at z -11.8 with a shared edge.
            Slab("Terrace_Throat", parent, TerraceWestEdge, 13.5f, OverlookZMax, -10f,
                TierOverlook - 0.46f, TierOverlook + 0.04f, _ground);

            // --- Main Gate. 4.5 m opening; the cliff lip forms the south jamb. ----
            var gate = NewGroup("MainGate", parent);
            // Posts placed so the CLEAR gap is 4.5 m: z -19.6 to -15.1.
            Cube("MainGate_Post_South", gate,
                new Vector3(MainGateX, TierOverlook + 2f, -20.1f),
                new Vector3(0.9f, 4f, 1f), _concrete);
            Cube("MainGate_Post_North", gate,
                new Vector3(MainGateX, TierOverlook + 2f, -14.6f),
                new Vector3(0.9f, 4f, 1f), _concrete);
            Cube("MainGate_Lintel", gate,
                new Vector3(MainGateX, TierOverlook + 4.2f, -17.35f),
                new Vector3(1f, 0.4f, 6.5f), _metal);
            // Leaf modelled OPEN, folded back along the inside of the barrier. A
            // closed leaf is a solid collider across the only way through.
            Cube("MainGate_Leaf", gate,
                new Vector3(MainGateX + 0.45f, TierOverlook + 1.3f, -13.6f),
                new Vector3(0.2f, 2.6f, 3f), _metal);
            Cube("MainGate_LampPost", gate, new Vector3(10.4f, TierOverlook + 1.8f, -19.8f),
                new Vector3(0.2f, 3.6f, 0.2f), _metal);
            Cube("MainGate_LampHead", gate, new Vector3(10.4f, TierOverlook + 3.7f, -19.8f),
                new Vector3(0.5f, 0.4f, 0.5f), _metal);

            // --- Electric fence, continuing the barrier line north ----------------
            var fence = NewGroup("ElectricFence", parent);
            for (int i = 0; i < 3; i++)
                Cube($"ElectricFence_Post_{i}", fence,
                    new Vector3(MainGateX, TierOverlook + 1f, -14f + i * 0.85f),
                    new Vector3(0.2f, 2f, 0.2f), _metal);
            Cube("ElectricFence_Rail_Lower", fence,
                new Vector3(MainGateX, TierOverlook + 1.4f, -13.15f),
                new Vector3(0.22f, 0.12f, 2f), _metal);
            Cube("ElectricFence_Rail_Upper", fence,
                new Vector3(MainGateX, TierOverlook + 2f, -13.15f),
                new Vector3(0.22f, 0.12f, 2f), _metal);
            // Obvious power connection: box at the fence's inboard end, conduit run
            // overhead to the console. Conduit sits 2.4 m up, clear of a 1.8 m player.
            Cube("ElectricFence_PowerBox", fence,
                new Vector3(7.8f, TierOverlook + 0.9f, -13.6f),
                new Vector3(0.7f, 1.4f, 0.5f), _metal);
            Cube("ElectricFence_Conduit", fence,
                new Vector3(12.4f, TierOverlook + 4f, -12.3f),
                new Vector3(7.8f, 0.14f, 0.14f), _metal);

            // --- Short overlook fence on the south lip ----------------------------
            // Cannot extend west of x 12: at x 10 the entry path's southern edge
            // reaches z -20.2 and the rail becomes a traversal blocker.
            var overlook = NewGroup("SouthOverlookFence", parent);
            for (int i = 0; i < 6; i++)
                Cube($"Overlook_FencePost_S_{i}", overlook,
                    new Vector3(12f + i * 1f, TierOverlook + 0.6f, OverlookZMin + 0.3f),
                    new Vector3(0.22f, 1.2f, 0.22f), _wood);
            Cube("Overlook_FenceRail_S", overlook,
                new Vector3(14.5f, TierOverlook + 1.05f, OverlookZMin + 0.3f),
                new Vector3(5f, 0.15f, 0.18f), _wood);

            // --- Emergency defence / light sub-control ----------------------------
            // Waist-high console, deliberately NOT a tall wall panel: the operator
            // has to keep the gate and the lower approach in view while using it.
            // This is not the lighthouse control system; that stays in operations.
            var control = NewGroup("DefenceControl", parent);
            Cube("Control_ConsoleBody", control,
                new Vector3(16.3f, TierOverlook + 0.5f, -14.6f), new Vector3(1.2f, 1f, 0.8f), _metal);
            Cube("Control_ConsoleFace", control,
                new Vector3(16.1f, TierOverlook + 1.05f, -14.6f), new Vector3(0.9f, 0.1f, 0.7f), _plank);
            Cube("Control_Lever", control,
                new Vector3(15.95f, TierOverlook + 1.45f, -15.1f), new Vector3(0.12f, 0.9f, 0.12f), _metal);
            for (int i = 0; i < 3; i++)
                Cube($"Control_Gauge_{i}", control,
                    new Vector3(16.15f, TierOverlook + 1.12f, -15f + i * 0.45f),
                    new Vector3(0.36f, 0.06f, 0.36f), _plank);

            // Indicator mast set to the side, so it never stands in the westward view.
            // StatusBoard is the reserved face for ELECTRIC FENCE / SHOCK TRAP /
            // GATE / POWER state signage. No UI yet, just the physical location.
            Cube("Control_IndicatorMast", control,
                new Vector3(16.8f, TierOverlook + 1.6f, -16.2f), new Vector3(0.25f, 3.2f, 0.25f), _metal);
            Cube("Control_StatusBoard", control,
                new Vector3(16.65f, TierOverlook + 2.4f, -16.2f), new Vector3(0.12f, 1.2f, 1.6f), _plank);
            Cube("Control_WarningLight", control,
                new Vector3(16.8f, TierOverlook + 3.35f, -16.2f), new Vector3(0.45f, 0.45f, 0.45f), _metal);

            // --- Trap bench, north-east, clear of the diagonal through-route ------
            var bench = NewGroup("TrapBench", parent);
            Cube("TrapBench_Top", bench,
                new Vector3(15.7f, TierOverlook + 0.5f, -12.3f), new Vector3(2.6f, 1f, 0.9f), _plank);
            Cube("TrapBench_ToolRack", bench,
                new Vector3(15.7f, TierOverlook + 1.4f, -11.9f), new Vector3(2.4f, 1.6f, 0.25f), _plank);
            Cube("TrapBench_Crate_A", bench,
                new Vector3(16.3f, TierOverlook + 0.7f, -19.9f), new Vector3(1.3f, 1.4f, 1.3f), _plank);
            Cube("TrapBench_Crate_B", bench,
                new Vector3(16.3f, TierOverlook + 2.1f, -19.9f), new Vector3(1.3f, 1.4f, 1.3f), _plank);
        }

        // ------------------------------------------------------- 04 — final ascent

        static void BuildFinalAscent(Transform parent)
        {
            // Four beats, turning LEFT. Deliberately not one monumental staircase.
            // Tops out at the landing's east edge rather than inside it.
            // Launches off the terrace's north lip. The previous NW-corner launch
            // cleared the rock by 0.31 m; this clears it by ~1.9 m.
            Ramp("Path_AscentA_ShortRise", parent, WpOverlookExit, WpAscentATop, 4f, _ground, surfaceOnLine: true);
            Shoulder("Cliff_AscentAShoulder", parent, WpOverlookExit, WpAscentATop, 6f, 10f);

            // One landing, not a slab plus a lip: the south-east corner is cut
            // parallel to ascent A's iso-height line so the ramp arrives flush.
            PolygonDeck("Ascent_Landing", parent, new[]
            {
                new Vector2(-2.5f, -12f),
                new Vector2(4.5f, -12f),
                new Vector2(7.5f, -7.5f),
                new Vector2(7.5f, -6.5f),
                new Vector2(-2.5f, -6.5f)
            }, TierLanding + 0.04f, 2.5f, _ground);

            // Chunky broad stairs, short run. The surrounding cliff carries the scale.
            Stair("Stair_AscentBroad", parent, WpLanding, WpStairsTop, 5f);

            StairTopPad(parent, WpLanding, WpStairsTop, 5f, 1.6f);

            // Surface-aligned: its top face passes through the stair head and the
            // compound threshold, so it neither overhangs the last step nor lands
            // proud of the plateau.
            Ramp("Path_AscentD_FinalRise", parent, WpStairsTop, WpCompoundEntrance, 4f, _ground, true);
            // Cliff_FinalRiseShoulder removed: the reveal test measured byte-identical
            // terrain exposure and deck support with and without it.

            // Retaining walls only where the cut genuinely needs holding back.
            Cube("Ascent_Retain_West", parent, new Vector3(-8.6f, 14f, -3.5f),
                new Vector3(0.5f, 5f, 9f), _concrete);
            // Moved clear of the landing slab, which it used to stand on top of.
            Cube("Ascent_Retain_East", parent, new Vector3(7.6f, 13f, -3f),
                new Vector3(0.5f, 4f, 7f), _concrete);

            // INNER GATE — a simple secondary containment barrier for the upper
            // compound. Deliberately lighter than the Main Gate: shorter posts, no
            // lintel, and no defensive control system of its own.
            var gate = NewGroup("InnerGate", parent);
            Cube("InnerGate_Post_West", gate, new Vector3(-8.6f, TierCompound + 1.6f, CompoundSouth),
                new Vector3(0.6f, 3.2f, 0.7f), _concrete);
            Cube("InnerGate_Post_East", gate, new Vector3(-3.4f, TierCompound + 1.6f, CompoundSouth),
                new Vector3(0.6f, 3.2f, 0.7f), _concrete);
            // Hinged on the EAST post, standing ajar rather than flat open. The
            // route enters on the gate centreline and then angles north-east toward
            // the yard, so a leaf swung flat north converges with the walking
            // corridor and clipped it by 0.27 m. Swung to 65 degrees it opens away
            // from the route and clears it by about 1.5 m, still on the east post.
            var leaf = Cube("InnerGate_Leaf", gate, new Vector3(-2.40f, TierCompound + 1.1f, CompoundSouth + 2.01f),
                new Vector3(GateOpening - 0.2f, 2.2f, 0.15f), _metal);
            leaf.transform.rotation = Quaternion.Euler(0f, -65f, 0f);
        }

        // --------------------------------------------------- tier 3 main compound
        // UNCHANGED except the approved Storage move from (-18, 4) to (-19, 12).

        /// <summary>
        /// The upper compound: four buildings framing a compact work yard.
        ///
        ///   Keeper's House       people / shift / health / story
        ///   Generator Shed       MAKE power - local generation only
        ///   Workshop             repair / defense
        ///   Stores / Radio       supplies / arrivals / information
        ///
        /// Two deliberate redundancies: the routine station radio lives in Stores
        /// while the lighthouse keeps a secondary emergency set, and the shed makes
        /// power while the lighthouse routes and monitors it. The standalone
        /// electrical building is gone; nothing replaces it.
        /// </summary>
        // --- Approved compound layout ----------------------------------------
        static readonly Vector2 ShedC = new Vector2(17f, 13f);          // Generator
        static readonly Vector2 WorkC = new Vector2(19f, 26.2f);
        static readonly Vector2 StoresC = new Vector2(-17.5f, 8.5f);
        static readonly Vector2 HouseC = new Vector2(-18f, 20.5f);
        const float ShedYaw = -5f, WorkYaw = -17f, StoresYaw = 18f, HouseYaw = -3f;

        /// <summary>
        /// The yard is formed BETWEEN the buildings: a 10 m throat at the gate that
        /// widens to about 20 m across the working centre, with an irregular north
        /// edge at the lighthouse stair. Convex, because PolygonDeck fans it.
        /// </summary>
        static readonly Vector2[] YardOutline =
        {
            new Vector2(-7.5f, 6.5f), new Vector2(2.5f, 6.5f), new Vector2(9.5f, 11.5f),
            new Vector2(10.5f, 19.5f), new Vector2(6.5f, 25f), new Vector2(-3f, 25.5f),
            new Vector2(-10f, 21f), new Vector2(-10.5f, 12.5f)
        };

        static void BuildCompound(Transform parent)
        {
            PolygonDeck("MainYard", parent, YardOutline, TierCompound + 0.04f, 0.5f, _ground);

            BuildCourtyardEdges(parent);
            BuildInnerGateThroat(parent);

            // ================= EAST: industrial ==================================
            // --- Generator / Utility Shed: MAKE POWER, broad frontage onto the yard
            var shed = NewGroup("GeneratorShed", parent);
            Shell("Shed_Body", shed, ShedC, new Vector2(10f, 8f), 4.2f, ShedYaw, Doorways[0], _concrete);
            Roof("Shed_Roof", shed, ShedC, new Vector2(10.6f, 8.6f), 4.2f, 1f, _metal, ShedYaw);
            DoorLeaves(shed, Doorways[0], _metal, true);
            // Yawed 90 degrees from the original footprint so the 3.2 m face fronts
            // the doorway rather than the 2.0 m end. The doorway is on local -X, so
            // the long axis belongs on Z. Centre is unchanged.
            Prop("Generator_Body", shed, ShedC, ShedYaw, new Vector2(0.5f, 0f), TierCompound + 0.9f,
                new Vector3(2f, 1.8f, 3.2f), _metal);
            // The cap follows the body: at the old 1.7 it would hang off the turned
            // deck, so it keeps its end and its 0.05 m inset from that end.
            Prop("Generator_FuelCap", shed, ShedC, ShedYaw, new Vector2(1.1f, 0f), TierCompound + 1.95f,
                new Vector3(0.7f, 0.3f, 0.7f), _plank);
            Prop("Generator_Breaker", shed, ShedC, ShedYaw, new Vector2(3.5f, -2.5f), TierCompound + 1.5f,
                new Vector3(0.9f, 1.4f, 0.35f), _metal);
            Prop("Generator_FusePanel", shed, ShedC, ShedYaw, new Vector2(3.5f, -1.1f), TierCompound + 1.5f,
                new Vector3(0.9f, 1.2f, 0.35f), _metal);

            // Lean-to on the north face, opening into the service space.
            Prop("Shed_LeanToRoof", shed, ShedC, ShedYaw, new Vector2(0f, 4.7f), TierCompound + 2.9f,
                new Vector3(6f, 0.2f, 1.4f), _metal);
            Prop("Shed_LeanToPost_W", shed, ShedC, ShedYaw, new Vector2(-2.8f, 5.3f), TierCompound + 1.4f,
                new Vector3(0.2f, 2.8f, 0.2f), _wood);
            Prop("Shed_LeanToPost_E", shed, ShedC, ShedYaw, new Vector2(2.8f, 5.3f), TierCompound + 1.4f,
                new Vector3(0.2f, 2.8f, 0.2f), _wood);
            // Drums follow the art shell's awning, which sits on the -Z gable.
            Prop("Shed_FuelDrum_A", shed, ShedC, ShedYaw, new Vector2(-1.6f, -4.7f), TierCompound + 0.45f,
                new Vector3(0.8f, 0.9f, 0.8f), _metal);
            Prop("Shed_FuelDrum_B", shed, ShedC, ShedYaw, new Vector2(-0.6f, -4.7f), TierCompound + 0.45f,
                new Vector3(0.8f, 0.9f, 0.8f), _metal);

            // --- Workshop: set back north, angled, side entrance into a nook ------
            var workshop = NewGroup("Workshop", parent);
            Shell("Workshop_Body", workshop, WorkC, new Vector2(11f, 8f), 4.5f, WorkYaw, Doorways[1], _wood);
            Roof("Workshop_Roof", workshop, WorkC, new Vector2(11.6f, 8.6f), 4.5f, 1.8f, _metal, WorkYaw);
            DoorLeaves(workshop, Doorways[1], _plank, false);
            Prop("Workshop_AlcoveCanopy", workshop, WorkC, WorkYaw, new Vector2(-6.9f, -2.5f), TierCompound + 2.6f,
                new Vector3(3f, 0.2f, 3f), _metal);
            Prop("Workshop_AlcovePost", workshop, WorkC, WorkYaw, new Vector2(-8.2f, -3.8f), TierCompound + 1.3f,
                new Vector3(0.2f, 2.6f, 0.2f), _wood);
            Prop("Workshop_BenchProp", workshop, WorkC, WorkYaw, new Vector2(-6.6f, -1.2f), TierCompound + 0.5f,
                new Vector3(2.4f, 1f, 1f), _plank);
            Prop("Workshop_ToolRack", workshop, WorkC, WorkYaw, new Vector2(1f, 3.6f), TierCompound + 1.5f,
                new Vector3(4f, 1.6f, 0.3f), _plank);
            Prop("Workshop_ScrapBin", workshop, WorkC, WorkYaw, new Vector2(4f, 3f), TierCompound + 0.5f,
                new Vector3(1.8f, 1f, 1.8f), _metal);

            // --- the service space between them, at its east mouth ----------------
            var service = NewGroup("ServiceSpace", parent);
            Cube("Service_ScrapPile", service, new Vector3(22.6f, TierCompound + 0.5f, 20.6f),
                new Vector3(2.2f, 1f, 1.8f), _metal);
            Cube("Service_PipeRun", service, new Vector3(23.6f, TierCompound + 0.45f, 22.8f),
                new Vector3(1.6f, 0.9f, 2.4f), _metal);
            Cube("Service_SparePartsCrate", service, new Vector3(21.4f, TierCompound + 0.6f, 23.4f),
                new Vector3(1.4f, 1.2f, 1.4f), _plank);

            // ================= WEST: domestic / administrative ====================
            // --- Stores / Radio: closest to the gate, angled to the arrival --------
            var stores = NewGroup("StoresRadio", parent);
            Shell("Stores_Body", stores, StoresC, new Vector2(9f, 7f), 3.8f, StoresYaw, Doorways[2], _wood);
            Roof("Stores_Roof", stores, StoresC, new Vector2(9.6f, 7.6f), 3.8f, 1.2f, _metal, StoresYaw);
            DoorLeaves(stores, Doorways[2], _plank, false);
            Prop("Stores_DoorRecess", stores, StoresC, StoresYaw, new Vector2(5.4f, 1.5f), TierCompound + 2.4f,
                new Vector3(1.4f, 0.2f, 2.4f), _plank);
            Prop("Stores_RadioSet", stores, StoresC, StoresYaw, new Vector2(-3f, -2f), TierCompound + 1.1f,
                new Vector3(1.6f, 1.2f, 0.8f), _metal);
            Prop("Stores_ManifestDesk", stores, StoresC, StoresYaw, new Vector2(-3f, 0f), TierCompound + 0.5f,
                new Vector3(1.6f, 1f, 1.2f), _plank);
            Prop("Cabinet_Ammunition", stores, StoresC, StoresYaw, new Vector2(3f, -2.2f), TierCompound + 0.8f,
                new Vector3(1.8f, 1.6f, 0.8f), _metal);
            Prop("Stores_DeliveryShelf", stores, StoresC, StoresYaw, new Vector2(0f, 2.6f), TierCompound + 0.8f,
                new Vector3(3f, 1.6f, 0.8f), _plank);

            // --- Keeper's House: porch onto the yard, the domestic frontage --------
            var house = NewGroup("KeepersHouse", parent);
            Shell("House_Body", house, HouseC, new Vector2(12f, 9f), 5.5f, HouseYaw, Doorways[3], _wood);
            Roof("House_Roof", house, HouseC, new Vector2(12.6f, 9.6f), 5.5f, 2.6f, _plank, HouseYaw);
            DoorLeaves(house, Doorways[3], _plank, false);
            Prop("House_Porch", house, HouseC, HouseYaw, new Vector2(7.4f, 0f), TierCompound + 0.15f,
                new Vector3(2.8f, 0.3f, 4.2f), _plank);
            Prop("House_PorchCanopy", house, HouseC, HouseYaw, new Vector2(7.4f, 0f), TierCompound + 2.8f,
                new Vector3(2.8f, 0.2f, 4.2f), _plank);
            Prop("House_PorchPost_N", house, HouseC, HouseYaw, new Vector2(8.6f, 1.8f), TierCompound + 1.5f,
                new Vector3(0.2f, 2.6f, 0.2f), _wood);
            Prop("House_PorchPost_S", house, HouseC, HouseYaw, new Vector2(8.6f, -1.8f), TierCompound + 1.5f,
                new Vector3(0.2f, 2.6f, 0.2f), _wood);
            Prop("House_Window_N", house, HouseC, HouseYaw, new Vector2(-5.9f, 3f), TierCompound + 2.4f,
                new Vector3(0.3f, 1.4f, 1.4f), _metal);
            Prop("House_Window_S", house, HouseC, HouseYaw, new Vector2(-5.9f, -3f), TierCompound + 2.4f,
                new Vector3(0.3f, 1.4f, 1.4f), _metal);
            Prop("Cabinet_Medical", house, HouseC, HouseYaw, new Vector2(5.4f, -2f), TierCompound + 0.8f,
                new Vector3(0.8f, 1.6f, 1.6f), _metal);
            Prop("House_StationClock", house, HouseC, HouseYaw, new Vector2(5.4f, 2f), TierCompound + 2.6f,
                new Vector3(0.25f, 0.9f, 0.9f), _plank);
            Prop("House_IncidentBoard", house, HouseC, HouseYaw, new Vector2(5.4f, 3.4f), TierCompound + 1.6f,
                new Vector3(0.2f, 1.4f, 2f), _plank);
            Prop("House_Bunks", house, HouseC, HouseYaw, new Vector2(-3.6f, 2f), TierCompound + 0.6f,
                new Vector3(3.6f, 1.2f, 2f), _wood);

            // --- Courtyard props, kept off every doorway sightline -----------------
            var yardProps = NewGroup("CourtyardProps", parent);
            Cube("Yard_SupplyCrate_A", yardProps, new Vector3(7f, TierCompound + 0.7f, 10.5f),
                new Vector3(1.3f, 1.4f, 1.3f), _plank);
            Cube("Yard_SupplyCrate_B", yardProps, new Vector3(7f, TierCompound + 0.7f, 11.8f),
                new Vector3(1.3f, 1.4f, 1.3f), _plank);
            Cube("Yard_DeliveryCart", yardProps, new Vector3(-4.5f, TierCompound + 0.5f, 21f),
                new Vector3(1.4f, 1f, 2.2f), _wood);
        }

        /// <summary>
        /// Approach from the Inner Gate. The rock spur and retaining edge that used
        /// to pinch it have been removed; the gate posts and the 10 m south end of
        /// the yard polygon carry the compression now.
        /// </summary>
        static void BuildInnerGateThroat(Transform parent)
        {
            var throat = NewGroup("InnerGateThroat", parent);

            // Rock_GateSpur, Rock_GateSpurCap and Yard_RetainSouthWest removed.
            // The throat is now framed by the gate posts and the narrowed south end
            // of the yard polygon alone.
            Cube("Yard_GateLampPost", throat, new Vector3(-7.4f, TierCompound + 1.8f, 9.4f),
                new Vector3(0.2f, 3.6f, 0.2f), _metal);
            Cube("Yard_GateLampHead", throat, new Vector3(-7.4f, TierCompound + 3.7f, 9.4f),
                new Vector3(0.5f, 0.4f, 0.5f), _metal);
        }

        /// <summary>Kerbs and corners following the new irregular yard edge.</summary>
        static void BuildCourtyardEdges(Transform parent)
        {
            var edges = NewGroup("CourtyardEdges", parent);

            Slab("Yard_KerbWest", edges, -10.9f, -10.4f, 13f, 21f,
                TierCompound, TierCompound + 0.35f, _concrete);
            Slab("Yard_KerbEast", edges, 10.1f, 10.6f, 12.5f, 19.5f,
                TierCompound, TierCompound + 0.3f, _concrete);
            Slab("Yard_UtilityCornerNW", edges, -9.5f, -6f, 21.5f, 24.5f,
                TierCompound, TierCompound + 0.25f, _ground);
            Slab("Yard_ServiceStripSE", edges, 4.5f, 8.5f, 8f, 11.5f,
                TierCompound, TierCompound + 0.2f, _ground);
            // Split for the lighthouse stair, which lands between them.
            Slab("Yard_RetainNorth_W", edges, -6f, -2.8f, 24.9f, 25.3f,
                TierCompound, TierCompound + 0.45f, _concrete);
            Slab("Yard_RetainNorth_E", edges, 2.8f, 6f, 24.7f, 25.1f,
                TierCompound, TierCompound + 0.45f, _concrete);

            Cube("Rock_YardEdge_NE", edges, new Vector3(10.8f, TierCompound + 0.2f, 22.6f),
                new Vector3(2.4f, 0.8f, 2f), _rock);
            Cube("Rock_YardEdge_SW", edges, new Vector3(-10.8f, TierCompound + 0.25f, 11f),
                new Vector3(2.8f, 0.9f, 2.4f), _rock);
        }

        // ------------------------------------------- tier 4 lighthouse — UNCHANGED

        // --- Lighthouse shell dimensions -------------------------------------
        // Outer radii are unchanged; the interiors are cut out of them. Every
        // storey shares a wall thickness so the inner face reads as one bore.
        const float TowerWall = 0.4f;
        const float PlinthOuter = 6.2f, PlinthInner = 5.1f;
        const float L1Outer = 5.5f;
        const float ShaftOuter = 4.6f;
        const float L2Outer = 5.0f;
        const float L3Outer = 4.0f;
        /// <summary>Storey levels, from the knoll up.</summary>
        const float OpsFloor = TierLighthouse;          // 21.0, flush with the knoll
        const float L1Top = 26.8f;
        const float ShaftTop = 32.8f;
        const float MechFloor = ShaftTop;               // 32.8
        const float L2Top = 36.8f;
        const float LanternFloor = 37.1f;               // flush with the balcony
        const float L3Top = 40.5f;
        /// <summary>The doorway faces south, down the stair to the compound.</summary>
        const float DoorBearing = 180f;
        const float DoorHalfAngle = 9.5f;
        const float DoorHead = 2.2f;
        const float NewelRadius = 0.5f;

        static void BuildLighthouse(Transform parent)
        {
            float d0 = DoorBearing - DoorHalfAngle;
            float d1 = DoorBearing + DoorHalfAngle;

            // --- plinth: a ring, so the entry is level with the knoll ---------
            // Solid, it presented a 1 m wall to the top of the compound stair.
            RingWall("Lighthouse_Plinth", parent, PlinthInner, PlinthOuter,
                OpsFloor, OpsFloor + 1f, d1, d0 + 360f, _rock);

            // --- Operations: the room the beacon is run from ------------------
            var ops = new List<Vector3>();
            var opsFaces = new List<Face>();
            AppendArcWall(ops, opsFaces, L1Outer - TowerWall, L1Outer, OpsFloor + 1f, L1Top, d1, d0 + 360f);
            // Lintel over the doorway, which is cut from the knoll upward.
            AppendArcWall(ops, opsFaces, L1Outer - TowerWall, L1Outer,
                OpsFloor + DoorHead, L1Top, d0, d1);
            Mesh("Lighthouse_L1_Operations", parent, ops, opsFaces, _concrete);

            // Roof ring closing the gap between the Operations wall and the
            // narrower shaft that rises out of it.
            RingWall("Lighthouse_L1_Roof", parent, ShaftOuter - TowerWall, L1Outer,
                L1Top, L1Top + 0.2f, 0f, 360f, _concrete);

            RingWall("Lighthouse_Shaft", parent, ShaftOuter - TowerWall, ShaftOuter,
                L1Top, ShaftTop, 0f, 360f, _concrete);

            // --- Mechanical -----------------------------------------------------
            RingWall("Lighthouse_L2_Mechanical", parent, L2Outer - TowerWall, L2Outer,
                MechFloor, L2Top, 0f, 360f, _concrete);
            // Floor with a stairwell sector left open where the flight arrives.
            RingWall("Lighthouse_MechanicalFloor", parent, NewelRadius, L2Outer - TowerWall,
                MechFloor - 0.2f, MechFloor, 270f, 120f + 360f, _metal);

            // --- balcony: a ring, so the flight can carry on through it ---------
            BalconyRing(parent, 12.4f, 7.2f, L2Top, L2Top + 0.3f);

            // --- Lantern room ----------------------------------------------------
            var lantern = new List<Vector3>();
            var lanternFaces = new List<Face>();
            AppendArcWall(lantern, lanternFaces, L3Outer - TowerWall, L3Outer,
                LanternFloor - 0.2f, L3Top, d1, d0 + 360f);
            AppendArcWall(lantern, lanternFaces, L3Outer - TowerWall, L3Outer,
                LanternFloor + DoorHead, L3Top, d0, d1);
            Mesh("Lighthouse_L3_LanternRoom", parent, lantern, lanternFaces, _metal);

            RingWall("Lighthouse_LanternFloor", parent, NewelRadius, L3Outer - TowerWall,
                LanternFloor - 0.2f, LanternFloor, 180f, 30f + 360f, _metal);

            Cylinder("Lighthouse_Cap", parent,
                new Vector3(LighthouseXZ.x, L3Top + 0.7f, LighthouseXZ.y), 4.2f, 1.4f, _metal);

            // --- vertical circulation ---------------------------------------------
            Cylinder("Lighthouse_Newel", parent,
                new Vector3(LighthouseXZ.x, (OpsFloor + LanternFloor) / 2f, LighthouseXZ.y),
                NewelRadius, LanternFloor - OpsFloor, _concrete);

            // Winders around the newel. Starting north keeps the first tread clear
            // of anyone stepping in through the south door.
            SpiralStair("Lighthouse_Stair_OpsToMechanical", parent,
                OpsFloor, MechFloor, NewelRadius, 4f, 0f, 630f, 28);
            SpiralStair("Lighthouse_Stair_MechanicalToLantern", parent,
                MechFloor, LanternFloor, NewelRadius, 3.4f, 270f, 270f, 11);

            // --- Operations floor fittings ----------------------------------------
            // Station-wide power routing and the secondary radio. Local generator
            // electrics live in the shed; the routine station radio lives in Stores.
            Cube("Lighthouse_StationSwitchboard", parent,
                new Vector3(-4.5f, OpsFloor + 1.2f, 38f), new Vector3(0.35f, 2f, 2.4f), _metal);
            Cube("Lighthouse_EmergencyRadio", parent,
                new Vector3(4.5f, OpsFloor + 1f, 38f), new Vector3(0.7f, 1.2f, 1.6f), _metal);
            Cube("Lighthouse_BeaconConsole", parent,
                new Vector3(0f, OpsFloor + 0.6f, 42f), new Vector3(2.4f, 1.2f, 0.8f), _metal);

            // Regraded to the broad stair's climb: base pulled back to z 21.6 so the
            // 4 m rise is taken over a 10.4 m run instead of 6 m.
            Stair("Stair_CompoundToLighthouse", parent,
                new Vector3(0f, TierCompound, 21.6f), new Vector3(0f, TierLighthouse, CompoundNorth), 5f);

            Cube("Retain_Knoll_West", parent,
                new Vector3(-7.75f, TierLighthouse + 0.55f, CompoundNorth), new Vector3(8.5f, 1.1f, 0.5f), _concrete);
            Cube("Retain_Knoll_East", parent,
                new Vector3(7.75f, TierLighthouse + 0.55f, CompoundNorth), new Vector3(8.5f, 1.1f, 0.5f), _concrete);
        }

        /// <summary>A hollow cylindrical wall, optionally with an angular gap in it.</summary>
        static ProBuilderMesh RingWall(string name, Transform parent, float innerR, float outerR,
            float yMin, float yMax, float fromDeg, float toDeg, Material material)
        {
            var pos = new List<Vector3>();
            var faces = new List<Face>();
            AppendArcWall(pos, faces, innerR, outerR, yMin, yMax, fromDeg, toDeg);
            return Mesh(name, parent, pos, faces, material);
        }

        /// <summary>
        /// An arc of wall between two radii, as a run of prisms. Bearings are degrees
        /// clockwise from north, so 180 faces the compound.
        /// </summary>
        static void AppendArcWall(List<Vector3> pos, List<Face> faces, float innerR, float outerR,
            float yMin, float yMax, float fromDeg, float toDeg)
        {
            int segments = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(toDeg - fromDeg) / 11.25f));
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, i / (float)segments);
                float a1 = Mathf.Lerp(fromDeg, toDeg, (i + 1) / (float)segments);
                AppendQuadPrism(pos, faces,
                    Bearing(a0, outerR), Bearing(a0, innerR),
                    Bearing(a1, innerR), Bearing(a1, outerR), yMin, yMax);
            }
        }

        /// <summary>Winders around a newel: each tread is one wedge.</summary>
        static ProBuilderMesh SpiralStair(string name, Transform parent, float fromY, float toY,
            float innerR, float outerR, float startDeg, float sweepDeg, int steps)
        {
            float riser = (toY - fromY) / steps;
            float perStep = sweepDeg / steps;
            var pos = new List<Vector3>();
            var faces = new List<Face>();

            for (int i = 0; i < steps; i++)
            {
                float top = fromY + (i + 1) * riser;
                float a0 = startDeg + i * perStep;
                float a1 = a0 + perStep;
                AppendQuadPrism(pos, faces,
                    Bearing(a0, outerR), Bearing(a0, innerR),
                    Bearing(a1, innerR), Bearing(a1, outerR), top - 0.28f, top);
            }

            return Mesh(name, parent, pos, faces, _concrete);
        }

        /// <summary>The gallery deck, built as four beams so the flight passes through.</summary>
        static void BalconyRing(Transform parent, float outer, float inner, float yMin, float yMax)
        {
            float o = outer / 2f, n = inner / 2f;
            var pos = new List<Vector3>();
            var faces = new List<Face>();
            AppendBox(pos, faces, new Vector3(-o, yMin, -o), new Vector3(o, yMax, -n));
            AppendBox(pos, faces, new Vector3(-o, yMin, n), new Vector3(o, yMax, o));
            AppendBox(pos, faces, new Vector3(-o, yMin, -n), new Vector3(-n, yMax, n));
            AppendBox(pos, faces, new Vector3(n, yMin, -n), new Vector3(o, yMax, n));
            Mesh("Lighthouse_Balcony", parent, pos, faces, _metal);
        }

        static Vector2 Bearing(float degrees, float radius) => new Vector2(
            Mathf.Sin(degrees * Mathf.Deg2Rad) * radius,
            Mathf.Cos(degrees * Mathf.Deg2Rad) * radius);

        /// <summary>Finishes an accumulated vertex/face list, centred on the lighthouse.</summary>
        static ProBuilderMesh Mesh(string name, Transform parent, List<Vector3> pos, List<Face> faces,
            Material material)
        {
            var pb = ProBuilderMesh.Create(pos, faces);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(LighthouseXZ.x, 0f, LighthouseXZ.y);
            Finish(pb, material);
            return pb;
        }

        /// <summary>
        /// Extrudes a convex quad, given anticlockwise in XZ, between two heights.
        /// Wound to match AppendBox and PolygonDeck.
        /// </summary>
        static void AppendQuadPrism(List<Vector3> pos, List<Face> faces,
            Vector2 a, Vector2 b, Vector2 c, Vector2 d, float yMin, float yMax)
        {
            int i = pos.Count;
            foreach (var p in new[] { a, b, c, d })
                pos.Add(new Vector3(p.x, yMax, p.y));
            foreach (var p in new[] { a, b, c, d })
                pos.Add(new Vector3(p.x, yMin, p.y));

            faces.Add(new Face(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }));
            faces.Add(new Face(new[] { i + 4, i + 5, i + 6, i + 4, i + 6, i + 7 }));
            for (int k = 0; k < 4; k++)
            {
                int j = (k + 1) % 4;
                faces.Add(new Face(new[] { i + k, i + j, i + 4 + j, i + k, i + 4 + j, i + 4 + k }));
            }
        }

        public static Vector3 LanternCentre =>
            new Vector3(LighthouseXZ.x, TierLighthouse + 1f + 4.8f + 6f + 4f + 1.8f, LighthouseXZ.y);

        // ----------------------------------------------------------------- markers

        static void BuildMarkers(Transform parent)
        {
            const BlockoutMarker.MarkerKind task = BlockoutMarker.MarkerKind.TaskStation;
            const BlockoutMarker.MarkerKind defense = BlockoutMarker.MarkerKind.DefenseSocket;
            const BlockoutMarker.MarkerKind control = BlockoutMarker.MarkerKind.SystemControl;

            // The fuel point tracks the cap, which moved with the generator's approved
            // 90-degree yaw.
            Marker(parent, "Generator_FuelPoint", At(ShedC, ShedYaw, new Vector2(1.1f, 0f), TierCompound + 2.1f), task,
                "Pour fuel can here.");
            // The start point gains from the turn: it used to sit 0.1 m inside the old
            // footprint and is now 0.5 m clear of the door-facing side.
            Marker(parent, "Generator_StartPoint", At(ShedC, ShedYaw, new Vector2(-1f, 0.9f), TierCompound + 1.4f), task,
                "Prime and start.");
            // CANONICAL at -2.2. The yawed generator body spans local z -1.6..1.6, so
            // the original -1.5 fell 0.1 m INSIDE the machine. This stands 0.600 m off
            // the -Z service face: inside the 0.5-0.7 m working band, and it leaves
            // 0.250 m between the player capsule (0.35 m radius) and the generator.
            Marker(parent, "Generator_RepairPoint", At(ShedC, ShedYaw, new Vector2(0.5f, -2.2f), TierCompound + 1.0f), task,
                "Damage repair panel.");
            Marker(parent, "Workshop_Bench", At(WorkC, WorkYaw, new Vector2(-6.6f, -1.2f), TierCompound + 1.1f), task,
                "Trap repair, ammo crafting (GDD 24).");
            // Fuse panel follows the generator: shed electrics are generator-local.
            Marker(parent, "Fuse_Storage", At(ShedC, ShedYaw, new Vector2(3.5f, -1.1f), TierCompound + 1.5f), task,
                "Generator fuse panel. Station-wide routing is in the lighthouse.");
            Marker(parent, "Generator_Breaker", At(ShedC, ShedYaw, new Vector2(3.5f, -2.5f), TierCompound + 1.5f), task,
                "Generator breaker.");

            // --- Stores / Radio Office ------------------------------------------
            Marker(parent, "Ammo_Storage", At(StoresC, StoresYaw, new Vector2(3f, -2.2f), TierCompound + 1.7f), task,
                "Ammunition cabinet, Stores.");
            Marker(parent, "Radio_Point", At(StoresC, StoresYaw, new Vector2(-3f, -2f), TierCompound + 1.7f), control,
                "Routine station radio: manifests, arrivals, weather, dock traffic.");
            Marker(parent, "Manifest_Point", At(StoresC, StoresYaw, new Vector2(-3f, 0f), TierCompound + 1.1f), task,
                "Vessel manifest and expected-arrival records. Verify visitors here.");
            Marker(parent, "Delivery_Records", At(StoresC, StoresYaw, new Vector2(0f, 2.6f), TierCompound + 1.7f), task,
                "Delivery inventory and spare parts.");

            // --- Keeper's House --------------------------------------------------
            Marker(parent, "Medical_Storage", At(HouseC, HouseYaw, new Vector2(5.4f, -2f), TierCompound + 1.7f), task,
                "Medical cabinet, Keeper's House.");
            Marker(parent, "StationClock_Point", At(HouseC, HouseYaw, new Vector2(5.4f, 2f), TierCompound + 2.6f), control,
                "Station clock and shift log.");
            Marker(parent, "IncidentBoard_Point", At(HouseC, HouseYaw, new Vector2(5.4f, 3.4f), TierCompound + 1.6f),
                BlockoutMarker.MarkerKind.Landmark, "Incident board. Story and personal space.");
            Marker(parent, "Bunks_Point", At(HouseC, HouseYaw, new Vector2(-3.6f, 2f), TierCompound + 1.4f),
                BlockoutMarker.MarkerKind.Landmark, "Bunks and rest area.");

            // Inspection happens at the DOCK. The Main Gate is the controlled
            // passage a visitor uses only after players decide to admit them.
            Marker(parent, "Dock_InspectionPoint", new Vector3(2f, TierDock + 1.0f, -40f),
                BlockoutMarker.MarkerKind.Inspection,
                "Inspect sailors and crates here, before anything climbs the path.");

            // --- Main Gate terrace ------------------------------------------------
            Marker(parent, "MainGate_BarricadeSocket", new Vector3(MainGateX, TierOverlook + 0.2f, -17.35f),
                defense, "Primary barricade, in the Main Gate opening.");
            Marker(parent, "MainGate_TrapSocket", new Vector3(6.8f, TierOverlook + 0.2f, -18.2f), defense,
                "Shock trap OUTSIDE the gate. Not team-safe - disarm before admitting anyone.");
            Marker(parent, "MainGate_ControlStand", new Vector3(15f, TierOverlook + 0.2f, -14.6f), control,
                "Operate defences from here. Gate and lower approach stay in view.");
            Marker(parent, "MainGate_SafePassageLane", new Vector3(11f, TierOverlook + 0.2f, -17.3f),
                BlockoutMarker.MarkerKind.Landmark,
                "Lane a legitimate NPC walks once defences are made safe.");

            // Reserved physical locations for defence-state signage (no UI yet).
            Marker(parent, "Indicator_ElectricFence", new Vector3(8.5f, TierOverlook + 1.8f, -12.4f), control,
                "ELECTRIC FENCE: ARMED / SAFE");
            Marker(parent, "Indicator_ShockTrap", new Vector3(16.6f, TierOverlook + 2.9f, -16.2f), control,
                "SHOCK TRAP: ARMED / SAFE");
            Marker(parent, "Indicator_GateState", new Vector3(8.6f, TierOverlook + 3.2f, -14.6f), control,
                "GATE: OPEN / CLOSED");
            Marker(parent, "Indicator_Power", new Vector3(16.6f, TierOverlook + 2.1f, -16.2f), control,
                "POWER: ON / OFF");

            // --- Inner gate, upper compound ---------------------------------------
            Marker(parent, "InnerGate_BarricadeSocket", new Vector3(-6f, TierCompound + 0.2f, CompoundSouth),
                defense, "Secondary containment barrier. No control system of its own.");

            Marker(parent, "ShiftBell_Point", new Vector3(3.8f, TierCompound + 1.4f, 22.4f), control,
                "Ring to end the shift. Outdoors on purpose - a group ritual (GDD 15).");
            Marker(parent, "BeaconControl_Point", new Vector3(0f, TierLighthouse + 1.4f, 41.2f), control,
                "Beacon controls, Operations floor.");
            Marker(parent, "StationPower_Point", new Vector3(-3.9f, TierLighthouse + 1.7f, 38f), control,
                "Station power routing and status. The shed MAKES power; this ROUTES it.");
            Marker(parent, "Radio_Emergency_Point", new Vector3(3.9f, TierLighthouse + 1.5f, 38f), control,
                "Secondary radio: emergency, coast guard, beacon traffic.");

            Marker(parent, "Courtyard_Centre", WpYardCentre + Vector3.up * 0.2f,
                BlockoutMarker.MarkerKind.Landmark, "Main Yard 16 x 14. Keep clear for sightlines.");
            Marker(parent, "Spawn_Player", new Vector3(0f, TierDock + 0.6f, -48f),
                BlockoutMarker.MarkerKind.SpawnPoint, "Blockout walk starts at the dock.");
            Marker(parent, "Entrance_InnerGate", WpCompoundEntrance + Vector3.up * 0.2f,
                BlockoutMarker.MarkerKind.Entrance, "Inner gate into the upper compound.");
            Marker(parent, "Entrance_MainGate", new Vector3(MainGateX, TierOverlook + 0.2f, -17.35f),
                BlockoutMarker.MarkerKind.Entrance, "Primary controlled chokepoint.");
            Marker(parent, "Boat_ArrivalBerth", new Vector3(8.5f, TierDock + 0.6f, -51.2f),
                BlockoutMarker.MarkerKind.Landmark,
                "Vessels come alongside here. 12 m of berthing face.");
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
            ("CAM_Dock",          new Vector3(0f, TierDock + 0.4f + EyeHeight, -48f),      LanternCentre),
            ("CAM_LowerLeft",     new Vector3(-14f, TierLowerAscent + EyeHeight, -28f),    LanternCentre),
            ("CAM_RightTraverse", new Vector3(0f, 6.5f + EyeHeight, -22f),                 LanternCentre),
            ("CAM_Overlook",      new Vector3(15.5f, TierOverlook + EyeHeight, -17.5f),    LanternCentre),
            ("CAM_MainGate",      new Vector3(11.5f, TierOverlook + EyeHeight, -17.3f),
                new Vector3(MainGateX, TierOverlook + 1.5f, -17.35f)),
            ("CAM_TerraceControl", new Vector3(15f, TierOverlook + EyeHeight, -14.6f),
                new Vector3(MainGateX, TierOverlook + 1.5f, -17.35f)),
            ("CAM_FinalAscent",   new Vector3(4f, TierLanding + EyeHeight, -9f),           LanternCentre),
            ("CAM_CompoundEntry", new Vector3(-6f, TierCompound + EyeHeight, 2f),          LanternCentre),
            // Added for the terrain review pass.
            ("CAM_InnerGate",     new Vector3(-4f, TierCompound + EyeHeight, 6f),          LanternCentre),
            ("CAM_Courtyard",     new Vector3(0f, TierCompound + EyeHeight, 17f),          LanternCentre),
            ("CAM_Lighthouse",    new Vector3(0f, TierLighthouse + EyeHeight, 30f),        LanternCentre)
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
            WarmLamp(group, "Lamp_Dock", new Vector3(0f, 3.4f, -43f), 20f, 6f);
            WarmLamp(group, "Lamp_Berth", new Vector3(8.5f, 3.4f, -49.5f), 18f, 5.5f);
            WarmLamp(group, "Lamp_LowerLeft", new Vector3(-14f, 7f, -28f), 18f, 6f);
            WarmLamp(group, "Lamp_Traverse", new Vector3(0f, 9.5f, -22f), 18f, 6f);
            WarmLamp(group, "Lamp_MainGate", new Vector3(10.4f, 12.7f, -19.8f), 22f, 8f);
            WarmLamp(group, "Lamp_TerraceControl", new Vector3(16.3f, 12f, -15.4f), 14f, 5f);
            WarmLamp(group, "Lamp_AscentLanding", new Vector3(1.5f, 14.5f, -8.5f), 16f, 6f);
            WarmLamp(group, "Lamp_CompoundGate", new Vector3(-6f, 20f, 2f), 20f, 8f);

            WarmLamp(group, "Lamp_Yard", new Vector3(0f, TierCompound + 6f, 17f), 32f, 9f);
            // Follow the doorways. These were left at the pre-mirror positions, so
            // every practical lamp was lighting the wrong side of the yard, and
            // Lamp_Stores was emitted twice at the same point.
            // Moved onto the art lamp fixture of SM_GeneratorShed (was 11.2 / +3.6 / 12.6,
            // which sat 0.53 m off the modelled fixture).
            WarmLamp(group, "Lamp_GeneratorShed", new Vector3(11.72f, TierCompound + 3.65f, 12.54f), 16f, 6f);
            WarmLamp(group, "Lamp_Workshop", new Vector3(13.6f, TierCompound + 3.6f, 21.9f), 16f, 6f);
            WarmLamp(group, "Lamp_KeepersHouse", new Vector3(-11.2f, TierCompound + 4.4f, 20.9f), 16f, 5.5f);
            WarmLamp(group, "Lamp_Stores", new Vector3(-11.9f, TierCompound + 3.6f, 8.3f), 16f, 5.5f);

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
            // Soft, not None. With shadows off these practicals shine straight THROUGH
            // the roof fascia and beams, painting a hard-edged warm patch on surfaces
            // that should be occluded -- which reads exactly like a duplicate mesh
            // underneath, but is purely a lighting-rig setting.
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.25f;
        }

        // ------------------------------------------------------------------ player

        static void BuildPlayer(Transform parent)
        {
            var go = new GameObject("BlockoutPlayer (PLACEHOLDER - delete in Phase 2)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, TierDock + 1.2f, -48f);

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

        /// <summary>
        /// Convex polygon deck, extruded downward, built from explicit vertices.
        /// Used where a receiving surface must present an angled edge to a diagonal
        /// ramp: a straight edge meeting a diagonal ramp gives a step that varies
        /// across the seam, while an edge cut parallel to the ramp's iso-height line
        /// gives the same small step everywhere along it.
        /// </summary>
        static ProBuilderMesh PolygonDeck(string name, Transform parent, Vector2[] outline,
            float topY, float depth, Material material)
        {
            int n = outline.Length;
            var positions = new List<Vector3>(n * 2);
            for (int i = 0; i < n; i++)
                positions.Add(new Vector3(outline[i].x, 0f, outline[i].y));
            for (int i = 0; i < n; i++)
                positions.Add(new Vector3(outline[i].x, -depth, outline[i].y));

            var faces = new List<Face>();
            // Top and bottom as triangle fans; the outline is convex by construction.
            for (int i = 1; i < n - 1; i++)
                faces.Add(new Face(new[] { 0, i + 1, i }));
            for (int i = 1; i < n - 1; i++)
                faces.Add(new Face(new[] { n, n + i, n + i + 1 }));
            // Side walls.
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                faces.Add(new Face(new[] { i, j, n + j, i, n + j, n + i }));
            }

            var pb = ProBuilderMesh.Create(positions, faces);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(0f, topY, 0f);
            Finish(pb, material);
            return pb;
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


        /// <summary>
        /// A building as four walls around an open interior, with a doorway cut into
        /// one of them: two jambs and a lintel over the gap. Built as one mesh so the
        /// object keeps its name, its footprint bounds and its yaw.
        /// </summary>
        static ProBuilderMesh Shell(string name, Transform parent, Vector2 centreXZ, Vector2 footprint,
            float height, float yaw, DoorwaySpec door, Material material)
        {
            const float t = 0.3f;
            float hx = footprint.x / 2f, hz = footprint.y / 2f;
            float dz0 = door.LocalZ - door.Width / 2f;
            float dz1 = door.LocalZ + door.Width / 2f;
            bool east = door.LocalX > 0f;
            float doorX = east ? hx - t : -hx;
            float backX = east ? -hx : hx - t;

            var pos = new List<Vector3>();
            var faces = new List<Face>();

            // End walls, inset so they butt against the long walls rather than
            // overlapping them.
            AppendBox(pos, faces, new Vector3(-hx + t, 0f, -hz), new Vector3(hx - t, height, -hz + t));
            AppendBox(pos, faces, new Vector3(-hx + t, 0f, hz - t), new Vector3(hx - t, height, hz));
            AppendBox(pos, faces, new Vector3(backX, 0f, -hz), new Vector3(backX + t, height, hz));

            AppendBox(pos, faces, new Vector3(doorX, 0f, -hz), new Vector3(doorX + t, height, dz0));
            AppendBox(pos, faces, new Vector3(doorX, 0f, dz1), new Vector3(doorX + t, height, hz));
            AppendBox(pos, faces, new Vector3(doorX, door.LintelY, dz0), new Vector3(doorX + t, height, dz1));

            var pb = ProBuilderMesh.Create(pos, faces);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(centreXZ.x, TierCompound, centreXZ.y);
            pb.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Finish(pb, material);
            return pb;
        }

        /// <summary>
        /// Appends one box to a shared vertex/face list, wound the same way as
        /// PolygonDeck: top ring first, then the ring below it.
        /// </summary>
        static void AppendBox(List<Vector3> pos, List<Face> faces, Vector3 min, Vector3 max)
        {
            int b = pos.Count;
            pos.Add(new Vector3(min.x, max.y, min.z));
            pos.Add(new Vector3(max.x, max.y, min.z));
            pos.Add(new Vector3(max.x, max.y, max.z));
            pos.Add(new Vector3(min.x, max.y, max.z));
            for (int i = 0; i < 4; i++)
                pos.Add(new Vector3(pos[b + i].x, min.y, pos[b + i].z));

            faces.Add(new Face(new[] { b, b + 2, b + 1, b, b + 3, b + 2 }));
            faces.Add(new Face(new[] { b + 4, b + 5, b + 6, b + 4, b + 6, b + 7 }));
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                faces.Add(new Face(new[] { b + i, b + j, b + 4 + j, b + i, b + 4 + j, b + 4 + i }));
            }
        }

        /// <summary>
        /// Door leaves standing open into the building, so the opening stays clear and
        /// nothing protrudes into the yard or the service passage.
        /// </summary>
        static void DoorLeaves(Transform parent, DoorwaySpec door, Material material, bool doubleLeaf)
        {
            const float t = 0.3f;
            float inner = door.LocalX > 0f ? door.LocalX - t : door.LocalX + t;
            float swing = door.LocalX > 0f ? -1f : 1f;
            float leaf = doubleLeaf ? door.Width / 2f : door.Width;
            float x = inner + swing * leaf / 2f;
            float y = TierCompound + door.LintelY / 2f;
            var size = new Vector3(leaf, door.LintelY, 0.12f);

            if (doubleLeaf)
            {
                Prop($"{door.Building}_DoorLeaf_A", parent, door.Centre, door.Yaw,
                    new Vector2(x, door.LocalZ - door.Width / 2f), y, size, material);
                Prop($"{door.Building}_DoorLeaf_B", parent, door.Centre, door.Yaw,
                    new Vector2(x, door.LocalZ + door.Width / 2f), y, size, material);
            }
            else
            {
                Prop($"{door.Building}_DoorLeaf", parent, door.Centre, door.Yaw,
                    new Vector2(x, door.LocalZ - door.Width / 2f), y, size, material);
            }
        }

        /// <summary>World position of a point given in a building's local XZ frame.</summary>
        public static Vector3 At(Vector2 centreXZ, float yaw, Vector2 local, float y) =>
            new Vector3(centreXZ.x, y, centreXZ.y) +
            Quaternion.Euler(0f, yaw, 0f) * new Vector3(local.x, 0f, local.y);

        /// <summary>A prop placed and rotated in a building's local frame.</summary>
        static ProBuilderMesh Prop(string name, Transform parent, Vector2 centreXZ, float yaw,
            Vector2 local, float y, Vector3 size, Material material)
        {
            var pb = Cube(name, parent, At(centreXZ, yaw, local, y), size, material);
            pb.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return pb;
        }

        static ProBuilderMesh Roof(string name, Transform parent, Vector2 centreXZ, Vector2 footprint,
            float wallHeight, float roofHeight, Material material, float yaw = 0f)
        {
            var pb = ShapeGenerator.GeneratePrism(PivotLocation.Center,
                new Vector3(footprint.x, roofHeight, footprint.y));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = new Vector3(centreXZ.x, TierCompound + wallHeight + roofHeight / 2f, centreXZ.y);
            pb.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Finish(pb, material);
            return pb;
        }

        /// <summary>
        /// Sloped walkable slab between two points, at any heading. Oriented along
        /// the full 3D direction so diagonal ramps stay flush with the route.
        /// </summary>
        static ProBuilderMesh Ramp(string name, Transform parent, Vector3 from, Vector3 to, float width,
            Material material, bool surfaceOnLine = false)
        {
            Vector3 delta = to - from;
            const float thickness = 0.4f;
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(width, thickness, delta.magnitude));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            // surfaceOnLine drops the slab by half its thickness so its TOP face
            // passes through the waypoints, giving a flush joint with flat decks.
            pb.transform.position = (from + to) / 2f +
                (surfaceOnLine ? rotation * (Vector3.down * (thickness / 2f)) : Vector3.zero);
            pb.transform.rotation = rotation;
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
            // Tread must clear the capsule diameter (0.70 m) and the riser must
            // stay under the controller's step offset (0.45 m).
            float run = horizontal.magnitude;
            int steps = Mathf.Clamp(Mathf.FloorToInt(run / 0.7f), Mathf.CeilToInt(rise / 0.42f), 40);

            var pb = ShapeGenerator.GenerateStair(PivotLocation.Center,
                new Vector3(width, rise, run), steps, true);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = (from + to) / 2f;
            pb.transform.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            Finish(pb, _concrete);
            return pb;
        }

        /// <summary>
        /// Broad rock shoulder sitting flush beneath a path segment, so the path
        /// never floats and the player cannot drop into a seam beside it.
        /// </summary>
        static ProBuilderMesh Shoulder(string name, Transform parent, Vector3 from, Vector3 to,
            float width, float thickness)
        {
            Vector3 delta = to - from;
            var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center,
                new Vector3(width, thickness, delta.magnitude));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            // Ramp slabs are 0.4 thick centred on the line, so their top face sits
            // 0.2 above it. Sit the shoulder 0.08 UNDER that: exactly flush means
            // the next segment's shoulder pokes through the current one at a bend,
            // so it sits 0.6 under and the path slab carries the walking surface.
            pb.transform.position = (from + to) / 2f + rotation * (Vector3.up * (-0.7f - thickness / 2f));
            pb.transform.rotation = rotation;
            Finish(pb, _cliff);
            return pb;
        }

        /// <summary>
        /// Low kerb running alongside a path segment, offset to its seaward side.
        /// Follows the ramp's slope so it never floats or rises to eye height.
        /// </summary>
        /// <summary>
        /// Fills the wedge above a diagonal stair: its south edge lies exactly on
        /// the stair's top edge, so the pad never overhangs the flight below.
        /// </summary>
        static void StairTopPad(Transform parent, Vector3 from, Vector3 to, float width, float depth)
        {
            var dir = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
            var side = new Vector2(dir.z, -dir.x) * (width * 0.5f);
            var top = new Vector2(to.x, to.z);
            var ahead = new Vector2(dir.x, dir.z) * depth;

            PolygonDeck("Ascent_StairTopPad", parent, new[]
            {
                top - side, top + side, top + side + ahead, top - side + ahead
            }, to.y, 1.5f, _ground);
        }

        static ProBuilderMesh Kerb(string name, Transform parent, Vector3 from, Vector3 to, float offset)
        {
            // Inset from both ends so a kerb never overhangs the segment below it
            // at a bend.
            Vector3 full = to - from;
            float inset = Mathf.Min(3f, full.magnitude * 0.3f);
            from += full.normalized * inset;
            to -= full.normalized * inset;

            Vector3 delta = to - from;
            var horizontal = new Vector3(delta.x, 0f, delta.z).normalized;
            // Seaward side is to the path's right when climbing inland.
            var side = new Vector3(horizontal.z, 0f, -horizontal.x);

            Vector3 a = from + side * offset;
            Vector3 b = to + side * offset;

            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center,
                new Vector3(0.35f, 0.45f, (b - a).magnitude));
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.position = (a + b) / 2f + Vector3.up * 0.2f;
            pb.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
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
