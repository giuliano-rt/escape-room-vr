using System;
using EscapeRoomVR.Feedback;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Puzzles
{
    /// <summary>
    /// Alavanca de duas posições (cima/baixo). O jogador aciona pegando/tocando
    /// o braço da alavanca com o controle: o XRSimpleInteractable avisa o
    /// "selectEntered" e aqui a gente alterna o estado e anima o braço.
    ///
    /// Escolhemos XRSimpleInteractable em vez de uma junta física de propósito:
    /// o estado da alavanca tem de ser inequívoco para o enigma validar, e uma
    /// junta pode parar no meio do curso.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class Lever : MonoBehaviour
    {
        [Header("Braço da alavanca")]
        [Tooltip("Transform que gira. Normalmente um filho deste objeto.")]
        [SerializeField] private Transform handle;

        [Tooltip("Ângulo do braço na posição 'baixo', em graus.")]
        [SerializeField] private float downAngle = 35f;

        [Tooltip("Ângulo do braço na posição 'cima', em graus.")]
        [SerializeField] private float upAngle = -35f;

        [Tooltip("Eixo local em torno do qual o braço gira.")]
        [SerializeField] private Vector3 rotationAxis = Vector3.right;

        [Tooltip("Segundos para o braço completar o curso.")]
        [SerializeField] private float travelDuration = 0.18f;

        [Header("Estado")]
        [Tooltip("Posição inicial da alavanca.")]
        [SerializeField] private bool startsUp;

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;

        /// <summary>True quando a alavanca está na posição de cima.</summary>
        public bool IsUp { get; private set; }

        /// <summary>Disparado a cada acionamento, com o novo estado.</summary>
        public event Action<Lever> StateChanged;

        private XRSimpleInteractable interactable;
        private Quaternion downRotation;
        private Quaternion upRotation;
        private float lerpProgress = 1f;
        private Quaternion lerpFrom;
        private Quaternion lerpTo;

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();

            if (handle == null)
                handle = transform;

            Vector3 axis = rotationAxis.sqrMagnitude < 0.0001f ? Vector3.right : rotationAxis.normalized;
            Quaternion baseRotation = handle.localRotation;
            downRotation = baseRotation * Quaternion.AngleAxis(downAngle, axis);
            upRotation = baseRotation * Quaternion.AngleAxis(upAngle, axis);

            IsUp = startsUp;
            handle.localRotation = IsUp ? upRotation : downRotation;
        }

        private void OnEnable()
        {
            if (interactable != null)
                interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void Update()
        {
            if (lerpProgress >= 1f)
                return;

            lerpProgress += Time.deltaTime / Mathf.Max(0.01f, travelDuration);
            handle.localRotation = Quaternion.Slerp(lerpFrom, lerpTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lerpProgress)));
        }

        private void OnSelectEntered(SelectEnterEventArgs args) => Toggle();

        /// <summary>Alterna a alavanca entre cima e baixo.</summary>
        public void Toggle() => SetUp(!IsUp);

        /// <summary>Força a alavanca para uma posição específica.</summary>
        public void SetUp(bool up)
        {
            IsUp = up;

            lerpFrom = handle.localRotation;
            lerpTo = up ? upRotation : downRotation;
            lerpProgress = 0f;

            if (audioCue != null)
                audioCue.Play(CueSound.Beep, 0.7f, up ? 1.15f : 0.85f);

            StateChanged?.Invoke(this);
        }
    }
}
