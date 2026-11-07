using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Instala, via linha de comando, os pacotes de que o projeto depende.
    /// Os nomes vão sem versão de propósito: o Package Manager resolve a
    /// versão mais recente compatível com esta versão do Editor.
    /// </summary>
    public static class PackageSetup
    {
        private static readonly string[] RequiredPackages =
        {
            "com.unity.render-pipelines.universal", // URP: pipeline recomendada para Quest
            "com.unity.ugui",                       // Canvas + TextMeshPro (HUD e telas)
            "com.unity.inputsystem",                // Input System (exigido pelo XRI)
            "com.unity.xr.interaction.toolkit",     // XR Interaction Toolkit: interação e locomoção
            "com.unity.xr.openxr"                   // OpenXR: runtime usado pelo Meta Quest
        };

        [MenuItem("Escape Room VR/1. Instalar pacotes")]
        public static void InstallRequiredPackages()
        {
            var toAdd = new List<string>(RequiredPackages);
            Debug.Log($"[PackageSetup] Adicionando {toAdd.Count} pacotes...");

            AddAndRemoveRequest request = Client.AddAndRemove(toAdd.ToArray(), null);

            // Em batchmode não há loop de editor para aguardar, então esperamos ativamente.
            while (!request.IsCompleted)
                Thread.Sleep(200);

            if (request.Status == StatusCode.Failure)
            {
                Debug.LogError($"[PackageSetup] FALHOU: {request.Error?.message}");
                EditorApplication.Exit(1);
                return;
            }

            foreach (var package in request.Result)
                Debug.Log($"[PackageSetup] OK {package.name}@{package.version}");

            Debug.Log("[PackageSetup] Concluido.");
        }
    }
}
