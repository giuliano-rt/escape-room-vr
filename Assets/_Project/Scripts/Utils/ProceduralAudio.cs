using UnityEngine;

namespace EscapeRoomVR.Utils
{
    /// <summary>
    /// Gera os efeitos sonoros do jogo em tempo de execução, por síntese.
    /// Evita depender de arquivos de áudio externos: todo o feedback sonoro
    /// (cliques, erro, acerto, destravamento) sai de ondas calculadas aqui.
    /// Os clipes são criados uma única vez e reaproveitados (cache estático).
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        private static AudioClip click;
        private static AudioClip beep;
        private static AudioClip error;
        private static AudioClip success;
        private static AudioClip unlockCue;
        private static AudioClip tick;

        /// <summary>Clique curto e seco: teclas do teclado numérico.</summary>
        public static AudioClip Click => click != null ? click : click = BuildClick();

        /// <summary>Bipe neutro: alavancas, gaveta, interações genéricas.</summary>
        public static AudioClip Beep => beep != null ? beep : beep = BuildTone("SfxBeep", 660f, 0.12f, 0.35f);

        /// <summary>Zumbido grave descendente: código errado, ação negada.</summary>
        public static AudioClip Error => error != null ? error : error = BuildSweep("SfxError", 300f, 120f, 0.45f);

        /// <summary>Arpejo ascendente curto: enigma resolvido.</summary>
        public static AudioClip Success => success != null ? success : success = BuildArpeggio("SfxSuccess", new[] { 523f, 659f, 784f }, 0.11f);

        /// <summary>Arpejo maior e mais longo: cofre ou porta destravando.</summary>
        public static AudioClip Unlock => unlockCue != null ? unlockCue : unlockCue = BuildArpeggio("SfxUnlock", new[] { 392f, 523f, 659f, 1047f }, 0.14f);

        /// <summary>Tique curtíssimo: contagem regressiva no último minuto.</summary>
        public static AudioClip Tick => tick != null ? tick : tick = BuildTone("SfxTick", 1200f, 0.05f, 0.2f);

        /// <summary>Tom senoidal simples com envelope de decaimento exponencial.</summary>
        private static AudioClip BuildTone(string name, float frequency, float duration, float volume)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-6f * t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            return CreateClip(name, data);
        }

        /// <summary>Ruído filtrado muito curto, para soar como um clique mecânico.</summary>
        private static AudioClip BuildClick()
        {
            int samples = Mathf.CeilToInt(SampleRate * 0.04f);
            var data = new float[samples];
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = Random.Range(-1f, 1f);
                // Filtro passa-baixa de 1a ordem: tira o excesso de agudo do ruido branco.
                previous = Mathf.Lerp(previous, noise, 0.35f);
                data[i] = previous * Mathf.Exp(-14f * t) * 0.4f;
            }

            return CreateClip("SfxClick", data);
        }

        /// <summary>Tom que varre de uma frequência a outra: usado no som de erro.</summary>
        private static AudioClip BuildSweep(string name, float fromHz, float toHz, float duration)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float frequency = Mathf.Lerp(fromHz, toHz, t);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                // Mistura senoide com onda quadrada para um timbre mais eletronico.
                float square = Mathf.Sin(phase) >= 0f ? 1f : -1f;
                data[i] = Mathf.Lerp(Mathf.Sin(phase), square, 0.3f) * Mathf.Exp(-3f * t) * 0.3f;
            }

            return CreateClip(name, data);
        }

        /// <summary>Sequência de notas tocadas em seguida, uma por frequência informada.</summary>
        private static AudioClip BuildArpeggio(string name, float[] frequencies, float noteDuration)
        {
            int samplesPerNote = Mathf.CeilToInt(SampleRate * noteDuration);
            var data = new float[samplesPerNote * frequencies.Length];

            for (int n = 0; n < frequencies.Length; n++)
            {
                for (int i = 0; i < samplesPerNote; i++)
                {
                    float t = (float)i / SampleRate;
                    float envelope = Mathf.Exp(-5f * t / noteDuration);
                    float sample = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t);
                    // Terceiro harmonico discreto para dar corpo a nota.
                    sample += 0.25f * Mathf.Sin(2f * Mathf.PI * frequencies[n] * 3f * t);
                    data[n * samplesPerNote + i] = sample * envelope * 0.28f;
                }
            }

            return CreateClip(name, data);
        }

        private static AudioClip CreateClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
