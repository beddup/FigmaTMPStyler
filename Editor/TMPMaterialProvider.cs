using TMPro;
using UnityEditor;
using UnityEngine;
using System.IO;
using FigmaClient.Editor;


namespace FigmaTMPStyler.Editor
{
    public class TMPMaterialProvider
    {
        public class TMPMaterial
        {
            public TMP_FontAsset Font { get; private set; }
            public Material OutlineAndDropShadow;
            public Material InnerShadow;

            public TMPMaterial(TMP_FontAsset font)
            {
                Font = font;
            }

            public void ApplyNodeOutlineAndDropShadow(Node node)
            {
                TMPMaterialCreator.OutlineInfo outlineInfo = null;
                TMPMaterialCreator.ShadowInfo shadowInfo = null;
                var solidColorOutline = node.GetSolidColorOutlineFill();
                if (solidColorOutline != null)
                {
                    outlineInfo = new TMPMaterialCreator.OutlineInfo()
                        { Width = node.strokeWeight, Color = solidColorOutline.FillColor() };
                }

                var dropShadow = node.GetDropShadow();

                if (dropShadow != null)
                {
                    shadowInfo = new TMPMaterialCreator.ShadowInfo()
                    {
                        DropShadow = true, Offset = new Vector2(dropShadow.offset.x, dropShadow.offset.y),
                        Blur = dropShadow.radius, Spread = dropShadow.spread, Color = dropShadow.color.ToColor()
                    };
                }

                if (solidColorOutline != null || dropShadow != null)
                {
                    OutlineAndDropShadow =
                        new TMPMaterialCreator().CreateTMPMaterial(Font, node.style.fontSize, outlineInfo, shadowInfo);
                }
            }

            public void ApplyNodeInnerShadow(Node node)
            {
                TMPMaterialCreator.ShadowInfo shadowInfo = null;
                var innerShadow = node.GetInnerShadow();
                if (innerShadow != null)
                {
                    shadowInfo = new TMPMaterialCreator.ShadowInfo()
                    {
                        DropShadow = false, Offset = new Vector2(innerShadow.offset.x, innerShadow.offset.y),
                        Blur = innerShadow.radius, Spread = innerShadow.spread, Color = innerShadow.color.ToColor()
                    };
                    InnerShadow =
                        new TMPMaterialCreator().CreateTMPMaterial(Font, node.style.fontSize, null, shadowInfo);
                }
            }
        }

        public static TMPMaterial GetTMPMaterial(Node node, TMP_FontAsset tmpFont, string materialSaveFolder, bool ignoreCache)
        {
            if (tmpFont == null)
            {
                Debug.LogError("tmp font is null");
                return null;
            }

            TMPMaterial matInfo = new TMPMaterial(tmpFont);

            CheckTextNodeMaterial(node);

            if (node.GetSolidColorOutlineFill() != null || node.GetDropShadow() != null)
            {
                string matName = GetOutlineAndDropShadowMaterialName(node, tmpFont);
                var matInAssetDatabase = FindMaterial(matName);
                if (!ignoreCache && matInAssetDatabase != null)
                {
                    matInfo.OutlineAndDropShadow = matInAssetDatabase;
                }
                else
                {
                    matInfo.ApplyNodeOutlineAndDropShadow(node);
                    SaveMaterial(matInfo.OutlineAndDropShadow, matName, materialSaveFolder);
                }
            }

            if (node.GetInnerShadow() != null)
            {
                string matName = GetInnerShadowMaterialName(node, tmpFont);
                var matInAssetDatabase = FindMaterial(matName);
                if (!ignoreCache && matInAssetDatabase != null)
                {
                    matInfo.InnerShadow = matInAssetDatabase;
                }
                else
                {
                    matInfo.ApplyNodeInnerShadow(node);
                    SaveMaterial(matInfo.InnerShadow, matName, materialSaveFolder);
                }
            }

            return matInfo;
        }


        private static void CheckTextNodeMaterial(Node textNode)
        {
            // 不支持多个 outline 
            if (textNode.GetValidSolidColorOutlinesFills().Count > 1)
            {
                Debug.LogError(
                    $"node {textNode.name}(id:{textNode.id}) has more than one solid color outline, which is not supported");
            }

            // 不支持非纯色描边
            if (textNode.HasNonSolidColorOutlines())
            {
                Debug.LogError(
                    $"node {textNode.name}(id:{textNode.id}) has non-solid-color outline, which is not supported");
            }

            if (textNode.GetDropShadows().Count > 1)
            {
                Debug.LogError(
                    $"node {textNode.name}(id:{textNode.id}) has more than one dropshadow, which is not supported");
            }

            if (textNode.GetInnerShadows().Count > 1)
            {
                Debug.LogError(
                    $"node {textNode.name}(id:{textNode.id}) has more than one innershadow, which is not supported");
            }
        }

        private static Material FindMaterial(string matName)
        {
            var guids = AssetDatabase.FindAssets($"{matName} t:Material");
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            return null;
        }

        private static string GetOutlineAndDropShadowMaterialName(Node textNode, TMP_FontAsset font)
        {
            string matName = font.name;
            matName = $"{matName} Size_{textNode.style.fontSize.ToString("F1")}";
            var solidColorOutline = textNode.GetSolidColorOutlineFill();
            if (solidColorOutline != null)
            {
                matName = $"{matName} Outline_{textNode.strokeWeight.ToString("F1")}_{solidColorOutline.color.ToHexColor()}";
            }

            var dropShadow = textNode.GetDropShadow();
            if (dropShadow != null)
            {
                matName = $"{matName} DropShadow_X{dropShadow.offset.x.ToString("F1")}_Y{dropShadow.offset.y.ToString("F1")}_Blur{dropShadow.radius.ToString("F1")}_{dropShadow.color.ToHexColor()}";
            }

            return matName;
        }

        private static string GetInnerShadowMaterialName(Node textNode, TMP_FontAsset font)
        {
            string matName = font.name;
            var innerShadow = textNode.GetInnerShadow();
            if (innerShadow != null)
            {
                matName =
                    $"{matName} Size_{textNode.style.fontSize.ToString("F1")} InnerShadow_X{innerShadow.offset.x.ToString("F1")}_Y{innerShadow.offset.y.ToString("F1")}_Blur{innerShadow.radius.ToString("F1")}_{innerShadow.color.ToHexColor()}";
            }

            return matName;
        }


        private static void SaveMaterial(Material mat, string matName, string matFolder)
        {
            string assetFolder = Path.Combine(matFolder, "Materials");
            if (!Directory.Exists(assetFolder))
            {
                Directory.CreateDirectory(assetFolder);
            }

            AssetDatabase.CreateAsset(mat, Path.Combine(assetFolder, $"{matName}.mat"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}