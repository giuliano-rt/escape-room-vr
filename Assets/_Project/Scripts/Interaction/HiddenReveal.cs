using System;
using EscapeRoomVR.Core;
using EscapeRoomVR.Feedback;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Interaction
{
    /// <summary>
    /// Objeto que esconde algo atrás de si: ao ser retirado do lugar pela
    /// primeira vez, mostra os objetos escondidos. É o quadro da parede, que
    /// revela o painel com a combinação das alavancas e os dois primeiros
    /// dígitos do código do cofre.
    ///
    /// Começa cinemático (fixo na parede) e vira dinâmico quando é pego, para
    /// que o jogador possa levá-lo ou jogá-lo no chão depois.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class HiddenReveal : MonoBehaviour
    {
        [Header("O que está escondido")]
        [Tooltip("Objetos desligados no início e ligados quando este for retirado.")]
        [SerializeField] private GameObject[] hiddenObjects = Array.Empty<GameObject>();

        [Header("Progresso")]
        [SerializeField] private bool completesStep = true;
        [SerializeField] private PuzzleStep step = PuzzleStep.PaintingRemoved;

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;

        [Tooltip("Ações extras ao revelar.")]
        [SerializeField] private UnityEvent onRevealed;

        /// <summary>True depois que o esconderijo foi revelado.</summary>
        public bool WasRevealed { get; private set; }

        /// <summary>Disparado na revelação.</summary>
        public event Action Revealed;

        private XRGrabInteractable grabInteractable;
        private Rigidbody body;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();

            foreach (var hidden in hiddenObjects)
            {
                if (hidden != null)
                    hidden.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (grabInteractable != null)
                grabInteractable.selectEntered.AddListener(OnGrabbed);
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => Reveal();

        /// <summary>Mostra os objetos escondidos e libera o objeto da parede.</summary>
        public void Reveal()
        {
            if (WasRevealed)
                return;

            WasRevealed = true;

            // Solta o objeto da parede para ele cair/ser carregado normalmente.
            if (body != null)
                body.isKinematic = false;

            foreach (var hidden in hiddenObjects)
            {
                if (hidden != null)
                    hidden.SetActive(true);
            }

            if (audioCue != null)
                audioCue.Play(CueSound.Success, 0.7f);

            if (completesStep && GameManager.Instance != null)
                GameManager.Instance.CompleteStep(step);

            onRevealed?.Invoke();
            Revealed?.Invoke();
        }
    }
}
