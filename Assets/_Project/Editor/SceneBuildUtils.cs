using System;
using System.Collections;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace EscapeRoomVR.EditorTools
{
    /// <summary>
    /// Ferramentas de apoio ao construtor de cena: criação de primitivas,
    /// textos 3D e, principalmente, atribuição de campos privados
    /// serializados via SerializedObject.
    ///
    /// Usar SerializedObject em vez de deixar os campos públicos mantém os
    /// componentes de runtime encapsulados: só o Editor mexe nos campos, e as
    /// ligações ficam salvas na cena como se tivessem sido arrastadas à mão.
    /// </summary>
    public static class SceneBuildUtils
    {
        /// <summary>Cria um cubo com posição e tamanho em unidades de mundo.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size,
            Material material, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            Finish(go, material, keepCollider);
            return go;
        }

        /// <summary>Cria uma esfera. O tamanho é o diâmetro.</summary>
        public static GameObject Sphere(string name, Transform parent, Vector3 localPosition, float diameter,
            Material material, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * diameter;

            Finish(go, material, keepCollider);
            return go;
        }

        /// <summary>Cria um cilindro. Height é a altura total.</summary>
        public static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, float diameter,
            float height, Material material, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);

            Finish(go, material, keepCollider);
            return go;
        }

        /// <summary>GameObject vazio, útil como pivô ou agrupador.</summary>
        public static GameObject Empty(string name, Transform parent, Vector3 localPosition = default,
            Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            return go;
        }

        /// <summary>
        /// Texto 3D (TextMeshPro). <paramref name="size"/> é o retângulo do
        /// texto em metros.
        ///
        /// O corpo da fonte é definido por auto-size em vez de um valor fixo:
        /// em TextMeshPro o fontSize não tem relação direta com metros, então
        /// escolher o número na mão daria textos microscópicos ou gigantes.
        /// Com o auto-size, quem manda no tamanho é o retângulo, que está em
        /// unidades de mundo e é fácil de conferir na planta da sala.
        /// </summary>
        public static TextMeshPro Text3D(string name, Transform parent, Vector3 localPosition, Vector2 size,
            string content, Color color, Quaternion? localRotation = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshPro>();
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = false;

            ApplyDefaultFont(text);

            text.enableAutoSizing = true;
            text.fontSizeMin = 0.01f;
            text.fontSizeMax = 400f;
            text.ForceMeshUpdate();

            return text;
        }

        /// <summary>
        /// Garante que o texto tenha fonte. O TextMeshPro só atribui a fonte
        /// padrão sozinho quando o componente desperta em Play Mode; criado
        /// por script no Editor, ele nasce sem fonte e não renderiza nada.
        /// </summary>
        public static void ApplyDefaultFont(TMP_Text text)
        {
            if (text == null || text.font != null)
                return;

            var defaultFont = TMP_Settings.defaultFontAsset;

            if (defaultFont == null)
            {
                Debug.LogError("[SceneBuildUtils] Os recursos do TextMeshPro nao estao no projeto. " +
                               "Rode 'Escape Room VR > 0. Importar recursos do TextMeshPro' antes de gerar a cena.");
                return;
            }

            text.font = defaultFont;
        }

        private static void Finish(GameObject go, Material material, bool keepCollider)
        {
            if (material != null)
            {
                var renderer = go.GetComponent<Renderer>();

                if (renderer != null)
                    renderer.sharedMaterial = material;
            }

            if (keepCollider)
                return;

            var collider = go.GetComponent<Collider>();

            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
        }

        /// <summary>
        /// Atribui um campo privado serializado de um componente.
        /// Avisa no console se o nome do campo não existir, para um erro de
        /// digitação não passar em silêncio.
        /// </summary>
        public static void SetField(Component component, string fieldName, object value)
        {
            if (component == null)
            {
                Debug.LogWarning($"[SceneBuildUtils] Componente nulo ao atribuir '{fieldName}'.");
                return;
            }

            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[SceneBuildUtils] Campo '{fieldName}' nao existe em {component.GetType().Name}.");
                return;
            }

            Assign(property, value, component.GetType().Name, fieldName);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Atribui vários campos de uma vez: nome, valor, nome, valor...</summary>
        public static void SetFields(Component component, params object[] pairs)
        {
            if (pairs.Length % 2 != 0)
            {
                Debug.LogWarning("[SceneBuildUtils] SetFields espera pares nome/valor.");
                return;
            }

            var serialized = new SerializedObject(component);

            for (int i = 0; i < pairs.Length; i += 2)
            {
                string fieldName = pairs[i] as string;
                var property = fieldName != null ? serialized.FindProperty(fieldName) : null;

                if (property == null)
                {
                    Debug.LogWarning($"[SceneBuildUtils] Campo '{pairs[i]}' nao existe em {component.GetType().Name}.");
                    continue;
                }

                Assign(property, pairs[i + 1], component.GetType().Name, fieldName);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedProperty property, object value, string owner, string fieldName)
        {
            switch (value)
            {
                case null:
                    property.objectReferenceValue = null;
                    return;

                case bool b:
                    property.boolValue = b;
                    return;

                case int i:
                    property.intValue = i;
                    return;

                case float f:
                    property.floatValue = f;
                    return;

                case string s:
                    property.stringValue = s;
                    return;

                case Color c:
                    property.colorValue = c;
                    return;

                case Vector2 v2:
                    property.vector2Value = v2;
                    return;

                case Vector3 v3:
                    property.vector3Value = v3;
                    return;

                case Enum e:
                    // Os enums do projeto sao sequenciais a partir de zero,
                    // entao o valor numerico coincide com o indice do enum.
                    property.enumValueIndex = Convert.ToInt32(e);
                    return;

                case UnityEngine.Object o:
                    property.objectReferenceValue = o;
                    return;

                case IList list:
                    property.arraySize = list.Count;

                    for (int index = 0; index < list.Count; index++)
                        Assign(property.GetArrayElementAtIndex(index), list[index], owner, fieldName);

                    return;

                default:
                    Debug.LogWarning($"[SceneBuildUtils] Tipo nao suportado para {owner}.{fieldName}: {value.GetType().Name}.");
                    return;
            }
        }
    }
}
