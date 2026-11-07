using System.Collections.Generic;
using System.IO;
using EscapeRoomVR.Interaction;
using EscapeRoomVR.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Renderiza a cena de vários pontos de vista e grava PNGs em
    /// Screenshots/. Serve para conferir enquadramento, iluminação e se os
    /// textos dos enigmas estão legíveis, sem precisar abrir o Editor.
    ///
    /// Também tem um modo que revela todos os enigmas antes de fotografar,
    /// para dar para ver os painéis escondidos e as portas abertas.
    /// </summary>
    public static class SceneScreenshotTool
    {
        private const string ScenePath = "Assets/_Project/Scenes/EscapeRoom.unity";
        private const string OutputFolder = "Screenshots";
        private const int Width = 1280;
        private const int Height = 720;

        private readonly struct Shot
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 LookAt;
            public readonly float FieldOfView;

            /// <summary>Esconde o teto, para a foto de cima enxergar a sala.</summary>
            public readonly bool HideCeiling;

            public Shot(string name, Vector3 position, Vector3 lookAt, float fieldOfView = 60f,
                bool hideCeiling = false)
            {
                Name = name;
                Position = position;
                LookAt = lookAt;
                FieldOfView = fieldOfView;
                HideCeiling = hideCeiling;
            }
        }

        private static readonly Shot[] Shots =
        {
            new Shot("01-visao-geral", new Vector3(0f, 1.65f, -2.6f), new Vector3(0f, 1.3f, 1f), 75f),
            new Shot("02-mesa-gaveta", new Vector3(-1.9f, 1.4f, 1.4f), new Vector3(-3.0f, 0.6f, 1.4f)),
            new Shot("03-quadro", new Vector3(0f, 1.6f, -1.9f), new Vector3(0f, 1.65f, -3.45f)),
            new Shot("04-alavancas", new Vector3(2.0f, 1.45f, 0f), new Vector3(3.45f, 1.35f, 0f)),
            new Shot("05-cofre-teclado", new Vector3(-2.0f, 1.4f, -0.8f), new Vector3(-3.45f, 1.3f, -0.8f)),
            new Shot("06-porta-leitor", new Vector3(0.3f, 1.55f, 1.6f), new Vector3(0.7f, 1.3f, 3.45f)),
            new Shot("07-corredor", new Vector3(0f, 1.6f, 3.9f), new Vector3(0f, 1.6f, 6.9f)),
            new Shot("08-planta-de-cima", new Vector3(0f, 7.5f, 0.6f), new Vector3(0f, 0f, 0.6f), 80f,
                hideCeiling: true),
            new Shot("09-hud", new Vector3(0f, 1.5f, -2.6f), new Vector3(0f, 1.25f, -1.0f), 55f)
        };

        [MenuItem("Escape Room VR/4. Gerar screenshots da cena")]
        public static void CaptureClosed() => Capture(revealPuzzles: false, suffix: "");

        [MenuItem("Escape Room VR/5. Gerar screenshots com enigmas revelados")]
        public static void CaptureRevealed() => Capture(revealPuzzles: true, suffix: "-revelado");

        private static void Capture(bool revealPuzzles, string suffix)
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (revealPuzzles)
                RevealEverything();

            Directory.CreateDirectory(OutputFolder);

            var cameraGo = new GameObject("Screenshot Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;

            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };

            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var written = new List<string>();

            try
            {
                var ceilings = FindCeilings();

                foreach (var shot in Shots)
                {
                    foreach (var ceiling in ceilings)
                        ceiling.SetActive(!shot.HideCeiling);

                    cameraGo.transform.position = shot.Position;
                    cameraGo.transform.rotation = Quaternion.LookRotation(
                        (shot.LookAt - shot.Position).normalized, Vector3.up);
                    camera.fieldOfView = shot.FieldOfView;

                    camera.targetTexture = renderTexture;
                    camera.Render();

                    var previous = RenderTexture.active;
                    RenderTexture.active = renderTexture;
                    readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                    readback.Apply();
                    RenderTexture.active = previous;
                    camera.targetTexture = null;

                    string path = Path.Combine(OutputFolder, $"{shot.Name}{suffix}.png");
                    File.WriteAllBytes(path, readback.EncodeToPNG());
                    written.Add(path);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(readback);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }

            Debug.Log($"[SceneScreenshotTool] {written.Count} imagens gravadas em {Path.GetFullPath(OutputFolder)}");
        }

        /// <summary>
        /// Deixa a cena no estado "tudo resolvido" para fotografar: liga os
        /// painéis escondidos, levanta as alavancas certas e gira as portas
        /// para a posição aberta.
        ///
        /// Tudo é feito mexendo direto nos transforms e lendo os ângulos pelo
        /// SerializedObject, porque fora do Play Mode o Awake dos componentes
        /// não roda — chamar Unlock() aqui não faria nada.
        /// </summary>
        private static void RevealEverything()
        {
            var all = CollectAllTransforms();

            foreach (var transform in all)
            {
                if (transform.name == "Hidden Panel" || transform.name == "Lever Clue Display")
                    transform.gameObject.SetActive(true);

                // O quadro cobre o painel escondido; some com ele para a foto,
                // como se o jogador ja o tivesse retirado da parede.
                if (transform.name == "Painting")
                    transform.gameObject.SetActive(false);
            }

            foreach (var transform in all)
            {
                var door = transform.GetComponent<AnimatedDoor>();

                if (door != null)
                {
                    float angle = ReadFloat(door, "openAngle");
                    Vector3 axis = ReadVector3(door, "hingeAxis");
                    transform.localRotation *= Quaternion.AngleAxis(angle, axis.normalized);
                    continue;
                }

                var lever = transform.GetComponent<Lever>();

                if (lever == null)
                    continue;

                // A combinacao correta e cima / baixo / cima, pela ordem do nome.
                bool shouldBeUp = transform.name != "Lever 2";
                float leverAngle = ReadFloat(lever, shouldBeUp ? "upAngle" : "downAngle");
                Vector3 leverAxis = ReadVector3(lever, "rotationAxis");

                var handle = transform.Find("Handle");

                if (handle != null)
                    handle.localRotation *= Quaternion.AngleAxis(leverAngle, leverAxis.normalized);
            }
        }

        private static List<GameObject> FindCeilings()
        {
            var result = new List<GameObject>();

            foreach (var transform in CollectAllTransforms())
            {
                if (transform.name.Contains("Ceiling"))
                    result.Add(transform.gameObject);
            }

            return result;
        }

        private static List<Transform> CollectAllTransforms()
        {
            var result = new List<Transform>();

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Transform>(includeInactive: true));

            return result;
        }

        private static float ReadFloat(Component component, string fieldName)
        {
            var property = new SerializedObject(component).FindProperty(fieldName);
            return property != null ? property.floatValue : 0f;
        }

        private static Vector3 ReadVector3(Component component, string fieldName)
        {
            var property = new SerializedObject(component).FindProperty(fieldName);
            return property != null ? property.vector3Value : Vector3.up;
        }
    }
}
