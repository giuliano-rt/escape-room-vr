using EscapeRoomVR.Feedback;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Interaction
{
    /// <summary>
    /// Botão 3D genérico: um XRSimpleInteractable que dispara um UnityEvent
    /// quando acionado. Usado nos botões das telas (reiniciar, sair) para não
    /// depender de um Canvas com event system de UI.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class InteractableButton : MonoBehaviour
    {
        [Tooltip("O que acontece ao acionar o botão.")]
        [SerializeField] private UnityEvent onPressed;

        [SerializeField] private AudioCue audioCue;

        private XRSimpleInteractable interactable;

        private void Awake() => interactable = GetComponent<XRSimpleInteractable>();

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

        private void OnSelectEntered(SelectEnterEventArgs args) => Press();

        /// <summary>Aciona o botão.</summary>
        public void Press()
        {
            if (audioCue != null)
                audioCue.Play(CueSound.Click);

            onPressed?.Invoke();
        }
    }
}
