using EscapeRoomVR.Utils;
using UnityEngine;

namespace EscapeRoomVR.Feedback
{
    /// <summary>
    /// Nomes dos efeitos sonoros disponíveis, para escolher no Inspector
    /// sem precisar arrastar arquivos de áudio.
    /// </summary>
    public enum CueSound
    {
        Click,
        Beep,
        Error,
        Success,
        Unlock,
        Tick
    }

    /// <summary>
    /// Toca efeitos sonoros gerados por <see cref="ProceduralAudio"/> na posição
    /// do objeto, com áudio espacial (3D). Cada objeto interativo carrega um
    /// destes, então o som sempre vem de onde a ação aconteceu.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioCue : MonoBehaviour
    {
        [Tooltip("Distância em que o som já está no volume máximo.")]
        [SerializeField] private float minDistance = 0.4f;

        [Tooltip("Distância em que o som deixa de ser ouvido.")]
        [SerializeField] private float maxDistance = 12f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;           // 1 = totalmente 3D (audio espacial)
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;           // evita variacao de tom ao mover a cabeca
        }

        /// <summary>Toca o som indicado na posição deste objeto.</summary>
        public void Play(CueSound sound, float volume = 1f, float pitch = 1f)
        {
            if (source == null)
                return;

            source.pitch = pitch;
            source.PlayOneShot(Resolve(sound), volume);
        }

        /// <summary>Atalho sem parâmetros, para ligar direto em um UnityEvent.</summary>
        public void PlayClick() => Play(CueSound.Click);

        /// <summary>Atalho sem parâmetros, para ligar direto em um UnityEvent.</summary>
        public void PlayBeep() => Play(CueSound.Beep);

        /// <summary>Atalho sem parâmetros, para ligar direto em um UnityEvent.</summary>
        public void PlayError() => Play(CueSound.Error);

        private static AudioClip Resolve(CueSound sound)
        {
            switch (sound)
            {
                case CueSound.Click: return ProceduralAudio.Click;
                case CueSound.Error: return ProceduralAudio.Error;
                case CueSound.Success: return ProceduralAudio.Success;
                case CueSound.Unlock: return ProceduralAudio.Unlock;
                case CueSound.Tick: return ProceduralAudio.Tick;
                default: return ProceduralAudio.Beep;
            }
        }
    }
}
