using EscapeRoomVR.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeRoomVR.UI
{
    /// <summary>
    /// Tela de fim de partida. Fica desligada durante o jogo e aparece na
    /// frente do jogador na vitória ou na derrota, com o tempo usado e um
    /// botão para jogar de novo.
    /// </summary>
    public class EndScreenController : MonoBehaviour
    {
        [Header("Painel")]
        [Tooltip("Raiz da tela de fim. Desligada enquanto a partida corre.")]
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI detailLabel;

        [Header("Cores")]
        [SerializeField] private Color winColor = new Color(0.35f, 1f, 0.5f);
        [SerializeField] private Color loseColor = new Color(1f, 0.4f, 0.35f);

        private GameManager game;

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void Start()
        {
            game = GameManager.Instance;

            if (game != null)
                game.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (game != null)
                game.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Won:
                    Show("VOCE ESCAPOU!", $"Tempo usado: {FormatSeconds(game.ElapsedOnWin)}", winColor);
                    break;

                case GameState.Lost:
                    Show("O TEMPO ACABOU", "Voce ficou trancado na sala.", loseColor);
                    break;

                default:
                    if (panelRoot != null)
                        panelRoot.SetActive(false);
                    break;
            }
        }

        private void Show(string title, string detail, Color color)
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.color = color;
            }

            if (detailLabel != null)
                detailLabel.text = detail;
        }

        /// <summary>Recarrega a cena atual, reiniciando a partida do zero.</summary>
        public void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        /// <summary>Fecha a aplicação (usado no botão Sair, no build do Quest).</summary>
        public void Quit() => Application.Quit();

        private static string FormatSeconds(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
