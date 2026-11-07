using UnityEngine;

namespace EscapeRoomVR.UI
{
    /// <summary>
    /// Faz um painel acompanhar o olhar do jogador "com folga": ele fica
    /// parado enquanto o jogador olha para perto dele e só se reposiciona
    /// quando o olhar se afasta além de um ângulo.
    ///
    /// Em VR, um painel grudado na câmera causa desconforto e atrapalha a
    /// visão; este comportamento mantém o HUD acessível sem ficar no caminho.
    /// </summary>
    public class LazyFollow : MonoBehaviour
    {
        [Tooltip("Distância do painel até a cabeça do jogador, em metros.")]
        [SerializeField] private float distance = 1.6f;

        [Tooltip("Deslocamento vertical em relação à linha dos olhos.")]
        [SerializeField] private float verticalOffset = -0.35f;

        [Tooltip("Ângulo, em graus, que o olhar precisa se afastar para o painel se mover.")]
        [SerializeField] private float angleThreshold = 28f;

        [Tooltip("Velocidade do reposicionamento. Maior = mais rápido.")]
        [SerializeField] private float followSpeed = 2.5f;

        private Transform head;
        private bool isCatchingUp;

        private void Start()
        {
            TryBindHead();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (head == null && !TryBindHead())
                return;

            Vector3 targetPosition = DesiredPosition();
            Vector3 toPanel = transform.position - head.position;
            Vector3 toTarget = targetPosition - head.position;

            float angle = Vector3.Angle(toPanel, toTarget);

            if (angle > angleThreshold)
                isCatchingUp = true;
            else if (angle < 2f)
                isCatchingUp = false;

            if (isCatchingUp)
            {
                float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            }

            // O painel encara o jogador girando so em torno do eixo vertical:
            // inclinar junto com a cabeca deixaria o texto tombado.
            Vector3 lookDirection = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);

            if (lookDirection.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        private bool TryBindHead()
        {
            var mainCamera = Camera.main;

            if (mainCamera == null)
                return false;

            head = mainCamera.transform;
            return true;
        }

        /// <summary>
        /// Posição de descanso do painel: à frente do jogador, mas usando só a
        /// direção horizontal do olhar.
        ///
        /// Ignorar a inclinação da cabeça é o que tira o painel do caminho.
        /// Se ele acompanhasse o olhar para baixo, ficaria exatamente entre o
        /// jogador e tudo que ele tenta manipular — a gaveta, o teclado, os
        /// objetos na mesa. Assim ele fica parado na altura dos olhos e sai de
        /// cena sozinho quando o jogador abaixa a cabeça para interagir.
        /// </summary>
        private Vector3 DesiredPosition()
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);

            // Olhando quase reto para cima ou para baixo a projecao fica
            // degenerada; nesse caso vale a direcao do topo da cabeca.
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);

            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;

            return head.position + forward * distance + Vector3.up * verticalOffset;
        }

        private void SnapToTarget()
        {
            if (head == null)
                return;

            transform.position = DesiredPosition();
            transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
        }
    }
}
