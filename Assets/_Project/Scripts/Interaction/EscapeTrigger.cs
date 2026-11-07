using EscapeRoomVR.Core;
using UnityEngine;

namespace EscapeRoomVR.Interaction
{
    /// <summary>
    /// Volume logo depois da porta final. Quando o jogador atravessa (andando
    /// ou se teletransportando), a partida é ganha.
    ///
    /// A vitória é por atravessar, e não por abrir a porta, para que o jogador
    /// realmente "escape" da sala — é o fecho natural de um escape room.
    /// Como fallback, a porta também pode chamar <see cref="ForceWin"/> caso
    /// o jogador fique parado muito tempo depois de abri-la.
    /// </summary>
    public class EscapeTrigger : MonoBehaviour
    {
        [Tooltip("Porta que precisa estar aberta para a saída valer.")]
        [SerializeField] private AnimatedDoor exitDoor;

        [Tooltip("Segundos após a porta abrir em que a vitória é concedida mesmo sem atravessar. 0 desliga.")]
        [SerializeField] private float autoWinAfterOpen = 25f;

        private float autoWinAt = -1f;

        private void Awake()
        {
            var triggerCollider = GetComponent<Collider>();

            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnEnable()
        {
            if (exitDoor != null)
                exitDoor.Opened += OnDoorOpened;
        }

        private void OnDisable()
        {
            if (exitDoor != null)
                exitDoor.Opened -= OnDoorOpened;
        }

        private void Update()
        {
            if (autoWinAt < 0f || Time.time < autoWinAt)
                return;

            autoWinAt = -1f;
            ForceWin();
        }

        private void OnDoorOpened()
        {
            if (autoWinAfterOpen > 0f)
                autoWinAt = Time.time + autoWinAfterOpen;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (exitDoor != null && !exitDoor.IsOpen)
                return;

            // O corpo do jogador e o CharacterController do XR Origin; a camera
            // tambem tem collider em alguns setups, por isso aceitamos os dois.
            bool isPlayer = other.GetComponentInParent<CharacterController>() != null
                            || other.GetComponentInParent<Camera>() != null;

            if (!isPlayer)
                return;

            ForceWin();
        }

        /// <summary>Concede a vitória imediatamente.</summary>
        public void ForceWin()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Win();
        }
    }
}
