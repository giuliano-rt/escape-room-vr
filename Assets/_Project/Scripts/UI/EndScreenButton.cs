using EscapeRoomVR.Feedback;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.UI
{
    /// <summary>Ações disponíveis nos botões da tela de fim de partida.</summary>
    public enum EndScreenAction
    {
        Restart,
        Quit
    }

    /// <summary>
    /// Botão da tela de fim de partida. Usa referência direta ao
    /// <see cref="EndScreenController"/> em vez de UnityEvent para que o
    /// construtor de cena possa ligá-lo por código e a ligação fique salva
    /// na cena (listeners adicionados em runtime não são serializados).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class EndScreenButton : MonoBehaviour
    {
        [SerializeField] private EndScreenController endScreen;
        [SerializeField] private EndScreenAction action = EndScreenAction.Restart;
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

        /// <summary>Executa a ação configurada.</summary>
        public void Press()
        {
            if (audioCue != null)
                audioCue.Play(CueSound.Click);

            if (endScreen == null)
                return;

            if (action == EndScreenAction.Restart)
                endScreen.Restart();
            else
                endScreen.Quit();
        }
    }
}
