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
                Name = "00_terrace_plan",
                Position = new Vector3(11.5f, 46f, -16.5f),
                LookAt = new Vector3(11.5f, 9f, -16.4f),
                FieldOfView = 40f,
                LayoutLighting = true
            },
            new Shot
            {
                Name = "01_terrace_aerial",
                Position = new Vector3(-4f, 26f, -34f),
                LookAt = new Vector3(13f, 9.5f, -15f),
                FieldOfView = 55f,
                LayoutLighting = true
            },
            new Shot
            {
                Name = "02_approaching_the_main_gate",
                Position = new Vector3(3.2f, 8.4f, -20.6f),
                LookAt = new Vector3(12f, 10.2f, -16f),
                FieldOfView = 70f
            },
            new Shot
            {
                Name = "03_inside_the_gate",
                Position = new Vector3(10f, 10.7f, -17.6f),
                LookAt = new Vector3(16.4f, 10.6f, -15.4f),
                FieldOfView = 72f
            },
            new Shot
            {
                Name = "04_from_the_control_console",
                Position = new Vector3(14.6f, 10.7f, -14.9f),
                LookAt = new Vector3(6.8f, 9.6f, -18.2f),
                FieldOfView = 72f
            },
            new Shot
            {
                Name = "05_overlook_fence_to_dock",
                Position = new Vector3(14.5f, 10.7f, -19.6f),
                LookAt = new Vector3(0f, 1.2f, -44f),
                FieldOfView = 75f
            },
            new Shot
            {
                Name = "06_terrace_to_lighthouse",
                Position = new Vector3(12f, 10.7f, -16.5f),
                LookAt = new Vector3(0f, 32.6f, 38f),
                FieldOfView = 70f
            },
            new Shot
            {
                Name = "07_island_aerial",
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
