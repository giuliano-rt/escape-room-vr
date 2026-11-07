using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EscapeRoomVR.Puzzles
{
    /// <summary>
    /// Uma tecla do teclado numérico. Fica em um botão com XRSimpleInteractable:
    /// o jogador aponta/toca e o "selectEntered" manda o rótulo para o
    /// <see cref="Keypad"/>. A tecla também afunda alguns milímetros para dar
    /// a sensação de botão de verdade.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class KeypadKey : MonoBehaviour
    {
        [Tooltip("Rótulo da tecla: um dígito, ou 'C' para limpar e 'OK' para validar.")]
        [SerializeField] private string label = "0";

        [Tooltip("Teclado que recebe esta tecla.")]
        [SerializeField] private Keypad keypad;

        [Header("Animação do botão")]
        [Tooltip("Quanto o botão afunda, em metros.")]
        [SerializeField] private float pressDepth = 0.006f;

        [Tooltip("Segundos que o botão fica afundado.")]
        [SerializeField] private float pressDuration = 0.08f;

        /// <summary>Rótulo desta tecla.</summary>
        public string Label => label;

        private XRSimpleInteractable interactable;
        private Vector3 restPosition;
        private float pressedUntil;

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            restPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            if (interactable != null)
                interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void Update()
        {
            bool pressed = Time.time < pressedUntil;

            // O botao afunda no proprio eixo Z local, ou seja, para dentro do painel.
            Vector3 target = pressed ? restPosition + Vector3.forward * pressDepth : restPosition;
            transform.localPosition = Vector3.Lerp(transform.localPosition, target, 0.5f);
        }

        /// <summary>Configura a tecla por código (usado pelo construtor de cena).</summary>
        public void Configure(string keyLabel, Keypad owner)
        {
            label = keyLabel;
            keypad = owner;
        }

        private void OnSelectEntered(SelectEnterEventArgs args) => Press();

        /// <summary>Aciona a tecla.</summary>
        public void Press()
        {
            pressedUntil = Time.time + pressDuration;

            if (keypad != null)
                keypad.Press(label);
        }
    }
}
