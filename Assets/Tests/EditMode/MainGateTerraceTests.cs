using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Tests
{
    /// <summary>
    /// Validates the Main Gate defensive terrace: footprint, the corridor clearance
    /// budget, whether a legitimate NPC can walk the gate when defences are safe,
    /// whether four players can work the terrace without blocking the through-route,
    /// and the control-readability sightlines.
    /// </summary>
    public class MainGateTerraceTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";

        // Mirrors BuildPlayer's CharacterController.
        const float CapsuleRadius = 0.35f;
        const float CapsuleHeight = 1.8f;
        const float EyeHeight = 1.7f;

        /// <summary>Required clear margin beyond the path edge, per the brief.</summary>
        const float RequiredMargin = 1.5f;

        static GameObject _player;

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None)
                .FirstOrDefault()?.gameObject;
            if (_player != null)
                _player.SetActive(false);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void RestorePlayer()
        {
            if (_player != null)
                _player.SetActive(true);
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Find(string name)
        {
            var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == name);
            Assert.NotNull(t, $"'{name}' is missing from the scene.");
            return t.gameObject;
        }

        static Bounds BoundsOf(string name) => Find(name).GetComponent<Renderer>().bounds;

        /// <summary>True if a standing capsule at this spot touches nothing solid.</summary>
        static bool CapsuleClear(Vector3 footCentre, out string blocker, params string[] ignorePrefixes)
        {
            blocker = null;
            float r = CapsuleRadius - 0.02f;
            var bottom = footCentre + Vector3.up * (r + 0.08f);
            var top = bottom + Vector3.up * (CapsuleHeight - 2f * r);

            foreach (var hit in Physics.OverlapCapsule(bottom, top, r))
            {
                if (ignorePrefixes.Any(pre => hit.name.StartsWith(pre)))
                    continue;
                // Anything whose top is within a step of the floor is walkable over.
                if (hit.bounds.max.y - footCentre.y <= 0.45f)
                    continue;
                blocker = hit.name;
                return false;
            }

            return true;
        }

        static float? GroundAt(Vector3 point, float expectedY)
        {
            var origin = new Vector3(point.x, expectedY + 3f, point.z);
            // Take the highest surface within the walkable band. Anything above head
            // height is an overhead run, not the floor.
            float ceiling = expectedY + 1.2f;
            float? best = null;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 8f))
            {
                if (hit.point.y > ceiling)
                    continue;
                if (best == null || hit.point.y > best.Value)
                    best = hit.point.y;
            }
            return best;
        }

        /// <summary>
        /// Line of sight to a point. Hitting the object you are looking for counts
        /// as seeing it — pass its name prefix in <paramref name="targetPrefixes"/>.
        /// </summary>
        static bool CanSee(Vector3 eye, Vector3 target, out string blocker, params string[] targetPrefixes)
        {
            blocker = null;
            Vector3 dir = target - eye;
            if (!Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude))
                return true;
            if (targetPrefixes.Any(pre => hit.collider.name.StartsWith(pre)))
                return true;
            blocker = hit.collider.name;
            return false;
        }

        // ---------------------------------------------------------------- footprint

        [Test]
        public void Terrace_IsWithinTheApprovedFootprint()
        {
            var deck = BoundsOf("Terrace_Deck");
            Assert.That(deck.size.x, Is.InRange(10f, 14f), $"Terrace is {deck.size.x:0.0} m wide.");
            Assert.That(deck.size.z, Is.InRange(7f, 10f), $"Terrace is {deck.size.z:0.0} m deep.");
        }

        [Test]
        public void Terrace_IsBoundedAndNotAnOpenBench()
        {
            // The deck used to be a painted slab on a much larger walkable bench.
            var deck = BoundsOf("Terrace_Deck");
            var bench = BoundsOf("Cliff_OverlookBench");

            Assert.That(bench.max.x, Is.LessThanOrEqualTo(deck.max.x + 0.6f),
                "Walkable bench extends east beyond the terrace.");
            Find("Rock_TerraceEast");
            Find("Rock_TerraceNorth");
            Find("Rock_MainGateInfill");
        }

        [Test]
        public void MainGate_OpeningIsFourToFiveMetres()
        {
            var south = BoundsOf("MainGate_Post_South");
            var north = BoundsOf("MainGate_Post_North");
            float opening = north.min.z - south.max.z;
            Assert.That(opening, Is.InRange(4f, 5f), $"Main Gate opening is {opening:0.00} m.");
        }

        [Test]
        public void MainGate_IsOnThePrimaryRoute()
        {
            // The route must pass through the opening, not around it.
            var south = BoundsOf("MainGate_Post_South");
            var north = BoundsOf("MainGate_Post_North");

            var entry = Gen.OverlookDeckEdge;
            var inside = Gen.WpOverlookEntry;
            Assert.That(entry.x, Is.LessThan(Gen.MainGateX), "Route should enter from outside the gate line.");
            Assert.That(inside.x, Is.GreaterThan(Gen.MainGateX), "Route should continue inside the gate line.");

            // Where the route crosses the gate line, it must be within the opening.
            float t = (Gen.MainGateX - entry.x) / (inside.x - entry.x);
            float crossingZ = Mathf.Lerp(entry.z, inside.z, t);
            Assert.That(crossingZ, Is.InRange(south.max.z, north.min.z),
                $"Route crosses the gate line at z {crossingZ:0.00}, outside the opening.");
        }

        [Test]
        public void InnerGate_IsLighterThanTheMainGate()
        {
            float mainPost = BoundsOf("MainGate_Post_South").size.y;
            float innerPost = BoundsOf("InnerGate_Post_West").size.y;

            Assert.That(innerPost, Is.LessThan(mainPost),
                "The Inner Gate should not be as visually dominant as the Main Gate.");
            Assert.IsNull(
                Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(x => x.name == "InnerGate_Lintel"),
                "The Inner Gate should stay a simple barrier, with no lintel.");

            // And it must not carry a second full defensive control system.
            var innerControls = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(x => x.name.StartsWith("Control_"))
                .Where(x => x.position.z > Gen.WpCompoundEntrance.z - 6f)
                .Select(x => x.name)
                .ToArray();
            Assert.IsEmpty(innerControls,
                $"Defence controls duplicated at the Inner Gate: {string.Join(", ", innerControls)}");
        }

        // ---------------------------------------------------------------- clearance

        [Test]
        public void AscentLaunch_HasTheRequiredClearanceFromRock()
        {
            // This is the pinch the brief called out: the old NW-corner launch left
            // 0.31 m. Measure the ramp's own footprint against the flanking rock.
            var ramp = BoundsOf("Path_AscentA_ShortRise");
            var northRock = BoundsOf("Rock_TerraceNorth");
            var infill = BoundsOf("Rock_MainGateInfill");

            float throat = northRock.min.x - infill.max.x;
            float pathWidth = 4f;
            float marginEachSide = (throat - pathWidth) / 2f;

            Assert.That(marginEachSide, Is.GreaterThanOrEqualTo(RequiredMargin - 0.05f),
                $"Exit throat is {throat:0.00} m wide — only {marginEachSide:0.00} m clear " +
                $"either side of a {pathWidth} m path.");

            // And the launch itself must sit centred in that throat.
            float centre = (infill.max.x + northRock.min.x) * 0.5f;
            Assert.That(Gen.WpOverlookExit.x, Is.EqualTo(centre).Within(0.75f),
                $"Ascent A launches at x {Gen.WpOverlookExit.x}, off-centre in a throat centred on {centre:0.0}.");
        }

        [Test]
        public void EveryPathSegment_IsAboutFourMetresWide()
        {
            foreach (var name in new[]
            {
                "Path_IntroRamp", "Path_LowerLeftAscent", "Path_TraverseLeg1",
                "Path_TraverseLeg2", "Path_AscentA_ShortRise", "Path_AscentD_FinalRise"
            })
            {
                var b = BoundsOf(name);
                float width = Mathf.Min(b.size.x, b.size.z);
                Assert.That(width, Is.GreaterThanOrEqualTo(3.4f),
                    $"{name} is {width:0.0} m wide — corridor feels pinched for 4-player co-op.");
            }
        }

        // -------------------------------------------------------- NPC safe passage

        [Test]
        public void ALegitimateNpc_CanWalkTheGateWhenDefencesAreSafe()
        {
            // Walk a player-sized capsule along the admitted-visitor lane and assert
            // it never contacts fence or trap blockout geometry. Defences "safe"
            // means disarmed, not removed — the hardware is still physically there.
            var lane = new[]
            {
                new Vector3(6.2f, Gen.TierOverlook, -18.1f),
                new Vector3(Gen.MainGateX, Gen.TierOverlook, -17.6f),
                new Vector3(11f, Gen.TierOverlook, -17.3f),
                new Vector3(14f, Gen.TierOverlook, -17.4f),
                Gen.WpFenceLookout
            };

            var contacts = new List<string>();

            for (int i = 1; i < lane.Length; i++)
            {
                int steps = Mathf.CeilToInt(Vector3.Distance(lane[i - 1], lane[i]) / 0.4f);
                for (int s = 0; s <= steps; s++)
                {
                    var p = Vector3.Lerp(lane[i - 1], lane[i], s / (float)steps);
                    float? ground = GroundAt(p, Gen.TierOverlook);
                    Assert.NotNull(ground, $"No ground under the passage lane at {p}.");

                    var foot = new Vector3(p.x, ground.Value, p.z);
                    if (!CapsuleClear(foot, out var blocker, "Stair_"))
                        contacts.Add($"{p} touches {blocker}");
                }
            }

            Assert.IsEmpty(contacts,
                $"An NPC cannot walk the gate cleanly:\n  {string.Join("\n  ", contacts.Take(10))}");
        }

        [Test]
        public void TheDefenceHardware_IsClearOfTheGateOpening()
        {
            // Fence and trap must sit beside the opening, never across it, or a
            // "safe" defence would still be a physical obstruction.
            var south = BoundsOf("MainGate_Post_South");
            var north = BoundsOf("MainGate_Post_North");

            foreach (var name in new[]
            {
                "ElectricFence_Post_0", "ElectricFence_Post_1", "ElectricFence_Post_2",
                "ElectricFence_Rail_Lower", "ElectricFence_Rail_Upper", "ElectricFence_PowerBox"
            })
            {
                var b = BoundsOf(name);
                bool insideOpening = b.max.z > south.max.z && b.min.z < north.min.z;
                Assert.IsFalse(insideOpening, $"{name} stands inside the Main Gate opening.");
            }
        }

        // ----------------------------------------------------- four-player working

        [Test]
        public void FourPlayers_CanWorkTheTerraceWithoutBlockingTheRoute()
        {
            // One at the controls, one at the fence, one at the bench, one watching
            // the approach. Each must fit, and none may stand on the through-route.
            var stations = new (string Role, Vector3 Spot)[]
            {
                ("controls", new Vector3(14.6f, Gen.TierOverlook, -14.9f)),
                ("fence repair", new Vector3(9.4f, Gen.TierOverlook, -13.2f)),
                ("trap bench", new Vector3(15.4f, Gen.TierOverlook, -13.3f)),
                ("watching the approach", new Vector3(13.5f, Gen.TierOverlook, -19.4f))
            };

            var problems = new List<string>();

            foreach (var (role, spot) in stations)
            {
                float? ground = GroundAt(spot, Gen.TierOverlook);
                if (ground == null)
                {
                    problems.Add($"{role}: no floor at {spot}");
                    continue;
                }

                if (!CapsuleClear(new Vector3(spot.x, ground.Value, spot.z), out var blocker))
                    problems.Add($"{role}: cannot stand at {spot}, blocked by {blocker}");
            }

            Assert.IsEmpty(problems, string.Join("; ", problems));

            // And the four of them must not be crowded into one spot.
            for (int i = 0; i < stations.Length; i++)
            for (int j = i + 1; j < stations.Length; j++)
            {
                float gap = Vector3.Distance(stations[i].Spot, stations[j].Spot);
                Assert.That(gap, Is.GreaterThan(1.4f),
                    $"{stations[i].Role} and {stations[j].Role} are only {gap:0.0} m apart.");
            }
        }

        [Test]
        public void TheCentralSpace_StaysClearOfFurniture()
        {
            // Nothing may intrude on the middle of the terrace.
            var clear = new Bounds();
            clear.SetMinMax(new Vector3(9.5f, Gen.TierOverlook, -18.5f),
                            new Vector3(14.5f, Gen.TierOverlook + 3f, -14f));

            var intruders = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Control_") || r.name.StartsWith("TrapBench_") ||
                            r.name.StartsWith("ElectricFence_Post") || r.name.StartsWith("Overlook_Fence"))
                .Where(r => r.bounds.min.x < clear.max.x && r.bounds.max.x > clear.min.x &&
                            r.bounds.min.z < clear.max.z && r.bounds.max.z > clear.min.z)
                .Select(r => r.name)
                .ToArray();

            Assert.IsEmpty(intruders,
                $"Furniture intrudes on the central player space: {string.Join(", ", intruders)}");
        }

        // ------------------------------------------------------------- sightlines

        [Test]
        public void TheControlOperator_CanSeeTheMainGate()
        {
            var stand = new Vector3(14.6f, Gen.TierOverlook + EyeHeight, -14.9f);
            var gate = new Vector3(Gen.MainGateX, Gen.TierOverlook + 1.5f, -17.35f);

            Assert.IsTrue(CanSee(stand, gate, out var blocker),
                $"Operator cannot see the Main Gate — blocked by {blocker}.");
        }

        [Test]
        public void TheControlOperator_CanSeePartOfTheLowerApproach()
        {
            // Through the gate opening, down the traverse the enemies climb.
            // Through the gate opening to the apron outside it, where attackers
            // arrive. A flat terrace cannot see the traverse below its own floor.
            var stand = new Vector3(14.6f, Gen.TierOverlook + EyeHeight, -14.9f);
            var apron = new Vector3(6.8f, Gen.TierOverlook + 0.6f, -18.2f);

            Assert.IsTrue(CanSee(stand, apron, out var blocker),
                $"Operator cannot see the approach outside the gate — blocked by {blocker}.");
        }

        [Test]
        public void TheTerraceCentre_CanSeeTheMainGate()
        {
            var centre = new Vector3(12f, Gen.TierOverlook + EyeHeight, -16.5f);
            var gate = new Vector3(Gen.MainGateX, Gen.TierOverlook + 1.5f, -17.35f);

            Assert.IsTrue(CanSee(centre, gate, out var blocker),
                $"Terrace centre cannot see the Main Gate — blocked by {blocker}.");
        }

        [Test]
        public void APlayerAtTheGate_CanSeeTheControlConsole()
        {
            // They have to know where to run to make the defences safe.
            var atGate = new Vector3(9.5f, Gen.TierOverlook + EyeHeight, -17.4f);
            var mast = BoundsOf("Control_IndicatorMast").center;

            Assert.IsTrue(CanSee(atGate, mast, out var blocker, "Control_"),
                $"A player at the gate cannot see the control mast — blocked by {blocker}.");
        }

        [Test]
        public void TheOverlookFence_StillShowsTheDock()
        {
            // Standing at the rail, as the composition intends.
            var atRail = new Vector3(14.5f, Gen.TierOverlook + EyeHeight, -19.6f);
            var dock = new Vector3(0f, 1.2f, -44f);

            Assert.IsTrue(CanSee(atRail, dock, out var blocker, "Dock_"),
                $"The dock is no longer visible from the overlook fence — blocked by {blocker}.");
        }

        [Test]
        public void TheTerrace_StillSeesEnemiesOnTheTraverse()
        {
            var centre = new Vector3(12f, Gen.TierOverlook + EyeHeight, -16.5f);
            var onTraverse = Gen.WpTraverseMid + Vector3.up * 1.2f;

            Assert.IsTrue(CanSee(centre, onTraverse, out var blocker, "Path_Traverse"),
                $"Cannot watch the traverse from the terrace — blocked by {blocker}.");
        }

        [Test]
        public void TheLighthouse_IsStillVisibleFromTheTerraceGateAndConsole()
        {
            var lantern = Gen.LanternCentre;
            var viewpoints = new (string Name, Vector3 Eye)[]
            {
                ("terrace centre", new Vector3(12f, Gen.TierOverlook + EyeHeight, -16.5f)),
                ("Main Gate", new Vector3(9.5f, Gen.TierOverlook + EyeHeight, -17.4f)),
                ("control console", new Vector3(14.6f, Gen.TierOverlook + EyeHeight, -14.9f))
            };

            var blocked = new List<string>();
            foreach (var (name, eye) in viewpoints)
            {
                bool seen = CanSee(eye, lantern, out var b, "Lighthouse_") ||
                            CanSee(eye, lantern - Vector3.up * 6f, out b, "Lighthouse_");
                if (!seen)
                    blocked.Add($"{name} blocked by {b}");
            }

            Assert.IsEmpty(blocked, $"Lighthouse lost: {string.Join("; ", blocked)}");
        }
    }
}
