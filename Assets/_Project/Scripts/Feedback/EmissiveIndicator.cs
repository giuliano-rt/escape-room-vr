using UnityEngine;

namespace EscapeRoomVR.Feedback
{
    /// <summary>
    /// Luz indicadora de estado: pinta o material do objeto de vermelho
    /// (travado) ou verde (liberado) e acende/apaga uma Light opcional.
    /// Usada nos painéis dos enigmas como feedback visual imediato.
    /// Cria uma instância própria do material para não alterar o material
    /// compartilhado de todos os outros indicadores da cena.
    /// </summary>
    public class EmissiveIndicator : MonoBehaviour
    {
        [Header("Cores")]
        [SerializeField] private Color lockedColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] private Color unlockedColor = new Color(0.15f, 0.95f, 0.35f);

        [Tooltip("Intensidade da emissão (quanto o objeto parece acender).")]
        [SerializeField] private float emissionBoost = 2.5f;

        [Header("Luz opcional")]
        [Tooltip("Point Light que acompanha a cor do indicador. Pode ficar vazio.")]
        [SerializeField] private Light attachedLight;

        [Tooltip("Pisca enquanto estiver no estado travado.")]
        [SerializeField] private bool pulseWhenLocked = true;

        [SerializeField] private float pulseSpeed = 2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Renderer targetRenderer;
        private Material instanceMaterial;
        private bool isUnlocked;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();

            if (targetRenderer != null)
            {
                // .material (e nao .sharedMaterial) cria uma copia so deste objeto.
                instanceMaterial = targetRenderer.material;
                // Liga a emissao do shader URP/Lit para que a cor realmente "acenda".
                instanceMaterial.EnableKeyword("_EMISSION");
            }

            Apply(isUnlocked ? unlockedColor : lockedColor, 1f);
        }

        private void Update()
        {
            if (isUnlocked || !pulseWhenLocked)
                return;

            // Pulso entre 60% e 100% da intensidade, para chamar atencao sem incomodar.
            float pulse = 0.6f + 0.4f * (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f);
            Apply(lockedColor, pulse);
        }

        /// <summary>Alterna o indicador entre travado (false) e liberado (true).</summary>
        public void SetUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;
            Apply(unlocked ? unlockedColor : lockedColor, 1f);
        }

        /// <summary>Atalho para UnityEvent: coloca o indicador em verde.</summary>
        public void Unlock() => SetUnlocked(true);

        private void Apply(Color color, float intensity)
        {
            if (instanceMaterial != null)
            {
                instanceMaterial.SetColor(BaseColorId, color * intensity);
                instanceMaterial.SetColor(EmissionColorId, color * emissionBoost * intensity);
            }

            if (attachedLight != null)
            {
                attachedLight.color = color;
                attachedLight.intensity = intensity * 1.5f;
            }
        }
    }
}
