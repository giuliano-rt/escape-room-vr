using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Varre a cena procurando problemas que não aparecem como erro de
    /// compilação mas estragam o resultado em VR: materiais com shader
    /// quebrado (os famosos objetos magenta), renderers sem material e
    /// textos sem fonte.
    /// </summary>
    public static class SceneDiagnostics
    {
        private const string ScenePath = "Assets/_Project/Scenes/EscapeRoom.unity";

        [MenuItem("Escape Room VR/6. Diagnosticar a cena")]
        public static void Diagnose()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var report = new StringBuilder();
            int problems = 0;

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(includeInactive: true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null)
                        {
                            report.AppendLine($"  SEM MATERIAL: {Path(renderer.transform)}");
                            problems++;
                            continue;
                        }

                        if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                        {
                            report.AppendLine($"  SHADER QUEBRADO (magenta): {Path(renderer.transform)} " +
                                              $"[material: {material.name}]");
                            problems++;
                        }
                    }
                }

                foreach (var text in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
                {
                    if (text.font == null)
                    {
                        report.AppendLine($"  TEXTO SEM FONTE: {Path(text.transform)}");
                        problems++;
                        continue;
                    }

                    // Altura aproximada do texto renderizado, para achar rotulos
                    // pequenos demais para serem lidos dentro do jogo.
                    text.ForceMeshUpdate();
                    var bounds = text.textBounds;

                    report.AppendLine($"  TEXTO '{Shorten(text.text)}' em {Path(text.transform)}: " +
                                      $"altura {bounds.size.y:0.###} x largura {bounds.size.x:0.###} " +
                                      $"(fontSize {text.fontSize:0.##})");
                }
            }

            Debug.Log($"[SceneDiagnostics] {problems} problema(s) encontrado(s).\n{report}");
        }

        /// <summary>
        /// Lista os renderers de uma região da cena com o material de cada um.
        /// Serve para descobrir de onde vem um objeto estranho numa captura,
        /// como peças do XR Origin que só deveriam aparecer em Play Mode.
        /// </summary>
        [MenuItem("Escape Room VR/7. Listar renderers perto do jogador")]
        public static void ListRenderersNearPlayer()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var center = new Vector3(0f, 1.2f, -2.2f);
            var report = new StringBuilder();

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(includeInactive: false))
                {
                    float distance = Vector3.Distance(renderer.bounds.center, center);

                    if (distance > 1.5f)
                        continue;

                    var material = renderer.sharedMaterial;
                    string materialInfo = material == null
                        ? "sem material"
                        : $"{material.name} / shader {material.shader?.name}";

                    report.AppendLine($"  {distance:0.00}m  {Path(renderer.transform)}  [{materialInfo}]  " +
                                      $"pos {renderer.bounds.center}");
                }
            }

            Debug.Log($"[SceneDiagnostics] Renderers a ate 1,5 m de {center}:\n{report}");
        }

        private static string Path(Transform transform)
        {
            var parts = new StringBuilder(transform.name);

            for (var current = transform.parent; current != null; current = current.parent)
                parts.Insert(0, current.name + "/");

            return parts.ToString();
        }

        private static string Shorten(string value)
        {
            value = value.Replace("\n", " ");
            return value.Length <= 24 ? value : value.Substring(0, 24) + "...";
        }
    }
}
