using EscapeRoomVR.Core;
using EscapeRoomVR.Feedback;
using TMPro;
using UnityEngine;

namespace EscapeRoomVR.UI
{
    /// <summary>
    /// HUD da partida: cronômetro, objetivo atual e o texto das pistas.
    /// Vive num Canvas em world space que acompanha o olhar com folga
    /// (<see cref="LazyFollow"/>).
    ///
    /// Reage aos eventos do <see cref="GameManager"/> em vez de consultá-lo a
    /// cada frame, exceto pelo cronômetro, que precisa atualizar sempre.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        /// <summary>Instância única, para os objetos da cena mostrarem pistas.</summary>
        public static HudController Instance { get; private set; }

        [Header("Referências de texto")]
        [SerializeField] private TextMeshProUGUI timerLabel;
        [SerializeField] private TextMeshProUGUI objectiveLabel;
        [SerializeField] private TextMeshProUGUI clueLabel;
        [SerializeField] private TextMeshProUGUI progressLabel;

        [Header("Painel de pista")]
        [Tooltip("Objeto ligado/desligado junto com o texto da pista.")]
        [SerializeField] private GameObject cluePanel;

        [Header("Cores do cronômetro")]
        [SerializeField] private Color normalColor = new Color(0.85f, 0.95f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0.35f);

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;

        private GameManager game;
        private int lastTickSecond = -1;

        private void Awake()
        {
            Instance = this;

            if (cluePanel != null)
                cluePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (game != null)
            {
                game.StepCompleted -= OnStepCompleted;
                game.StateChanged -= OnStateChanged;
            }
        }

        private void Start()
        {
            game = GameManager.Instance;

            if (game == null)
            {
                Debug.LogWarning("[HudController] GameManager nao encontrado na cena.");
                return;
            }

            game.StepCompleted += OnStepCompleted;
            game.StateChanged += OnStateChanged;

            RefreshObjective();
            RefreshProgress();
        }

        private void Update()
        {
            if (game == null || timerLabel == null)
                return;

            var timer = game.Timer;
            timerLabel.text = timer.FormattedRemaining;
            timerLabel.color = timer.IsInWarning ? warningColor : normalColor;

            TickSoundIfNeeded(timer);
        }

        /// <summary>Toca um tique por segundo durante o último minuto.</summary>
        private void TickSoundIfNeeded(GameTimer timer)
        {
            if (!timer.IsRunning || !timer.IsInWarning || audioCue == null)
                return;

            int second = Mathf.CeilToInt(timer.Remaining);

            if (second == lastTickSecond)
                return;

            lastTickSecond = second;
            audioCue.Play(CueSound.Tick, 0.35f);
        }

        /// <summary>Mostra um texto de pista no HUD (chamado pelos objetos legíveis).</summary>
        public void ShowClue(string text)
        {
            if (clueLabel != null)
                clueLabel.text = text;

            if (cluePanel != null)
                cluePanel.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        /// <summary>Esconde o painel de pista.</summary>
        public void HideClue()
        {
            if (cluePanel != null)
                cluePanel.SetActive(false);
        }

        private void OnStepCompleted(PuzzleStep step)
        {
            RefreshObjective();
            RefreshProgress();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Running)
            {
                RefreshObjective();
                RefreshProgress();
            }
        }

        private void RefreshProgress()
        {
            if (progressLabel == null || game == null)
                return;

            progressLabel.text = $"Enigmas: {game.Progress.CompletedCount}/{PuzzleProgress.TotalSteps}";
        }

        private void RefreshObjective()
        {
            if (objectiveLabel == null || game == null)
                return;

            objectiveLabel.text = NextObjectiveText(game.Progress);
        }

        /// <summary>
        /// Traduz o progresso no próximo objetivo em texto. Fica aqui, e não no
        /// GameManager, porque é apresentação e não regra de jogo.
        /// </summary>
        private static string NextObjectiveText(PuzzleProgress progress)
        {
            if (!progress.IsCompleted(PuzzleStep.NoteFound))
                return "Procure algo escrito na sala.";

            if (!progress.IsCompleted(PuzzleStep.PaintingRemoved))
                return "O que o quadro esconde?";

            if (!progress.IsCompleted(PuzzleStep.LeversSolved))
                return "Repita o padrao do painel nas alavancas.";

            if (!progress.IsCompleted(PuzzleStep.SafeOpened))
                return "Digite o codigo de 4 digitos no cofre.";

            if (!progress.IsCompleted(PuzzleStep.CardInserted))
                return "Use o cartao no leitor da porta.";

            return "A porta esta aberta. Saia da sala!";
        }
    }
}
