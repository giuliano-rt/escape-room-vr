using System;
using UnityEngine;

namespace EscapeRoomVR.Core
{
    /// <summary>
    /// Ponto central da partida: controla o estado, o cronômetro e o progresso
    /// dos enigmas. Todos os outros componentes falam com ele por
    /// <see cref="Instance"/> e reagem aos eventos públicos, para que nenhum
    /// enigma precise conhecer os outros.
    /// </summary>
    [RequireComponent(typeof(GameTimer))]
    public class GameManager : MonoBehaviour
    {
        /// <summary>Instância única acessível por toda a cena.</summary>
        public static GameManager Instance { get; private set; }

        [Header("Configuração")]
        [Tooltip("Se marcado, a partida começa sozinha ao carregar a cena.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("Segundos de bônus concedidos a cada enigma resolvido.")]
        [SerializeField] private float bonusPerPuzzle = 0f;

        /// <summary>Cronômetro da partida.</summary>
        public GameTimer Timer { get; private set; }

        /// <summary>Progresso da cadeia de enigmas.</summary>
        public PuzzleProgress Progress { get; } = new PuzzleProgress();

        /// <summary>Estado atual da partida.</summary>
        public GameState State { get; private set; } = GameState.Idle;

        /// <summary>Tempo que o jogador levou para escapar (válido após a vitória).</summary>
        public float ElapsedOnWin { get; private set; }

        /// <summary>Disparado a cada mudança de estado da partida.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Repasse de <see cref="PuzzleProgress.StepCompleted"/> para quem só tem o manager.</summary>
        public event Action<PuzzleStep> StepCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Timer = GetComponent<GameTimer>();
            Progress.StepCompleted += OnStepCompleted;
            Timer.TimeExpired += OnTimeExpired;
        }

        private void Start()
        {
            if (autoStart)
                StartGame();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            Progress.StepCompleted -= OnStepCompleted;

            if (Timer != null)
                Timer.TimeExpired -= OnTimeExpired;
        }

        /// <summary>Começa (ou reinicia) a partida: zera o progresso e liga o cronômetro.</summary>
        public void StartGame()
        {
            Progress.Reset();
            Timer.StartCountdown();
            SetState(GameState.Running);
        }

        /// <summary>
        /// Encerra a partida com vitória. Chamado pela porta final ao abrir.
        /// Ignorado se a partida não estiver em andamento.
        /// </summary>
        public void Win()
        {
            if (State != GameState.Running)
                return;

            ElapsedOnWin = Timer.TotalSeconds - Timer.Remaining;
            Timer.StopCountdown();
            SetState(GameState.Won);
        }

        /// <summary>Encerra a partida com derrota. Chamado quando o tempo expira.</summary>
        public void Lose()
        {
            if (State != GameState.Running)
                return;

            Timer.StopCountdown();
            SetState(GameState.Lost);
        }

        /// <summary>
        /// Atalho para os enigmas reportarem conclusão sem conhecer o
        /// <see cref="PuzzleProgress"/>. Seguro contra chamadas repetidas.
        /// </summary>
        public void CompleteStep(PuzzleStep step)
        {
            if (State != GameState.Running)
                return;

            Progress.Complete(step);
        }

        private void OnStepCompleted(PuzzleStep step)
        {
            if (bonusPerPuzzle > 0f)
                Timer.AddTime(bonusPerPuzzle);

            StepCompleted?.Invoke(step);
        }

        private void OnTimeExpired() => Lose();

        private void SetState(GameState newState)
        {
            if (State == newState)
                return;

            State = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
