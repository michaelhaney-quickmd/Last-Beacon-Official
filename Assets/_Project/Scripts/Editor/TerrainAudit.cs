using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// READ ONLY. Audits the imported island Terrain against the approved ProBuilder
    /// gameplay surfaces: clearance beneath every hard surface, clearance along the
    /// route at its real widths, lighthouse sightlines with the TerrainCollider in
    /// play, and which broad cliff masses the Terrain now duplicates.
    /// </summary>
    public static class TerrainAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";

        [MenuItem("Tools/Last Beacon/Terrain/Audit Against Blockout")]
        public static void Audit()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault();
            if (terrain == null) { Debug.LogError("[TA] No Terrain in the scene."); return; }

            Debug.Log($"[TA] Terrain '{terrain.name}' pos {terrain.transform.position} " +
                      $"size {terrain.terrainData.size} res {terrain.terrainData.heightmapResolution} " +
                      $"collider {(terrain.GetComponent<TerrainCollider>()?.enabled == true ? "ON" : "OFF")}");

            // ---------------------------------------------- STEP 2: surface clearance
            var areas = new (string Name, float X, float Z, float W, float D, float Surface)[]
            {
                ("Dock apron",            -0.5f, -38.5f,  8f,   7f,    0.40f),
                ("Intro ramp",            -5.5f, -38f,    4.5f, 4.5f,  0.80f),
                ("Lower-left ascent",    -10.5f, -33f,    4.5f, 6f,    2.60f),
                ("Lower-left pivot shelf",-14f,  -25.7f,  8f,   7.4f,  4.00f),
                ("Rising traverse",       -7f,   -25f,    4f,   6f,    5.25f),
                ("Main Gate terrace",      11.2f,-16.2f, 11.6f, 8.8f,  9.04f),
                ("Terrace exit",           11f,  -12f,    4f,   4f,    9.00f),
                ("Ascent A",                8.5f, -10.5f, 4f,   4f,   10.25f),
                ("Final ascent landing",    2.5f, -9.25f,10f,   5.5f, 11.54f),
                ("Broad stair",            -1.5f, -5.5f,  5f,   9f,   13.75f),
                ("Inner Gate",             -3.5f,  6f,    9f,   8f,   17.00f),
                ("Courtyard",               0f,   17f,   18f,  14f,   17.04f),
                ("Generator Shed pad",    -18f,   13.5f, 11.7f,10.3f, 17.00f),
                ("Workshop pad",          -18.8f, 27.2f, 12.7f,10.6f, 17.00f),
                ("Stores/Radio pad",       18f,    7.8f, 10.9f, 9.7f, 17.00f),
                ("Keeper's House pad",     18f,   20f,   12f,   9f,   17.00f),
                ("Lighthouse knoll",        0f,   38f,   24f,  12f,   21.00f),
                ("Lighthouse plinth",       0f,   38f,   12.4f,12.3f, 21.00f)
            };

            Debug.Log("[TA] === STEP 2: terrain vs ProBuilder ===");
            foreach (var a in areas)
            {
                float minClear = float.MaxValue, maxTerrain = float.MinValue;
                Vector3 worst = Vector3.zero;
                float pbMin = float.MaxValue, pbMax = float.MinValue;
                int pbSamples = 0;

                for (float dx = -a.W / 2f; dx <= a.W / 2f; dx += 0.5f)
                for (float dz = -a.D / 2f; dz <= a.D / 2f; dz += 0.5f)
                {
                    var at = new Vector3(a.X + dx, 0f, a.Z + dz);
                    float ty = terrain.SampleHeight(at) + terrain.transform.position.y;
                    if (ty > maxTerrain) maxTerrain = ty;

                    float? pb = ProBuilderTop(at, a.Surface);
                    if (pb.HasValue)
                    {
                        pbSamples++;
                        pbMin = Mathf.Min(pbMin, pb.Value);
                        pbMax = Mathf.Max(pbMax, pb.Value);
                        float clear = pb.Value - ty;
                        if (clear < minClear) { minClear = clear; worst = at; }
                    }
                }

                string verdict = minClear == float.MaxValue ? "NO PB SURFACE FOUND"
                    : minClear < 0f ? "*** TERRAIN INTERSECTS ***"
                    : minClear < 0.3f ? "TIGHT" : "ok";
                Debug.Log($"[TA] {a.Name,-24} terrainY max {maxTerrain,7:0.00}  " +
                          $"pbY {pbMin,6:0.00}..{pbMax,6:0.00} ({pbSamples} pts)  " +
                          $"min clearance {(minClear == float.MaxValue ? 0f : minClear),6:0.00}  {verdict}  " +
                          $"worst at ({worst.x:0.0},{worst.z:0.0})");
            }

            // ------------------------------------------------- STEP 3: route clearance
            Debug.Log("[TA] === STEP 3: route clearance at real widths ===");
            var legs = new (string Name, Vector3 A, Vector3 B, float Width)[]
            {
                ("jetty",              Gen.WpJettyEnd, Gen.WpShoreApron, 5f),
                ("apron",              Gen.WpShoreApron, Gen.WpRampBase, 8f),
                ("intro ramp",         Gen.WpRampBase, Gen.WpIntroTop, 4.5f),
                ("lower-left ascent",  Gen.WpIntroTop, Gen.LowerLeftRampTop, 4.5f),
                ("pivot",              Gen.LowerLeftRampTop, Gen.WpLowerLeftTop, 4.5f),
                ("traverse leg 1",     Gen.WpLowerLeftTop, Gen.WpTraverseMid, 4f),
                ("traverse leg 2",     Gen.WpTraverseMid, Gen.OverlookDeckEdge, 4f),
                ("terrace crossing",   Gen.OverlookDeckEdge, Gen.WpOverlookEntry, 4f),
                ("terrace to exit",    Gen.WpOverlookEntry, Gen.WpOverlookExit, 4f),
                ("ascent A",           Gen.WpOverlookExit, Gen.WpAscentATop, 4f),
                ("landing",            Gen.WpAscentATop, Gen.WpLanding, 4f),
                ("broad stair",        Gen.WpLanding, Gen.WpStairsTop, 5f),
                ("final rise",         Gen.WpStairsTop, Gen.WpCompoundEntrance, 4f),
                ("gate to yard",       Gen.WpCompoundEntrance, Gen.WpYardCentre, 4f),
                ("yard to lighthouse", Gen.WpYardCentre, new Vector3(0f, 21f, 32f), 5f)
            };

            var pokes = new List<string>();
            foreach (var leg in legs)
            {
                var horiz = new Vector3(leg.B.x - leg.A.x, 0f, leg.B.z - leg.A.z).normalized;
                var side = new Vector3(horiz.z, 0f, -horiz.x);
                float worst = float.MaxValue;
                Vector3 worstAt = Vector3.zero;
                string worstEdge = "";

                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(leg.A, leg.B) / 0.5f));
                for (int s = 0; s <= steps; s++)
                foreach (var (off, edge) in new[] { (-leg.Width / 2f, "left"), (0f, "centre"), (leg.Width / 2f, "right") })
                {
                    var c = Vector3.Lerp(leg.A, leg.B, s / (float)steps) + side * off;
                    float ty = terrain.SampleHeight(c) + terrain.transform.position.y;
                    float clear = c.y - ty;
                    if (clear < worst) { worst = clear; worstAt = c; worstEdge = edge; }
                    if (clear < 0f)
                        pokes.Add($"{leg.Name} {edge} ({c.x:0.0},{c.z:0.0}) terrain {ty:0.00} vs route {c.y:0.00}");
                }

                Debug.Log($"[TA] {leg.Name,-20} min clearance {worst,6:0.00} m ({worstEdge} edge, " +
                          $"{worstAt.x:0.0},{worstAt.z:0.0})");
            }
            Debug.Log(pokes.Count == 0
                ? "[TA] No terrain penetration anywhere on the route."
                : $"[TA] *** {pokes.Count} ROUTE PENETRATIONS ***\n  " + string.Join("\n  ", pokes.Take(12)));

            // ------------------------------------------- STEP 4: lighthouse sightlines
            Debug.Log("[TA] === STEP 4: lighthouse visibility (TerrainCollider live) ===");
            foreach (var (name, eye, _) in Gen.ReviewCameras)
                Debug.Log($"[TA] {name,-20} {(SeesLantern(eye, out var b) ? "VISIBLE" : "BLOCKED by " + b)}");

            foreach (var (label, at) in new[]
                     {
                         ("dock", Gen.WpJettyEnd), ("intro ramp", Gen.WpIntroTop),
                         ("lower-left ascent", Gen.LowerLeftRampTop), ("pivot", Gen.WpLowerLeftTop),
                         ("traverse mid", Gen.WpTraverseMid), ("terrace", Gen.WpFenceLookout)
                     })
            {
                var eye = at + Vector3.up * Gen.EyeHeight;
                Debug.Log($"[TA] route/{label,-20} {(SeesLantern(eye, out var b) ? "VISIBLE" : "BLOCKED by " + b)}");
            }

            // ------------------------------------------------ STEP 6: collider conflicts
            Debug.Log("[TA] === STEP 6: what the player stands on ===");
            foreach (var leg in legs)
            {
                var mid = Vector3.Lerp(leg.A, leg.B, 0.5f);
                var hit = Physics.RaycastAll(mid + Vector3.up * 2f, Vector3.down, 6f)
                    .OrderByDescending(h => h.point.y).FirstOrDefault();
                Debug.Log($"[TA] {leg.Name,-20} top collider: " +
                          $"{(hit.collider != null ? hit.collider.name : "NONE")} at {hit.point.y:0.00}");
            }

            // --------------------------------- STEP 7: broad masses the terrain duplicates
            Debug.Log("[TA] === STEP 7: cliff/rock classification ===");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                         .Where(r => r.name.StartsWith("Cliff_") || r.name.StartsWith("Rock_"))
                         .OrderBy(r => r.name))
            {
                var b = r.bounds;
                float below = 0f, above = 0f;
                int n = 0;
                for (float x = b.min.x; x <= b.max.x; x += 1.5f)
                for (float z = b.min.z; z <= b.max.z; z += 1.5f)
                {
                    float ty = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                    n++;
                    if (ty >= b.max.y - 0.5f) below++;   // terrain already at or above this mass's top
                    if (ty < b.max.y - 2f) above++;      // mass stands well proud of terrain
                }
                float covered = n == 0 ? 0f : below / n * 100f;
                float proud = n == 0 ? 0f : above / n * 100f;
                float slope = b.size.y / Mathf.Max(0.1f, Mathf.Min(b.size.x, b.size.z));
                Debug.Log($"[TA] {r.name,-34} {b.size.x,5:0.0}x{b.size.z,5:0.0}x{b.size.y,5:0.0} " +
                          $"top {b.max.y,6:0.00}  terrain covers {covered,5:0}%  proud {proud,5:0}%  " +
                          $"aspect {slope,5:0.00}");
            }

            if (player != null) player.gameObject.SetActive(true);
        }

        /// <summary>
        /// Step 2, done precisely: for each named gameplay surface, sample only that
        /// object's own collider inside its own footprint. The coarse rectangle pass
        /// reports terrain rising BESIDE a path as an intersection, which it is not.
        /// </summary>
        [MenuItem("Tools/Last Beacon/Terrain/Audit Surfaces Precisely")]
        public static void SurfaceAudit()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault();
            float baseY = terrain.transform.position.y;

            var surfaces = new (string Label, string[] Objects)[]
            {
                ("1  Dock apron",            new[] { "Dock_Apron", "Dock_Deck", "Dock_SupplyApron" }),
                ("2  Intro ramp",            new[] { "Path_IntroRamp" }),
                ("3  Lower-left ascent",     new[] { "Path_LowerLeftAscent" }),
                ("4  Lower-left pivot",      new[] { "Shelf_LowerLeftPivot" }),
                ("5  Rising traverse",       new[] { "Path_TraverseLeg1", "Path_TraverseLeg2" }),
                ("6  Main Gate terrace",     new[] { "Terrace_Deck" }),
                ("7  Terrace exit",          new[] { "Terrace_Throat" }),
                ("8  Ascent A",              new[] { "Path_AscentA_ShortRise" }),
                ("9  Final ascent landing",  new[] { "Ascent_Landing" }),
                ("10 Broad stair",           new[] { "Stair_AscentBroad", "Ascent_StairTopPad" }),
                ("11 Inner Gate",            new[] { "Path_AscentD_FinalRise" }),
                ("12 Courtyard",             new[] { "MainYard" }),
                ("13 Generator Shed pad",    new[] { "Shed_Body" }),
                ("14 Workshop pad",          new[] { "Workshop_Body" }),
                ("15 Stores/Radio pad",      new[] { "Stores_Body" }),
                ("16 Keeper's House pad",    new[] { "House_Body" }),
                ("17 Lighthouse knoll",      new[] { "Cliff_BandD_Knoll", "Stair_CompoundToLighthouse" }),
                ("18 Lighthouse plinth",     new[] { "Lighthouse_Plinth" })
            };

            Debug.Log("[TS] === STEP 2 (precise): terrain vs each named gameplay surface ===");
            foreach (var (label, names) in surfaces)
            {
                float minClear = float.MaxValue, surfLo = float.MaxValue, surfHi = float.MinValue;
                float terrLo = float.MaxValue, terrHi = float.MinValue;
                Vector3 worst = Vector3.zero;
                string worstObj = "";
                int n = 0;

                foreach (var name in names)
                {
                    var go = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                        .FirstOrDefault(t => t.name == name);
                    if (go == null) { Debug.LogWarning($"[TS] {label}: {name} MISSING"); continue; }
                    var col = go.GetComponent<Collider>();
                    var b = go.GetComponent<Renderer>().bounds;

                    for (float x = b.min.x; x <= b.max.x; x += 0.4f)
                    for (float z = b.min.z; z <= b.max.z; z += 0.4f)
                    {
                        float top = float.MinValue;
                        foreach (var h in Physics.RaycastAll(new Vector3(x, b.max.y + 3f, z), Vector3.down,
                                     b.size.y + 6f))
                            if (h.collider == col && h.point.y > top) top = h.point.y;
                        if (top == float.MinValue) continue;   // not over this object

                        float ty = terrain.SampleHeight(new Vector3(x, 0f, z)) + baseY;
                        n++;
                        surfLo = Mathf.Min(surfLo, top); surfHi = Mathf.Max(surfHi, top);
                        terrLo = Mathf.Min(terrLo, ty);  terrHi = Mathf.Max(terrHi, ty);
                        if (top - ty < minClear)
                        {
                            minClear = top - ty; worst = new Vector3(x, top, z); worstObj = name;
                        }
                    }
                }

                if (n == 0) { Debug.Log($"[TS] {label,-24} NO SAMPLES"); continue; }
                string verdict = minClear < 0f ? "*** TERRAIN THROUGH SURFACE ***"
                    : minClear < 0.3f ? "TIGHT" : "ok";
                Debug.Log($"[TS] {label,-24} surface {surfLo,6:0.00}..{surfHi,6:0.00}  " +
                          $"terrain {terrLo,6:0.00}..{terrHi,6:0.00}  clearance min {minClear,6:0.00}  " +
                          $"{verdict}  worst {worstObj} ({worst.x:0.0},{worst.z:0.0})");
            }

            if (player != null) player.gameObject.SetActive(true);
        }

        /// <summary>Highest non-terrain collider near an expected surface height.</summary>
        static float? ProBuilderTop(Vector3 at, float expected)
        {
            float? best = null;
            foreach (var h in Physics.RaycastAll(new Vector3(at.x, expected + 4f, at.z), Vector3.down, 10f))
            {
                if (h.collider is TerrainCollider) continue;
                if (h.point.y > expected + 1.5f) continue;
                if (h.point.y < expected - 2.5f) continue;
                if (best == null || h.point.y > best.Value) best = h.point.y;
            }
            return best;
        }

        static bool SeesLantern(Vector3 eye, out string blocker)
        {
            blocker = "";
            var dir = Gen.LanternCentre - eye;
            if (!Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude))
                return true;
            if (hit.collider.name.StartsWith("Lighthouse_")) return true;
            blocker = hit.collider is TerrainCollider ? "TERRAIN" : hit.collider.name;
            return false;
        }
    }
}
