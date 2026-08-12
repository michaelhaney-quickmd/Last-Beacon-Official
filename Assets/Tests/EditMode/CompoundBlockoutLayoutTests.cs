using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Tests
{
    /// <summary>
    /// Guards the Phase 1 blockout against the measurable constraints the GDD
    /// states outright (Sections 7, 36). These are layout budgets, not art
    /// opinions — if a hand edit breaks one, the compound has stopped being
    /// compact and the test should fail loudly (GDD Rule 4).
    /// </summary>
    public class CompoundBlockoutLayoutTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Compound_Blockout.unity";

        /// <summary>Matches BlockoutWalker.walkSpeed.</summary>
        const float WalkSpeed = 4.5f;

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static GameObject Find(string name)
        {
            var go = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name);
            Assert.NotNull(go, $"Blockout object '{name}' is missing from the scene.");
            return go.gameObject;
        }

        static Bounds CompoundBounds()
        {
            var walls = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Wall_"))
                .Select(t => t.GetComponent<Renderer>())
                .Where(r => r != null)
                .ToArray();

            Assert.IsNotEmpty(walls, "No perimeter walls found.");
            var bounds = walls[0].bounds;
            foreach (var r in walls.Skip(1))
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        [Test]
        public void CompoundFootprint_IsWithinGddRange()
        {
            var bounds = CompoundBounds();
            // GDD Section 7: "50-70 meters across".
            Assert.That(bounds.size.x, Is.InRange(50f, 70f), "Compound width outside the 50-70 m band.");
            Assert.That(bounds.size.z, Is.InRange(50f, 70f), "Compound depth outside the 50-70 m band.");
        }

        [Test]
        public void CompoundCrossing_TakesEightToFifteenSeconds()
        {
            var bounds = CompoundBounds();
            float crossing = Mathf.Max(bounds.size.x, bounds.size.z) / WalkSpeed;
            // GDD Section 7: "8-15 seconds".
            Assert.That(crossing, Is.InRange(8f, 15f),
                $"Crossing the compound takes {crossing:0.0}s at {WalkSpeed} m/s.");
        }

        [Test]
        public void Dock_IsFifteenToTwentyFiveSecondsFromCompound()
        {
            var courtyard = Find("Courtyard_Centre").transform.position;
            var dock = Find("Task_DockDelivery").transform.position;

            float travel = Vector3.Distance(
                new Vector3(courtyard.x, 0f, courtyard.z),
                new Vector3(dock.x, 0f, dock.z)) / WalkSpeed;

            // GDD Section 7: dock is "15-25 seconds from the compound".
            Assert.That(travel, Is.InRange(15f, 25f),
                $"Dock is {travel:0.0}s from the courtyard at {WalkSpeed} m/s.");
        }

        [Test]
        public void Lighthouse_IsTallEnoughToSeeFromTheDock()
        {
            var parts = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Lighthouse_"))
                .Select(t => t.GetComponent<Renderer>())
                .Where(r => r != null)
                .ToArray();

            Assert.IsNotEmpty(parts, "No lighthouse geometry found.");
            float top = parts.Max(r => r.bounds.max.y);

            // GDD Section 36: the lighthouse should remain visible from most
            // exterior locations. It has to clear every other structure by a margin.
            Assert.That(top, Is.GreaterThan(18f), $"Lighthouse tops out at {top:0.0} m.");
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
        public void VerticalSliceLocations_AllExist()
        {
            // GDD Section 37: exactly what the initial vertical slice needs.
            foreach (var name in new[]
            {
                "House_Body",       // Keeper's House exterior
                "Shed_Body",        // generator shed
                "Workshop_Body",    // workshop
                "Electrical_Body",  // control station
                "Storage_Body",     // storage
                "GateLeaf",         // main gate
                "Jetty_Deck"        // short dock path
            })
            {
                Find(name);
            }
        }

        [Test]
        public void SeaCave_IsNotBuiltYet()
        {
            // GDD Section 37: "No sea cave required initially." Guards scope creep.
            var cave = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name.ToLowerInvariant().Contains("cave"));
            Assert.IsNull(cave, "A sea cave appeared in the vertical-slice blockout.");
        }

        [Test]
        public void Courtyard_StaysClearForSightlines()
        {
            // GDD Section 27: players doing separate jobs should still see each other.
            var courtyard = Find("Courtyard_Centre").transform.position;

            var blockers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.name.EndsWith("_Body") || r.name.StartsWith("Lighthouse_"))
                .Where(r => r.bounds.SqrDistance(courtyard) < 8f * 8f)
                .Select(r => r.name)
                .ToArray();

            Assert.IsEmpty(blockers,
                $"Structures intrude on the central courtyard: {string.Join(", ", blockers)}");
        }
    }
}
