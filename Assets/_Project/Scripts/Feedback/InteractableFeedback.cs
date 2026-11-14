using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Feedback
{
    /// <summary>
    /// Retorno sensorial de qualquer objeto interativo: ele se acende quando o
    /// jogador mira nele e toca um som ao ser pego e ao ser solto.
    ///
    /// Em VR, nada na cena diz ao jogador o que dá para tocar — todo objeto
    /// parece igualmente sólido. O realce no hover é o que transforma a sala
    /// de "um monte de caixas" em "um monte de coisas que eu posso usar", e é
    /// também o que torna a interação legível para quem assiste de fora, numa
    /// gravação de tela.
    ///
    /// O realce é feito mexendo na emissão de cópias do material, e não
    /// trocando o material inteiro, para o objeto manter a própria cor e só
    /// parecer iluminado por dentro.
    /// </summary>
    public class InteractableFeedback : MonoBehaviour
    {
        [Header("Realce ao mirar")]
        [SerializeField] private Color highlightColor = new Color(0.45f, 0.85f, 1f);

        [Tooltip("Intensidade do brilho ao mirar no objeto.")]
        [SerializeField] private float highlightStrength = 0.55f;

        [Tooltip("Velocidade com que o brilho entra e sai.")]
        [SerializeField] private float fadeSpeed = 10f;

        [Header("Som")]
        [SerializeField] private AudioCue audioCue;

        [Tooltip("Toca um som ao pegar e ao soltar o objeto.")]
        [SerializeField] private bool playGrabSounds = true;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private XRBaseInteractable interactable;
        private readonly List<Material> highlightMaterials = new List<Material>();
        private readonly List<Color> baseEmissions = new List<Color>();

        private float currentStrength;
        private float targetStrength;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            CacheMaterials();
        }

        private void OnEnable()
        {
            if (interactable == null)
                return;

            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            if (interactable == null)
                return;

            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
        }

        private void Update()
        {
            if (Mathf.Approximately(currentStrength, targetStrength))
                return;

            currentStrength = Mathf.MoveTowards(currentStrength, targetStrength, fadeSpeed * Time.deltaTime);
            ApplyEmission(currentStrength);
        }

        /// <summary>
        /// Guarda uma cópia do material de cada renderer e a emissão original
        /// dele, para o realce poder ser somado e desfeito sem afetar os
        /// outros objetos que usam o mesmo material compartilhado.
        /// </summary>
        private void CacheMaterials()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                // Texto do TextMeshPro nao deve acender junto: o material dele
                // e de fonte, e mexer na emissao borra o glifo.
                if (renderer.GetComponent<TMPro.TMP_Text>() != null)
                    continue;

                var material = renderer.material;

                if (!material.HasProperty(EmissionColorId))
                    continue;

                material.EnableKeyword("_EMISSION");
                highlightMaterials.Add(material);
                baseEmissions.Add(material.GetColor(EmissionColorId));
            }
        }

        private void ApplyEmission(float strength)
        {
            for (int i = 0; i < highlightMaterials.Count; i++)
                highlightMaterials[i].SetColor(EmissionColorId, baseEmissions[i] + highlightColor * (highlightStrength * strength));
        }

        private void OnHoverEntered(HoverEnterEventArgs args) => targetStrength = 1f;

        private void OnHoverExited(HoverExitEventArgs args) => targetStrength = 0f;

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (playGrabSounds && audioCue != null)
                audioCue.Play(CueSound.Click, 0.8f, 1.2f);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (playGrabSounds && audioCue != null)
                audioCue.Play(CueSound.Click, 0.5f, 0.85f);
        }
    }
}
