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

        /// <summary>
        /// As etapas que realmente trancam o caminho até a saída.
        ///
        /// <see cref="PuzzleStep.NoteFound"/> fica de fora de propósito: o
        /// bilhete da gaveta é uma dica, não uma tranca, e quem for direto ao
        /// quadro escapa sem nunca lê-lo. Contá-lo no placar fazia uma
        /// partida vencida terminar marcando "4/5", como se algo tivesse
        /// ficado para trás.
        /// </summary>
        public static readonly PuzzleStep[] RequiredSteps =
        {
            PuzzleStep.PaintingRemoved,
            PuzzleStep.LeversSolved,
            PuzzleStep.SafeOpened,
            PuzzleStep.CardInserted
        };

        /// <summary>Quantidade total de etapas, incluindo as opcionais.</summary>
        public static int TotalSteps => Enum.GetValues(typeof(PuzzleStep)).Length;

        /// <summary>Quantos enigmas obrigatórios existem. É o total do placar.</summary>
        public static int RequiredStepCount => RequiredSteps.Length;

        /// <summary>Quantas etapas já foram concluídas, contando as opcionais.</summary>
        public int CompletedCount => completed.Count;

        /// <summary>Quantos enigmas obrigatórios já foram resolvidos.</summary>
        public int RequiredCompletedCount
        {
            get
            {
                int total = 0;

                foreach (var step in RequiredSteps)
                {
                    if (completed.Contains(step))
                        total++;
                }

                return total;
            }
        }

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
