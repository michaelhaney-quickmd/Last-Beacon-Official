using System.Collections.Generic;
using System.Linq;
using LastBeacon.Blockout;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Tests
{
    /// <summary>
    /// Validates the vertical island blockout: elevation hierarchy, the approved
    /// serpentine route, overlook size and placement, path grades, and lighthouse
    /// composition (GDD Rule 4). Numbers are measured from the scene.
    /// </summary>
    public class VerticalIslandBlockoutTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";

        /// <summary>Matches BlockoutWalker.walkSpeed.</summary>
        const float WalkSpeed = 4.5f;
        const float EyeHeight = 1.7f;

        static readonly string[] BuildingBodies =
        {
            "House_Body", "Shed_Body", "Workshop_Body", "Electrical_Body", "Storage_Body"
        };

        static readonly string[] RequiredMarkers =
        {
            "Generator_FuelPoint", "Generator_StartPoint", "Generator_RepairPoint",
            "Workshop_Bench", "Ammo_Storage", "Fuse_Storage", "Medical_Storage",
            "MainGate_InspectionPoint", "MainGate_TrapSocket", "MainGate_BarricadeSocket",
            "ShiftBell_Point", "BeaconControl_Point", "Radio_Point"
        };

        static readonly string[] RequiredCameras =
        {
            "CAM_Dock", "CAM_LowerLeft", "CAM_RightTraverse",
            "CAM_Overlook", "CAM_FinalAscent", "CAM_CompoundEntry"
        };

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Find(string name)
        {
            var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == name);
            Assert.NotNull(t, $"Blockout object '{name}' is missing from the scene.");
            return t.gameObject;
        }

        static Bounds BoundsOf(string name) => Find(name).GetComponent<Renderer>().bounds;

        static Bounds CombinedBounds(System.Func<Renderer, bool> filter, string what)
        {
            var parts = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(filter).ToArray();
            Assert.IsNotEmpty(parts, $"No geometry found for {what}.");

            var bounds = parts[0].bounds;
            foreach (var r in parts.Skip(1))
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static bool LighthouseVisibleFrom(Vector3 eye, out string blocker)
        {
            blocker = null;
            var targets = new[]
            {
                Gen.LanternCentre,
                Gen.LanternCentre + Vector3.up * 2.5f,
                Gen.LanternCentre - Vector3.up * 6f
            };

            foreach (var target in targets)
            {
                Vector3 direction = target - eye;
                if (Physics.Raycast(eye, direction.normalized, out var hit, direction.magnitude))
                {
                    if (hit.collider.name.StartsWith("Lighthouse_"))
                        return true;
                    blocker ??= hit.collider.name;
                }
                else
                {
                    blocker ??= "clear miss";
                }
            }

            return false;
        }

        // -------------------------------------------------------------------- roots

        [Test]
        public void Blockout_HasExactlyOneGeneratedRoot()
        {
            var roots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.name == Gen.RootName)
                .ToArray();

            Assert.AreEqual(1, roots.Length, $"Expected one generated root, found {roots.Length}.");
        }

        [Test]
        public void AllGeometry_LivesUnderTheGeneratedRoot()
        {
            var root = Find(Gen.RootName).transform;
            var strays = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => !r.transform.IsChildOf(root))
                .Select(r => r.name)
                .ToArray();

            Assert.IsEmpty(strays, $"Geometry outside the generated root: {string.Join(", ", strays)}");
        }

        [Test]
        public void Regenerating_CannotStackDuplicateRoots()
        {
            var decoy = new GameObject(Gen.RootName);
            try
            {
                Assert.AreEqual(2, Gen.ClearExistingRoots(), "ClearExistingRoots did not remove every root.");
                Assert.AreEqual(0, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Count(t => t.parent == null && t.name == Gen.RootName), "A root survived the clear.");
            }
            finally
            {
                if (decoy != null)
                    Object.DestroyImmediate(decoy);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
            }
        }

        // ---------------------------------------------------------------- elevation

        [Test]
        public void RouteWaypoints_MatchTheApprovedElevationBands()
        {
            Assert.That(Gen.WpJettyEnd.y, Is.InRange(0f, 1f), "Dock must be at Y 0.");
            Assert.That(Gen.WpLowerLeftTop.y, Is.InRange(3f, 5f), "Lower-left ascent must land in Y 3-5.");
            Assert.That(Gen.WpOverlookEntry.y, Is.InRange(7f, 10f), "Right overlook must land in Y 7-10.");
            Assert.That(Gen.WpCompoundEntrance.y, Is.InRange(14f, 17f), "Compound entrance must land in Y 14-17.");
        }

        [Test]
        public void ElevationHierarchy_IsStrictlyIncreasing()
        {
            float dock = BoundsOf("Dock_Deck").max.y;
            float lowerShelf = BoundsOf("Shelf_LowerLeftPivot").max.y;
            float overlook = BoundsOf("Overlook_Deck").max.y;
            float landing = BoundsOf("Ascent_Landing").max.y;
            float compound = BoundsOf("MainYard").max.y;
            float knoll = BoundsOf("Cliff_BandD_Knoll").max.y;

            Assert.That(dock, Is.LessThan(lowerShelf), "Dock below lower-left shelf.");
            Assert.That(lowerShelf, Is.LessThan(overlook), "Lower-left shelf below overlook.");
            Assert.That(overlook, Is.LessThan(landing), "Overlook below ascent landing.");
            Assert.That(landing, Is.LessThan(compound), "Landing below compound.");
            Assert.That(compound, Is.LessThan(knoll), "Compound below lighthouse base.");

            Assert.That(overlook, Is.EqualTo(9f).Within(0.3f), "Overlook elevation.");
            Assert.That(compound, Is.EqualTo(17f).Within(0.3f), "Compound elevation.");
            Assert.That(knoll, Is.EqualTo(21f).Within(0.3f), "Lighthouse base elevation.");
        }

        [Test]
        public void Lighthouse_IsUnchanged()
        {
            var plinth = BoundsOf("Lighthouse_Plinth");
            Assert.That(plinth.center.x, Is.EqualTo(0f).Within(0.2f), "Lighthouse moved in X.");
            Assert.That(plinth.center.z, Is.EqualTo(38f).Within(0.2f), "Lighthouse moved in Z.");
            Assert.That(plinth.min.y, Is.EqualTo(21f).Within(0.2f), "Lighthouse base elevation changed.");

            var operations = BoundsOf("Lighthouse_L1_Operations");
            Assert.That(Mathf.Max(operations.size.x, operations.size.z), Is.InRange(10f, 12f),
                "Lighthouse exterior diameter changed.");
        }

        // -------------------------------------------------------------------- route

        [Test]
        public void Route_RunsLeftThenRightThenLeft()
        {
            float apron = Gen.WpRampBase.x;
            float lowerLeft = Gen.WpLowerLeftTop.x;
            float overlook = Gen.WpFenceLookout.x;
            float entrance = Gen.WpCompoundEntrance.x;

            Assert.That(lowerLeft, Is.LessThan(apron - 5f),
                $"First beat must swing LEFT: apron x {apron}, lower-left x {lowerLeft}.");
            Assert.That(overlook, Is.GreaterThan(lowerLeft + 20f),
                $"Second beat must swing RIGHT: lower-left x {lowerLeft}, overlook x {overlook}.");
            Assert.That(entrance, Is.LessThan(overlook - 15f),
                $"Third beat must swing back LEFT: overlook x {overlook}, entrance x {entrance}.");
        }

        [Test]
        public void Overlook_IsDirectlyOnThePrimaryRoute()
        {
            // The traverse must end inside the shelf and the ascent must start
            // inside it, so no through-line bypasses the fence.
            var deck = BoundsOf("Overlook_Deck");

            foreach (var (name, p) in new[]
            {
                ("traverse terminus", Gen.WpOverlookEntry),
                ("fence lookout", Gen.WpFenceLookout),
                ("ascent origin", Gen.WpOverlookExit)
            })
            {
                Assert.That(p.x, Is.InRange(deck.min.x, deck.max.x), $"{name} is off the shelf in X.");
                Assert.That(p.z, Is.InRange(deck.min.z, deck.max.z), $"{name} is off the shelf in Z.");
            }
        }

        [Test]
        public void Overlook_StaysCompact()
        {
            var deck = BoundsOf("Overlook_Deck");
            Assert.That(deck.size.x, Is.LessThanOrEqualTo(11.5f), $"Overlook is {deck.size.x:0.0} m wide.");
            Assert.That(deck.size.z, Is.LessThanOrEqualTo(8.5f), $"Overlook is {deck.size.z:0.0} m deep.");
            Assert.That(deck.size.x * deck.size.z, Is.LessThanOrEqualTo(95f),
                "Overlook area exceeds the approved 11 x 8 envelope.");
        }

        [Test]
        public void Overlook_HasItsRequiredFurniture()
        {
            Find("Overlook_FencePost_S_0");
            Find("Overlook_FenceRail_S");
            Find("Overlook_LampPost");
            Find("Overlook_Crate_A");
            Find("Overlook_TrapSocket");
        }

        [Test]
        public void RouteDistance_LandsInTheApprovedBand()
        {
            var route = Gen.Route;
            float toEntrance = 0f, total = 0f;

            for (int i = 1; i < route.Length; i++)
            {
                float leg = Vector3.Distance(route[i - 1], route[i]);
                total += leg;
                if (i < route.Length - 1)
                    toEntrance += leg;
            }

            Assert.That(toEntrance / WalkSpeed, Is.InRange(18f, 23f),
                $"Dock to compound entrance is {toEntrance:0.0} m, {toEntrance / WalkSpeed:0.0}s.");
            Assert.That(total / WalkSpeed, Is.InRange(20f, 24.5f),
                $"Dock to main yard is {total:0.0} m, {total / WalkSpeed:0.0}s.");
        }

        [Test]
        public void PathsAndStairs_AreWideEnough()
        {
            foreach (var name in new[]
            {
                "Path_LowerLeftAscent", "Path_TraverseLeg1", "Path_TraverseLeg2",
                "Path_AscentA_ShortRise", "Stair_AscentBroad", "Path_AscentD_FinalRise"
            })
            {
                var b = BoundsOf(name);
                float width = Mathf.Min(b.size.x, b.size.z);
                Assert.That(width, Is.GreaterThanOrEqualTo(2.9f), $"{name} measures {width:0.0} m across.");
            }
        }

        [Test]
        public void NoRampOrStair_IsTooSteep()
        {
            var segments = new (string Name, Vector3 From, Vector3 To)[]
            {
                ("lower-left ascent", Gen.WpRampBase, Gen.WpLowerLeftTop),
                ("traverse leg 1", Gen.WpLowerLeftTop, Gen.WpTraverseMid),
                ("traverse leg 2", Gen.WpTraverseMid, Gen.WpOverlookEntry),
                ("ascent A", Gen.WpOverlookExit, Gen.WpAscentATop),
                ("broad stairs", Gen.WpLanding, Gen.WpStairsTop),
                ("final rise", Gen.WpStairsTop, Gen.WpCompoundEntrance)
            };

            foreach (var (name, from, to) in segments)
            {
                Vector3 d = to - from;
                float run = new Vector2(d.x, d.z).magnitude;
                float angle = Mathf.Atan2(d.y, run) * Mathf.Rad2Deg;
                Assert.That(angle, Is.LessThanOrEqualTo(32f), $"{name} climbs at {angle:0.0} degrees.");
            }
        }

        [Test]
        public void FinalAscent_IsFourBeatsNotOneStaircase()
        {
            Find("Path_AscentA_ShortRise");
            Find("Ascent_Landing");
            Find("Stair_AscentBroad");
            Find("Path_AscentD_FinalRise");

            var stair = BoundsOf("Stair_AscentBroad");
            float run = new Vector2(stair.size.x, stair.size.z).magnitude;
            Assert.That(run, Is.LessThan(14f), $"Broad stair run is {run:0.0} m — too monumental.");
        }

        [Test]
        public void EveryRouteBeat_HasGeometryUnderIt()
        {
            foreach (var p in Gen.Route)
            {
                bool grounded = Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out _, 4f);
                Assert.IsTrue(grounded, $"Route waypoint {p} has no walkable surface beneath it.");
            }
        }

        // ---------------------------------------------------------------- compound

        [Test]
        public void ApprovedBuildings_AreUnmoved()
        {
            var expected = new Dictionary<string, Vector2>
            {
                { "Shed_Body", new Vector2(-18f, 15.5f) },
                { "Workshop_Body", new Vector2(-18f, 27f) },
                { "House_Body", new Vector2(18f, 20f) },
                { "Electrical_Body", new Vector2(17f, 8f) }
            };

            foreach (var (name, centre) in expected.Select(kv => (kv.Key, kv.Value)))
            {
                var b = BoundsOf(name);
                Assert.That(b.center.x, Is.EqualTo(centre.x).Within(0.3f), $"{name} moved in X.");
                Assert.That(b.center.z, Is.EqualTo(centre.y).Within(0.3f), $"{name} moved in Z.");
            }
        }

        [Test]
        public void Storage_MovedToTheApprovedPosition()
        {
            var b = BoundsOf("Storage_Body");
            Assert.That(b.center.x, Is.EqualTo(-19f).Within(0.5f), "Storage X.");
            Assert.That(b.center.z, Is.EqualTo(12f).Within(0.5f), "Storage Z.");
        }

        [Test]
        public void CompoundEntrance_IsClearOfEveryBuilding()
        {
            var entrance = Gen.WpCompoundEntrance;
            var blockers = BuildingBodies
                .Select(n => (Name: n, B: BoundsOf(n)))
                .Where(x => entrance.x > x.B.min.x - 1.5f && entrance.x < x.B.max.x + 1.5f &&
                            entrance.z > x.B.min.z - 1.5f && entrance.z < x.B.max.z + 1.5f)
                .Select(x => x.Name)
                .ToArray();

            Assert.IsEmpty(blockers, $"Buildings crowd the compound entrance: {string.Join(", ", blockers)}");
        }

        // ----------------------------------------------------------------- markers

        [Test]
        public void RequiredGameplayMarkers_AllExist()
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Select(t => t.name).ToHashSet();
            var missing = RequiredMarkers.Where(n => !all.Contains(n)).ToArray();
            Assert.IsEmpty(missing, $"Missing gameplay markers: {string.Join(", ", missing)}");
        }

        [Test]
        public void GameplayMarkers_AreUnique()
        {
            var duplicates = Object.FindObjectsByType<BlockoutMarker>(FindObjectsSortMode.None)
                .GroupBy(m => m.name).Where(g => g.Count() > 1)
                .Select(g => $"{g.Key} x{g.Count()}").ToArray();

            Assert.IsEmpty(duplicates, $"Duplicate markers: {string.Join(", ", duplicates)}");
        }

        [Test]
        public void ReviewCameraMarkers_AllExistAtEyeHeight()
        {
            foreach (var name in RequiredCameras)
            {
                var cam = Find(name);
                Assert.IsTrue(
                    Physics.Raycast(cam.transform.position + Vector3.up * 0.1f, Vector3.down, out var hit, 5f),
                    $"{name} is not standing above any surface.");

                float height = cam.transform.position.y - hit.point.y;
                Assert.That(height, Is.EqualTo(EyeHeight).Within(0.7f),
                    $"{name} sits {height:0.00} m above its surface.");
            }
        }

        // ------------------------------------------------------------ composition

        [Test]
        public void Lighthouse_IsVisibleFromEveryReviewCamera()
        {
            var blocked = new List<string>();

            foreach (var (name, eye, _) in Gen.ReviewCameras)
            {
                if (!LighthouseVisibleFrom(eye, out var blocker))
                    blocked.Add($"{name} blocked by {blocker}");
            }

            Assert.IsEmpty(blocked, $"Lighthouse not visible: {string.Join("; ", blocked)}");
        }

        [Test]
        public void Lighthouse_IsVisibleFromEveryRouteBeat()
        {
            var blocked = new List<string>();

            foreach (var waypoint in Gen.Route)
            {
                var eye = waypoint + Vector3.up * EyeHeight;
                if (!LighthouseVisibleFrom(eye, out var blocker))
                    blocked.Add($"{waypoint} blocked by {blocker}");
            }

            Assert.IsEmpty(blocked, $"Lighthouse lost on the climb: {string.Join("; ", blocked)}");
        }

        [Test]
        public void Lighthouse_IsTallerThanEveryOtherStructure()
        {
            float top = CombinedBounds(r => r.name.StartsWith("Lighthouse_"), "lighthouse").max.y;

            var taller = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => !r.name.StartsWith("Lighthouse_") && !r.name.StartsWith("Rock_"))
                .Where(r => r.bounds.max.y >= top)
                .Select(r => r.name)
                .ToArray();

            Assert.IsEmpty(taller, $"Structures rival the lighthouse: {string.Join(", ", taller)}");
        }

        // -------------------------------------------------------------------- scope

        [Test]
        public void NoUnityTerrain_IsUsed()
        {
            Assert.IsEmpty(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None),
                "The blockout must not use Unity Terrain yet.");
        }

        [Test]
        public void IslandIsBuiltFromSeparateEditableMeshes()
        {
            var meshes = Object.FindObjectsByType<UnityEngine.ProBuilder.ProBuilderMesh>(FindObjectsSortMode.None);
            Assert.That(meshes.Length, Is.GreaterThan(40),
                "The island looks merged; cliffs, paths, stairs and buildings must stay separate meshes.");
        }

        [Test]
        public void OldTerraceGeometry_IsGone()
        {
            foreach (var name in new[]
            {
                "Cliff_BandA_Base", "Cliff_BandB_Centre", "Cliff_BandC_Centre",
                "Path_LandingSpine", "Path_TerraceSpine", "Stair_LandingToGate",
                "Stair_GateToCompound", "Ramp_MaintenanceRoute"
            })
            {
                Assert.IsNull(
                    Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == name),
                    $"Superseded terrace geometry '{name}' is still in the scene.");
            }
        }
    }
}
