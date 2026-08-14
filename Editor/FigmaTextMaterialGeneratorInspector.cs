using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
using TMPro;
using FigmaClient.Editor;

namespace FigmaTMPStyler.Editor
{
    [CustomEditor(typeof(FigmaTextTMPMaterialGenerator))]
    public class FigmaTextMaterialGeneratorInspector :  UnityEditor.Editor
    {
        private FigmaTextTMPMaterialGenerator Generator => (FigmaTextTMPMaterialGenerator)target;
      
        private Node TextNode;
        private string FileKey;
        private string NodeId;

        public override void OnInspectorGUI()
        {
            Generator.FigmaToken = EditorGUILayout.TextField("Figma Token", Generator.FigmaToken);

            Generator.MaterialSavePath = EditorGUILayout.TextField("Materials Save Folder", Generator.MaterialSavePath);

            Generator.NodeLink = EditorGUILayout.TextField("Node Link", Generator.NodeLink);
            LoadLocalSavedNode();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load And Apply"))
            {
                TextNode = null;
                LoadAndApply(false);
            }
            if (GUILayout.Button("Load And Apply (Ignore Cache)"))
            {
                TextNode = null;
                LoadAndApply(true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            if (TextNode != null)
            {
                EditorGUILayout.Space(5);
                if (TextNode.type == "TEXT")
                {
                    EditorGUILayout.LabelField($"Node: {TextNode.name}", EditorStyles.boldLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Node type is '{TextNode.type}', not a TEXT node.", MessageType.Warning);
                }
            }
        }

        private bool ParseNodeLink()
        {
            if (string.IsNullOrEmpty(Generator.NodeLink?.Trim())) return false;

            var parts = Generator.NodeLink.Replace("https://www.figma.com/design/", "").Split('?', '&', '/');
            FileKey = parts[0];
            NodeId = parts.First(item => item.Trim().StartsWith("node-id=")).Replace("node-id=", "");

            return !string.IsNullOrEmpty(FileKey) && !string.IsNullOrEmpty(NodeId);
        }

        private void LoadLocalSavedNode()
        {
            if (TextNode != null) return;
            if (!ParseNodeLink()) return;

            string localPath = Path.Combine(Application.persistentDataPath, $"{FileKey}_{NodeId}.json");
            if (File.Exists(localPath))
            {
                FigmaNodeParser nodeParser = new FigmaNodeParser();
                TextNode = nodeParser.ParseNode<Node>(System.IO.File.ReadAllText(localPath));
            }
        }

        private async void LoadAndApply(bool ignoreCache)
        {
            if (string.IsNullOrEmpty(Generator.FigmaToken))
            {
                Debug.LogError("No Figma Token");
                return;
            }

            if (!ParseNodeLink())
            {
                Debug.LogError("Node Link may not be valid, can not get file key or node id from it");
                return;
            }

            var nodeData = await Client.GetNodeDataAsync(FileKey, NodeId, Generator.FigmaToken);
            if (!string.IsNullOrEmpty(nodeData))
            {
                string localPath = Path.Combine(Application.persistentDataPath, $"{FileKey}_{NodeId}.json");
                File.WriteAllText(localPath, nodeData);

                FigmaNodeParser nodeParser = new FigmaNodeParser();
                TextNode = nodeParser.ParseNode<Node>(nodeData);

                if (TextNode != null && TextNode.type == "TEXT")
                {
                    ApplyMaterials(TextNode, ignoreCache);
                }

                Repaint();
            }
        }

        private void ApplyMaterials(Node textNode, bool ignoreCache)
        {
            var tmpText = Generator.GetComponent<TextMeshProUGUI>();
            var matInfo =
                TMPMaterialProvider.GetTMPMaterial(textNode, tmpText.font, Generator.MaterialSavePath, ignoreCache);

            var style = textNode.style;
            tmpText.fontSize = style.fontSize;
            tmpText.text = textNode.characters;

            tmpText.fontMaterial = matInfo.OutlineAndDropShadow ?? (matInfo.InnerShadow ?? tmpText.font.material);


            // alignment
            var verticalAlignment = style.textAlignVertical;
            var horizontalAlignment = style.textAlignHorizontal;
            int alignment = 0;
            alignment += (verticalAlignment == "TOP" ? 1 : 0) << 8;
            alignment += (verticalAlignment == "CENTER" ? 1 : 0) << 9;
            alignment += (verticalAlignment == "BOTTOM" ? 1 : 0) << 10;
            alignment += (horizontalAlignment == "LEFT" ? 1 : 0) << 0;
            alignment += (horizontalAlignment == "CENTER" ? 1 : 0) << 1;
            alignment += (horizontalAlignment == "RIGHT" ? 1 : 0) << 2;
            alignment += (horizontalAlignment == "JUSTIFIED" ? 1 : 0) << 3;
            tmpText.alignment = (TextAlignmentOptions)alignment;
            FontStyles fontStyle = 0;
            fontStyle |= (style.textDecoration == "UNDERLINE" ? FontStyles.Underline : 0);
            fontStyle |= (style.textDecoration == "STRIKETHROUGH" ? FontStyles.Strikethrough : 0);

            fontStyle |= (style.textCase == "UPPER" ? FontStyles.UpperCase : 0);
            fontStyle |= (style.textCase == "LOWER" ? FontStyles.LowerCase : 0);
            fontStyle |= (style.textCase == "SMALL_CAPS" ? FontStyles.SmallCaps : 0);
            tmpText.fontStyle = fontStyle;

            // color
            var fills = textNode.GetValidFills();
            if (fills.Count > 1)
                Debug.LogError(
                    $"[Figma Importer] Text node {textNode.name}({textNode.id} has multiple fill, which is not supported.");
            var fill = fills[0];
            switch (fills[0].renderType)
            {
                case Fill.FillRenderType.Color:
                    tmpText.color = textNode.opacity * fill.FillColor();
                    break;
                case Fill.FillRenderType.GRADIENT:
                    var preset = TMPColorGradientResolver.GetTextGradientColorPreset(fill, Generator.MaterialSavePath);
                    tmpText.enableVertexGradient = true;
                    tmpText.colorGradientPreset = preset;
                    break;
                default:
                    Debug.LogError(
                        $"[Figma Importer] do not support fill type {fills[0].renderType} in Text node {textNode.name}({textNode.id}");
                    break;
            }

            if (matInfo.OutlineAndDropShadow != null && matInfo.InnerShadow != null) // need to create extra gameobject for innershadow
            {
                // inner shadow 需要一个独立的材质
                var innerShadowGenerator = GameObject.Instantiate(Generator, Generator.transform);
                var innerShadowText = innerShadowGenerator.GetComponent<TextMeshProUGUI>();
                innerShadowText.gameObject.name = $"{Generator.name} innershadow";
                innerShadowText.fontMaterial = matInfo.InnerShadow;
                (innerShadowText.transform as RectTransform).anchorMin = Vector2.zero;
                (innerShadowText.transform as RectTransform).anchorMax = Vector2.one;
                (innerShadowText.transform as RectTransform).offsetMin = Vector2.zero;
                (innerShadowText.transform as RectTransform).offsetMax = Vector2.zero;
                DestroyImmediate(innerShadowGenerator);
            }
        }
    }
}
