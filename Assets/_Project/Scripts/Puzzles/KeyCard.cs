using UnityEngine;

namespace EscapeRoomVR.Puzzles
{
    /// <summary>
    /// Marcador: identifica o objeto como o cartão-chave que abre a porta final.
    /// Existe para o <see cref="CardReader"/> poder recusar qualquer outro
    /// objeto que o jogador tente encaixar no leitor.
    /// </summary>
    public class KeyCard : MonoBehaviour
    {
        [Tooltip("Identificador do cartão. O leitor só aceita o cartão com o id que ele espera.")]
        [SerializeField] private string cardId = "EXIT";

        /// <summary>Identificador deste cartão.</summary>
        public string CardId => cardId;
    }
}
