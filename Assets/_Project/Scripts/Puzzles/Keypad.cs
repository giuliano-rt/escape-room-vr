using System;
using System.Text;
using EscapeRoomVR.Core;
using EscapeRoomVR.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EscapeRoomVR.Puzzles
{
    /// <summary>
    /// Teclado numérico virtual do cofre. Recebe os dígitos das teclas
    /// (<see cref="KeypadKey"/>), mostra o que foi digitado no display e
    /// valida contra o código correto.
    ///
    /// A validação acontece automaticamente quando o número de dígitos
    /// digitados chega ao tamanho do código, e também ao apertar OK.
    /// </summary>
    public class Keypad : MonoBehaviour
    {
        [Header("Código")]
        [Tooltip("Código correto. Só dígitos.")]
        [SerializeField] private string correctCode = "7429";

        [Tooltip("Valida sozinho quando o jogador completa o número de dígitos.")]
        [SerializeField] private bool autoValidate = true;

        [Header("Display")]
        [Tooltip("Texto 3D que mostra os dígitos digitados.")]
        [SerializeField] private TextMeshPro display;

        [Tooltip("Caractere mostrado nas casas ainda não digitadas.")]
        [SerializeField] private string placeholder = "-";

        [Header("Consequência")]
        [Tooltip("Porta destravada e aberta quando o código é aceito (a porta do cofre).")]
        [SerializeField] private Interaction.AnimatedDoor doorToUnlock;

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;
        [SerializeField] private EmissiveIndicator indicator;

        [Tooltip("Ações extras ao destravar, ligadas manualmente no Inspector.")]
        [SerializeField] private UnityEvent onUnlocked;

        /// <summary>True depois que o código correto foi aceito.</summary>
        public bool IsUnlocked { get; private set; }

        /// <summary>Disparado quando o código correto é aceito.</summary>
        public event Action Unlocked;

        /// <summary>Disparado quando um código errado é submetido.</summary>
        public event Action Rejected;

        private readonly StringBuilder entered = new StringBuilder();
        private float displayResetAt = -1f;

        private void Awake() => RefreshDisplay();

        private void Update()
        {
            // Depois de um erro, o display pisca a mensagem e volta ao normal.
            if (displayResetAt > 0f && Time.time >= displayResetAt)
            {
                displayResetAt = -1f;
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Processa uma tecla. Dígitos somam ao código; "C" limpa; "OK" valida.
        /// Chamado por <see cref="KeypadKey"/>.
        /// </summary>
        public void Press(string key)
        {
            if (IsUnlocked || string.IsNullOrEmpty(key))
                return;

            if (audioCue != null)
                audioCue.Play(CueSound.Click, 0.9f);

            if (key == "C")
            {
                entered.Clear();
                RefreshDisplay();
                return;
            }

            if (key == "OK")
            {
                Validate();
                return;
            }

            if (entered.Length >= correctCode.Length)
                entered.Clear();

            entered.Append(key);
            RefreshDisplay();

            if (autoValidate && entered.Length == correctCode.Length)
                Validate();
        }

        /// <summary>Compara o que foi digitado com o código correto.</summary>
        public void Validate()
        {
            if (IsUnlocked)
                return;

            if (entered.ToString() == correctCode)
                Accept();
            else
                Reject();
        }

        private void Accept()
        {
            IsUnlocked = true;

            if (display != null)
            {
                display.text = "OK";
                display.color = new Color(0.3f, 1f, 0.4f);
            }

            if (indicator != null)
                indicator.SetUnlocked(true);

            if (audioCue != null)
                audioCue.Play(CueSound.Unlock);

            if (GameManager.Instance != null)
                GameManager.Instance.CompleteStep(PuzzleStep.SafeOpened);

            if (doorToUnlock != null)
                doorToUnlock.Unlock();

            onUnlocked?.Invoke();
            Unlocked?.Invoke();
        }

        private void Reject()
        {
            entered.Clear();

            if (display != null)
            {
                display.text = "ERRO";
                display.color = new Color(1f, 0.35f, 0.3f);
            }

            if (audioCue != null)
                audioCue.Play(CueSound.Error);

            displayResetAt = Time.time + 1f;
            Rejected?.Invoke();
        }

        private void RefreshDisplay()
        {
            if (display == null)
                return;

            var text = new StringBuilder();

            for (int i = 0; i < correctCode.Length; i++)
                text.Append(i < entered.Length ? entered[i].ToString() : placeholder);

            display.text = text.ToString();
            display.color = new Color(0.45f, 1f, 0.85f);
        }
    }
}
