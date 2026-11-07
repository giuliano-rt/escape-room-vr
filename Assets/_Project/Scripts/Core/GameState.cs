namespace EscapeRoomVR.Core
{
    /// <summary>Estados possíveis da partida.</summary>
    public enum GameState
    {
        /// <summary>Tela inicial, aguardando o jogador iniciar.</summary>
        Idle,
        /// <summary>Partida em andamento, cronômetro correndo.</summary>
        Running,
        /// <summary>Jogador escapou antes do tempo acabar.</summary>
        Won,
        /// <summary>O tempo acabou.</summary>
        Lost
    }
}
