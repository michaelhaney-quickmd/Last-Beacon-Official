using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Tests
{
    /// <summary>
    /// Developer traversal test. Walks the approved route using the real player
    /// capsule's dimensions rather than a point, and checks it across the path's
    /// width instead of only down the centreline.
    ///
    /// This is a geometry test, not a physics simulation — it proves the surfaces
    /// are continuous, unobstructed and within the controller's step and slope
    /// limits. Play Mode is still the final word on feel.
    /// </summary>
    public class RouteTraversalTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";

        // These must mirror BuildPlayer's CharacterController exactly.
        const float CapsuleRadius = 0.35f;
        const float CapsuleHeight = 1.8f;
        const float StepOffset = 0.45f;
        const float SlopeLimit = 50f;

        /// <summary>How far either side of the centreline to check, in metres.</summary>
        static readonly float[] LateralOffsets = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };

        /// <summary>Sample spacing along the route.</summary>
        const float SampleStep = 0.5f;

        static GameObject _player;

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // The placeholder player is itself a collider. Left active it reads as
            // "ground" 2.8 m above the dock and obstructs its own spawn point.
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

        /// <summary>Ground height under a point, or null if there is no surface.</summary>
        static float? GroundAt(Vector3 point, float expectedY)
        {
            // Probe from well above the expected surface so overhangs are caught.
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

        static IEnumerable<(int Segment, float T, Vector3 Centre)> Samples()
        {
            var route = Gen.WalkPath;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 a = route[i - 1], b = route[i];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / SampleStep));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    yield return (i, t, Vector3.Lerp(a, b, t));
                }
            }
        }

        static Vector3 LateralAxis(int segment)
        {
            var route = Gen.WalkPath;
            Vector3 delta = route[segment] - route[segment - 1];
            var horizontal = new Vector3(delta.x, 0f, delta.z).normalized;
            return new Vector3(horizontal.z, 0f, -horizontal.x);
        }

        // ------------------------------------------------------------------ tests

        [Test]
        public void EverySampleAcrossTheRouteWidth_HasGroundBeneathIt()
        {
            var holes = new List<string>();

            foreach (var (segment, _, centre) in Samples())
            {
                var side = LateralAxis(segment);

                foreach (float offset in LateralOffsets)
                {
                    var p = centre + side * offset;
                    float? ground = GroundAt(p, centre.y);

                    if (ground == null)
                        holes.Add($"seg{segment} {p} offset {offset:+0.0;-0.0} — no surface");
                    else if (Mathf.Abs(ground.Value - centre.y) > 1.2f)
                        holes.Add($"seg{segment} {p} offset {offset:+0.0;-0.0} — surface {ground.Value:0.00} vs expected {centre.y:0.00}");
                }
            }

            Assert.IsEmpty(holes,
                $"{holes.Count} gap(s) the player could fall into:\n  {string.Join("\n  ", holes.Take(15))}");
        }

        [Test]
        public void ThePlayerCapsule_FitsEverywhereAlongTheRoute()
        {
            var blocked = new List<string>();
            // Slightly under the real radius so merely touching a surface is not a hit.
            float radius = CapsuleRadius - 0.02f;

            var probe = new GameObject("__TraversalProbe");
            var probeCollider = probe.AddComponent<CapsuleCollider>();
            probeCollider.radius = radius;
            probeCollider.height = CapsuleHeight;
            probeCollider.direction = 1;
            probe.SetActive(true);

            foreach (var (segment, _, centre) in Samples())
            {
                var side = LateralAxis(segment);

                foreach (float offset in LateralOffsets)
                {
                    var foot = centre + side * offset;
                    float? ground = GroundAt(foot, centre.y);
                    if (ground == null)
                        continue; // reported by the gap test

                    // Capsule standing on the surface, lifted clear of it.
                    var bottom = new Vector3(foot.x, ground.Value + radius + 0.08f, foot.z);
                    var top = bottom + Vector3.up * (CapsuleHeight - 2f * radius);

                    var centreOfCapsule = (bottom + top) * 0.5f;
                    probe.transform.SetPositionAndRotation(centreOfCapsule, Quaternion.identity);
                    Physics.SyncTransforms();

                    foreach (var hit in Physics.OverlapCapsule(bottom, top, radius))
                    {
                        if (hit == probeCollider)
                            continue;

                        // A stair riser is a vertical face by design. Stairs are
                        // validated by riser/tread in TheBroadStair_... instead.
                        if (hit.name.StartsWith("Stair_"))
                            continue;

                        if (!Physics.ComputePenetration(
                                probeCollider, probe.transform.position, probe.transform.rotation,
                                hit, hit.transform.position, hit.transform.rotation,
                                out var direction, out float depth))
                            continue;

                        // A vertical push means the capsule is fractionally inside the
                        // floor it stands on. A horizontal push is a wall.
                        bool isWall = Mathf.Abs(direction.y) < 0.6f;
                        if (isWall && depth > 0.12f)
                        {
                            blocked.Add($"seg{segment} {foot} offset {offset:+0.0;-0.0} — {hit.name} blocks {depth:0.00} m horizontally");
                            break;
                        }
                    }
                }
            }

            Object.DestroyImmediate(probe);

            Assert.IsEmpty(blocked,
                $"{blocked.Count} point(s) where the player capsule is obstructed:\n  {string.Join("\n  ", blocked.Take(15))}");
        }

        [Test]
        public void NoVerticalStepAlongTheRoute_ExceedsTheControllerStepOffset()
        {
            var lips = new List<string>();

            foreach (float offset in LateralOffsets)
            {
                float? previous = null;
                Vector3 previousPoint = default;

                foreach (var (segment, _, centre) in Samples())
                {
                    var p = centre + LateralAxis(segment) * offset;
                    float? ground = GroundAt(p, centre.y);

                    if (ground.HasValue && previous.HasValue)
                    {
                        float step = Mathf.Abs(ground.Value - previous.Value);
                        // Only flag steps that are abrupt: a slope covers ground
                        // gradually, a lip does it within one sample.
                        if (step > StepOffset && Vector3.Distance(p, previousPoint) < SampleStep * 1.5f)
                            lips.Add($"seg{segment} offset {offset:+0.0;-0.0} at {p} — {step:0.00} m step");
                    }

                    if (ground.HasValue)
                    {
                        previous = ground;
                        previousPoint = p;
                    }
                }
            }

            Assert.IsEmpty(lips,
                $"{lips.Count} lip(s) above the {StepOffset} m step offset:\n  {string.Join("\n  ", lips.Take(15))}");
        }

        [Test]
        public void EveryRampGrade_IsInsideTheControllerSlopeLimit()
        {
            var steep = new List<string>();

            var segments = new (string Name, Vector3 From, Vector3 To)[]
            {
                ("intro ramp", Gen.WpRampBase, Gen.WpIntroTop),
                ("lower-left ascent", Gen.WpIntroTop, Gen.WpLowerLeftTop),
                ("traverse leg 1", Gen.WpLowerLeftTop, Gen.WpTraverseMid),
                ("traverse leg 2", Gen.WpTraverseMid, Gen.OverlookDeckEdge),
                ("ascent A", Gen.WpOverlookExit, Gen.WpAscentATop),
                ("final rise", Gen.WpStairsTop, Gen.WpCompoundEntrance)
            };

            foreach (var (name, from, to) in segments)
            {
                Vector3 d = to - from;
                float run = new Vector2(d.x, d.z).magnitude;
                float angle = Mathf.Atan2(d.y, run) * Mathf.Rad2Deg;

                // Ramps are walked, not climbed: keep a real margin under the limit.
                if (angle > SlopeLimit - 15f)
                    steep.Add($"{name} at {angle:0.0} degrees");
            }

            Assert.IsEmpty(steep,
                $"Ramps too close to the {SlopeLimit} degree slope limit: {string.Join(", ", steep)}");
        }

        [Test]
        public void TheBroadStair_HasWalkableRisersAndTreads()
        {
            var stair = GameObject.Find("Stair_AscentBroad");
            Assert.NotNull(stair, "Stair_AscentBroad is missing.");

            var b = stair.GetComponent<Renderer>().bounds;
            float rise = Gen.WpStairsTop.y - Gen.WpLanding.y;
            var d = Gen.WpStairsTop - Gen.WpLanding;
            float run = new Vector2(d.x, d.z).magnitude;

            // Mirrors the generator: tread must clear the capsule, riser must stay
            // under the step offset.
            int steps = Mathf.Clamp(Mathf.FloorToInt(run / 0.7f), Mathf.CeilToInt(rise / 0.42f), 40);
            float riser = rise / steps;
            float tread = run / steps;

            Assert.That(riser, Is.LessThanOrEqualTo(StepOffset),
                $"Riser is {riser:0.00} m, above the {StepOffset} m step offset.");
            Assert.That(tread, Is.GreaterThanOrEqualTo(2f * CapsuleRadius),
                $"Tread is {tread:0.00} m, too shallow for a {CapsuleRadius * 2f:0.00} m capsule.");
            Assert.That(Mathf.Min(b.size.x, b.size.z), Is.GreaterThanOrEqualTo(2f * CapsuleRadius + 1f),
                "Stair is too narrow for comfortable passage.");
        }

        [Test]
        public void TheRouteHasHeadroom()
        {
            var lowSpots = new List<string>();

            foreach (var (segment, _, centre) in Samples())
            {
                var side = LateralAxis(segment);

                foreach (float offset in LateralOffsets)
                {
                    var foot = centre + side * offset;
                    float? ground = GroundAt(foot, centre.y);
                    if (ground == null)
                        continue;

                    var from = new Vector3(foot.x, ground.Value + 0.15f, foot.z);
                    if (Physics.Raycast(from, Vector3.up, out var hit, CapsuleHeight + 0.2f))
                        lowSpots.Add($"seg{segment} {foot} — {hit.collider.name} at {hit.distance + 0.15f:0.00} m");
                }
            }

            Assert.IsEmpty(lowSpots,
                $"{lowSpots.Count} point(s) with less than {CapsuleHeight} m of headroom:\n  {string.Join("\n  ", lowSpots.Take(15))}");
        }

        [Test]
        public void ControllerSettings_MatchWhatTheseTestsAssume()
        {
            Assert.NotNull(_player, "No CharacterController in the scene.");
            var player = _player.GetComponent<CharacterController>();

            Assert.That(player.radius, Is.EqualTo(CapsuleRadius).Within(0.001f), "Capsule radius drifted.");
            Assert.That(player.height, Is.EqualTo(CapsuleHeight).Within(0.001f), "Capsule height drifted.");
            Assert.That(player.stepOffset, Is.EqualTo(StepOffset).Within(0.001f), "Step offset drifted.");
            Assert.That(player.slopeLimit, Is.EqualTo(SlopeLimit).Within(0.001f), "Slope limit drifted.");
        }

        [Test]
        public void ThePlayerSpawns_OnTheDockAndNotInsideGeometry()
        {
            Assert.NotNull(_player, "No player in the scene.");
            var pos = _player.transform.position;
            float? ground = GroundAt(pos, pos.y);
            Assert.NotNull(ground, "Player spawns over nothing.");
            Assert.That(pos.y - ground.Value, Is.InRange(-0.1f, 2f),
                $"Player spawns {pos.y - ground.Value:0.00} m off the ground.");

            float radius = CapsuleRadius - 0.02f;
            var bottom = new Vector3(pos.x, ground.Value + radius + 0.08f, pos.z);
            var top = bottom + Vector3.up * (CapsuleHeight - 2f * radius);
            var hits = Physics.OverlapCapsule(bottom, top, radius);

            Assert.IsEmpty(hits.Select(h => h.name),
                $"Player spawns inside geometry: {string.Join(", ", hits.Select(h => h.name))}");
        }
    }
}
