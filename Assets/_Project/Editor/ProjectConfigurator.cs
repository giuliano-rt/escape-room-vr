using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.Management;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Deixa o projeto configurado para VR sem ninguém precisar abrir
    /// Project Settings: cria e atribui os assets do URP, liga o OpenXR no
    /// PC e no Android, escolhe os perfis de controle do Meta Quest e ajusta
    /// as opções de build do Quest.
    ///
    /// É idempotente: rodar de novo não duplica nada.
    /// </summary>
    public static class ProjectConfigurator
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string PipelineAssetPath = SettingsFolder + "/EscapeRoom_URP.asset";
        private const string RendererAssetPath = SettingsFolder + "/EscapeRoom_Renderer.asset";

        /// <summary>
        /// Importa os TMP Essential Resources (fonte padrão, materiais e o
        /// asset de configuração do TextMeshPro). Sem eles, qualquer texto
        /// criado por script fica sem fonte e simplesmente não aparece.
        ///
        /// Fica num menu separado porque a importação de um .unitypackage é
        /// assíncrona: em linha de comando ela precisa de uma execução só dela.
        /// </summary>
        [MenuItem("Escape Room VR/0. Importar recursos do TextMeshPro")]
        public static void ImportTextMeshProResources()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                Debug.Log("[ProjectConfigurator] Recursos do TextMeshPro ja estao no projeto.");
                return;
            }

            Debug.Log("[ProjectConfigurator] Importando TMP Essential Resources...");
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
        }

        [MenuItem("Escape Room VR/2. Configurar projeto (URP + XR + Quest)")]
        public static void ConfigureAll()
        {
            EnsureFolders();
            ConfigureUniversalRenderPipeline();
            SampleMaterialConverter.ConvertToUniversalRenderPipeline();
            ConfigureInteractionLayers();
            ConfigureQualityAndPhysics();
            ConfigurePlayerSettings();
            ConfigureOpenXR();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectConfigurator] Projeto configurado.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets/_Project");
            CreateFolderIfMissing(SettingsFolder);
            CreateFolderIfMissing("Assets/_Project/Materials");
            CreateFolderIfMissing("Assets/_Project/Scenes");
        }

        private static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Cria o UniversalRenderPipelineAsset e o renderer, e atribui nas
        /// Graphics Settings. Sem isso os materiais URP aparecem em magenta.
        /// </summary>
        private static void ConfigureUniversalRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);

            if (pipeline == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);

                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
                }

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            // Perfil enxuto: o Quest 2 e um aparelho movel e nao aguenta
            // sombras em alta resolucao nem muitas luzes por objeto.
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;            // MSAA 4x e o padrao recomendado em VR
            pipeline.shadowDistance = 20f;
            pipeline.shadowCascadeCount = 1;

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            EditorUtility.SetDirty(pipeline);
        }

        /// <summary>
        /// Dá nome à camada de interação 31, usada pelo teletransporte.
        ///
        /// O Teleport Interactor dos Starter Assets vem configurado para
        /// enxergar só essa camada. Ela nasce sem nome num projeto novo, o que
        /// aparece como aviso no Project Validation do XRI; nomeá-la aqui
        /// deixa a configuração versionada, em vez de depender de alguém
        /// lembrar de clicar no botão "Fix".
        /// </summary>
        private static void ConfigureInteractionLayers()
        {
            const string path = "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset";
            const int teleportLayer = 31;
            const string teleportName = "Teleport";

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (settings == null)
            {
                Debug.LogWarning($"[ProjectConfigurator] {path} nao encontrado; " +
                                 "nomeie a camada de interacao 31 como 'Teleport' manualmente.");
                return;
            }

            var serialized = new SerializedObject(settings);
            var names = serialized.FindProperty("m_LayerNames");

            if (names == null || names.arraySize <= teleportLayer)
            {
                Debug.LogWarning("[ProjectConfigurator] Lista de camadas de interacao em formato inesperado.");
                return;
            }

            var entry = names.GetArrayElementAtIndex(teleportLayer);

            if (entry.stringValue == teleportName)
                return;

            entry.stringValue = teleportName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            Debug.Log($"[ProjectConfigurator] Camada de interacao {teleportLayer} nomeada como '{teleportName}'.");
        }

        private static void ConfigureQualityAndPhysics()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // 72 Hz e a taxa nativa do Quest 2; o passo fixo da fisica acompanha.
            Time.fixedDeltaTime = 1f / 72f;

            QualitySettings.vSyncCount = 0;
            // Qualificado porque URP tambem define tipos com estes nomes.
            QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Giuliano Tumoli";
            PlayerSettings.productName = "Escape Room VR";
            PlayerSettings.applicationIdentifier = "com.giulianotumoli.escaperoomvr";

            // --- Android / Meta Quest ---
            var android = NamedBuildTarget.Android;

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Vulkan primeiro: e o caminho recomendado no Quest atual.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
            });

            // --- PC (para rodar com o simulador no Editor e no build de PC) ---
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);
        }

        /// <summary>
        /// Liga o OpenXR como loader de XR no PC e no Android e habilita os
        /// perfis de controle do Quest. Equivale a mexer em
        /// Project Settings > XR Plug-in Management.
        /// </summary>
        private static void ConfigureOpenXR()
        {
            EnableOpenXRForGroup(BuildTargetGroup.Standalone);

            // O OpenXR so cria as configuracoes de Android se o modulo
            // "Android Build Support" estiver instalado neste Editor.
            if (UnityEditor.BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EnableOpenXRForGroup(BuildTargetGroup.Android);
                return;
            }

            Debug.LogWarning("[ProjectConfigurator] O modulo Android Build Support nao esta instalado nesta " +
                             "versao do Editor, por isso a configuracao de XR para Android foi pulada. " +
                             "Instale pelo Unity Hub (Add modules > Android Build Support + OpenJDK + " +
                             "Android SDK & NDK Tools) e rode este menu de novo antes de buildar para o Quest.");
        }

        private static void EnableOpenXRForGroup(BuildTargetGroup group)
        {
            var settings = GetOrCreateXRGeneralSettings(group);

            if (settings == null)
            {
                Debug.LogWarning($"[ProjectConfigurator] Nao foi possivel preparar XR settings para {group}. " +
                                 "Habilite o OpenXR manualmente em Project Settings > XR Plug-in Management.");
                return;
            }

            const string loaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";

            if (!XRPackageMetadataStore.AssignLoader(settings.Manager, loaderTypeName, group))
                Debug.LogWarning($"[ProjectConfigurator] Falhou ao atribuir o OpenXRLoader em {group}.");

            // As instancias de feature do OpenXR sao criadas sob demanda. Sem
            // este refresh, GetSettingsForBuildTargetGroup devolve settings
            // sem nenhuma feature (ou nulo, no caso do Android recem-ligado).
            FeatureHelpers.RefreshFeatures(group);

            EnableOpenXRFeatures(group);
        }

        private static XRGeneralSettings GetOrCreateXRGeneralSettings(BuildTargetGroup group)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget perTarget);

            if (perTarget == null)
            {
                const string path = SettingsFolder + "/XRGeneralSettings.asset";
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);

                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, path);
                }

                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            if (!perTarget.HasSettingsForBuildTarget(group))
                perTarget.CreateDefaultSettingsForBuildTarget(group);

            if (!perTarget.HasManagerSettingsForBuildTarget(group))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            return perTarget.SettingsForBuildTarget(group);
        }

        private static void EnableOpenXRFeatures(BuildTargetGroup group)
        {
            var openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);

            if (openXrSettings == null)
            {
                Debug.LogWarning($"[ProjectConfigurator] OpenXRSettings nao encontrado para {group}.");
                return;
            }

            // Perfil de controle: sem um perfil ativo os controles nao mandam input.
            EnableFeature<OculusTouchControllerProfile>(openXrSettings);
            EnableFeature<MetaQuestTouchPlusControllerProfile>(openXrSettings);

            if (group == BuildTargetGroup.Android)
                EnableFeatureByName(openXrSettings, "MetaQuestFeature");

            EditorUtility.SetDirty(openXrSettings);
        }

        private static void EnableFeature<T>(OpenXRSettings settings) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var feature = settings.GetFeature<T>();

            if (feature == null)
            {
                Debug.LogWarning($"[ProjectConfigurator] Feature {typeof(T).Name} nao disponivel.");
                return;
            }

            feature.enabled = true;
        }

        /// <summary>
        /// Habilita uma feature pelo nome do tipo. A MetaQuestFeature é interna
        /// ao pacote em algumas versões, então chegamos nela por reflexão em
        /// vez de referenciar o tipo diretamente.
        /// </summary>
        private static void EnableFeatureByName(OpenXRSettings settings, string typeName)
        {
            var features = settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>();
            var match = features.FirstOrDefault(f => f != null && f.GetType().Name == typeName);

            if (match == null)
            {
                Debug.LogWarning($"[ProjectConfigurator] Feature {typeName} nao encontrada; " +
                                 "habilite 'Meta Quest Support' manualmente se for buildar para o Quest.");
                return;
            }

            match.enabled = true;
        }
    }
}
