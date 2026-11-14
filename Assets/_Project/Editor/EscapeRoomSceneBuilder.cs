using System.Collections.Generic;
using EscapeRoomVR.Core;
using EscapeRoomVR.Feedback;
using EscapeRoomVR.Interaction;
using EscapeRoomVR.Puzzles;
using EscapeRoomVR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Monta a cena inteira do escape room por código: sala, mobília,
    /// os cinco enigmas em cadeia, iluminação, HUD, telas de fim e o XR Origin.
    ///
    /// Construir a cena por script, em vez de montá-la à mão no Editor, tem
    /// duas vantagens aqui: a cena pode ser regerada do zero a qualquer momento
    /// (útil ao ajustar a planta da sala) e todo o posicionamento fica
    /// documentado e versionado em texto, não escondido dentro de um .unity.
    ///
    /// Convenção importante de hierarquia: objetos que têm filhos são sempre
    /// GameObjects vazios com escala 1. As primitivas (que usam a escala do
    /// transform para definir o tamanho) ficam sempre nas folhas da árvore.
    /// Sem isso, um texto filho de uma caixa achatada sairia esticado.
    ///
    /// Cadeia de enigmas:
    ///   1. Gaveta da mesa   -> bilhete com a primeira dica
    ///   2. Quadro da parede -> painel escondido: padrao das alavancas + "7 4 ? ?"
    ///   3. Alavancas (cima/baixo/cima) -> revela os digitos restantes "? ? 2 9"
    ///   4. Teclado do cofre (7429) -> abre o cofre -> cartao-chave
    ///   5. Cartao no leitor -> abre a porta final -> atravessar = vitoria
    /// </summary>
    public static class EscapeRoomSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/EscapeRoom.unity";

        private const string XrOriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        private const string SimulatorPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/XR Interaction Simulator/XR Interaction Simulator.prefab";

        // --- Planta da sala (metros) ---
        private const float RoomHalfX = 3.5f;
        private const float RoomHalfZ = 3.5f;
        private const float RoomHeight = 3f;
        private const float WallThickness = 0.15f;

        // Vao da porta de saida, na parede norte (z = +RoomHalfZ)
        private const float DoorHalfWidth = 0.55f;
        private const float DoorHeight = 2.1f;

        /// <summary>
        /// Camada de interacao usada pelo teletransporte. O Teleport Interactor
        /// dos Starter Assets so enxerga esta camada; uma TeleportationArea
        /// deixada na camada padrao e simplesmente ignorada pelo raio.
        /// </summary>
        private const int TeleportInteractionLayer = 31;

        // --- Solucao dos enigmas ---
        private const string SafeCode = "7429";
        private static readonly bool[] LeverPattern = { true, false, true };   // cima, baixo, cima

        [MenuItem("Escape Room VR/3. Gerar cena do Escape Room")]
        public static void BuildScene()
        {
            MaterialLibrary.EnsureAll();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureEnvironmentLighting();

            var interactionManager = new GameObject("XR Interaction Manager");
            interactionManager.AddComponent<XRInteractionManager>();

            CreateGameManager();
            BuildRoomShell();
            BuildLighting();
            SpawnPlayer();

            // A porta final e construida primeiro porque os enigmas seguintes
            // precisam referenciar a porta que eles destravam.
            AnimatedDoor exitDoor = BuildExitDoor();
            BuildCardReader(exitDoor);
            BuildEscapeCorridor(exitDoor);

            AnimatedDoor safeDoor = BuildSafe();
            BuildKeypad(safeDoor);

            GameObject leverClue = BuildLeverClueDisplay();
            BuildLevers(leverClue);

            BuildPainting();
            BuildDesk();

            BuildProps();
            BuildHud();
            BuildEndScreen();

            AddFeedbackToEveryInteractable();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings();
            RememberSceneForNextEditorLaunch();

            Debug.Log($"[EscapeRoomSceneBuilder] Cena gerada em {ScenePath}. Codigo do cofre: {SafeCode}.");
        }

        // ------------------------------------------------------------------
        // Ambiente e iluminacao
        // ------------------------------------------------------------------

        /// <summary>
        /// Luz ambiente achatada e sem skybox: a sala é fechada, então um
        /// ambiente uniforme custa menos que GI e garante que nada fique
        /// completamente preto no Quest.
        /// </summary>
        private static void ConfigureEnvironmentLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.40f, 0.40f, 0.46f);
            RenderSettings.fog = false;
        }

        private static void BuildLighting()
        {
            var root = new GameObject("Lighting").transform;

            // Quatro luminarias no teto, cobrindo os quatro quadrantes da sala.
            // Sao luzes pontuais, e nao uma direcional, porque a sala e fechada:
            // uma direcional nao teria de onde "entrar". Cada uma ganha uma
            // placa emissiva, para a fonte da luz aparecer no teto em vez de a
            // sala parecer iluminada por nada.
            var lampPositions = new[]
            {
                new Vector3(-1.7f, 2.6f, -1.7f),
                new Vector3(1.7f, 2.6f, -1.7f),
                new Vector3(-1.7f, 2.6f, 1.7f),
                new Vector3(1.7f, 2.6f, 1.7f)
            };

            var lampMat = MaterialLibrary.Get(MaterialLibrary.Names.Lamp);
            var trimMat = MaterialLibrary.Get(MaterialLibrary.Names.Trim);

            for (int i = 0; i < lampPositions.Length; i++)
            {
                var position = lampPositions[i];

                // So duas das quatro projetam sombra: sombra em tempo real e o
                // item mais caro da cena no Quest, e quatro fontes deixariam a
                // sala cheia de sombras cruzadas sem ganho de leitura.
                CreatePointLight(root, position, new Color(1f, 0.97f, 0.92f), 4.5f, 8f,
                    i == 0 || i == 3 ? LightShadows.Hard : LightShadows.None);

                var fixture = SceneBuildUtils.Empty($"Lamp {i + 1}", root,
                    new Vector3(position.x, RoomHeight - 0.02f, position.z)).transform;

                SceneBuildUtils.Box("Glow", fixture, Vector3.zero,
                    new Vector3(0.8f, 0.03f, 0.8f), lampMat, keepCollider: false);

                SceneBuildUtils.Box("Frame", fixture, new Vector3(0f, 0.02f, 0f),
                    new Vector3(0.9f, 0.04f, 0.9f), trimMat, keepCollider: false);
            }

            // A parede norte fica longe das quatro luminarias, e e justamente
            // onde estao a porta final e o leitor de cartao.
            CreatePointLight(root, new Vector3(0f, 2.5f, 2.9f),
                new Color(1f, 0.97f, 0.92f), 2.5f, 5f, LightShadows.None);

            // Luz fria no corredor, para a saida parecer convidativa.
            CreatePointLight(root, new Vector3(0f, 2.4f, 5f),
                new Color(0.70f, 0.85f, 1f), 4f, 9f, LightShadows.None);

            // Realces sobre os dois enigmas que ficam nas paredes laterais.
            CreatePointLight(root, new Vector3(-2.7f, 2.1f, -0.9f),
                new Color(0.95f, 0.95f, 1f), 2f, 3.5f, LightShadows.None);

            CreatePointLight(root, new Vector3(2.7f, 2.1f, 0f),
                new Color(0.95f, 0.95f, 1f), 2f, 3.5f, LightShadows.None);
        }

        private static void CreatePointLight(Transform parent, Vector3 position, Color color,
            float intensity, float range, LightShadows shadows)
        {
            var go = new GameObject("Point Light");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
        }

        // ------------------------------------------------------------------
        // Estrutura da sala
        // ------------------------------------------------------------------

        private static void BuildRoomShell()
        {
            var root = new GameObject("Room").transform;

            var floorMat = MaterialLibrary.Get(MaterialLibrary.Names.Floor);
            var wallMat = MaterialLibrary.Get(MaterialLibrary.Names.Wall);
            var ceilingMat = MaterialLibrary.Get(MaterialLibrary.Names.Ceiling);

            float width = RoomHalfX * 2f;
            float depth = RoomHalfZ * 2f;

            // O piso tambem e a area de teletransporte de dentro da sala.
            var floor = SceneBuildUtils.Box("Floor", root, new Vector3(0f, -0.05f, 0f),
                new Vector3(width, 0.1f, depth), floorMat);
            AddTeleportArea(floor);

            SceneBuildUtils.Box("Ceiling", root, new Vector3(0f, RoomHeight + 0.05f, 0f),
                new Vector3(width, 0.1f, depth), ceilingMat);

            float wallCenterY = RoomHeight * 0.5f;
            float outerX = RoomHalfX + WallThickness * 0.5f;
            float outerZ = RoomHalfZ + WallThickness * 0.5f;

            SceneBuildUtils.Box("Wall West", root, new Vector3(-outerX, wallCenterY, 0f),
                new Vector3(WallThickness, RoomHeight, depth + WallThickness * 2f), wallMat);

            SceneBuildUtils.Box("Wall East", root, new Vector3(outerX, wallCenterY, 0f),
                new Vector3(WallThickness, RoomHeight, depth + WallThickness * 2f), wallMat);

            SceneBuildUtils.Box("Wall South", root, new Vector3(0f, wallCenterY, -outerZ),
                new Vector3(width, RoomHeight, WallThickness), wallMat);

            // Parede norte em tres pedacos, deixando o vao da porta.
            float sideWidth = RoomHalfX - DoorHalfWidth;
            float sideCenterX = DoorHalfWidth + sideWidth * 0.5f;

            SceneBuildUtils.Box("Wall North Left", root, new Vector3(-sideCenterX, wallCenterY, outerZ),
                new Vector3(sideWidth, RoomHeight, WallThickness), wallMat);

            SceneBuildUtils.Box("Wall North Right", root, new Vector3(sideCenterX, wallCenterY, outerZ),
                new Vector3(sideWidth, RoomHeight, WallThickness), wallMat);

            SceneBuildUtils.Box("Wall North Lintel", root,
                new Vector3(0f, DoorHeight + (RoomHeight - DoorHeight) * 0.5f, outerZ),
                new Vector3(DoorHalfWidth * 2f, RoomHeight - DoorHeight, WallThickness), wallMat);

            BuildBaseboards(root, sideWidth, sideCenterX);
        }

        /// <summary>
        /// Rodapé em volta da sala. É geometria barata e sem colisor, mas é o
        /// que separa visualmente a parede do chão: sem essa linha, os dois
        /// planos se encontram num canto duro e o ambiente lê como uma caixa
        /// em vez de um cômodo.
        /// </summary>
        private static void BuildBaseboards(Transform root, float sideWidth, float sideCenterX)
        {
            var trimMat = MaterialLibrary.Get(MaterialLibrary.Names.Trim);

            const float height = 0.12f;
            const float depth = 0.03f;
            float y = height * 0.5f;
            float inner = RoomHalfX - depth * 0.5f;

            SceneBuildUtils.Box("Baseboard West", root, new Vector3(-inner, y, 0f),
                new Vector3(depth, height, RoomHalfZ * 2f), trimMat, keepCollider: false);

            SceneBuildUtils.Box("Baseboard East", root, new Vector3(inner, y, 0f),
                new Vector3(depth, height, RoomHalfZ * 2f), trimMat, keepCollider: false);

            SceneBuildUtils.Box("Baseboard South", root, new Vector3(0f, y, -(RoomHalfZ - depth * 0.5f)),
                new Vector3(RoomHalfX * 2f, height, depth), trimMat, keepCollider: false);

            // A parede norte tem o vao da porta no meio, entao o rodape dela
            // acompanha os dois trechos laterais.
            SceneBuildUtils.Box("Baseboard North Left", root,
                new Vector3(-sideCenterX, y, RoomHalfZ - depth * 0.5f),
                new Vector3(sideWidth, height, depth), trimMat, keepCollider: false);

            SceneBuildUtils.Box("Baseboard North Right", root,
                new Vector3(sideCenterX, y, RoomHalfZ - depth * 0.5f),
                new Vector3(sideWidth, height, depth), trimMat, keepCollider: false);
        }

        /// <summary>Corredor curto depois da porta, para o jogador poder sair de verdade.</summary>
        private static void BuildEscapeCorridor(AnimatedDoor exitDoor)
        {
            var root = new GameObject("Escape Corridor").transform;

            var floorMat = MaterialLibrary.Get(MaterialLibrary.Names.Floor);
            var wallMat = MaterialLibrary.Get(MaterialLibrary.Names.Wall);

            const float startZ = 3.65f;
            const float endZ = 7f;
            const float halfWidth = 2.2f;

            float centerZ = (startZ + endZ) * 0.5f;
            float length = endZ - startZ;

            var floor = SceneBuildUtils.Box("Corridor Floor", root, new Vector3(0f, -0.05f, centerZ),
                new Vector3(halfWidth * 2f, 0.1f, length), floorMat);
            AddTeleportArea(floor);

            SceneBuildUtils.Box("Corridor Ceiling", root, new Vector3(0f, RoomHeight + 0.05f, centerZ),
                new Vector3(halfWidth * 2f, 0.1f, length), wallMat);

            SceneBuildUtils.Box("Corridor Wall Back", root, new Vector3(0f, RoomHeight * 0.5f, endZ + 0.075f),
                new Vector3(halfWidth * 2f, RoomHeight, WallThickness), wallMat);

            SceneBuildUtils.Box("Corridor Wall Left", root,
                new Vector3(-halfWidth - 0.075f, RoomHeight * 0.5f, centerZ),
                new Vector3(WallThickness, RoomHeight, length), wallMat);

            SceneBuildUtils.Box("Corridor Wall Right", root,
                new Vector3(halfWidth + 0.075f, RoomHeight * 0.5f, centerZ),
                new Vector3(WallThickness, RoomHeight, length), wallMat);

            // Em Unity o texto e legivel pelo lado OPOSTO ao seu +Z local, por
            // isso cada letreiro aponta o +Z para longe de quem vai ler.
            SceneBuildUtils.Text3D("Exit Sign", root, new Vector3(0f, 2.4f, endZ - 0.06f),
                new Vector2(2f, 0.5f), "SAIDA", new Color(0.4f, 1f, 0.5f),
                Quaternion.LookRotation(Vector3.forward));

            // Gatilho de vitoria: atravessar o vao depois da porta aberta.
            var trigger = SceneBuildUtils.Empty("Escape Trigger", root, new Vector3(0f, 1.2f, 4.9f));
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(halfWidth * 2f - 0.2f, 2.4f, 1.8f);

            var escape = trigger.AddComponent<EscapeTrigger>();
            SceneBuildUtils.SetFields(escape,
                "exitDoor", exitDoor,
                "autoWinAfterOpen", 25f);
        }

        private static void SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[EscapeRoomSceneBuilder] Prefab do XR Origin nao encontrado em {XrOriginPrefabPath}. " +
                               "Importe os Starter Assets do XR Interaction Toolkit.");
                return;
            }

            var origin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            origin.transform.position = new Vector3(0f, 0f, -2.2f);
            origin.transform.rotation = Quaternion.identity;   // de frente para +Z, na direcao da porta

            // O simulador permite testar no PC, com teclado e mouse, sem o oculos.
            var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);

            if (simulatorPrefab != null)
                PrefabUtility.InstantiatePrefab(simulatorPrefab);
            else
                Debug.LogWarning("[EscapeRoomSceneBuilder] Prefab do XR Interaction Simulator nao encontrado.");
        }

        private static void CreateGameManager()
        {
            var go = new GameObject("Game Manager");
            var game = go.AddComponent<GameManager>();   // RequireComponent traz o GameTimer

            SceneBuildUtils.SetFields(game.GetComponent<GameTimer>(),
                "totalSeconds", 600f,
                "warningSeconds", 60f);

            SceneBuildUtils.SetFields(game,
                "autoStart", true,
                "bonusPerPuzzle", 0f);
        }

        // ------------------------------------------------------------------
        // Enigma 1: mesa, gaveta e bilhete
        // ------------------------------------------------------------------

        private static void BuildDesk()
        {
            var wood = MaterialLibrary.Get(MaterialLibrary.Names.Wood);
            var woodDark = MaterialLibrary.Get(MaterialLibrary.Names.WoodDark);
            var paper = MaterialLibrary.Get(MaterialLibrary.Names.Paper);
            var metal = MaterialLibrary.Get(MaterialLibrary.Names.Metal);

            var desk = SceneBuildUtils.Empty("Desk", null, new Vector3(-3.0f, 0f, 1.4f)).transform;

            SceneBuildUtils.Box("Top", desk, new Vector3(0f, 0.75f, 0f), new Vector3(0.75f, 0.06f, 1.5f), wood);
            SceneBuildUtils.Box("Side -Z", desk, new Vector3(0f, 0.36f, -0.72f), new Vector3(0.7f, 0.72f, 0.06f), woodDark);
            SceneBuildUtils.Box("Side +Z", desk, new Vector3(0f, 0.36f, 0.72f), new Vector3(0.7f, 0.72f, 0.06f), woodDark);
            SceneBuildUtils.Box("Back", desk, new Vector3(-0.32f, 0.36f, 0f), new Vector3(0.06f, 0.72f, 1.5f), woodDark);
            SceneBuildUtils.Box("Shelf", desk, new Vector3(0f, 0.30f, 0f), new Vector3(0.68f, 0.03f, 1.4f), woodDark);

            // Travessa entre o topo da gaveta e o tampo: sem ela da para
            // espiar o bilhete por cima, de fora, e o enigma se entrega.
            SceneBuildUtils.Box("Apron", desk, new Vector3(0.33f, 0.665f, 0f),
                new Vector3(0.05f, 0.12f, 1.5f), wood);

            // --- Gaveta: raiz vazia (escala 1) com as tabuas como filhas ---
            var drawer = SceneBuildUtils.Empty("Drawer", desk, new Vector3(0f, 0.52f, 0f)).transform;

            // Frente em tom mais escuro que o resto da mesa: sem esse contraste
            // a gaveta fechada some no movel e o jogador nao percebe que abre.
            SceneBuildUtils.Box("Front", drawer, new Vector3(0.33f, 0f, 0f), new Vector3(0.04f, 0.19f, 1.42f), woodDark);
            SceneBuildUtils.Box("Bottom", drawer, new Vector3(0f, -0.08f, 0f), new Vector3(0.64f, 0.03f, 1.38f), woodDark);
            SceneBuildUtils.Box("Side -Z", drawer, new Vector3(0f, 0f, -0.69f), new Vector3(0.64f, 0.18f, 0.03f), woodDark);
            SceneBuildUtils.Box("Side +Z", drawer, new Vector3(0f, 0f, 0.69f), new Vector3(0.64f, 0.18f, 0.03f), woodDark);
            SceneBuildUtils.Box("Back", drawer, new Vector3(-0.33f, 0f, 0f), new Vector3(0.04f, 0.18f, 1.38f), woodDark);
            SceneBuildUtils.Box("Handle", drawer, new Vector3(0.37f, 0f, 0f), new Vector3(0.05f, 0.04f, 0.3f), metal);

            var drawerBody = drawer.gameObject.AddComponent<Rigidbody>();
            drawerBody.isKinematic = true;
            drawerBody.useGravity = false;

            var drawerGrab = drawer.gameObject.AddComponent<XRGrabInteractable>();
            drawerGrab.movementType = XRBaseInteractable.MovementType.Kinematic;

            var drawerAudio = AddAudioCue(drawer.gameObject);
            var slidingDrawer = drawer.gameObject.AddComponent<SlidingDrawer>();

            SceneBuildUtils.SetFields(slidingDrawer,
                "slideAxis", Vector3.right,     // a gaveta corre para dentro da sala
                "travelDistance", 0.32f,
                "openThreshold", 0.45f,
                "followSpeed", 18f,
                "audioCue", drawerAudio);

            // --- Bilhete dentro da gaveta ---
            // Filho da gaveta e cinematico: acompanha a gaveta sem depender de
            // atrito e nao escorrega para dentro do movel quando ela corre.
            var note = SceneBuildUtils.Empty("Note", drawer, new Vector3(0.02f, -0.055f, 0.35f)).transform;

            SceneBuildUtils.Box("Paper", note, Vector3.zero, new Vector3(0.21f, 0.004f, 0.29f), paper);

            SceneBuildUtils.Text3D("Text", note, new Vector3(0f, 0.004f, 0f),
                new Vector2(0.19f, 0.26f),
                "O QUADRO\nESCONDE\nO PADRAO", new Color(0.15f, 0.12f, 0.10f),
                Quaternion.Euler(90f, 0f, 0f));

            var noteBody = note.gameObject.AddComponent<Rigidbody>();
            noteBody.isKinematic = true;
            noteBody.useGravity = false;

            var noteGrab = note.gameObject.AddComponent<XRGrabInteractable>();
            noteGrab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            noteGrab.throwOnDetach = false;

            // Para de flutuar assim que o jogador o solta pela primeira vez.
            SceneBuildUtils.SetField(note.gameObject.AddComponent<BecomesPhysicalOnGrab>(),
                "physicalMass", 0.05f);

            var noteAudio = AddAudioCue(note.gameObject);
            var inspectable = note.gameObject.AddComponent<Inspectable>();

            SceneBuildUtils.SetFields(inspectable,
                "clueText", "Atras do quadro esta o padrao das alavancas e o inicio do codigo do cofre.",
                "readDistance", 0.5f,
                "completesStep", true,
                "step", PuzzleStep.NoteFound,
                "audioCue", noteAudio);
        }

        // ------------------------------------------------------------------
        // Enigma 2: quadro e painel escondido
        // ------------------------------------------------------------------

        private static void BuildPainting()
        {
            var root = new GameObject("Painting Puzzle").transform;

            var canvasMat = MaterialLibrary.Get(MaterialLibrary.Names.Canvas);
            var frameMat = MaterialLibrary.Get(MaterialLibrary.Names.WoodDark);

            const float wallZ = -3.42f;   // face interna da parede sul fica em z = -3.5

            // Painel escondido, encostado na parede; nasce desligado.
            var hidden = SceneBuildUtils.Empty("Hidden Panel", root, new Vector3(0f, 1.65f, wallZ - 0.05f)).transform;

            // Placa escura com texto claro. O inverso (texto claro sobre um
            // material emissivo claro) fica ilegivel sob a luz da sala.
            SceneBuildUtils.Box("Plate", hidden, Vector3.zero, new Vector3(0.98f, 0.70f, 0.02f),
                MaterialLibrary.Get(MaterialLibrary.Names.MetalDark), keepCollider: false);

            SceneBuildUtils.Text3D("Text", hidden, new Vector3(0f, 0f, 0.015f),
                new Vector2(0.92f, 0.62f),
                "ALAVANCAS\nCIMA - BAIXO - CIMA\n\nCOFRE\n7  4  ?  ?", new Color(0.45f, 1f, 0.9f),
                Quaternion.LookRotation(Vector3.back));

            // Quadro por cima: comeca fixo na parede e solta ao ser pego.
            var painting = SceneBuildUtils.Empty("Painting", root, new Vector3(0f, 1.65f, wallZ)).transform;

            SceneBuildUtils.Box("Canvas", painting, Vector3.zero, new Vector3(0.95f, 0.72f, 0.05f), canvasMat);
            SceneBuildUtils.Box("Frame Top", painting, new Vector3(0f, 0.39f, 0f), new Vector3(1.03f, 0.06f, 0.06f), frameMat, keepCollider: false);
            SceneBuildUtils.Box("Frame Bottom", painting, new Vector3(0f, -0.39f, 0f), new Vector3(1.03f, 0.06f, 0.06f), frameMat, keepCollider: false);
            SceneBuildUtils.Box("Frame Left", painting, new Vector3(-0.49f, 0f, 0f), new Vector3(0.06f, 0.84f, 0.06f), frameMat, keepCollider: false);
            SceneBuildUtils.Box("Frame Right", painting, new Vector3(0.49f, 0f, 0f), new Vector3(0.06f, 0.84f, 0.06f), frameMat, keepCollider: false);

            var body = painting.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = 2.5f;

            var grab = painting.gameObject.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;

            var audio = AddAudioCue(painting.gameObject);
            var reveal = painting.gameObject.AddComponent<HiddenReveal>();

            SceneBuildUtils.SetFields(reveal,
                "hiddenObjects", new List<UnityEngine.Object> { hidden.gameObject },
                "completesStep", true,
                "step", PuzzleStep.PaintingRemoved,
                "audioCue", audio);

            // O Awake do HiddenReveal tambem desliga isso em runtime, mas
            // desligar aqui deixa a cena salva coerente com o que o jogador ve.
            hidden.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Enigma 3: alavancas
        // ------------------------------------------------------------------

        /// <summary>Painel com os dois ultimos digitos do codigo; nasce desligado.</summary>
        private static GameObject BuildLeverClueDisplay()
        {

            var root = SceneBuildUtils.Empty("Lever Clue Display", null, new Vector3(3.44f, 1.80f, 0f)).transform;

            SceneBuildUtils.Box("Plate", root, Vector3.zero, new Vector3(0.04f, 0.32f, 1.1f),
                MaterialLibrary.Get(MaterialLibrary.Names.MetalDark), keepCollider: false);

            SceneBuildUtils.Text3D("Text", root, new Vector3(-0.03f, 0f, 0f),
                new Vector2(1.02f, 0.24f),
                "COFRE:  ?  ?  2  9", new Color(0.45f, 1f, 0.9f),
                Quaternion.LookRotation(Vector3.right));

            return root.gameObject;
        }

        private static void BuildLevers(GameObject clueDisplay)
        {
            var root = new GameObject("Lever Puzzle").transform;

            var plateMat = MaterialLibrary.Get(MaterialLibrary.Names.MetalDark);
            var metalMat = MaterialLibrary.Get(MaterialLibrary.Names.Metal);
            var rubberMat = MaterialLibrary.Get(MaterialLibrary.Names.Rubber);
            var redMat = MaterialLibrary.Get(MaterialLibrary.Names.IndicatorRed);

            SceneBuildUtils.Box("Backing Plate", root, new Vector3(3.46f, 1.15f, 0f),
                new Vector3(0.05f, 0.75f, 2.0f), plateMat, keepCollider: false);

            // Da esquerda para a direita do ponto de vista de quem olha a parede
            // leste: quem esta na sala ve o +Z a sua esquerda, por isso a ordem
            // comeca no z positivo. Assim o rotulo "1" fica mesmo a esquerda.
            float[] positionsZ = { 0.6f, 0f, -0.6f };
            var levers = new List<UnityEngine.Object>();

            for (int i = 0; i < positionsZ.Length; i++)
            {
                var leverRoot = SceneBuildUtils.Empty($"Lever {i + 1}", root,
                    new Vector3(3.40f, 1.15f, positionsZ[i])).transform;

                SceneBuildUtils.Box("Base", leverRoot, new Vector3(0.02f, 0f, 0f),
                    new Vector3(0.06f, 0.16f, 0.16f), plateMat, keepCollider: false);

                // O pivo do braco: girar este transform faz a alavanca subir e descer.
                var handle = SceneBuildUtils.Empty("Handle", leverRoot).transform;

                SceneBuildUtils.Box("Arm", handle, new Vector3(-0.11f, 0f, 0f),
                    new Vector3(0.22f, 0.035f, 0.035f), metalMat);

                SceneBuildUtils.Sphere("Knob", handle, new Vector3(-0.21f, 0f, 0f), 0.06f, rubberMat);

                SceneBuildUtils.Text3D($"Label", leverRoot, new Vector3(-0.03f, 0.24f, 0f),
                    new Vector2(0.2f, 0.12f), (i + 1).ToString(), new Color(0.9f, 0.9f, 0.9f),
                    Quaternion.LookRotation(Vector3.right));

                // O XRSimpleInteractable usa os colliders dos filhos (braco e manopla).
                leverRoot.gameObject.AddComponent<XRSimpleInteractable>();
                var audio = AddAudioCue(leverRoot.gameObject);

                var lever = leverRoot.gameObject.AddComponent<Lever>();
                SceneBuildUtils.SetFields(lever,
                    "handle", handle,
                    "downAngle", 32f,
                    "upAngle", -32f,
                    "rotationAxis", Vector3.forward,   // o braco gira no plano XY
                    "travelDuration", 0.18f,
                    "startsUp", false,
                    "audioCue", audio);

                levers.Add(lever);
            }

            var indicator = SceneBuildUtils.Empty("Lever Indicator", root, new Vector3(3.42f, 0.88f, 0f)).transform;
            var bulb = SceneBuildUtils.Sphere("Bulb", indicator, Vector3.zero, 0.09f, redMat, keepCollider: false);

            var lightGo = SceneBuildUtils.Empty("Light", indicator);
            var pointLight = lightGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = 0.9f;
            pointLight.intensity = 0.5f;
            pointLight.shadows = LightShadows.None;

            var emissive = bulb.AddComponent<EmissiveIndicator>();
            SceneBuildUtils.SetFields(emissive,
                "attachedLight", pointLight,
                "pulseWhenLocked", true,
                "pulseSpeed", 2f);

            var puzzleAudio = AddAudioCue(root.gameObject);
            var puzzle = root.gameObject.AddComponent<LeverPuzzle>();

            SceneBuildUtils.SetFields(puzzle,
                "levers", levers,
                "expectedPattern", LeverPattern,
                "objectsToReveal", new List<UnityEngine.Object> { clueDisplay },
                "audioCue", puzzleAudio,
                "indicator", emissive);

            clueDisplay.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Enigma 4: cofre e teclado numerico
        // ------------------------------------------------------------------

        private static AnimatedDoor BuildSafe()
        {
            var root = new GameObject("Safe").transform;

            var shellMat = MaterialLibrary.Get(MaterialLibrary.Names.MetalDark);
            var doorMat = MaterialLibrary.Get(MaterialLibrary.Names.Metal);
            var cardMat = MaterialLibrary.Get(MaterialLibrary.Names.Card);

            const float centerY = 1.35f;
            const float centerZ = -1.3f;

            SceneBuildUtils.Box("Back", root, new Vector3(-3.47f, centerY, centerZ), new Vector3(0.04f, 0.72f, 0.72f), shellMat);
            SceneBuildUtils.Box("Top", root, new Vector3(-3.25f, centerY + 0.35f, centerZ), new Vector3(0.46f, 0.04f, 0.72f), shellMat);
            SceneBuildUtils.Box("Bottom", root, new Vector3(-3.25f, centerY - 0.35f, centerZ), new Vector3(0.46f, 0.04f, 0.72f), shellMat);
            SceneBuildUtils.Box("Side -Z", root, new Vector3(-3.25f, centerY, centerZ - 0.34f), new Vector3(0.46f, 0.72f, 0.04f), shellMat);
            SceneBuildUtils.Box("Side +Z", root, new Vector3(-3.25f, centerY, centerZ + 0.34f), new Vector3(0.46f, 0.72f, 0.04f), shellMat);

            // Porta do cofre: pivo na borda +Z, abrindo para dentro da sala.
            var pivot = SceneBuildUtils.Empty("Door Pivot", root,
                new Vector3(-3.03f, centerY, centerZ + 0.34f)).transform;

            SceneBuildUtils.Box("Leaf", pivot, new Vector3(0f, 0f, -0.33f), new Vector3(0.04f, 0.70f, 0.66f), doorMat);
            SceneBuildUtils.Box("Leaf Handle", pivot, new Vector3(0.05f, 0f, -0.58f), new Vector3(0.06f, 0.06f, 0.18f), shellMat);

            var doorAudio = AddAudioCue(pivot.gameObject);
            var door = pivot.gameObject.AddComponent<AnimatedDoor>();

            SceneBuildUtils.SetFields(door,
                "hingeAxis", Vector3.up,
                "closedAngle", 0f,
                "openAngle", -100f,       // giro negativo em Y leva a folha para +X, para fora
                "openDuration", 1.1f,
                "startLocked", true,
                "audioCue", doorAudio);

            // --- Cartao-chave, dentro do cofre ---
            var card = SceneBuildUtils.Empty("Key Card", root, new Vector3(-3.25f, centerY - 0.30f, centerZ)).transform;

            SceneBuildUtils.Box("Plastic", card, Vector3.zero, new Vector3(0.09f, 0.005f, 0.06f), cardMat);

            SceneBuildUtils.Text3D("Text", card, new Vector3(0f, 0.004f, 0f),
                new Vector2(0.085f, 0.05f), "SAIDA", Color.white,
                Quaternion.Euler(90f, 0f, 0f));

            var cardBody = card.gameObject.AddComponent<Rigidbody>();
            cardBody.isKinematic = true;
            cardBody.useGravity = false;

            var cardGrab = card.gameObject.AddComponent<XRGrabInteractable>();
            cardGrab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            cardGrab.throwOnDetach = false;

            SceneBuildUtils.SetField(card.gameObject.AddComponent<BecomesPhysicalOnGrab>(),
                "physicalMass", 0.05f);

            var keyCard = card.gameObject.AddComponent<KeyCard>();
            SceneBuildUtils.SetField(keyCard, "cardId", "EXIT");

            return door;
        }

        private static void BuildKeypad(AnimatedDoor safeDoor)
        {
            var panelMat = MaterialLibrary.Get(MaterialLibrary.Names.MetalDark);
            var keyMat = MaterialLibrary.Get(MaterialLibrary.Names.Key);
            var redMat = MaterialLibrary.Get(MaterialLibrary.Names.IndicatorRed);

            // O teclado encara +X (o interior da sala). Logo, o eixo +Z local
            // aponta para dentro da sala e as teclas afundam no sentido -Z local.
            var root = SceneBuildUtils.Empty("Keypad", null, new Vector3(-3.46f, 1.35f, -0.30f),
                Quaternion.LookRotation(Vector3.right)).transform;

            SceneBuildUtils.Box("Panel", root, Vector3.zero, new Vector3(0.40f, 0.58f, 0.05f), panelMat, keepCollider: false);

            var displayText = SceneBuildUtils.Text3D("Display", root, new Vector3(0f, 0.20f, 0.028f),
                new Vector2(0.34f, 0.12f), "----", new Color(0.45f, 1f, 0.85f),
                Quaternion.Euler(0f, 180f, 0f));

            var keypadAudio = AddAudioCue(root.gameObject);

            var indicatorRoot = SceneBuildUtils.Empty("Indicator", root, new Vector3(0.15f, 0.26f, 0.03f)).transform;
            var bulb = SceneBuildUtils.Sphere("Bulb", indicatorRoot, Vector3.zero, 0.035f, redMat, keepCollider: false);
            var emissive = bulb.AddComponent<EmissiveIndicator>();
            SceneBuildUtils.SetField(emissive, "pulseWhenLocked", true);

            var keypad = root.gameObject.AddComponent<Keypad>();

            SceneBuildUtils.SetFields(keypad,
                "correctCode", SafeCode,
                "autoValidate", true,
                "display", displayText,
                "placeholder", "-",
                "doorToUnlock", safeDoor,
                "audioCue", keypadAudio,
                "indicator", emissive);

            // Grade 3 x 4: digitos, limpar e confirmar.
            string[,] layout =
            {
                { "1", "2", "3" },
                { "4", "5", "6" },
                { "7", "8", "9" },
                { "C", "0", "OK" }
            };

            float[] rowY = { 0.095f, 0.015f, -0.065f, -0.145f };

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    // O eixo +X local aponta para -Z do mundo. Sem este sinal
                    // invertido as colunas do teclado sairiam espelhadas.
                    float localX = 0.11f - 0.11f * col;

                    var keyRoot = SceneBuildUtils.Empty($"Key {layout[row, col]}", root,
                        new Vector3(localX, rowY[row], 0.032f)).transform;

                    SceneBuildUtils.Box("Cap", keyRoot, Vector3.zero,
                        new Vector3(0.085f, 0.07f, 0.025f), keyMat);

                    SceneBuildUtils.Text3D("Label", keyRoot, new Vector3(0f, 0f, 0.016f),
                        new Vector2(0.08f, 0.06f), layout[row, col], new Color(0.12f, 0.12f, 0.14f),
                        Quaternion.Euler(0f, 180f, 0f));

                    keyRoot.gameObject.AddComponent<XRSimpleInteractable>();

                    var keyScript = keyRoot.gameObject.AddComponent<KeypadKey>();
                    SceneBuildUtils.SetFields(keyScript,
                        "label", layout[row, col],
                        "keypad", keypad,
                        "pressDepth", -0.008f,   // afunda no sentido -Z local, para dentro do painel
                        "pressDuration", 0.08f);
                }
            }
        }

        // ------------------------------------------------------------------
        // Enigma 5: porta final e leitor de cartao
        // ------------------------------------------------------------------

        private static AnimatedDoor BuildExitDoor()
        {
            var root = new GameObject("Exit Door").transform;

            var doorMat = MaterialLibrary.Get(MaterialLibrary.Names.WoodDark);
            var metalMat = MaterialLibrary.Get(MaterialLibrary.Names.Metal);

            // Pivo na borda esquerda do vao; a folha abre para o corredor (+Z).
            var pivot = SceneBuildUtils.Empty("Door Pivot", root,
                new Vector3(-DoorHalfWidth, 0f, RoomHalfZ + WallThickness * 0.5f)).transform;

            SceneBuildUtils.Box("Leaf", pivot, new Vector3(DoorHalfWidth, DoorHeight * 0.5f, 0f),
                new Vector3(DoorHalfWidth * 2f - 0.04f, DoorHeight - 0.03f, 0.08f), doorMat);

            SceneBuildUtils.Box("Handle", pivot, new Vector3(DoorHalfWidth * 1.75f, 1.05f, -0.09f),
                new Vector3(0.05f, 0.05f, 0.18f), metalMat, keepCollider: false);

            var audio = AddAudioCue(pivot.gameObject);
            var door = pivot.gameObject.AddComponent<AnimatedDoor>();

            SceneBuildUtils.SetFields(door,
                "hingeAxis", Vector3.up,
                "closedAngle", 0f,
                "openAngle", -95f,
                "openDuration", 1.6f,
                "startLocked", true,
                "audioCue", audio);

            return door;
        }

        private static void BuildCardReader(AnimatedDoor exitDoor)
        {
            var panelMat = MaterialLibrary.Get(MaterialLibrary.Names.MetalDark);
            var redMat = MaterialLibrary.Get(MaterialLibrary.Names.IndicatorRed);

            // Encara -Z, ou seja, o interior da sala.
            var root = SceneBuildUtils.Empty("Card Reader", null, new Vector3(0.95f, 1.15f, 3.42f),
                Quaternion.LookRotation(Vector3.back)).transform;

            SceneBuildUtils.Box("Panel", root, Vector3.zero, new Vector3(0.22f, 0.32f, 0.06f), panelMat, keepCollider: false);

            SceneBuildUtils.Text3D("Label", root, new Vector3(0f, 0.11f, 0.035f),
                new Vector2(0.2f, 0.07f), "LEITOR", new Color(0.85f, 0.9f, 0.95f),
                Quaternion.Euler(0f, 180f, 0f));

            // Fenda do cartao: o collider em trigger e o que o socket usa para
            // perceber um objeto agarrado chegando perto.
            var slot = SceneBuildUtils.Empty("Card Slot", root, new Vector3(0f, -0.04f, 0.055f)).transform;

            // A area de encaixe e bem maior que o cartao de proposito. Mirar um
            // cartao de 9 x 6 cm numa fenda do mesmo tamanho, com a mao
            // tremendo no ar, e frustrante; com uma caixa de 30 cm o jogador
            // so precisa levar o cartao ate perto do leitor.
            var slotCollider = slot.gameObject.AddComponent<BoxCollider>();
            slotCollider.isTrigger = true;
            slotCollider.size = new Vector3(0.3f, 0.3f, 0.3f);

            var socket = slot.gameObject.AddComponent<XRSocketInteractor>();
            socket.showInteractableHoverMeshes = false;

            // Com o snap no hover, o cartao pula para dentro da fenda assim que
            // chega perto, ainda na mao do jogador: ele ve que vai encaixar
            // antes de soltar, em vez de tentar as cegas.
            socket.hoverSocketSnapping = true;
            socket.socketSnappingRadius = 0.18f;

            var indicatorRoot = SceneBuildUtils.Empty("Indicator", root, new Vector3(0.07f, 0.05f, 0.035f)).transform;
            var bulb = SceneBuildUtils.Sphere("Bulb", indicatorRoot, Vector3.zero, 0.035f, redMat, keepCollider: false);
            var emissive = bulb.AddComponent<EmissiveIndicator>();
            SceneBuildUtils.SetField(emissive, "pulseWhenLocked", true);

            var audio = AddAudioCue(slot.gameObject);
            var reader = slot.gameObject.AddComponent<CardReader>();

            SceneBuildUtils.SetFields(reader,
                "expectedCardId", "EXIT",
                "doorToUnlock", exitDoor,
                "audioCue", audio,
                "indicator", emissive);
        }

        // ------------------------------------------------------------------
        // Objetos soltos (fisica) e interface
        // ------------------------------------------------------------------

        /// <summary>
        /// Objetos decorativos com física de verdade (gravidade, massa, arremesso).
        /// Servem para o jogador sentir o peso das coisas e para demonstrar o
        /// agarrar físico do XRI, enquanto os itens de enigma ficam estáveis.
        /// </summary>
        private static void BuildProps()
        {
            var root = new GameObject("Props").transform;

            var woodMat = MaterialLibrary.Get(MaterialLibrary.Names.Wood);
            var metalMat = MaterialLibrary.Get(MaterialLibrary.Names.Metal);
            var rubberMat = MaterialLibrary.Get(MaterialLibrary.Names.Rubber);

            var mug = SceneBuildUtils.Cylinder("Mug", root, new Vector3(-2.95f, 0.84f, 1.9f), 0.09f, 0.11f, metalMat);
            MakePhysicsGrabbable(mug, 0.35f);

            var bookA = SceneBuildUtils.Box("Book A", root, new Vector3(2.6f, 0.04f, -2.7f),
                new Vector3(0.2f, 0.05f, 0.28f), woodMat);
            MakePhysicsGrabbable(bookA, 0.8f);

            var bookB = SceneBuildUtils.Box("Book B", root, new Vector3(2.55f, 0.11f, -2.62f),
                new Vector3(0.18f, 0.05f, 0.26f), woodMat);
            MakePhysicsGrabbable(bookB, 0.8f);

            var ball = SceneBuildUtils.Sphere("Ball", root, new Vector3(1.4f, 0.12f, -1.8f), 0.16f, rubberMat);
            MakePhysicsGrabbable(ball, 0.4f);

            // Banqueta: serve de referencia de escala dentro da sala.
            var stool = SceneBuildUtils.Empty("Stool", root, new Vector3(1.9f, 0f, 1.6f)).transform;
            SceneBuildUtils.Box("Seat", stool, new Vector3(0f, 0.45f, 0f), new Vector3(0.36f, 0.05f, 0.36f), woodMat);
            SceneBuildUtils.Box("Leg 1", stool, new Vector3(-0.15f, 0.22f, -0.15f), new Vector3(0.04f, 0.45f, 0.04f), woodMat);
            SceneBuildUtils.Box("Leg 2", stool, new Vector3(0.15f, 0.22f, -0.15f), new Vector3(0.04f, 0.45f, 0.04f), woodMat);
            SceneBuildUtils.Box("Leg 3", stool, new Vector3(-0.15f, 0.22f, 0.15f), new Vector3(0.04f, 0.45f, 0.04f), woodMat);
            SceneBuildUtils.Box("Leg 4", stool, new Vector3(0.15f, 0.22f, 0.15f), new Vector3(0.04f, 0.45f, 0.04f), woodMat);
        }

        private static void MakePhysicsGrabbable(GameObject go, float mass)
        {
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;

            AddAudioCue(go);
        }

        private static void BuildHud()
        {
            var root = SceneBuildUtils.Empty("HUD", null, new Vector3(0f, 1.25f, -1.0f)).transform;

            // Mais longe e um pouco abaixo da linha dos olhos: o painel fica
            // legivel de relance sem ocupar a area onde o jogador manipula as
            // coisas, que e sempre perto e abaixo.
            var lazy = root.gameObject.AddComponent<LazyFollow>();
            SceneBuildUtils.SetFields(lazy,
                "distance", 2.1f,
                "verticalOffset", -0.3f,
                "angleThreshold", 35f,
                "followSpeed", 2.5f);

            var canvasRect = CreateWorldCanvas("Canvas", root, new Vector2(900f, 420f), 0.0009f);

            CreatePanelBackground(canvasRect, new Vector2(900f, 230f), new Vector2(0f, 110f),
                new Color(0.05f, 0.06f, 0.09f, 0.72f));

            var timer = CreateUiText(canvasRect, "Timer", new Vector2(0f, 168f), new Vector2(880f, 110f),
                "10:00", 96f, new Color(0.85f, 0.95f, 1f));

            var objective = CreateUiText(canvasRect, "Objective", new Vector2(0f, 88f), new Vector2(860f, 60f),
                "Procure algo escrito na sala.", 40f, new Color(0.95f, 0.92f, 0.70f));

            var progress = CreateUiText(canvasRect, "Progress", new Vector2(0f, 38f), new Vector2(860f, 48f),
                $"Enigmas: 0/{PuzzleProgress.RequiredStepCount}", 32f, new Color(0.70f, 0.78f, 0.85f));

            // Painel de pista: aparece so quando o jogador le algo de perto.
            var cluePanel = new GameObject("Clue Panel");
            var clueRect = cluePanel.AddComponent<RectTransform>();
            cluePanel.transform.SetParent(canvasRect, false);
            clueRect.anchoredPosition = new Vector2(0f, -110f);
            clueRect.sizeDelta = new Vector2(880f, 180f);

            CreatePanelBackground(clueRect, new Vector2(880f, 180f), Vector2.zero,
                new Color(0.09f, 0.08f, 0.05f, 0.85f));

            var clue = CreateUiText(clueRect, "Clue", Vector2.zero, new Vector2(840f, 160f),
                string.Empty, 38f, new Color(1f, 0.96f, 0.85f));

            cluePanel.SetActive(false);

            var audio = AddAudioCue(root.gameObject);
            var hud = root.gameObject.AddComponent<HudController>();

            SceneBuildUtils.SetFields(hud,
                "timerLabel", timer,
                "objectiveLabel", objective,
                "clueLabel", clue,
                "progressLabel", progress,
                "cluePanel", cluePanel,
                "audioCue", audio);
        }

        private static void BuildEndScreen()
        {
            var root = SceneBuildUtils.Empty("End Screen", null, new Vector3(0f, 1.4f, -1.2f)).transform;

            var lazy = root.gameObject.AddComponent<LazyFollow>();
            SceneBuildUtils.SetFields(lazy,
                "distance", 1.9f,
                "verticalOffset", -0.1f,
                "angleThreshold", 20f,
                "followSpeed", 3f);

            var controller = root.gameObject.AddComponent<EndScreenController>();

            var panel = SceneBuildUtils.Empty("Panel", root).transform;
            var canvasRect = CreateWorldCanvas("Canvas", panel, new Vector2(1000f, 520f), 0.0012f);

            CreatePanelBackground(canvasRect, new Vector2(1000f, 520f), Vector2.zero,
                new Color(0.04f, 0.05f, 0.08f, 0.92f));

            var title = CreateUiText(canvasRect, "Title", new Vector2(0f, 150f), new Vector2(960f, 140f),
                "VOCE ESCAPOU!", 90f, new Color(0.35f, 1f, 0.5f));

            var detail = CreateUiText(canvasRect, "Detail", new Vector2(0f, 30f), new Vector2(960f, 90f),
                "Tempo usado: 00:00", 44f, new Color(0.85f, 0.9f, 0.95f));

            SceneBuildUtils.SetFields(controller,
                "panelRoot", panel.gameObject,
                "titleLabel", title,
                "detailLabel", detail);

            // Os botoes ficam 3 cm a frente do painel para nao brigarem com
            // ele no z-buffer, e usam colliders 3D (nao UI) para o jogador
            // poder apertar com a mao ou com o raio do controle.
            CreateEndScreenButton(panel, "Restart Button", new Vector3(-0.22f, -0.26f, -0.03f),
                "JOGAR\nDE NOVO", EndScreenAction.Restart, controller);

            CreateEndScreenButton(panel, "Quit Button", new Vector3(0.22f, -0.26f, -0.03f),
                "SAIR", EndScreenAction.Quit, controller);

            panel.gameObject.SetActive(false);
        }

        private static void CreateEndScreenButton(Transform parent, string name, Vector3 localPosition,
            string label, EndScreenAction action, EndScreenController controller)
        {
            var root = SceneBuildUtils.Empty(name, parent, localPosition).transform;

            SceneBuildUtils.Box("Body", root, Vector3.zero, new Vector3(0.34f, 0.14f, 0.04f),
                MaterialLibrary.Get(MaterialLibrary.Names.MetalDark));

            SceneBuildUtils.Text3D("Label", root, new Vector3(0f, 0f, -0.025f),
                new Vector2(0.32f, 0.12f), label, new Color(0.9f, 0.95f, 1f));

            root.gameObject.AddComponent<XRSimpleInteractable>();
            var audio = AddAudioCue(root.gameObject);

            var script = root.gameObject.AddComponent<EndScreenButton>();
            SceneBuildUtils.SetFields(script,
                "endScreen", controller,
                "action", action,
                "audioCue", audio);
        }

        private static RectTransform CreateWorldCanvas(string name, Transform parent, Vector2 size, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            go.transform.localScale = Vector3.one * scale;

            return rect;
        }

        /// <summary>
        /// Fundo de painel. Usa RawImage porque ela desenha um retângulo sólido
        /// mesmo sem textura, o que evita criar sprites só para o HUD.
        /// </summary>
        private static void CreatePanelBackground(Transform parent, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            var go = new GameObject("Background");
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);

            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<UnityEngine.UI.RawImage>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateUiText(Transform parent, string name, Vector2 anchoredPosition,
            Vector2 size, string content, float fontSize, Color color)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);

            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshProUGUI>();
            SceneBuildUtils.ApplyDefaultFont(text);
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;

            return text;
        }

        /// <summary>
        /// Marca uma superficie como destino de teletransporte, na camada de
        /// interacao que o Teleport Interactor dos Starter Assets escuta.
        /// Sem ajustar a camada, a area existe mas o raio nunca a encontra.
        /// </summary>
        private static void AddTeleportArea(GameObject floor)
        {
            var area = floor.AddComponent<TeleportationArea>();
            area.interactionLayers = 1 << TeleportInteractionLayer;
        }

        /// <summary>
        /// Dá realce ao mirar e som ao pegar a tudo que for interativo.
        ///
        /// Feito numa passada só, no fim, em vez de item por item: é um
        /// comportamento que vale para qualquer interativo da sala, e varrer a
        /// cena garante que nada novo fique de fora por esquecimento.
        /// </summary>
        private static void AddFeedbackToEveryInteractable()
        {
            var interactables = Object.FindObjectsByType<XRBaseInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var interactable in interactables)
            {
                var target = interactable.gameObject;

                if (target.GetComponent<InteractableFeedback>() != null)
                    continue;

                var feedback = target.AddComponent<InteractableFeedback>();
                SceneBuildUtils.SetField(feedback, "audioCue", AddAudioCue(target));
            }

            Debug.Log($"[EscapeRoomSceneBuilder] Feedback aplicado a {interactables.Length} objetos interativos.");
        }

        private static AudioCue AddAudioCue(GameObject go)
        {
            if (go.GetComponent<AudioSource>() == null)
                go.AddComponent<AudioSource>();

            var existing = go.GetComponent<AudioCue>();
            return existing != null ? existing : go.AddComponent<AudioCue>();
        }

        /// <summary>
        /// Deixa a cena gerada como a última cena aberta, para o Editor abrir
        /// nela da próxima vez em vez de numa cena vazia.
        ///
        /// Quando o construtor roda em linha de comando, a Unity grava esse
        /// registro vazio ao sair, e quem abre o projeto depois cai numa cena
        /// "Untitled" sem entender por quê. O arquivo fica em Library/, que não
        /// é versionado e a Unity reescreve sozinha — mexer nele aqui é só uma
        /// conveniência, não altera o projeto.
        /// </summary>
        private static void RememberSceneForNextEditorLaunch()
        {
            try
            {
                System.IO.File.WriteAllText("Library/LastSceneManagerSetup.txt",
                    "sceneSetups:\n" +
                    $"- path: {ScenePath}\n" +
                    "  isLoaded: 1\n" +
                    "  isActive: 1\n" +
                    "  isSubScene: 0\n");
            }
            catch (System.IO.IOException exception)
            {
                // Nao vale derrubar a geracao da cena por causa de uma comodidade.
                Debug.LogWarning($"[EscapeRoomSceneBuilder] Nao deu para registrar a cena de abertura: {exception.Message}");
            }
        }

        private static void RegisterSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            if (scenes.Exists(s => s.path == ScenePath))
                return;

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
