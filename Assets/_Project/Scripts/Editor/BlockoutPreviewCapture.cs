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
                Name = "00_compound_plan",
                Position = new Vector3(0f, 66f, 17f),
                LookAt = new Vector3(0f, 17f, 17.1f),
                FieldOfView = 55f,
                LayoutLighting = true
            },
            new Shot
            {
                Name = "01_compound_aerial",
                Position = new Vector3(34f, 40f, -18f),
                LookAt = new Vector3(-4f, 20f, 22f),
                FieldOfView = 55f,
                LayoutLighting = true
            },
            new Shot
            {
                Name = "02_entering_from_inner_gate",
                Position = new Vector3(-6f, 18.7f, 3f),
                LookAt = new Vector3(0f, 30f, 34f),
                FieldOfView = 72f
            },
            new Shot
            {
                Name = "03_courtyard_centre_north",
                Position = new Vector3(0f, 18.7f, 17f),
                LookAt = new Vector3(0f, 32.6f, 38f),
                FieldOfView = 72f
            },
            new Shot
            {
                Name = "04_courtyard_centre_west",
                Position = new Vector3(0f, 18.7f, 17f),
                LookAt = new Vector3(-18f, 19f, 15.5f),
                FieldOfView = 75f
            },
            new Shot
            {
                Name = "05_generator_doorway_east",
                Position = new Vector3(-12.4f, 18.7f, 15.5f),
                LookAt = new Vector3(12f, 19f, 19f),
                FieldOfView = 75f
            },
            new Shot
            {
                Name = "06_keepers_house_porch_west",
                Position = new Vector3(11.2f, 18.7f, 20f),
                LookAt = new Vector3(-16f, 19f, 18f),
                FieldOfView = 75f
            },
            new Shot
            {
                Name = "07_stores_door_to_yard",
                Position = new Vector3(11.4f, 18.7f, 8f),
                LookAt = new Vector3(-2f, 22f, 26f),
                FieldOfView = 74f
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
