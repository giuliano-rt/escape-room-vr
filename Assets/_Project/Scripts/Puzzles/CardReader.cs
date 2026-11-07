using System;
using EscapeRoomVR.Core;
using EscapeRoomVR.Feedback;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EscapeRoomVR.Puzzles
{
    /// <summary>
    /// Leitor de cartão ao lado da porta final. É um XRSocketInteractor:
    /// o encaixe é feito pelo próprio XRI (o objeto "cola" no slot), e aqui
    /// só validamos *qual* objeto foi encaixado.
    ///
    /// Qualquer objeto agarrável pode entrar no socket; se não for o
    /// <see cref="KeyCard"/> esperado, o leitor recusa com som de erro e
    /// devolve o objeto ao jogador.
    /// </summary>
    [RequireComponent(typeof(XRSocketInteractor))]
    public class CardReader : MonoBehaviour
    {
        [Tooltip("Id do cartão que este leitor aceita.")]
        [SerializeField] private string expectedCardId = "EXIT";

        [Header("Consequência")]
        [Tooltip("Porta destravada e aberta quando o cartão certo é encaixado.")]
        [SerializeField] private Interaction.AnimatedDoor doorToUnlock;

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;
        [SerializeField] private EmissiveIndicator indicator;

        [Tooltip("Ações extras, ligadas manualmente no Inspector.")]
        [SerializeField] private UnityEvent onAccepted;

        /// <summary>True depois que o cartão correto foi aceito.</summary>
        public bool HasAccepted { get; private set; }

        /// <summary>Disparado quando o cartão correto é aceito.</summary>
        public event Action Accepted;

        private XRSocketInteractor socket;

        private void Awake() => socket = GetComponent<XRSocketInteractor>();

        private void OnEnable()
        {
            if (socket != null)
                socket.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (socket != null)
                socket.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (HasAccepted)
                return;

            var card = args.interactableObject.transform.GetComponentInParent<KeyCard>();

            if (card == null || card.CardId != expectedCardId)
            {
                if (audioCue != null)
                    audioCue.Play(CueSound.Error);

                return;
            }

            HasAccepted = true;

            if (indicator != null)
                indicator.SetUnlocked(true);

            if (audioCue != null)
                audioCue.Play(CueSound.Unlock);

            if (GameManager.Instance != null)
                GameManager.Instance.CompleteStep(PuzzleStep.CardInserted);

            if (doorToUnlock != null)
                doorToUnlock.Unlock();

            onAccepted?.Invoke();
            Accepted?.Invoke();
        }
    }
}
