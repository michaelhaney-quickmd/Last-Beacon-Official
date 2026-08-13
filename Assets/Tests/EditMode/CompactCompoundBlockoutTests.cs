using System.Collections.Generic;
using System.Linq;
using LastBeacon.Blockout;
using LastBeacon.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Tests
{
    /// <summary>
    /// Validates the Phase 1 compact compound blockout against the constraints the
    /// GDD states outright (Sections 7, 8, 36, 37) and the circulation budgets the
    /// layout was designed to (GDD Rule 4). These are measurements, not art
    /// opinions — if a hand edit breaks one, the compound has stopped being
    /// compact or readable and the test should fail loudly.
    /// </summary>
    public class CompactCompoundBlockoutTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Compound_Blockout.unity";

        /// <summary>Matches BlockoutWalker.walkSpeed.</summary>
        const float WalkSpeed = 4.5f;

        /// <summary>Narrow maintenance passage floor, in metres.</summary>
        const float MinimumPassage = 2f;

        const float PlayerEyeHeight = 1.7f;

        /// <summary>The five box-shaped buildings, by their body mesh names.</summary>
        static readonly string[] BuildingBodies =
        {
            "House_Body", "Shed_Body", "Workshop_Body", "Electrical_Body", "Storage_Body"
        };

        static readonly string[] RequiredMarkers =
        {
            "Generator_FuelPoint",
            "Generator_StartPoint",
            "Generator_RepairPoint",
            "Workshop_Bench",
            "Ammo_Storage",
            "Fuse_Storage",
            "Medical_Storage",
            "MainGate_InspectionPoint",
            "MainGate_TrapSocket",
            "MainGate_BarricadeSocket",
            "ShiftBell_Point",
            "BeaconControl_Point",
            "Radio_Point"
        };

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Edit-mode raycasts read stale transforms without this.
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

        static Bounds LighthouseBounds()
        {
            var parts = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Lighthouse_"))
                .ToArray();
            Assert.IsNotEmpty(parts, "No lighthouse geometry found.");

            var bounds = parts[0].bounds;
            foreach (var r in parts.Skip(1))
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static Bounds CompoundBounds()
        {
            var walls = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Wall_"))
                .ToArray();
            Assert.IsNotEmpty(walls, "No perimeter walls found.");

            var bounds = walls[0].bounds;
            foreach (var r in walls.Skip(1))
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static bool RangesOverlap(float aMin, float aMax, float bMin, float bMax)
            => aMin < bMax && bMin < aMax;

        // -------------------------------------------------------------------- roots

        [Test]
        public void Blockout_HasExactlyOneGeneratedRoot()
        {
            var roots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.name == CompactCompoundBlockoutGenerator.RootName)
                .ToArray();

            Assert.AreEqual(1, roots.Length,
                $"Expected exactly one '{CompactCompoundBlockoutGenerator.RootName}' root, found {roots.Length}.");
        }

        [Test]
        public void AllBlockoutGeometry_LivesUnderTheGeneratedRoot()
        {
            var root = Find(CompactCompoundBlockoutGenerator.RootName).transform;

            var strays = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => !r.transform.IsChildOf(root))
                .Select(r => r.name)
                .ToArray();

            Assert.IsEmpty(strays,
                $"Geometry outside the generated root: {string.Join(", ", strays)}");
        }

        [Test]
        public void Regenerating_CannotStackDuplicateRoots()
        {
            // Prove the generator clears prior output instead of appending to it.
            var decoy = new GameObject(CompactCompoundBlockoutGenerator.RootName);
            try
            {
                int before = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Count(t => t.parent == null && t.name == CompactCompoundBlockoutGenerator.RootName);
                Assert.AreEqual(2, before, "Test setup failed to create a duplicate root.");

                int removed = CompactCompoundBlockoutGenerator.ClearExistingRoots();
                Assert.AreEqual(2, removed, "ClearExistingRoots did not remove every root.");

                int after = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Count(t => t.parent == null && t.name == CompactCompoundBlockoutGenerator.RootName);
                Assert.AreEqual(0, after, "A generated root survived the clear.");
            }
            finally
            {
                if (decoy != null)
                    Object.DestroyImmediate(decoy);
                // The scene is reopened for the remaining tests by OneTimeSetUp ordering;
                // reopen explicitly so this destructive test cannot leak.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
            }
        }

        // ------------------------------------------------------------------ spaces

        [Test]
        public void RequiredSpaces_AllExist()
        {
            foreach (var name in new[]
            {
                "Lighthouse_L1_Operations",
                "House_Body",
                "Shed_Body",
                "Workshop_Body",
                "Electrical_Body",
                "Storage_Body",
                "MainYard",
                "GateLeaf"
            })
            {
                Find(name);
            }
        }

        [Test]
        public void Lighthouse_HasThreeFunctionalLayers()
        {
            // GDD Section 8: ground/operations, mechanical, lantern room.
            Find("Lighthouse_L1_Operations");
            Find("Lighthouse_L2_Mechanical");
            Find("Lighthouse_L3_LanternRoom");
        }

        [Test]
        public void Lighthouse_ExteriorDiameterIsTenToTwelveMetres()
        {
            var operations = BoundsOf("Lighthouse_L1_Operations");
            float diameter = Mathf.Max(operations.size.x, operations.size.z);
            Assert.That(diameter, Is.InRange(10f, 12f), $"Lighthouse diameter is {diameter:0.0} m.");
        }

        [Test]
        public void BuildingFootprints_MatchSpec()
        {
            var expected = new Dictionary<string, Vector2>
            {
                { "House_Body", new Vector2(12f, 9f) },
                { "Shed_Body", new Vector2(10f, 8f) },
                { "Workshop_Body", new Vector2(12f, 9f) },
                { "Electrical_Body", new Vector2(8f, 7f) },
                { "Storage_Body", new Vector2(10f, 8f) }
            };

            foreach (var (name, size) in expected.Select(kv => (kv.Key, kv.Value)))
            {
                var bounds = BoundsOf(name);
                Assert.That(bounds.size.x, Is.EqualTo(size.x).Within(0.5f), $"{name} width.");
                Assert.That(bounds.size.z, Is.EqualTo(size.y).Within(0.5f), $"{name} depth.");
            }
        }

        [Test]
        public void MainYard_IsAtLeastTwentyByEighteen()
        {
            var yard = BoundsOf("MainYard");
            Assert.That(yard.size.x, Is.GreaterThanOrEqualTo(20f - 0.5f), "Main Yard width.");
            Assert.That(yard.size.z, Is.GreaterThanOrEqualTo(18f - 0.5f), "Main Yard depth.");
        }

        [Test]
        public void MainYard_StaysClearOfEveryBuilding()
        {
            var yard = BoundsOf("MainYard");
            var intruders = new List<string>();

            foreach (var name in BuildingBodies.Concat(new[] { "Lighthouse_Plinth" }))
            {
                var b = BoundsOf(name);
                if (RangesOverlap(b.min.x, b.max.x, yard.min.x, yard.max.x) &&
                    RangesOverlap(b.min.z, b.max.z, yard.min.z, yard.max.z))
                {
                    intruders.Add(name);
                }
            }

            Assert.IsEmpty(intruders,
                $"Structures intrude on the Main Yard: {string.Join(", ", intruders)}");
        }

        // --------------------------------------------------------------- footprint

        [Test]
        public void CompoundFootprint_IsWithinTargetRange()
        {
            var bounds = CompoundBounds();
            // Task spec: 55-65 m. GDD Section 7: 50-70 m.
            Assert.That(bounds.size.x, Is.InRange(55f, 65f), "Compound width.");
            Assert.That(bounds.size.z, Is.InRange(55f, 65f), "Compound depth.");
        }

        [Test]
        public void CompoundCrossing_TakesEightToFifteenSeconds()
        {
            var bounds = CompoundBounds();
            float crossing = Mathf.Max(bounds.size.x, bounds.size.z) / WalkSpeed;
            Assert.That(crossing, Is.InRange(8f, 15f),
                $"Crossing the compound takes {crossing:0.0}s at {WalkSpeed} m/s.");
        }

        [Test]
        public void Dock_IsFifteenToTwentyFiveSecondsFromCompound()
        {
            var yard = Find("Courtyard_Centre").transform.position;
            var dock = Find("Task_DockDelivery").transform.position;

            float travel = Vector3.Distance(
                new Vector3(yard.x, 0f, yard.z),
                new Vector3(dock.x, 0f, dock.z)) / WalkSpeed;

            Assert.That(travel, Is.InRange(15f, 25f),
                $"Dock is {travel:0.0}s from the yard at {WalkSpeed} m/s.");
        }

        // -------------------------------------------------------------- circulation

        [Test]
        public void WalkwaysBetweenBuildings_ClearTheMinimumPassage()
        {
            var boxes = BuildingBodies
                .Select(n => (Name: n, Bounds: BoundsOf(n)))
                .Append((Name: "Lighthouse", Bounds: BoundsOf("Lighthouse_Plinth")))
                .ToArray();

            var failures = new List<string>();

            for (int i = 0; i < boxes.Length; i++)
            {
                for (int j = i + 1; j < boxes.Length; j++)
                {
                    var a = boxes[i];
                    var b = boxes[j];

                    bool xOverlap = RangesOverlap(a.Bounds.min.x, a.Bounds.max.x, b.Bounds.min.x, b.Bounds.max.x);
                    bool zOverlap = RangesOverlap(a.Bounds.min.z, a.Bounds.max.z, b.Bounds.min.z, b.Bounds.max.z);

                    if (xOverlap && zOverlap)
                    {
                        failures.Add($"{a.Name} and {b.Name} intersect");
                        continue;
                    }

                    // Only the axis they share needs a passage measured across it.
                    if (xOverlap)
                    {
                        float gap = Mathf.Max(a.Bounds.min.z, b.Bounds.min.z) - Mathf.Min(a.Bounds.max.z, b.Bounds.max.z);
                        if (gap < MinimumPassage)
                            failures.Add($"{a.Name}-{b.Name} north/south gap {gap:0.0} m");
                    }
                    else if (zOverlap)
                    {
                        float gap = Mathf.Max(a.Bounds.min.x, b.Bounds.min.x) - Mathf.Min(a.Bounds.max.x, b.Bounds.max.x);
                        if (gap < MinimumPassage)
                            failures.Add($"{a.Name}-{b.Name} east/west gap {gap:0.0} m");
                    }
                }
            }

            Assert.IsEmpty(failures, $"Passages below {MinimumPassage} m: {string.Join("; ", failures)}");
        }

        [Test]
        public void WalkwaysBehindBuildings_ClearTheMinimumPassage()
        {
            // The rear walkways are what close the two circulation loops.
            var compound = CompoundBounds();
            var failures = new List<string>();

            foreach (var name in BuildingBodies.Concat(new[] { "Lighthouse_Plinth" }))
            {
                var b = BoundsOf(name);
                var gaps = new[]
                {
                    ("west", b.min.x - compound.min.x),
                    ("east", compound.max.x - b.max.x),
                    ("south", b.min.z - compound.min.z),
                    ("north", compound.max.z - b.max.z)
                };

                foreach (var (side, gap) in gaps)
                {
                    if (gap < MinimumPassage)
                        failures.Add($"{name} {side} gap {gap:0.0} m");
                }
            }

            Assert.IsEmpty(failures, $"Wall clearances below {MinimumPassage} m: {string.Join("; ", failures)}");
        }

        [Test]
        public void MainGate_OpeningIsFourToFiveMetresWide()
        {
            var west = BoundsOf("Wall_S_West");
            var east = BoundsOf("Wall_S_East");
            float opening = east.min.x - west.max.x;

            Assert.That(opening, Is.InRange(4f, 5f), $"Main gate opening is {opening:0.00} m.");
        }

        [Test]
        public void Doorways_ClearTheMinimumDoorWidth()
        {
            const float minimumDoor = 1.2f;

            foreach (var name in new[] { "House_Door", "Shed_Door", "Workshop_Door", "Storage_Door", "Electrical_Door" })
            {
                var b = BoundsOf(name);
                // Doors sit on either a north/south or east/west facing wall.
                float width = Mathf.Max(b.size.x, b.size.z);
                Assert.That(width, Is.GreaterThanOrEqualTo(minimumDoor - 0.01f),
                    $"{name} is {width:0.00} m wide.");
            }
        }

        // ------------------------------------------------------------------ markers

        [Test]
        public void RequiredGameplayMarkers_AllExist()
        {
            var missing = RequiredMarkers
                .Where(n => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .All(t => t.name != n))
                .ToArray();

            Assert.IsEmpty(missing, $"Missing gameplay markers: {string.Join(", ", missing)}");
        }

        [Test]
        public void RequiredGameplayMarkers_AreLabelledBlockoutMarkers()
        {
            foreach (var name in RequiredMarkers)
            {
                var go = Find(name);
                Assert.NotNull(go.GetComponent<BlockoutMarker>(),
                    $"Marker '{name}' has no BlockoutMarker component, so it draws no editor label (Rule 9).");
            }
        }

        [Test]
        public void GameplayMarkers_AreUnique()
        {
            var duplicates = Object.FindObjectsByType<BlockoutMarker>(FindObjectsSortMode.None)
                .GroupBy(m => m.name)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key} x{g.Count()}")
                .ToArray();

            Assert.IsEmpty(duplicates, $"Duplicate markers: {string.Join(", ", duplicates)}");
        }

        // ------------------------------------------------------- line of sight

        [Test]
        public void Lighthouse_IsVisibleFromEveryKeyWorkLocation()
        {
            // GDD Section 36: the lighthouse should remain visible from most
            // exterior locations. Raycast rather than infer it from height.
            var lantern = BoundsOf("Lighthouse_L3_LanternRoom").center;

            var viewpoints = new (string Name, Vector3 Position)[]
            {
                ("main yard", new Vector3(0f, PlayerEyeHeight, 0f)),
                ("gate area", new Vector3(0f, PlayerEyeHeight, -27f)),
                ("workshop entrance", new Vector3(-19f, PlayerEyeHeight, 3f)),
                ("generator entrance", new Vector3(13.5f, PlayerEyeHeight, -2f))
            };

            var blocked = new List<string>();

            foreach (var (name, position) in viewpoints)
            {
                Vector3 direction = lantern - position;
                if (Physics.Raycast(position, direction.normalized, out var hit, direction.magnitude))
                {
                    if (!hit.collider.name.StartsWith("Lighthouse_"))
                        blocked.Add($"{name} blocked by {hit.collider.name}");
                }
                else
                {
                    blocked.Add($"{name} hit nothing (expected the lighthouse)");
                }
            }

            Assert.IsEmpty(blocked, $"Lighthouse not visible: {string.Join("; ", blocked)}");
        }

        [Test]
        public void Lighthouse_IsTallerThanEveryOtherStructure()
        {
            float lighthouseTop = LighthouseBounds().max.y;

            var taller = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => !r.name.StartsWith("Lighthouse_") && !r.name.StartsWith("Rock_"))
                .Where(r => r.bounds.max.y >= lighthouseTop)
                .Select(r => r.name)
                .ToArray();

            Assert.IsEmpty(taller, $"Structures rival the lighthouse: {string.Join(", ", taller)}");
            Assert.That(lighthouseTop, Is.GreaterThan(18f), $"Lighthouse tops out at {lighthouseTop:0.0} m.");
        }

        // -------------------------------------------------------------------- scope

        [Test]
        public void SeaCave_IsNotBuiltYet()
        {
            // GDD Section 37: "No sea cave required initially." Guards scope creep.
            var cave = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name.ToLowerInvariant().Contains("cave"));
            Assert.IsNull(cave, "A sea cave appeared in the vertical-slice blockout.");
        }
    }
}
