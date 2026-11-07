using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Interaction
{
    /// <summary>
    /// Objeto que começa parado no lugar onde foi colocado e vira um corpo
    /// físico de verdade a partir do momento em que o jogador o solta.
    ///
    /// Resolve um conflito entre duas coisas que o jogo precisa. O bilhete
    /// dentro da gaveta e o cartão dentro do cofre não podem escorregar,
    /// quicar para fora ou cair atrás de um móvel antes de o jogador chegar
    /// neles — se isso acontecer, a partida fica impossível de terminar. Mas,
    /// depois de pegos, ficar flutuando no ar quando soltos parece defeito.
    ///
    /// Então eles nascem cinemáticos e sem gravidade, e a troca para corpo
    /// dinâmico acontece no <c>selectExited</c>, e não no <c>selectEntered</c>:
    /// o XRGrabInteractable guarda o estado original do Rigidbody quando a
    /// pegada começa e o restaura ao soltar, então qualquer mudança feita
    /// durante a pegada seria desfeita.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class BecomesPhysicalOnGrab : MonoBehaviour
    {
        [Tooltip("Massa aplicada ao objeto quando ele passa a ter física.")]
        [SerializeField] private float physicalMass = 0.2f;

        /// <summary>True depois que o objeto passou a ser um corpo dinâmico.</summary>
        public bool IsPhysical { get; private set; }

        private XRGrabInteractable grabInteractable;
        private Rigidbody body;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable() => grabInteractable.selectExited.AddListener(OnSelectExited);

        private void OnDisable() => grabInteractable.selectExited.RemoveListener(OnSelectExited);

        private void OnSelectExited(SelectExitEventArgs args) => MakePhysical();

        /// <summary>Transforma o objeto num corpo dinâmico comum.</summary>
        public void MakePhysical()
        {
            if (IsPhysical)
                return;

            IsPhysical = true;

            body.isKinematic = false;
            body.useGravity = true;
            body.mass = physicalMass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // A partir daqui o objeto pode ser carregado e arremessado como
            // qualquer outro: o velocity tracking da o peso na mao do jogador.
            grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grabInteractable.throwOnDetach = true;
        }
    }
}
