using System;
using UnityEngine;

namespace EscapeRoomVR.Core
{
    /// <summary>
    /// Cronômetro de contagem regressiva da partida.
    /// Não conhece regras de jogo: apenas conta e avisa quando zera.
    /// Quem controla start/stop é o <see cref="GameManager"/>.
    /// </summary>
    public class GameTimer : MonoBehaviour
    {
        [Tooltip("Tempo total da partida, em segundos (600 = 10 minutos).")]
        [SerializeField] private float totalSeconds = 600f;

        [Tooltip("Abaixo deste tempo restante o HUD entra em estado de alerta.")]
        [SerializeField] private float warningSeconds = 60f;

        /// <summary>Segundos restantes. Nunca negativo.</summary>
        public float Remaining { get; private set; }

        /// <summary>True enquanto o cronômetro está descontando tempo.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Tempo total configurado para a partida.</summary>
        public float TotalSeconds => totalSeconds;

        /// <summary>True quando o tempo restante entrou na faixa de alerta.</summary>
        public bool IsInWarning => Remaining <= warningSeconds;

        /// <summary>Disparado uma única vez quando o tempo chega a zero.</summary>
        public event Action TimeExpired;

        /// <summary>Disparado quando o tempo restante cruza o limite de alerta.</summary>
        public event Action WarningStarted;

        private bool warningFired;

        private void Awake()
        {
            Remaining = totalSeconds;
        }

        private void Update()
        {
            if (!IsRunning)
                return;

            Remaining -= Time.deltaTime;

            if (!warningFired && Remaining <= warningSeconds)
            {
                warningFired = true;
                WarningStarted?.Invoke();
            }

            if (Remaining <= 0f)
            {
                Remaining = 0f;
                IsRunning = false;
                TimeExpired?.Invoke();
            }
        }

        /// <summary>Reinicia a contagem no tempo total e começa a descontar.</summary>
        public void StartCountdown()
        {
            Remaining = totalSeconds;
            warningFired = false;
            IsRunning = true;
        }

        /// <summary>Congela o cronômetro mantendo o tempo restante atual.</summary>
        public void StopCountdown() => IsRunning = false;

        /// <summary>Adiciona (ou subtrai, com valor negativo) segundos ao tempo restante.</summary>
        public void AddTime(float seconds)
        {
            Remaining = Mathf.Clamp(Remaining + seconds, 0f, totalSeconds);
        }

        /// <summary>Formata o tempo restante como MM:SS para exibição no HUD.</summary>
        public string FormattedRemaining
        {
            get
            {
                int total = Mathf.CeilToInt(Remaining);
                return $"{total / 60:00}:{total % 60:00}";
            }
        }
    }
}
