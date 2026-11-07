using EscapeRoomVR.Core;
using EscapeRoomVR.Feedback;
using EscapeRoomVR.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Interaction
{
    /// <summary>
    /// Objeto que revela um texto quando o jogador o pega e o aproxima do rosto.
    /// É assim que o bilhete da gaveta entrega a primeira pista.
    ///
    /// A leitura é por proximidade da câmera (e não por um botão) porque o gesto
    /// de "trazer perto dos olhos para ler" é natural em VR e dispensa explicar
    /// um controle novo ao jogador.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Inspectable : MonoBehaviour
    {
        [Header("Conteúdo")]
        [Tooltip("Texto mostrado no HUD enquanto o objeto estiver perto do rosto.")]
        [TextArea(2, 5)]
        [SerializeField] private string clueText = "";

        [Tooltip("Distância da câmera, em metros, para o texto aparecer.")]
        [SerializeField] private float readDistance = 0.45f;

        [Header("Progresso")]
        [Tooltip("Etapa marcada como concluída na primeira leitura. Deixe em None para não marcar nada.")]
        [SerializeField] private bool completesStep = true;

        [SerializeField] private PuzzleStep step = PuzzleStep.NoteFound;

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;

        /// <summary>True depois que o jogador leu o objeto pela primeira vez.</summary>
        public bool WasRead { get; private set; }

        private XRGrabInteractable grabInteractable;
        private Transform headTransform;
        private bool isHeld;
        private bool isShowingClue;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (grabInteractable == null)
                return;

            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            if (grabInteractable == null)
                return;

            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);

            if (isShowingClue)
                HideClue();
        }

        private void Update()
        {
            if (!isHeld)
                return;

            if (headTransform == null)
            {
                // A camera principal e o "rosto" do jogador dentro do XR Origin.
                var mainCamera = Camera.main;
                if (mainCamera == null)
                    return;

                headTransform = mainCamera.transform;
            }

            bool close = Vector3.Distance(transform.position, headTransform.position) <= readDistance;

            if (close && !isShowingClue)
                ShowClue();
            else if (!close && isShowingClue)
                HideClue();
        }

        private void OnGrabbed(SelectEnterEventArgs args) => isHeld = true;

        private void OnReleased(SelectExitEventArgs args)
        {
            isHeld = false;

            if (isShowingClue)
                HideClue();
        }

        private void ShowClue()
        {
            isShowingClue = true;

            if (HudController.Instance != null)
                HudController.Instance.ShowClue(clueText);

            if (!WasRead)
            {
                WasRead = true;

                if (audioCue != null)
                    audioCue.Play(CueSound.Success, 0.6f);

                if (completesStep && GameManager.Instance != null)
                    GameManager.Instance.CompleteStep(step);
            }
        }

        private void HideClue()
        {
            isShowingClue = false;

            if (HudController.Instance != null)
                HudController.Instance.HideClue();
        }
    }
}
