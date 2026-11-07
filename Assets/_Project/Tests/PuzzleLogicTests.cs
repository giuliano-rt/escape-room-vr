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
        public void Placar_nao_conta_o_bilhete_porque_ele_e_opcional()
        {
            var progress = new PuzzleProgress();

            progress.Complete(PuzzleStep.NoteFound);

            Assert.AreEqual(0, progress.RequiredCompletedCount,
                "O bilhete e uma dica, nao deveria mexer no placar de enigmas.");
            Assert.AreEqual(1, progress.CompletedCount,
                "Mesmo opcional, a etapa deveria continuar registrada.");
        }

        [Test]
        public void Escapar_sem_ler_o_bilhete_fecha_o_placar()
        {
            var progress = new PuzzleProgress();

            // O caminho de quem vai direto ao quadro, sem abrir a gaveta.
            foreach (var step in PuzzleProgress.RequiredSteps)
                progress.Complete(step);

            Assert.AreEqual(PuzzleProgress.RequiredStepCount, progress.RequiredCompletedCount,
                "Quem escapou deveria ver o placar completo, mesmo pulando a dica.");
            Assert.IsFalse(progress.IsCompleted(PuzzleStep.NoteFound));
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
