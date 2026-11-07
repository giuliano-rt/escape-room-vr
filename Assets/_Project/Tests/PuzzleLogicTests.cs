using EscapeRoomVR.Core;
using NUnit.Framework;

namespace EscapeRoomVR.Tests
{
    /// <summary>
    /// Testes da lógica pura de progressão, sem cena e sem VR.
    /// São rápidos e cobrem as regras que mais causariam estrago se
    /// quebrassem: contar uma etapa duas vezes ou perder uma conclusão.
    /// </summary>
    public class PuzzleLogicTests
    {
        [Test]
        public void Progresso_comeca_vazio()
        {
            var progress = new PuzzleProgress();

            Assert.AreEqual(0, progress.CompletedCount);
            Assert.IsFalse(progress.IsCompleted(PuzzleStep.NoteFound));
        }

        [Test]
        public void Concluir_etapa_marca_e_dispara_evento_uma_unica_vez()
        {
            var progress = new PuzzleProgress();
            int disparos = 0;
            progress.StepCompleted += _ => disparos++;

            bool primeira = progress.Complete(PuzzleStep.NoteFound);
            bool repetida = progress.Complete(PuzzleStep.NoteFound);

            Assert.IsTrue(primeira, "A primeira conclusao deveria contar.");
            Assert.IsFalse(repetida, "Repetir a mesma etapa nao deveria contar de novo.");
            Assert.AreEqual(1, disparos, "O evento deveria disparar so na primeira vez.");
            Assert.AreEqual(1, progress.CompletedCount);
        }

        [Test]
        public void Todas_as_etapas_da_cadeia_sao_contadas()
        {
            var progress = new PuzzleProgress();

            progress.Complete(PuzzleStep.NoteFound);
            progress.Complete(PuzzleStep.PaintingRemoved);
            progress.Complete(PuzzleStep.LeversSolved);
            progress.Complete(PuzzleStep.SafeOpened);
            progress.Complete(PuzzleStep.CardInserted);

            Assert.AreEqual(PuzzleProgress.TotalSteps, progress.CompletedCount,
                "A cadeia deveria cobrir exatamente todas as etapas declaradas no enum.");
        }

        [Test]
        public void Reset_limpa_o_progresso()
        {
            var progress = new PuzzleProgress();
            progress.Complete(PuzzleStep.NoteFound);

            progress.Reset();

            Assert.AreEqual(0, progress.CompletedCount);
            Assert.IsFalse(progress.IsCompleted(PuzzleStep.NoteFound));
        }
    }
}
