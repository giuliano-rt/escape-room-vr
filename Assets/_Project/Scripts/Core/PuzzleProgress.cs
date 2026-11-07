using System;
using System.Collections.Generic;

namespace EscapeRoomVR.Core
{
    /// <summary>
    /// Guarda quais etapas já foram concluídas.
    /// É só estado + evento: a lógica de cada enigma vive no componente dele.
    /// </summary>
    public class PuzzleProgress
    {
        private readonly HashSet<PuzzleStep> completed = new HashSet<PuzzleStep>();

        /// <summary>Disparado quando uma etapa é concluída pela primeira vez.</summary>
        public event Action<PuzzleStep> StepCompleted;

        /// <summary>Quantidade total de etapas da cadeia de enigmas.</summary>
        public static int TotalSteps => Enum.GetValues(typeof(PuzzleStep)).Length;

        /// <summary>Quantas etapas já foram concluídas.</summary>
        public int CompletedCount => completed.Count;

        /// <summary>True se a etapa já foi concluída.</summary>
        public bool IsCompleted(PuzzleStep step) => completed.Contains(step);

        /// <summary>
        /// Marca uma etapa como concluída. Chamadas repetidas são ignoradas,
        /// então os enigmas podem avisar sem medo de contar duas vezes.
        /// </summary>
        /// <returns>True se esta foi a primeira vez que a etapa foi concluída.</returns>
        public bool Complete(PuzzleStep step)
        {
            if (!completed.Add(step))
                return false;

            StepCompleted?.Invoke(step);
            return true;
        }

        /// <summary>Limpa o progresso (usado ao reiniciar a partida).</summary>
        public void Reset() => completed.Clear();
    }
}
