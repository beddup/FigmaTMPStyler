using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using TMPro;
namespace FigmaTMPStyler.Editor
{
    /// <summary>
    /// Figma parameters parsed from a deterministic TMP material name, so they can be
    /// reused with a different fontSize without re-fetching from Figma.
    ///
    /// Material name formats:
    ///   Outline + DropShadow:
    ///     {fontName} Size_{fontSize} [Outline_{strokeWeight}_{hexColor}] DropShadow[_OutlineWidth{shadowOutlineWidth}]_X{offsetX}_Y{offsetY}_Blur{blur}_{hexColor}
    ///   InnerShadow:
    ///     {fontName} Size_{fontSize} InnerShadow[_OutlineWidth{shadowOutlineWidth}]_X{offsetX}_Y{offsetY}_Blur{blur}_{hexColor}
    /// </summary>
    public class RefreshTMPMaterialByName
    {
        
        [MenuItem("Assets/Figma TMP Styler/Sync Selected TMP Materials By Name")]
        private static void SynMatValueByName()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return;

            foreach (var selectedObject in Selection.objects)
            {
                Material tmpMat = selectedObject as Material;
                if (tmpMat != null)
                {
                    var matPath = AssetDatabase.GetAssetPath(tmpMat);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    var tmpMatName = System.IO.Path.GetFileNameWithoutExtension(matPath);
                    var parameters = Parse(tmpMatName);
                    if (parameters == null)
                    {
                        Debug.LogError($"Can not Parse parameter from material {matPath}");
                        continue;
                    }
                    var fontAsset = FindFontAsset(parameters.FontName);
                    if (fontAsset == null)
                    {
                        Debug.LogError($"Can not Find right TMP font for material {matPath}");
                        continue;
                    }
                    Debug.Log($"Parse Result from {matPath}:\n font->{fontAsset.name}\n Size->{parameters.FontSize}\n Outline->{parameters.Outline}\n Shadow->{parameters.Shadow}");
                    var newMaterial = new TMPMaterialCreator().CreateTMPMaterial(fontAsset, parameters.FontSize, parameters.Outline, parameters.Shadow);

                    if (newMaterial == null)
                    {
                        Debug.LogError($"can not sync mat value for material {tmpMatName}");
                        continue;
                    }

                    newMaterial.name = mat.name;
                    EditorUtility.CopySerialized(newMaterial, mat);
                    Object.DestroyImmediate(newMaterial);
                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        public string FontName;
        public float FontSize;
        public TMPMaterialCreator.OutlineInfo Outline;
        public TMPMaterialCreator.ShadowInfo DropShadow;
        public TMPMaterialCreator.ShadowInfo InnerShadow;
        public TMPMaterialCreator.ShadowInfo Shadow;

        public bool HasOutline => Outline != null;
        public bool HasDropShadow => DropShadow != null;
        public bool HasInnerShadow => InnerShadow != null;
        public bool HasShadow => Shadow != null;
        public bool IsValid => HasOutline || HasShadow;

        // Integer or decimal values are accepted (e.g. Size_46, Size_46.0, Outline_2, Outline_2.0, Blur2, Blur2.0).
        private static readonly Regex HeaderRegex = new Regex(
            @"^(.+)\s+Size_(\d+(?:\.\d+)?)(?:\s+(.*))?$");

        private static readonly Regex OutlineRegex = new Regex(
            @"^Outline_(\d+(?:\.\d+)?)_([0-9A-Fa-f]{6,8})$");

        private static readonly Regex DropShadowRegex = new Regex(
            @"^DropShadow(?:_OutlineWidth(\d+(?:\.\d+)?))?_X(-?\d+(?:\.\d+)?)_Y(-?\d+(?:\.\d+)?)_Blur(\d+(?:\.\d+)?)_([0-9A-Fa-f]{6,8})$");

        private static readonly Regex InnerShadowRegex = new Regex(
            @"^InnerShadow(?:_OutlineWidth(\d+(?:\.\d+)?))?_X(-?\d+(?:\.\d+)?)_Y(-?\d+(?:\.\d+)?)_Blur(\d+(?:\.\d+)?)_([0-9A-Fa-f]{6,8})$");
        
        private static RefreshTMPMaterialByName Parse(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;

            // Unity/TMP may append " (Instance)" when the material is an instance copy.
            // Strip it so the regex can match the deterministic name format.
            materialName = materialName.Replace(" (Instance)", "");

            var headerMatch = HeaderRegex.Match(materialName);
            if (!headerMatch.Success)
            {
                return null;
            }

            var result = new RefreshTMPMaterialByName
            {
                FontName = headerMatch.Groups[1].Value,
                FontSize = ParseFloat(headerMatch.Groups[2].Value)
            };
            string effects = headerMatch.Groups[3].Success ? headerMatch.Groups[3].Value : string.Empty;

            foreach (string part in effects.Split(' '))
            {
                ParseEffectPart(part, result);
            }

            result.Shadow = result.DropShadow ?? result.InnerShadow;

            if (result.Shadow != null && result.Shadow.OutlineWidth == 0 && result.Outline != null) //legacy fallback
            {
                result.Shadow.OutlineWidth = result.Outline.Width;
            }
            return result.IsValid ? result : null;
        }

        private static void ParseEffectPart(string part, RefreshTMPMaterialByName result)
        {
            if (string.IsNullOrWhiteSpace(part)) return;

            var outlineMatch = OutlineRegex.Match(part);
            if (outlineMatch.Success)
            {
                result.Outline = new TMPMaterialCreator.OutlineInfo
                {
                    Width = ParseFloat(outlineMatch.Groups[1].Value),
                    Color = HexToColor(outlineMatch.Groups[2].Value)
                };
                return;
            }

            var dropShadowMatch = DropShadowRegex.Match(part);
            if (dropShadowMatch.Success)
            {
                result.DropShadow = new TMPMaterialCreator.ShadowInfo
                {
                    DropShadow = true,
                    OutlineWidth = dropShadowMatch.Groups[1].Success ? ParseFloat(dropShadowMatch.Groups[1].Value) : 0,
                    Offset = new Vector2(ParseFloat(dropShadowMatch.Groups[2].Value),
                        ParseFloat(dropShadowMatch.Groups[3].Value)),
                    Blur = ParseFloat(dropShadowMatch.Groups[4].Value),
                    Color = HexToColor(dropShadowMatch.Groups[5].Value)
                };
                return;
            }

            var innerShadowMatch = InnerShadowRegex.Match(part);
            if (innerShadowMatch.Success)
            {
                result.InnerShadow = new TMPMaterialCreator.ShadowInfo
                {
                    DropShadow = false,
                    OutlineWidth = innerShadowMatch.Groups[1].Success ? ParseFloat(innerShadowMatch.Groups[1].Value) : 0,
                    Offset = new Vector2(ParseFloat(innerShadowMatch.Groups[2].Value),
                        ParseFloat(innerShadowMatch.Groups[3].Value)),
                    Blur = ParseFloat(innerShadowMatch.Groups[4].Value),
                    Color = HexToColor(innerShadowMatch.Groups[5].Value)
                };
            }
        }

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString($"#{hex}", out Color color))
                return color;
            return Color.white;
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }
        
        private static TMP_FontAsset FindFontAsset(string fontInfo)
        {
            string[] guids = AssetDatabase.FindAssets($"t:TMP_FontAsset");
            TMP_FontAsset font = null;
            foreach (string guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                string fontName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fontInfo.StartsWith(fontName))
                {
                    if (font != null)
                    {
                        Debug.LogError($"Find more than 1 TMp font for material {fontInfo}");
                        return null;
                    }
                    font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                }
            }

            return font;
        }
    }
}
