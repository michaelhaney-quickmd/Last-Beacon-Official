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
        /// <summary>Matches the CharacterController on the placeholder walker.</summary>
        const float StepOffset = 0.45f;

        static readonly string[] BuildingBodies =
        {
            "House_Body", "Shed_Body", "Workshop_Body", "Stores_Body"
        };

        static readonly string[] RequiredMarkers =
        {
            "Generator_FuelPoint", "Generator_StartPoint", "Generator_RepairPoint",
            "Workshop_Bench", "Ammo_Storage", "Fuse_Storage", "Medical_Storage",
            "Dock_InspectionPoint", "MainGate_TrapSocket", "MainGate_BarricadeSocket",
            "MainGate_ControlStand", "InnerGate_BarricadeSocket",
            "ShiftBell_Point", "BeaconControl_Point", "Radio_Point",
            "Radio_Emergency_Point", "StationPower_Point", "Manifest_Point"
        };

        static readonly string[] RequiredCameras =
        {
            "CAM_Dock", "CAM_LowerLeft", "CAM_RightTraverse",
            "CAM_Overlook", "CAM_MainGate", "CAM_TerraceControl", "CAM_FinalAscent", "CAM_CompoundEntry",
            "CAM_InnerGate", "CAM_Courtyard", "CAM_Lighthouse"
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
            // Art shells live under LB_ArtProto on purpose: they must survive a
            // regenerate, and they replace only the LOOK of blockout masses whose
            // colliders stay authoritative under the generated root.
            var root = Find(Gen.RootName).transform;
            var artRoot = GameObject.Find("LB_ArtProto");
            var strays = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => !r.transform.IsChildOf(root))
                .Where(r => artRoot == null || !r.transform.IsChildOf(artRoot.transform))
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
            float overlook = BoundsOf("Terrace_Deck").max.y;
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
            var deck = BoundsOf("Terrace_Deck");

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
            var deck = BoundsOf("Terrace_Deck");
            // 11.55 m: the extra 0.55 is the chamfer that receives traverse leg 2,
            // not usable terrace. The working area is unchanged.
            Assert.That(deck.size.x, Is.LessThanOrEqualTo(11.7f), $"Terrace is {deck.size.x:0.0} m wide.");
            Assert.That(deck.size.z, Is.LessThanOrEqualTo(9f), $"Terrace is {deck.size.z:0.0} m deep.");
            Assert.That(deck.size.x * deck.size.z, Is.LessThanOrEqualTo(104f),
                "Overlook area exceeds the approved 11 x 8 envelope.");
        }

        [Test]
        public void Overlook_HasItsRequiredFurniture()
        {
            // Main Gate
            Find("MainGate_Post_South");
            Find("MainGate_Post_North");
            Find("MainGate_Leaf");
            Find("MainGate_BarricadeSocket");
            Find("MainGate_TrapSocket");
            // Electric fence and its power connection
            Find("ElectricFence_Post_0");
            Find("ElectricFence_Rail_Lower");
            Find("ElectricFence_PowerBox");
            Find("ElectricFence_Conduit");
            // Emergency defence / light sub-control
            Find("Control_ConsoleBody");
            Find("Control_Lever");
            Find("Control_Gauge_0");
            Find("Control_WarningLight");
            Find("Control_StatusBoard");
            // Trap bench and short-fence overlook
            Find("TrapBench_Top");
            Find("TrapBench_ToolRack");
            Find("Overlook_FencePost_S_0");
            Find("Overlook_FenceRail_S");
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

            // Measured along the flight, from the lowest vertex to the highest. The
            // bounding box diagonal grows with the stair's WIDTH as well as its run,
            // so it reads a broad diagonal stair as longer than it is.
            var mesh = Find("Stair_AscentBroad").GetComponent<MeshFilter>().sharedMesh;
            var xf = Find("Stair_AscentBroad").transform;
            var world = mesh.vertices.Select(xf.TransformPoint).ToArray();
            var lowest = world.OrderBy(v => v.y).First();
            var highest = world.OrderByDescending(v => v.y).First();
            float run = new Vector2(highest.x - lowest.x, highest.z - lowest.z).magnitude;
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
                { "Shed_Body", new Vector2(17f, 13f) },
                { "Workshop_Body", new Vector2(19f, 26.2f) },
                { "House_Body", new Vector2(-18f, 20.5f) }
            };

            foreach (var (name, centre) in expected.Select(kv => (kv.Key, kv.Value)))
            {
                var b = BoundsOf(name);
                Assert.That(b.center.x, Is.EqualTo(centre.x).Within(0.3f), $"{name} moved in X.");
                Assert.That(b.center.z, Is.EqualTo(centre.y).Within(0.3f), $"{name} moved in Z.");
            }
        }

        [Test]
        public void BuildingRotations_MatchTheApprovedAsymmetry()
        {
            // Three buildings skew, one stays square-on. The square-on one is the
            // Keeper's House, so the domestic building reads as the anchor.
            var expected = new Dictionary<string, float>
            {
                { "Shed_Body", -5f },
                { "Workshop_Body", -17f },
                { "Stores_Body", 18f },
                { "House_Body", -3f }
            };

            foreach (var (name, yaw) in expected.Select(kv => (kv.Key, kv.Value)))
            {
                float actual = Find(name).transform.rotation.eulerAngles.y;
                if (actual > 180f)
                    actual -= 360f;
                Assert.That(actual, Is.EqualTo(yaw).Within(0.5f), $"{name} rotation.");
            }
        }

        [Test]
        public void TheServicePassage_StaysWideEnoughForFourPlayers()
        {
            // Tighter than the courtyard on purpose, but never a pinch point.
            // Measured across the gap at standing height, so the lean-to, its posts
            // and the fuel drums all count against the clear width.
            var shed = BoundsOf("Shed_Body");
            var workshop = BoundsOf("Workshop_Body");

            // Measured from inside the passage outward, not from inside a building:
            // a ray starting within a mesh does not reliably hit its own wall.
            var mid = new Vector3((shed.center.x + workshop.center.x) * 0.5f,
                                  Gen.TierCompound + 1f,
                                  (shed.center.z + workshop.center.z) * 0.5f);
            var across = new Vector3(workshop.center.x - shed.center.x, 0f,
                                     workshop.center.z - shed.center.z).normalized;
            var along = new Vector3(across.z, 0f, -across.x);

            float clear = float.MaxValue;
            for (float offset = -3f; offset <= 3f; offset += 0.5f)
            {
                var at = mid + along * offset;
                if (!Physics.Raycast(at, across, out var toWorkshop, 20f)) continue;
                if (!Physics.Raycast(at, -across, out var toShed, 20f)) continue;
                clear = Mathf.Min(clear, toWorkshop.distance + toShed.distance);
            }

            Assert.That(clear, Is.GreaterThanOrEqualTo(3.2f),
                $"Service passage is {clear:0.00} m clear; 3.2 m is the floor, 3.5 m preferred.");
            Assert.That(clear, Is.LessThan(8f),
                $"Service passage is {clear:0.00} m - it should still feel tighter than the yard.");
        }

        [Test]
        public void TheInnerGateThroat_IsFramedButUnobstructed()
        {
            // The spur and retaining edge were removed, so the throat is measured
            // where the player actually walks rather than between two named objects:
            // cast sideways across the approach at knee height and take the width.
            float throat = float.MaxValue;
            Vector3 narrowestAt = Vector3.zero;
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                var at = Vector3.Lerp(Gen.WpCompoundEntrance, Gen.WpYardCentre, t) + Vector3.up * 0.6f;
                float west = Physics.Raycast(at, Vector3.left, out var wh, 30f) ? wh.distance : 30f;
                float east = Physics.Raycast(at, Vector3.right, out var eh, 30f) ? eh.distance : 30f;
                if (west + east < throat) { throat = west + east; narrowestAt = at; }
            }

            Assert.That(throat, Is.InRange(4.5f, 14f),
                $"Gate approach is {throat:0.0} m wide at {narrowestAt}.");

            // And nothing may sit on the traversal line through it.
            var a = Gen.WpCompoundEntrance;
            var b = Gen.WpYardCentre;
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                var p = Vector3.Lerp(a, b, t);
                foreach (float lateral in new[] { -1.2f, 0f, 1.2f })
                {
                    var dir = (b - a).normalized;
                    var side = new Vector3(dir.z, 0f, -dir.x);
                    var at = p + side * lateral + Vector3.up * 1f;

                    var hits = Physics.OverlapSphere(at, 0.34f)
                        .Where(h => h.name.StartsWith("Rock_Gate") || h.name.StartsWith("Yard_Retain"))
                        .Select(h => h.name)
                        .ToArray();

                    Assert.IsEmpty(hits, $"Throat framing blocks the route at {at}: {string.Join(", ", hits)}");
                }
            }
        }

        [Test]
        public void EveryBuildingEntrance_IsReachableFromTheCourtyard()
        {
            var yard = BoundsOf("MainYard");
            var from = new Vector3(yard.center.x, yard.max.y + 1.6f, yard.center.z);

            foreach (var door in Gen.Doorways)
            {
                // Aim at the doorway itself, a metre out from the threshold.
                var at = door.Threshold(1.2f) + door.Outward * 1f;
                var dir = at - from;

                bool clear = !Physics.Raycast(from, dir.normalized, out var hit, dir.magnitude - 0.4f);
                Assert.IsTrue(clear, $"{door.Building} doorway is not visible from the courtyard centre" +
                                     (clear ? "" : $" - blocked by {hit.collider.name}"));
            }
        }

        [Test]
        public void EveryBuildingDoorway_IsAnActualOpening()
        {
            var blocked = new List<string>();

            foreach (var door in Gen.Doorways)
            foreach (float above in new[] { 0.4f, 1.0f, 1.7f })
            {
                // Straight through the wall plane: 1.2 m outside to 0.6 m inside, so
                // this measures the doorway rather than whatever stands in the room.
                var start = door.Threshold(door.Sill + above) + door.Outward * 1.2f;
                if (Physics.Raycast(start, -door.Outward, out var hit, 1.8f))
                    blocked.Add($"{door.Building} at {above:0.0} m — {hit.collider.name}");
            }

            Assert.IsEmpty(blocked, "Doorways that are not open:\n  " + string.Join("\n  ", blocked));
        }

        [Test]
        public void EveryDoorway_TakesThePlayerCapsule()
        {
            foreach (var door in Gen.Doorways)
            {
                Assert.That(door.Width, Is.GreaterThanOrEqualTo(0.9f),
                    $"{door.Building} doorway is {door.Width:0.00} m wide.");
                Assert.That(door.Height, Is.GreaterThanOrEqualTo(2f),
                    $"{door.Building} doorway is {door.Height:0.00} m high.");

                // A capsule standing in the opening must not intersect anything. It
                // stands on the threshold surface, which may sit above the floor.
                var at = door.Threshold(door.Sill + 0.9f);
                // Anything whose top is within a step of the floor is something you
                // step ONTO — the porch, a kerb, the plateau itself — not an obstruction.
                var hits = Physics.OverlapCapsule(at + Vector3.up * 0.55f, at - Vector3.up * 0.55f, 0.35f)
                    .Where(c => c.bounds.max.y > Gen.TierCompound + door.Sill + StepOffset)
                    .Select(c => c.name)
                    .Distinct()
                    .ToArray();
                Assert.IsEmpty(hits, $"{door.Building} doorway is obstructed by " + string.Join(", ", hits));
            }
        }

        [Test]
        public void EveryDoorway_HasHeadroomAboveWhatYouStandOn()
        {
            // Measured from the surface under the doorway, not from the compound
            // floor: a raised porch or sill eats into the clear height.
            foreach (var door in Gen.Doorways)
            foreach (float across in new[] { -0.3f, 0f, 0.3f })
            {
                var side = Vector3.Cross(Vector3.up, door.Outward).normalized * across;
                var at = door.Threshold(1f) + side;

                Assert.IsTrue(Physics.Raycast(at, Vector3.down, out var floor, 3f),
                    $"{door.Building} doorway has no floor under it.");
                Assert.IsTrue(Physics.Raycast(floor.point + Vector3.up * 0.05f, Vector3.up, out var head, 6f),
                    $"{door.Building} doorway has no lintel above it.");

                float clear = head.point.y - floor.point.y;
                Assert.That(clear, Is.GreaterThanOrEqualTo(2f),
                    $"{door.Building} doorway has {clear:0.00} m clear above {floor.collider.name} " +
                    $"(lintel {head.collider.name}).");
            }
        }

        [Test]
        public void TheLighthouseDoor_IsAnOpeningYouCanWalkThrough()
        {
            // The tower door faces south, at the head of the compound stair. The
            // threshold is level with the knoll, so there is no step to climb.
            foreach (float above in new[] { 0.4f, 1f, 1.7f })
            {
                var start = new Vector3(0f, Gen.TierLighthouse + above, 30f);
                Assert.IsFalse(Physics.Raycast(start, Vector3.forward, out var hit, 4f),
                    $"Lighthouse doorway blocked at {above:0.0} m by " +
                    (hit.collider != null ? hit.collider.name : "nothing"));
            }
        }

        [Test]
        public void TheLighthouseDoor_HasHeadroomFromTheStairTop()
        {
            // Under the wall itself: the plinth in front of it is recessed open.
            var at = new Vector3(0f, Gen.TierLighthouse + 1f, 32.7f);
            Assert.IsTrue(Physics.Raycast(at, Vector3.down, out var floor, 3f),
                "No floor in the lighthouse doorway.");
            Assert.IsTrue(Physics.Raycast(floor.point + Vector3.up * 0.05f, Vector3.up, out var head, 8f),
                "No lintel over the lighthouse doorway.");

            Assert.That(floor.point.y, Is.EqualTo(Gen.TierLighthouse).Within(0.3f),
                $"Doorway threshold sits at {floor.point.y:0.00}, not level with the knoll.");
            Assert.That(head.point.y - floor.point.y, Is.GreaterThanOrEqualTo(2f),
                $"Only {head.point.y - floor.point.y:0.00} m clear under {head.collider.name}.");
        }

        [Test]
        public void EveryLighthouseStorey_HasAFloorAndStandingRoom()
        {
            // Sampled off-axis so the probe misses the newel, and away from the
            // stairwell openings.
            var storeys = new (string Name, float Floor, Vector3 At)[]
            {
                // South-east of the newel: the winders start at north, so the low
                // treads are on the far side of the room from this probe.
                ("Operations", 21f,   new Vector3(3f, 0f, 36.3f)),
                ("Mechanical", 32.8f, new Vector3(2.5f, 0f, 38f)),
                ("Lantern",    37.1f, new Vector3(-2f, 0f, 38f))
            };

            foreach (var (name, expected, at) in storeys)
            {
                var from = new Vector3(at.x, expected + 1.2f, at.z);
                Assert.IsTrue(Physics.Raycast(from, Vector3.down, out var floor, 3f),
                    $"{name} has no floor under {at}.");
                Assert.That(floor.point.y, Is.EqualTo(expected).Within(0.25f),
                    $"{name} floor is at {floor.point.y:0.00}, expected {expected:0.00}.");

                Assert.IsTrue(Physics.Raycast(floor.point + Vector3.up * 0.05f, Vector3.up, out var head, 12f),
                    $"{name} has no ceiling.");
                Assert.That(head.point.y - floor.point.y, Is.GreaterThanOrEqualTo(2f),
                    $"{name} has {head.point.y - floor.point.y:0.00} m of standing room.");
            }
        }

        [Test]
        public void TheLighthouseWinders_AreWalkable()
        {
            // Walked, not just arithmetic: every tread must be there, the step up
            // must be within the controller's reach, and the floor above must not
            // come down on your head before the stairwell opens.
            var flights = new (string Name, float From, float To, float Inner, float Outer,
                float Start, float Sweep, int Steps)[]
            {
                ("Lighthouse_Stair_OpsToMechanical", 21f, 32.8f, 0.5f, 4f, 0f, 630f, 28),
                ("Lighthouse_Stair_MechanicalToLantern", 32.8f, 37.1f, 0.5f, 3.4f, 270f, 270f, 11)
            };

            var problems = new List<string>();

            foreach (var f in flights)
            {
                Find(f.Name);
                var centre = new Vector3(Gen.LighthouseXZ.x, 0f, Gen.LighthouseXZ.y);
                float mid = (f.Inner + f.Outer) / 2f;
                float previous = f.From;
                int samples = f.Steps * 3;

                for (int i = 0; i <= samples; i++)
                {
                    float a = (f.Start + f.Sweep * i / samples) * Mathf.Deg2Rad;
                    var at = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * mid;
                    float expected = Mathf.Lerp(f.From, f.To, i / (float)samples);

                    var tread = Physics.RaycastAll(new Vector3(at.x, expected + 1.5f, at.z), Vector3.down, 3.5f)
                        .Where(h => h.point.y <= expected + 1.2f)
                        .OrderByDescending(h => h.point.y)
                        .ToArray();

                    if (tread.Length == 0)
                    {
                        problems.Add($"{f.Name}: no tread at {a * Mathf.Rad2Deg:0} deg");
                        continue;
                    }

                    float y = tread[0].point.y;
                    if (Mathf.Abs(y - previous) > StepOffset)
                        problems.Add($"{f.Name}: {Mathf.Abs(y - previous):0.00} m step at " +
                                     $"{a * Mathf.Rad2Deg:0} deg");
                    previous = y;

                    if (Physics.Raycast(new Vector3(at.x, y + 0.06f, at.z), Vector3.up, out var head, 12f) &&
                        head.point.y - y < 2f)
                        problems.Add($"{f.Name}: {head.point.y - y:0.00} m headroom at " +
                                     $"{a * Mathf.Rad2Deg:0} deg under {head.collider.name}");
                }
            }

            Assert.IsEmpty(problems.Distinct(), "Winder problems:\n  " +
                string.Join("\n  ", problems.Distinct().Take(8)));
        }

        [Test]
        public void TheOperationsFittings_StandInsideTheRoom()
        {
            var centre = new Vector2(Gen.LighthouseXZ.x, Gen.LighthouseXZ.y);

            foreach (var name in new[]
                     {
                         "Lighthouse_StationSwitchboard", "Lighthouse_EmergencyRadio",
                         "Lighthouse_BeaconConsole", "StationPower_Point",
                         "Radio_Emergency_Point", "BeaconControl_Point"
                     })
            {
                var at = Find(name).transform.position;
                float r = Vector2.Distance(new Vector2(at.x, at.z), centre);

                // Inside the bore, not buried in the wall it used to sit in.
                Assert.That(r, Is.LessThan(5.1f), $"{name} is {r:0.00} m out — inside the wall.");
                Assert.That(at.y, Is.GreaterThan(Gen.TierLighthouse),
                    $"{name} is below the Operations floor.");
            }
        }

        [Test]
        public void TheLighthouseStair_ClimbsAtTheSameGradeAsTheBroadStair()
        {
            float broad = Grade(Gen.WpLanding, Gen.WpStairsTop);
            var stair = BoundsOf("Stair_CompoundToLighthouse");
            float run = stair.size.z;
            float lighthouse = Mathf.Atan2(stair.size.y, run) * Mathf.Rad2Deg;

            Assert.That(lighthouse, Is.EqualTo(broad).Within(1.5f),
                $"Lighthouse stair climbs at {lighthouse:0.0} deg, broad stair at {broad:0.0} deg.");
        }

        static float Grade(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }

        [Test]
        public void StoresRadio_IsOnTheApprovedPlot()
        {
            // Moved onto the former electrical plot: the building nearest the Inner
            // Gate, so manifest and radio answers travel toward the gate and dock.
            var b = BoundsOf("Stores_Body");
            Assert.That(b.center.x, Is.EqualTo(-17.5f).Within(0.5f), "Stores X.");
            Assert.That(b.center.z, Is.EqualTo(8.5f).Within(0.5f), "Stores Z.");
        }

        [Test]
        public void TheCompoundHasFourBuildings_AndNoStandaloneElectrical()
        {
            foreach (var name in BuildingBodies)
                Find(name);

            Assert.IsNull(
                Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == "Electrical_Body"),
                "The standalone electrical building should be gone; the shed makes " +
                "power and the lighthouse routes it.");
        }

        [Test]
        public void NoTwoBuildings_Intersect()
        {
            // Storage once sat 40.5 m2 inside the Generator Shed because no test
            // covered building separation after the flat-compound suite was retired.
            var problems = new List<string>();

            for (int i = 0; i < BuildingBodies.Length; i++)
            for (int j = i + 1; j < BuildingBodies.Length; j++)
            {
                var a = BoundsOf(BuildingBodies[i]);
                var b = BoundsOf(BuildingBodies[j]);

                bool xOver = a.min.x < b.max.x && b.min.x < a.max.x;
                bool zOver = a.min.z < b.max.z && b.min.z < a.max.z;
                if (xOver && zOver)
                {
                    problems.Add($"{BuildingBodies[i]} and {BuildingBodies[j]}");
                }
            }

            Assert.IsEmpty(problems, $"Buildings intersect: {string.Join(", ", problems)}");
        }

        [Test]
        public void TheRadioAndPowerSplits_AreBothPresent()
        {
            // Routine radio in Stores, emergency set in the lighthouse.
            Find("Radio_Point");
            Find("Radio_Emergency_Point");
            // Generator-local electrics in the shed, station routing in the lighthouse.
            Find("Fuse_Storage");
            Find("StationPower_Point");
        }

        [Test]
        public void GeneratorInteractionMarkers_StandOutsideTheMachineAndWithinReach()
        {
            // Regression guard. Yawing the generator 90 degrees widened its footprint
            // across Z and swallowed Generator_RepairPoint, which had been authored
            // against the old footprint. Nothing caught it, because the marker tests
            // only asserted that the names existed. These assert the geometry.
            var body = BoundsOf("Generator_Body");
            const float CapsuleRadius = 0.35f;
            const float MaxReach = 1.2f;

            foreach (var name in new[] { "Generator_StartPoint", "Generator_RepairPoint" })
            {
                var p = Find(name).transform.position;

                // Plan distance to the machine: negative would mean standing in it.
                float dx = Mathf.Max(body.min.x - p.x, p.x - body.max.x);
                float dz = Mathf.Max(body.min.z - p.z, p.z - body.max.z);
                float gap = Mathf.Max(dx, dz);

                Assert.Greater(gap, CapsuleRadius,
                    $"{name} is {gap:0.000} m from the generator in plan — the player " +
                    $"capsule ({CapsuleRadius:0.00} m radius) cannot stand there.");
                Assert.Less(gap, MaxReach,
                    $"{name} is {gap:0.000} m from the generator — too far to be a " +
                    "service position for it.");
            }

            // The fuel point is top access, so it sits over the deck rather than clear
            // of it in plan. What matters is that it is above the machine, not inside.
            var fuel = Find("Generator_FuelPoint").transform.position;
            Assert.Greater(fuel.y, body.max.y,
                $"Generator_FuelPoint sits at y {fuel.y:0.000}, inside a generator that " +
                $"tops out at {body.max.y:0.000}.");
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
        public void TheIslandTerrain_MatchesTheApprovedImportSettings()
        {
            // Replaces NoUnityTerrain_IsUsed. Terrain is now the broad landmass
            // beneath the blockout, so the guard becomes an assertion about the
            // one approved terrain rather than a ban on having any.
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            Assert.That(terrains.Length, Is.EqualTo(1), "Exactly one island terrain is expected.");

            var t = terrains[0];
            Assert.That(t.name, Is.EqualTo("LB_IslandTerrain_Heightmap"), "Terrain name.");
            Assert.That(t.transform.position, Is.EqualTo(new Vector3(-82f, -12f, -82f)), "Terrain origin.");
            Assert.That(t.terrainData.size, Is.EqualTo(new Vector3(160f, 40f, 160f)), "Terrain size.");
            Assert.That(t.terrainData.heightmapResolution, Is.EqualTo(513), "Heightmap resolution.");
            Assert.IsTrue(t.GetComponent<TerrainCollider>() != null &&
                          t.GetComponent<TerrainCollider>().enabled, "TerrainCollider must stay enabled.");
        }

        [Test]
        public void TheTerrain_NeverBecomesTheWalkingSurfaceOnTheRoute()
        {
            // The blockout stays authoritative underfoot. If terrain is ever the top
            // collider on the route, it has poked through a gameplay surface.
            var route = Gen.WalkPath;
            var onTerrain = new List<string>();

            for (int i = 1; i < route.Length; i++)
            {
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    var at = Vector3.Lerp(route[i - 1], route[i], s / (float)steps);
                    var top = Physics.RaycastAll(at + Vector3.up * 2f, Vector3.down, 6f)
                        .OrderByDescending(h => h.point.y)
                        .FirstOrDefault();
                    if (top.collider is TerrainCollider)
                        onTerrain.Add($"seg{i} ({at.x:0.0},{at.z:0.0}) stands on terrain at {top.point.y:0.00}");
                }
            }

            Assert.IsEmpty(onTerrain.Distinct(), "Terrain is the walking surface at:\n  " +
                string.Join("\n  ", onTerrain.Distinct().Take(8)));
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
