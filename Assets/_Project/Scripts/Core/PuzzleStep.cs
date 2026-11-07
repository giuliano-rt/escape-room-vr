namespace EscapeRoomVR.Core
{
    /// <summary>
    /// Etapas da cadeia de enigmas, em ordem de resolução.
    /// Serve de chave para o progresso e para as mensagens do HUD.
    /// </summary>
    public enum PuzzleStep
    {
        /// <summary>Encontrar e inspecionar o bilhete dentro da gaveta.</summary>
        NoteFound = 0,
        /// <summary>Retirar o quadro da parede e revelar o padrão escondido.</summary>
        PaintingRemoved = 1,
        /// <summary>Acertar a combinação das três alavancas.</summary>
        LeversSolved = 2,
        /// <summary>Digitar o código correto no teclado do cofre.</summary>
        SafeOpened = 3,
        /// <summary>Encaixar o cartão-chave no leitor da porta.</summary>
        CardInserted = 4
    }
}
