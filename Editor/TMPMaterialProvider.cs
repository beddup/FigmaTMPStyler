using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Globalization;
using FigmaClient.Editor;
using Object = UnityEngine.Object;


namespace FigmaTMPStyler.Editor
{
    public class TMPMaterialProvider
    {
        public class TMPMaterialFeature
        {
            public TMPMaterialCreator.ShadowInfo DropShadow { get; private set; }
            public TMPMaterialCreator.ShadowInfo InnerShadow  { get; private set; }
            public TMPMaterialCreator.OutlineInfo Outline  { get; private set; }
            public bool Face  { get; private set; }

            public bool TrySetDropShadow(TMPMaterialCreator.ShadowInfo dropShadow)
            {
                if (DropShadow != null || InnerShadow != null) return false;
                DropShadow = dropShadow;
                return true;
            }
            public bool TrySetInnerShadow(TMPMaterialCreator.ShadowInfo innerShadow)
            {
                if (DropShadow != null || InnerShadow != null || Face) return false;
                InnerShadow = innerShadow;
                return true;
            }
            public bool TrySetOutline(TMPMaterialCreator.OutlineInfo outline)
            {
                if (Outline != null) return false;
                Outline = outline;
                return true;
            }
            public bool TrySetFace()
            {
                if (InnerShadow != null) return false;
                Face = true;
                return true;
            }
        }
        
        public class TMPMaterial
        {
            public TMP_FontAsset Font { get; private set; }
            public List<Material> Materials;

            public TMPMaterial(TMP_FontAsset font)
            {
                Font = font;
                Materials = new List<Material>();
            }

            public List<TMPMaterialFeature> ParseMaterials(Node node)
            {
                TMPMaterialCreator.OutlineInfo outlineInfo = null;
                
                var solidColorOutline = node.GetSolidColorOutlineFill();
                if (solidColorOutline != null)
                {
                    outlineInfo = new TMPMaterialCreator.OutlineInfo() { Width = node.strokeWeight, Color = solidColorOutline.FillColor(), StrokeAlign = node.strokeAlign};
                }
                else if (node.strokeWeight > 0)
                {
                    foreach (var stroke in node.strokes)
                    {
                        if (stroke.visible && stroke.opacity > 0)
                        {
                            outlineInfo = new TMPMaterialCreator.OutlineInfo() { Width = node.strokeWeight, Color = Color.black, StrokeAlign = node.strokeAlign};
                            break;
                        }
                    }
                }

                List<TMPMaterialFeature> materials = new List<TMPMaterialFeature>();
                TMPMaterialFeature currentMaterial = new TMPMaterialFeature();
                materials.Add(currentMaterial);
                
                var dropShadowEffects = node.GetDropShadows();
                if (dropShadowEffects != null && dropShadowEffects.Count > 0)
                {
                    foreach (var effect in dropShadowEffects)
                    {
                        var dropShadowInfo = new TMPMaterialCreator.ShadowInfo()
                        {
                            DropShadow = true, Offset = new Vector2(effect.offset.x, effect.offset.y),
                            Blur = effect.radius, Spread = effect.spread, Color = effect.color.ToColor(),
                            OutlineWidth = outlineInfo != null ? outlineInfo.Width : 0
                        };
                        
                        if (!currentMaterial.TrySetDropShadow(dropShadowInfo))
                        {
                            currentMaterial = new TMPMaterialFeature();
                            currentMaterial.TrySetDropShadow(dropShadowInfo);
                            materials.Add(currentMaterial);
                        }
                    }
                }

                if (!currentMaterial.TrySetFace())
                {
                    currentMaterial = new TMPMaterialFeature();
                    currentMaterial.TrySetFace();
                    materials.Add(currentMaterial);
                }
                
                var innerShadowEffects = node.GetInnerShadows();
                if (innerShadowEffects != null && innerShadowEffects.Count > 0)
                {
                    foreach (var effect in innerShadowEffects)
                    {
                        var innerShadowInfo = new TMPMaterialCreator.ShadowInfo()
                        {
                            DropShadow = false, Offset = new Vector2(effect.offset.x, effect.offset.y),
                            Blur = effect.radius, Spread = effect.spread, Color = effect.color.ToColor(),
                            OutlineWidth = outlineInfo != null ? outlineInfo.Width : 0
                        };
                        if (!currentMaterial.TrySetInnerShadow(innerShadowInfo))
                        {
                            currentMaterial = new TMPMaterialFeature();
                            currentMaterial.TrySetInnerShadow(innerShadowInfo);
                            materials.Add(currentMaterial);
                        }
                    }
                }
                if (!currentMaterial.TrySetOutline(outlineInfo))
                {
                    currentMaterial = new TMPMaterialFeature();
                    currentMaterial.TrySetOutline(outlineInfo);
                    materials.Add(currentMaterial);
                }

                return materials;
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

            var materialInfoList = matInfo.ParseMaterials(node);

            foreach (var materialFeature in materialInfoList)
            {
                var matName = GetMaterialName(materialFeature, node, tmpFont);
                var matInAssetDatabase = FindMaterial(matName);
                if (!ignoreCache && matInAssetDatabase != null)
                {
                    matInfo.Materials.Add(matInAssetDatabase);
                }
                else
                {
                    TMPMaterialCreator.ShadowInfo shadow = materialFeature.DropShadow ?? materialFeature.InnerShadow;
                    TMPMaterialCreator.OutlineInfo outline = materialFeature.Outline;
                    if (shadow == null && outline == null)
                    {
                        matInfo.Materials.Add(tmpFont.material);
                    }
                    else
                    {
                        var newMat = new TMPMaterialCreator().CreateTMPMaterial(tmpFont, node.style.fontSize, outline, shadow);
                        var matPath = SaveMaterial(newMat, matName, materialSaveFolder);
                        matInfo.Materials.Add(AssetDatabase.LoadAssetAtPath<Material>(matPath));
                    }
                }
            }
            return matInfo;
        }


        private static void CheckTextNodeMaterial(Node textNode)
        {
            // 不支持多个 outline 
            if (textNode.GetValidSolidColorOutlinesFills().Count > 1)
            {
                Debug.LogError($"node {textNode.name}(id:{textNode.id}) has more than one solid color outline, which is not supported");
            }

            // 不支持非纯色描边
            if (textNode.HasNonSolidColorOutlines())
            {
                Debug.LogError($"node {textNode.name}(id:{textNode.id}) has non-solid-color outline, which is not supported");
            }

            if (!string.IsNullOrEmpty(textNode.strokeAlign) && !string.Equals(textNode.strokeAlign, "outside", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.LogError($"node {textNode.name}(id:{textNode.id}) has {textNode.strokeAlign} outline, which is not supported, only 'outside' outline supported");
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

        private static string GetMaterialName(TMPMaterialFeature materialFeature, Node textNode, TMP_FontAsset font)
        {
            string matName = font.name;
            matName = $"{matName} Size_{FormatFloat(textNode.style.fontSize)}";
            var outline = materialFeature.Outline; 
            if (outline != null)
            {
                matName = $"{matName} Outline_{FormatFloat(outline.Width)}_{ColorUtility.ToHtmlStringRGBA(outline.Color)}";
            }

            var dropshadow = materialFeature.DropShadow; 
            if (dropshadow != null)
            {
                matName = $"{matName} DropShadow_OutlineWidth{FormatFloat(dropshadow.OutlineWidth)}_X{FormatFloat(dropshadow.Offset.x)}_Y{FormatFloat(dropshadow.Offset.y)}_Blur{FormatFloat(dropshadow.Blur)}_{ColorUtility.ToHtmlStringRGBA(dropshadow.Color)}";
            }
            
            var innershadow = materialFeature.InnerShadow; 
            if (innershadow != null)
            {
                matName = $"{matName} InnerShadow_OutlineWidth{FormatFloat(innershadow.OutlineWidth)}_X{FormatFloat(innershadow.Offset.x)}_Y{FormatFloat(innershadow.Offset.y)}_Blur{FormatFloat(innershadow.Blur)}_{ColorUtility.ToHtmlStringRGBA(innershadow.Color)}";
            }
            return matName;
        }

        private static string SaveMaterial(Material mat, string matName, string matFolder)
        {
            string assetFolder = Path.Combine(matFolder, "Materials");
            if (!Directory.Exists(assetFolder))
            {
                Directory.CreateDirectory(assetFolder);
            }

            mat.name = matName;

            var path = Path.Combine(assetFolder, $"{matName}.mat");
            var oldMat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (oldMat != null)
            {
                EditorUtility.CopySerialized(mat, oldMat);
                EditorUtility.SetDirty(oldMat);
            }
            else
            {
                AssetDatabase.CreateAsset(mat, path);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            return path;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
