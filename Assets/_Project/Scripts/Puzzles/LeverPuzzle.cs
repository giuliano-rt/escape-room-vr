using System;
using EscapeRoomVR.Core;
using EscapeRoomVR.Feedback;
using UnityEngine;
using UnityEngine.Events;

namespace EscapeRoomVR.Puzzles
{
    /// <summary>
    /// Enigma das alavancas: compara a posição das alavancas com a combinação
    /// esperada (a mesma desenhada no painel escondido atrás do quadro).
    /// Quando bate, marca a etapa no <see cref="GameManager"/> e dispara o
    /// UnityEvent que revela os dois últimos dígitos do código do cofre.
    /// </summary>
    public class LeverPuzzle : MonoBehaviour
    {
        [Header("Alavancas, na ordem da esquerda para a direita")]
        [SerializeField] private Lever[] levers = Array.Empty<Lever>();

        [Tooltip("Combinação correta: marcado = alavanca para cima.")]
        [SerializeField] private bool[] expectedPattern = { true, false, true };

        [Header("Consequência")]
        [Tooltip("Objetos ligados quando o enigma é resolvido (os dois últimos dígitos do código).")]
        [SerializeField] private GameObject[] objectsToReveal = Array.Empty<GameObject>();

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;
        [SerializeField] private EmissiveIndicator indicator;

        [Tooltip("Ações extras, ligadas manualmente no Inspector.")]
        [SerializeField] private UnityEvent onSolved;

        /// <summary>True depois que a combinação correta foi atingida.</summary>
        public bool IsSolved { get; private set; }

        /// <summary>Disparado uma única vez, na resolução.</summary>
        public event Action Solved;

        private void Awake()
        {
            // A pista final comeca escondida; so aparece quando as alavancas acertam.
            foreach (var revealed in objectsToReveal)
            {
                if (revealed != null)
                    revealed.SetActive(false);
            }
        }

        private void OnEnable()
        {
            foreach (var lever in levers)
            {
                if (lever != null)
                    lever.StateChanged += OnLeverChanged;
            }
        }

        private void OnDisable()
        {
            foreach (var lever in levers)
            {
                if (lever != null)
                    lever.StateChanged -= OnLeverChanged;
            }
        }

        private void OnLeverChanged(Lever lever)
        {
            if (IsSolved)
                return;

            if (!MatchesPattern())
                return;

            Solve();
        }

        private bool MatchesPattern()
        {
            if (levers.Length == 0 || expectedPattern.Length < levers.Length)
                return false;

            for (int i = 0; i < levers.Length; i++)
            {
                if (levers[i] == null || levers[i].IsUp != expectedPattern[i])
                    return false;
            }

            return true;
        }

        private void Solve()
        {
            IsSolved = true;

            foreach (var revealed in objectsToReveal)
            {
                if (revealed != null)
                    revealed.SetActive(true);
            }

            if (indicator != null)
                indicator.SetUnlocked(true);

            if (audioCue != null)
                audioCue.Play(CueSound.Success);

            if (GameManager.Instance != null)
                GameManager.Instance.CompleteStep(PuzzleStep.LeversSolved);

            onSolved?.Invoke();
            Solved?.Invoke();
        }
    }
}
