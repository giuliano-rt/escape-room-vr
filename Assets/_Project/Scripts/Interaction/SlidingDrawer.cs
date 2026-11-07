using System;
using EscapeRoomVR.Feedback;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Interaction
{
    /// <summary>
    /// Gaveta que o jogador puxa com a mão, correndo num trilho.
    ///
    /// O movimento é calculado aqui em vez de usar uma junta física: o
    /// XRGrabInteractable fica com trackPosition/trackRotation desligados
    /// (então o XRI não arrasta a gaveta livremente) e a cada frame a posição
    /// da mão é projetada no eixo do trilho e limitada ao curso. O resultado é
    /// uma gaveta que nunca sai do lugar, não treme e não atravessa o móvel —
    /// problemas comuns quando se resolve isso com ConfigurableJoint.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SlidingDrawer : MonoBehaviour
    {
        [Header("Trilho")]
        [Tooltip("Eixo de abertura, nas coordenadas locais do móvel (o pai).")]
        [SerializeField] private Vector3 slideAxis = Vector3.forward;

        [Tooltip("Curso total da gaveta, em metros.")]
        [SerializeField] private float travelDistance = 0.35f;

        [Tooltip("Fração do curso a partir da qual a gaveta conta como aberta.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float openThreshold = 0.45f;

        [Tooltip("Suavização do movimento. Maior = a gaveta acompanha a mão mais rápido.")]
        [SerializeField] private float followSpeed = 18f;

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;

        /// <summary>Quanto a gaveta está aberta, de 0 (fechada) a 1 (toda aberta).</summary>
        public float Openness => travelDistance <= 0f ? 0f : Mathf.Clamp01(currentDistance / travelDistance);

        /// <summary>True enquanto a gaveta estiver além do limiar de abertura.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Disparado na primeira vez que a gaveta é aberta.</summary>
        public event Action FirstOpened;

        private XRGrabInteractable grabInteractable;
        private Transform rail;              // espaço de referência do trilho (o móvel)
        private Vector3 closedLocalPosition;
        private Vector3 axis;

        private float currentDistance;       // quanto a gaveta está puxada, em metros
        private float targetDistance;
        private float grabDelta;             // diferença entre a mão e a gaveta no instante do grab
        private Transform heldBy;
        private bool everOpened;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();

            rail = transform.parent != null ? transform.parent : transform;
            closedLocalPosition = transform.localPosition;
            axis = slideAxis.sqrMagnitude < 0.0001f ? Vector3.forward : slideAxis.normalized;

            // O XRI nao deve mover a gaveta: quem define a posicao e este script.
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
            grabInteractable.throwOnDetach = false;

            var body = GetComponent<Rigidbody>();

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void OnEnable()
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        private void Update()
        {
            if (heldBy != null)
                targetDistance = Mathf.Clamp(HandDistance() + grabDelta, 0f, travelDistance);

            currentDistance = Mathf.Lerp(currentDistance, targetDistance,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));

            transform.localPosition = closedLocalPosition + axis * currentDistance;

            UpdateOpenState();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            heldBy = args.interactorObject.transform;
            // Guarda a folga para a gaveta nao "pular" para a posicao da mao.
            grabDelta = currentDistance - HandDistance();
        }

        private void OnSelectExited(SelectExitEventArgs args) => heldBy = null;

        /// <summary>Distância da mão ao longo do eixo do trilho, em metros.</summary>
        private float HandDistance()
        {
            if (heldBy == null)
                return currentDistance;

            Vector3 handLocal = rail.InverseTransformPoint(heldBy.position);
            return Vector3.Dot(handLocal - closedLocalPosition, axis);
        }

        private void UpdateOpenState()
        {
            bool nowOpen = Openness >= openThreshold;

            if (nowOpen == IsOpen)
                return;

            IsOpen = nowOpen;

            if (!IsOpen)
                return;

            if (audioCue != null)
                audioCue.Play(CueSound.Beep, 0.5f, 0.7f);

            if (everOpened)
                return;

            everOpened = true;
            FirstOpened?.Invoke();
        }
    }
}
