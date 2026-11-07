using System;
using EscapeRoomVR.Feedback;
using UnityEngine;

namespace EscapeRoomVR.Interaction
{
    /// <summary>
    /// Porta que gira em torno de um eixo por interpolação, sem depender de física.
    /// Usada na porta do cofre e na porta final: elas precisam abrir de forma
    /// garantida quando o enigma é resolvido, então a animação é controlada por
    /// código em vez de deixar a física decidir.
    ///
    /// O objeto que recebe este script deve ser o *pivô* da porta (a dobradiça);
    /// a folha da porta fica como filha, deslocada para o lado.
    /// </summary>
    public class AnimatedDoor : MonoBehaviour
    {
        [Header("Movimento")]
        [Tooltip("Eixo de rotação, em coordenadas locais. Y = porta comum.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.up;

        [Tooltip("Ângulo com a porta fechada, em graus.")]
        [SerializeField] private float closedAngle;

        [Tooltip("Ângulo com a porta aberta, em graus.")]
        [SerializeField] private float openAngle = 95f;

        [Tooltip("Segundos para completar a abertura.")]
        [SerializeField] private float openDuration = 1.4f;

        [Header("Estado inicial")]
        [Tooltip("Se marcado, a porta só abre depois de Unlock().")]
        [SerializeField] private bool startLocked = true;

        [Header("Feedback")]
        [SerializeField] private AudioCue audioCue;
        [SerializeField] private EmissiveIndicator indicator;

        /// <summary>True enquanto a porta estiver travada.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>True depois que a porta terminou de abrir.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Disparado quando a porta é destravada.</summary>
        public event Action Unlocked;

        /// <summary>Disparado quando a porta termina de abrir.</summary>
        public event Action Opened;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private float animationProgress;
        private bool isAnimating;

        private void Awake()
        {
            IsLocked = startLocked;

            Vector3 axis = hingeAxis.sqrMagnitude < 0.0001f ? Vector3.up : hingeAxis.normalized;
            Quaternion baseRotation = transform.localRotation;
            closedRotation = baseRotation * Quaternion.AngleAxis(closedAngle, axis);
            openRotation = baseRotation * Quaternion.AngleAxis(openAngle, axis);

            transform.localRotation = closedRotation;

            if (indicator != null)
                indicator.SetUnlocked(!IsLocked);
        }

        private void Update()
        {
            if (!isAnimating)
                return;

            animationProgress += Time.deltaTime / Mathf.Max(0.01f, openDuration);
            // SmoothStep deixa a abertura com aceleracao e desaceleracao suaves.
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animationProgress));
            transform.localRotation = Quaternion.Slerp(closedRotation, openRotation, eased);

            if (animationProgress >= 1f)
            {
                isAnimating = false;
                IsOpen = true;
                Opened?.Invoke();
            }
        }

        /// <summary>
        /// Destrava a porta. Por padrão ela abre em seguida, que é o
        /// comportamento desejado quando um enigma é resolvido.
        /// </summary>
        public void Unlock(bool openImmediately = true)
        {
            if (!IsLocked)
                return;

            IsLocked = false;

            if (indicator != null)
                indicator.SetUnlocked(true);

            if (audioCue != null)
                audioCue.Play(CueSound.Unlock);

            Unlocked?.Invoke();

            if (openImmediately)
                Open();
        }

        /// <summary>Abre a porta, se ela já estiver destravada.</summary>
        public void Open()
        {
            if (IsLocked || IsOpen || isAnimating)
            {
                if (IsLocked && audioCue != null)
                    audioCue.Play(CueSound.Error);

                return;
            }

            animationProgress = 0f;
            isAnimating = true;
        }

        /// <summary>Volta a porta à posição fechada imediatamente (usado no reinício).</summary>
        public void ResetClosed()
        {
            isAnimating = false;
            animationProgress = 0f;
            IsOpen = false;
            IsLocked = startLocked;
            transform.localRotation = closedRotation;

            if (indicator != null)
                indicator.SetUnlocked(!IsLocked);
        }
    }
}
