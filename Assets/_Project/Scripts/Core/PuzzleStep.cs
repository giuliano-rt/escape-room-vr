namespace EscapeRoomVR.Core
{
    /// <summary>
    /// Etapas da cadeia de enigmas, em ordem de resolução.
    /// Serve de chave para o progresso e para as mensagens do HUD.
    ///
    /// Nem toda etapa é uma tranca: <see cref="NoteFound"/> é só uma dica, e o
    /// jogador consegue escapar sem passar por ela. Quem decide o que conta
    /// como enigma no placar é <see cref="PuzzleProgress.RequiredSteps"/>.
    /// </summary>
    public enum PuzzleStep
    {
        /// <summary>
        /// Encontrar e ler o bilhete dentro da gaveta. É uma dica opcional:
        /// aponta para o quadro, mas nada impede o jogador de ir direto nele.
        /// </summary>
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
