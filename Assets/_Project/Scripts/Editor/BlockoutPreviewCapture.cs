using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Renders still images of the blockout scene so the layout can be reviewed
    /// without opening the editor. Batch usage:
    ///
    ///   Unity -batchmode -quit -projectPath . \
    ///     -executeMethod LastBeacon.Editor.BlockoutPreviewCapture.Capture \
    ///     -previewOutput /some/folder
    ///
    /// Note: omit -nographics, a graphics device is required to render.
    /// </summary>
    public static class BlockoutPreviewCapture
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const int Width = 1600;
        const int Height = 900;

        struct Shot
        {
            public string Name;
            public Vector3 Position;
            public Vector3 LookAt;
            public float FieldOfView;

            /// <summary>
            /// Floods the scene with neutral light for this shot only. The game is
            /// meant to be dark; a layout review is not.
            /// </summary>
            public bool LayoutLighting;
        }

        static readonly Shot[] Shots =
        {
            new Shot
            {
                Name = "00_dock_plan",
                Position = new Vector3(-3f, 40f, -38f),
                LookAt = new Vector3(-3f, 0f, -37.9f),
                FieldOfView = 45f,
                LayoutLighting = true
            },
            new Shot
            {
                Name = "01_dock_above",
                Position = new Vector3(14f, 16f, -54f),
                LookAt = new Vector3(-8f, 2f, -34f),
                FieldOfView = 55f,
                LayoutLighting = true
            },
            new Shot
            {
                Name = "02_dock_from_seaward",
                Position = new Vector3(0f, 3.2f, -52f),
                LookAt = new Vector3(-6f, 3.5f, -34f),
                FieldOfView = 70f
            },
            new Shot
            {
                Name = "03_stepping_ashore",
                Position = new Vector3(0f, 2.1f, -43f),
                LookAt = new Vector3(-8f, 2.6f, -34f),
                FieldOfView = 72f
            },
            new Shot
            {
                Name = "04_ramp_upward",
                Position = new Vector3(-2f, 2.1f, -40.5f),
                LookAt = new Vector3(-13f, 5f, -30f),
                FieldOfView = 72f
            },
            new Shot
            {
                Name = "05_island_aerial",
                Position = new Vector3(62f, 44f, -80f),
                LookAt = new Vector3(-2f, 14f, 4f),
                FieldOfView = 55f,
                LayoutLighting = true
            }
        };

        public static void Capture()
        {
            string output = GetArg("-previewOutput") ?? Path.Combine(Path.GetTempPath(), "last-beacon-preview");
            Directory.CreateDirectory(output);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camGo = new GameObject("__PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;

            try
            {
                foreach (var shot in Shots)
                {
                    GameObject fill = null;
                    bool fogWas = RenderSettings.fog;
                    var ambientWas = RenderSettings.ambientSkyColor;
                    var equatorWas = RenderSettings.ambientEquatorColor;
                    var groundWas = RenderSettings.ambientGroundColor;

                    if (shot.LayoutLighting)
                    {
                        RenderSettings.fog = false;
                        RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.48f);
                        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.35f, 0.38f);
                        RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.24f);

                        fill = new GameObject("__LayoutFill");
                        fill.transform.rotation = Quaternion.Euler(52f, 200f, 0f);
                        var fillLight = fill.AddComponent<Light>();
                        fillLight.type = LightType.Directional;
                        fillLight.color = new Color(1f, 0.98f, 0.94f);
                        fillLight.intensity = 1.1f;
                        fillLight.shadows = LightShadows.Soft;
                    }

                    cam.transform.position = shot.Position;
                    cam.transform.rotation = Quaternion.LookRotation((shot.LookAt - shot.Position).normalized, Vector3.up);
                    cam.fieldOfView = shot.FieldOfView;

                    var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                    {
                        antiAliasing = 4
                    };
                    cam.targetTexture = rt;
                    cam.Render();

                    var previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    texture.Apply();
                    RenderTexture.active = previous;

                    string path = Path.Combine(output, shot.Name + ".png");
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Debug.Log($"[Last Beacon] Preview written: {path}");

                    cam.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(texture);
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);

                    if (shot.LayoutLighting)
                    {
                        UnityEngine.Object.DestroyImmediate(fill);
                        RenderSettings.fog = fogWas;
                        RenderSettings.ambientSkyColor = ambientWas;
                        RenderSettings.ambientEquatorColor = equatorWas;
                        RenderSettings.ambientGroundColor = groundWas;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return null;
        }
    }
}
