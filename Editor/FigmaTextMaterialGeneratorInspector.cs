using System.Collections.Generic;
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
            var matInfo = TMPMaterialProvider.GetTMPMaterial(textNode, tmpText.font, Generator.MaterialSavePath, ignoreCache);

            var style = textNode.style;
            tmpText.fontSize = style.fontSize;
            tmpText.fontSizeMax = style.fontSize;
            tmpText.text = textNode.characters;

            tmpText.fontMaterial = matInfo.Materials[0];


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

            List<RectTransform> attachedTexts = new List<RectTransform>();
            for (int i = 1; i < matInfo.Materials.Count; i++)
            {
                var generator = GameObject.Instantiate(Generator);
                var text = generator.GetComponent<TextMeshProUGUI>();
                text.gameObject.name = $"{Generator.name} figma_attached";
                text.fontMaterial = matInfo.Materials[i];
                DestroyImmediate(generator);
                attachedTexts.Add(text.transform as RectTransform);
            }

            foreach (var text in attachedTexts)
            {
                text.SetParent(Generator.transform);
                text.localScale = Vector3.one;
                text.position = Generator.transform.position;
                text.anchorMin = Vector2.zero;
                text.anchorMax = Vector2.one;
                text.offsetMin = Vector2.zero;
                text.offsetMax = Vector2.zero;
            }
        }

        [MenuItem("Assets/Figma TMP Styler/Check Auto-generated TMP GameObject Under Selection")]
        private static void CheckAutoGeneratedTMPGameObject()
        {
            List<string> prefabPaths = CollectPrefabPathsFromSelection();
            if (prefabPaths.Count == 0)
            {
                Debug.LogWarning("[Figma TMP Styler] No prefab found under selection.");
                return;
            }

            int changedPrefabCount = 0;
            foreach (var prefabPath in prefabPaths)
            {
                GameObject contentRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    bool changed = FixAttachedTexts(contentRoot, prefabPath);
                    if (!changed) continue;

                    PrefabUtility.SaveAsPrefabAsset(contentRoot, prefabPath);
                    changedPrefabCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contentRoot);
                }
            }

            EditorUtility.DisplayDialog("Figma TMP Styler", changedPrefabCount > 0 ? $"{changedPrefabCount} Prefab Fixed, See detail log in console" : "All good!", "OK");
        }

        private static List<string> CollectPrefabPathsFromSelection()
        {
            var prefabPaths = new List<string>();
            foreach (var selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                    {
                        prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
                    }
                }
                else if (selected is GameObject && path.EndsWith(".prefab"))
                {
                    prefabPaths.Add(path);
                }
            }

            return prefabPaths.Distinct().ToList();
        }

        private static bool FixAttachedTexts(GameObject root, string prefabPath)
        {
            bool changed = false;
            var textComponents = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in textComponents)
            {
                Transform parent = text.transform;
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    if (!child.name.Contains("figma_attached")) continue;

                    var childText = child.GetComponent<TextMeshProUGUI>();
                    if (childText == null) continue;

                    var childRect = child as RectTransform;
                    if (childRect != null && !RectFullyOverlapsParent(childRect))
                    {
                        SetRectFullyOverlapParent(childRect);
                        Debug.Log($"[Figma TMP Styler]: Fix Overlay for {child.name} with its parent {parent.name} in prefab {prefabPath}");
                        changed = true;
                    }

                    if (childText.text != text.text)
                    {
                        childText.text = text.text;
                        Debug.Log($"[Figma TMP Styler]: Fix text for {child.name} with its parent {parent.name} in prefab {prefabPath}");
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private static bool RectFullyOverlapsParent(RectTransform rect)
        {
            return rect.anchorMin == Vector2.zero
                   && rect.anchorMax == Vector2.one
                   && rect.offsetMin == Vector2.zero
                   && rect.offsetMax == Vector2.zero
                   && rect.localScale == Vector3.one
                   && rect.localRotation == Quaternion.identity
                   && rect.localPosition == Vector3.zero;
        }

        private static void SetRectFullyOverlapParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.localPosition = Vector3.zero;
        }

    }
}
