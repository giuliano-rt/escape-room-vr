using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Cria e guarda os materiais da sala como assets reais, para a cena não
    /// depender de nenhum pacote de arte externo. Tudo é cor sólida + metallic
    /// + smoothness, que é leve o bastante para o Quest e já dá leitura
    /// suficiente de madeira, metal, parede e pista luminosa.
    /// </summary>
    public static class MaterialLibrary
    {
        private const string Folder = "Assets/_Project/Materials";

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        /// <summary>Nomes dos materiais usados pelo construtor de cena.</summary>
        public static class Names
        {
            public const string Floor = "M_Floor";
            public const string Wall = "M_Wall";
            public const string Ceiling = "M_Ceiling";
            public const string Wood = "M_Wood";
            public const string WoodDark = "M_WoodDark";
            public const string Metal = "M_Metal";
            public const string MetalDark = "M_MetalDark";
            public const string Paper = "M_Paper";
            public const string Card = "M_Card";
            public const string Rubber = "M_Rubber";
            public const string Canvas = "M_Canvas";
            public const string PanelGlow = "M_PanelGlow";
            public const string IndicatorRed = "M_IndicatorRed";
            public const string IndicatorGreen = "M_IndicatorGreen";
            public const string Key = "M_KeypadKey";
        }

        /// <summary>Garante que todos os materiais existam no projeto.</summary>
        public static void EnsureAll()
        {
            Cache.Clear();

            Get(Names.Floor, new Color(0.22f, 0.20f, 0.19f), 0f, 0.15f);
            Get(Names.Wall, new Color(0.46f, 0.44f, 0.41f), 0f, 0.08f);
            Get(Names.Ceiling, new Color(0.30f, 0.30f, 0.32f), 0f, 0.05f);
            Get(Names.Wood, new Color(0.42f, 0.27f, 0.15f), 0f, 0.30f);
            Get(Names.WoodDark, new Color(0.26f, 0.16f, 0.09f), 0f, 0.25f);
            // Metal pouco polido de proposito: com smoothness alto, as luzes
            // pontuais da sala estouram num brilho branco que apaga a forma
            // da porta do cofre.
            Get(Names.Metal, new Color(0.62f, 0.64f, 0.67f), 0.55f, 0.28f);
            Get(Names.MetalDark, new Color(0.19f, 0.20f, 0.23f), 0.45f, 0.25f);
            Get(Names.Paper, new Color(0.93f, 0.90f, 0.80f), 0f, 0.10f);
            Get(Names.Card, new Color(0.15f, 0.45f, 0.75f), 0.30f, 0.70f);
            Get(Names.Rubber, new Color(0.10f, 0.10f, 0.11f), 0f, 0.20f);
            Get(Names.Canvas, new Color(0.35f, 0.30f, 0.45f), 0f, 0.12f);
            Get(Names.Key, new Color(0.80f, 0.82f, 0.85f), 0.20f, 0.50f);

            // Materiais que acendem: emissao ligada para servirem de indicador.
            Get(Names.PanelGlow, new Color(0.05f, 0.35f, 0.40f), 0f, 0.6f,
                new Color(0.10f, 0.85f, 0.95f) * 1.5f);
            Get(Names.IndicatorRed, new Color(0.60f, 0.08f, 0.06f), 0f, 0.5f,
                new Color(0.90f, 0.15f, 0.10f) * 2f);
            Get(Names.IndicatorGreen, new Color(0.08f, 0.55f, 0.20f), 0f, 0.5f,
                new Color(0.15f, 0.95f, 0.35f) * 2f);

            AssetDatabase.SaveAssets();
        }

        /// <summary>Devolve (criando se preciso) um material pelo nome.</summary>
        public static Material Get(string name, Color? baseColor = null, float metallic = 0f,
            float smoothness = 0.3f, Color? emission = null)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null)
                return cached;

            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(ResolveShader());
                AssetDatabase.CreateAsset(material, path);
            }

            if (baseColor.HasValue)
            {
                material.SetColor("_BaseColor", baseColor.Value);

                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", baseColor.Value);
            }

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            else if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(material);
            Cache[name] = material;
            return material;
        }

        /// <summary>
        /// Usa o shader do URP quando ele existe e cai no Standard se o
        /// projeto estiver no pipeline embutido, para nunca gerar material rosa.
        /// </summary>
        private static Shader ResolveShader()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            return urpLit != null ? urpLit : Shader.Find("Standard");
        }
    }
}
