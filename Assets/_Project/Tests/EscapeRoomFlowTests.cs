using System.Collections;
using System.Linq;
using EscapeRoomVR.Core;
using EscapeRoomVR.Interaction;
using EscapeRoomVR.Puzzles;
using EscapeRoomVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EscapeRoomVR.Tests
{
    /// <summary>
    /// Testes de Play Mode que carregam a cena de verdade e resolvem a cadeia
    /// de enigmas por código, do bilhete até a vitória.
    ///
    /// São eles que garantem que a cena gerada pelo construtor está realmente
    /// ligada: se uma referência do Inspector ficar vazia, ou se um enigma
    /// parar de destravar o próximo, estes testes falham. Como o projeto é
    /// desenvolvido sem um óculos à mão, esta é a verificação mais próxima de
    /// jogar o jogo inteiro.
    /// </summary>
    public class EscapeRoomFlowTests
    {
        private const string SceneName = "EscapeRoom";
        private const string SafeCode = "7429";

        [UnitySetUp]
        public IEnumerator CarregarCena()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;   // deixa Awake/Start rodarem
        }

        [UnityTest]
        public IEnumerator Cena_tem_os_componentes_principais()
        {
            Assert.IsNotNull(GameManager.Instance, "GameManager nao encontrado na cena.");
            Assert.IsNotNull(Find<Keypad>(), "Teclado do cofre nao encontrado.");
            Assert.IsNotNull(Find<LeverPuzzle>(), "Enigma das alavancas nao encontrado.");
            Assert.IsNotNull(Find<CardReader>(), "Leitor de cartao nao encontrado.");
            Assert.IsNotNull(Find<KeyCard>(), "Cartao-chave nao encontrado.");
            Assert.IsNotNull(Find<SlidingDrawer>(), "Gaveta nao encontrada.");
            Assert.IsNotNull(Find<HiddenReveal>(), "Quadro da parede nao encontrado.");
            Assert.IsNotNull(Find<EscapeTrigger>(), "Gatilho de saida nao encontrado.");
            Assert.IsNotNull(Find<XRInteractionManager>(), "XR Interaction Manager nao encontrado.");

            Assert.AreEqual(3, FindAll<Lever>().Length, "A sala deveria ter exatamente 3 alavancas.");
            Assert.AreEqual(12, FindAll<KeypadKey>().Length, "O teclado deveria ter 12 teclas.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partida_comeca_rodando_com_o_cronometro_cheio()
        {
            var game = GameManager.Instance;

            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Timer.IsRunning, "O cronometro deveria estar correndo.");
            Assert.AreEqual(0, game.Progress.CompletedCount, "Nenhum enigma deveria estar resolvido no inicio.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Quadro_revela_o_painel_escondido()
        {
            var painting = Find<HiddenReveal>();
            var hiddenPanel = FindInactiveByName("Hidden Panel");

            Assert.IsNotNull(hiddenPanel, "Painel escondido nao encontrado.");
            Assert.IsFalse(hiddenPanel.activeSelf, "O painel deveria comecar escondido.");

            painting.Reveal();
            yield return null;

            Assert.IsTrue(hiddenPanel.activeSelf, "O painel deveria aparecer ao retirar o quadro.");
            Assert.IsTrue(GameManager.Instance.Progress.IsCompleted(PuzzleStep.PaintingRemoved));
        }

        [UnityTest]
        public IEnumerator Alavancas_so_resolvem_na_combinacao_certa()
        {
            var puzzle = Find<LeverPuzzle>();
            var levers = LeversEmOrdem();
            var clue = FindInactiveByName("Lever Clue Display");

            Assert.IsNotNull(clue, "Display da pista das alavancas nao encontrado.");
            Assert.IsFalse(clue.activeSelf, "A pista deveria comecar escondida.");

            // Combinacao errada: todas para cima.
            foreach (var lever in levers)
                lever.SetUp(true);

            yield return null;
            Assert.IsFalse(puzzle.IsSolved, "Com todas para cima o enigma nao deveria resolver.");

            // Combinacao certa: cima, baixo, cima.
            levers[0].SetUp(true);
            levers[1].SetUp(false);
            levers[2].SetUp(true);

            yield return null;

            Assert.IsTrue(puzzle.IsSolved, "A combinacao cima/baixo/cima deveria resolver o enigma.");
            Assert.IsTrue(clue.activeSelf, "A pista com os ultimos digitos deveria aparecer.");
            Assert.IsTrue(GameManager.Instance.Progress.IsCompleted(PuzzleStep.LeversSolved));
        }

        [UnityTest]
        public IEnumerator Teclado_recusa_codigo_errado_e_aceita_o_certo()
        {
            var keypad = Find<Keypad>();

            Digitar(keypad, "1234");
            yield return null;
            Assert.IsFalse(keypad.IsUnlocked, "Um codigo errado nao deveria destravar o cofre.");

            Digitar(keypad, SafeCode);
            yield return null;
            Assert.IsTrue(keypad.IsUnlocked, $"O codigo {SafeCode} deveria destravar o cofre.");
            Assert.IsTrue(GameManager.Instance.Progress.IsCompleted(PuzzleStep.SafeOpened));
        }

        [UnityTest]
        public IEnumerator Codigo_correto_abre_a_porta_do_cofre()
        {
            var keypad = Find<Keypad>();
            var safeDoor = PortaDoCofre();

            Assert.IsTrue(safeDoor.IsLocked, "O cofre deveria comecar trancado.");

            Digitar(keypad, SafeCode);

            // A porta abre por animacao; espera ate terminar, com limite de tempo.
            yield return EsperarAte(() => safeDoor.IsOpen, 4f, "a porta do cofre abrir");

            Assert.IsFalse(safeDoor.IsLocked);
            Assert.IsTrue(safeDoor.IsOpen);
        }

        [UnityTest]
        public IEnumerator Cartao_no_leitor_abre_a_porta_final_e_atravessar_vence()
        {
            var reader = Find<CardReader>();
            var exitDoor = PortaDeSaida();
            var card = Find<KeyCard>().GetComponent<XRGrabInteractable>();
            var socket = reader.GetComponent<XRSocketInteractor>();
            var manager = Find<XRInteractionManager>();

            Assert.IsTrue(exitDoor.IsLocked, "A porta final deveria comecar trancada.");

            // Encaixa o cartao no leitor como o XRI faria quando o jogador solta
            // o cartao dentro da fenda.
            manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)card);
            yield return null;

            Assert.IsTrue(reader.HasAccepted, "O leitor deveria aceitar o cartao certo.");
            Assert.IsTrue(GameManager.Instance.Progress.IsCompleted(PuzzleStep.CardInserted));

            yield return EsperarAte(() => exitDoor.IsOpen, 4f, "a porta final abrir");

            // Atravessar a saida e o que encerra a partida.
            Find<EscapeTrigger>().ForceWin();
            yield return null;

            Assert.AreEqual(GameState.Won, GameManager.Instance.State);
            Assert.IsFalse(GameManager.Instance.Timer.IsRunning, "O cronometro deveria parar na vitoria.");
        }

        /// <summary>
        /// Leva o cartão até a fenda pelo caminho normal — movendo o objeto no
        /// mundo — em vez de forçar o SelectEnter. É o que prova que a área de
        /// encaixe tem tamanho suficiente e que o socket realmente percebe o
        /// cartão chegando; forçar a selecção pularia exatamente essa parte.
        /// </summary>
        [UnityTest]
        public IEnumerator Leitor_percebe_o_cartao_quando_ele_chega_na_fenda()
        {
            var socket = Find<CardReader>().GetComponent<XRSocketInteractor>();
            var card = Find<KeyCard>().GetComponent<XRGrabInteractable>();

            Assert.IsFalse(socket.interactablesHovered.Contains(card),
                "O cartao nao deveria estar na fenda antes de ser levado ate la.");

            card.transform.position = socket.transform.position;
            Physics.SyncTransforms();

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.IsTrue(socket.interactablesHovered.Contains(card),
                "O socket deveria perceber o cartao dentro da area de encaixe.");
            // O cast escolhe a sobrecarga nova de CanSelect: a que recebe
            // XRBaseInteractable esta obsoleta no XRI 3.
            Assert.IsTrue(socket.CanSelect((IXRSelectInteractable)card),
                "O socket deveria aceitar o cartao quando ele e solto na fenda.");
        }

        /// <summary>
        /// O painel do HUD tem de ficar na altura dos olhos quando o jogador
        /// olha para baixo, senão ele se coloca justamente entre o jogador e a
        /// gaveta, o teclado e tudo mais que se manipula abaixo da linha
        /// dos olhos.
        /// </summary>
        [UnityTest]
        public IEnumerator HUD_nao_acompanha_o_olhar_para_baixo()
        {
            var hud = Find<LazyFollow>();
            var cabeca = Camera.main;

            Assert.IsNotNull(cabeca, "Camera principal nao encontrada.");

            // Sem isso o rastreamento simulado reescreve a rotacao da camera a
            // cada frame e o teste nao consegue "olhar para baixo".
            DesligarRastreamentoDaCabeca(cabeca.gameObject);

            cabeca.transform.rotation = Quaternion.identity;
            yield return null;
            yield return null;

            float alturaOlhandoReto = hud.transform.position.y;

            // Olha bem para baixo, como quem procura algo dentro da gaveta.
            cabeca.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            for (int i = 0; i < 60; i++)
                yield return null;

            float alturaOlhandoParaBaixo = hud.transform.position.y;

            Assert.AreEqual(alturaOlhandoReto, alturaOlhandoParaBaixo, 0.05f,
                "O painel deveria manter a altura quando o jogador abaixa a cabeca.");
        }

        [UnityTest]
        public IEnumerator Bilhete_e_cartao_ganham_fisica_depois_de_soltos()
        {
            foreach (var objeto in new[] { Find<Inspectable>().gameObject, Find<KeyCard>().gameObject })
            {
                var fisica = objeto.GetComponent<BecomesPhysicalOnGrab>();
                var corpo = objeto.GetComponent<Rigidbody>();

                Assert.IsNotNull(fisica, $"{objeto.name} deveria ter BecomesPhysicalOnGrab.");
                Assert.IsTrue(corpo.isKinematic, $"{objeto.name} deveria comecar parado no lugar.");

                fisica.MakePhysical();

                Assert.IsFalse(corpo.isKinematic, $"{objeto.name} deveria virar corpo dinamico.");
                Assert.IsTrue(corpo.useGravity, $"{objeto.name} deveria passar a cair.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Cadeia_completa_resolve_todos_os_cinco_enigmas()
        {
            var game = GameManager.Instance;

            // 1. Bilhete da gaveta.
            game.CompleteStep(PuzzleStep.NoteFound);

            // 2. Quadro da parede.
            Find<HiddenReveal>().Reveal();

            // 3. Alavancas.
            var levers = LeversEmOrdem();
            levers[0].SetUp(true);
            levers[1].SetUp(false);
            levers[2].SetUp(true);
            yield return null;

            // 4. Teclado do cofre.
            Digitar(Find<Keypad>(), SafeCode);
            yield return EsperarAte(() => PortaDoCofre().IsOpen, 4f, "o cofre abrir");

            // 5. Cartao no leitor.
            var card = Find<KeyCard>().GetComponent<XRGrabInteractable>();
            var socket = Find<CardReader>().GetComponent<XRSocketInteractor>();
            Find<XRInteractionManager>().SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)card);
            yield return EsperarAte(() => PortaDeSaida().IsOpen, 4f, "a porta final abrir");

            Find<EscapeTrigger>().ForceWin();
            yield return null;

            Assert.AreEqual(PuzzleProgress.TotalSteps, game.Progress.CompletedCount,
                "Todos os cinco enigmas deveriam estar marcados como resolvidos.");
            Assert.AreEqual(GameState.Won, game.State);
            Assert.Greater(game.ElapsedOnWin, 0f, "O tempo de escape deveria ter sido registrado.");
        }

        // ------------------------------------------------------------------
        // Apoio
        // ------------------------------------------------------------------

        /// <summary>
        /// Desliga o que estiver dirigindo a pose da câmera (o driver de
        /// tracking e o simulador), para o teste poder posicionar a cabeça.
        /// </summary>
        private static void DesligarRastreamentoDaCabeca(GameObject camera)
        {
            foreach (var comportamento in camera.GetComponents<MonoBehaviour>())
            {
                if (comportamento.GetType().Name.Contains("TrackedPoseDriver"))
                    comportamento.enabled = false;
            }

            foreach (var raiz in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (raiz.name.Contains("Simulator"))
                    raiz.SetActive(false);
            }
        }

        private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>();

        private static T[] FindAll<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        /// <summary>As alavancas na ordem dos rótulos 1, 2 e 3.</summary>
        private static Lever[] LeversEmOrdem() =>
            FindAll<Lever>().OrderBy(lever => lever.name).ToArray();

        private static void Digitar(Keypad keypad, string codigo)
        {
            foreach (char digito in codigo)
                keypad.Press(digito.ToString());
        }

        private static AnimatedDoor PortaDoCofre() =>
            FindAll<AnimatedDoor>().First(door => door.transform.parent != null
                                                  && door.transform.parent.name == "Safe");

        private static AnimatedDoor PortaDeSaida() =>
            FindAll<AnimatedDoor>().First(door => door.transform.parent != null
                                                  && door.transform.parent.name == "Exit Door");

        /// <summary>
        /// Procura um objeto pelo nome mesmo que esteja desativado — os painéis
        /// de pista começam desligados e não aparecem em FindObjectsByType.
        /// </summary>
        private static GameObject FindInactiveByName(string nome)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (transform.name == nome)
                        return transform.gameObject;
                }
            }

            return null;
        }

        private static IEnumerator EsperarAte(System.Func<bool> condicao, float limiteSegundos, string oQue)
        {
            float fim = Time.time + limiteSegundos;

            while (!condicao() && Time.time < fim)
                yield return null;

            Assert.IsTrue(condicao(), $"Tempo esgotado esperando {oQue}.");
        }
    }
}
