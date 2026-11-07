using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Converte os materiais dos exemplos do XR Interaction Toolkit para
    /// shaders do URP.
    ///
    /// Os Starter Assets vêm com materiais apontando para shaders do pipeline
    /// embutido (<c>Standard</c>, <c>XRIT/BiRP_Fresnel</c>). Sob o URP esses
    /// shaders não existem e tudo que os usa aparece em magenta — inclusive os
    /// modelos dos controles nas mãos do jogador.
    ///
    /// Os próprios Starter Assets trazem um <c>MaterialPipelineHandler</c> para
    /// resolver isso, mas ele decide pelo <c>GraphicsSettings.currentRenderPipeline</c>,
    /// que é nulo quando o Editor roda em batchmode; nesse caso ele conclui que
    /// o projeto está no pipeline embutido e devolve os shaders antigos. Por
    /// isso a conversão aqui é feita por nome de shader, de forma explícita.
    /// </summary>
    public static class SampleMaterialConverter
    {
        private const string SamplesFolder = "Assets/Samples";

        /// <summary>Shaders do pipeline embutido e o substituto no URP.</summary>
        private static readonly Dictionary<string, string> ShaderReplacements = new Dictionary<string, string>
        {
            { "Standard", "Universal Render Pipeline/Lit" },
            { "Standard (Specular setup)", "Universal Render Pipeline/Lit" },
            { "Legacy Shaders/Diffuse", "Universal Render Pipeline/Lit" },
            { "Mobile/Diffuse", "Universal Render Pipeline/Simple Lit" },
            { "XRIT/BiRP_Fresnel", "Shader Graphs/Unlit_Fresnel" }
        };

        [MenuItem("Escape Room VR/8. Converter materiais dos exemplos para URP")]
        public static void ConvertToUniversalRenderPipeline()
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { SamplesFolder });
            int converted = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null)
                    continue;

                if (!ShaderReplacements.TryGetValue(material.shader.name, out var replacementName))
                    continue;

                var replacement = Shader.Find(replacementName);

                if (replacement == null)
                {
                    Debug.LogWarning($"[SampleMaterialConverter] Shader '{replacementName}' nao encontrado; " +
                                     $"'{path}' continua em '{material.shader.name}'.");
                    continue;
                }

                // Guarda a cor base antes da troca: cada shader usa um nome de
                // propriedade diferente (_Color no Standard, _BaseColor no URP).
                Color baseColor = material.HasProperty("_Color")
                    ? material.GetColor("_Color")
                    : Color.white;

                material.shader = replacement;

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", baseColor);

                EditorUtility.SetDirty(material);
                converted++;
                Debug.Log($"[SampleMaterialConverter] {path} -> {replacementName}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SampleMaterialConverter] {converted} material(is) convertido(s) para o URP.");
        }
    }
}
