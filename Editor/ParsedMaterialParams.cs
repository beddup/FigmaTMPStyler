using System.Text.RegularExpressions;
using UnityEngine;

namespace FigmaTMPStyler.Editor
{
    /// <summary>
    /// Figma parameters parsed from a deterministic TMP material name, so they can be
    /// reused with a different fontSize without re-fetching from Figma.
    ///
    /// Material name formats:
    ///   Outline + DropShadow:
    ///     {fontName} Size_{fontSize} Outline_{strokeWeight}_{hexColor} DropShadow_X{offsetX}_Y{offsetY}_Blur{blur}_{hexColor}
    ///   InnerShadow:
    ///     {fontName} Size_{fontSize} InnerShadow_X{offsetX}_Y{offsetY}_Blur{blur}_{hexColor}
    /// </summary>
    public class ParsedMaterialParams
    {
        public string FontName;
        public TMPMaterialCreator.OutlineInfo Outline;
        public TMPMaterialCreator.ShadowInfo Shadow;

        public bool HasOutline => Outline != null && Outline.Valid;
        public bool HasShadow => Shadow != null && Shadow.Valid;
        public bool IsValid => HasOutline || HasShadow;

        // Integer or decimal values are accepted (e.g. Size_46, Size_46.0, Outline_2, Outline_2.0, Blur2, Blur2.0).
        private static readonly Regex OutlineShadowRegex = new Regex(
            @"^(.+?) Size_(\d+(?:\.\d+)?)(?: Outline_(\d+(?:\.\d+)?)_([0-9A-Fa-f]{6,8}))?(?: DropShadow_X(-?\d+(?:\.\d+)?)_Y(-?\d+(?:\.\d+)?)_Blur(\d+(?:\.\d+)?)_([0-9A-Fa-f]{6,8}))?$");

        private static readonly Regex InnerShadowRegex = new Regex(
            @"^(.+?) Size_(\d+(?:\.\d+)?) InnerShadow_X(-?\d+(?:\.\d+)?)_Y(-?\d+(?:\.\d+)?)_Blur(\d+(?:\.\d+)?)_([0-9A-Fa-f]{6,8})$");

        public static ParsedMaterialParams Parse(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;

            // Unity/TMP may append " (Instance)" when the material is an instance copy.
            // Strip it so the regex can match the deterministic name format.
            materialName = materialName.Replace(" (Instance)", "");

            var result = new ParsedMaterialParams();

            // Try outline + drop-shadow pattern first
            var match = OutlineShadowRegex.Match(materialName);
            if (match.Success)
            {
                result.FontName = match.Groups[1].Value;

                // Outline: strokeWeight (3), hexColor (4)
                if (match.Groups[3].Success && match.Groups[4].Success)
                {
                    result.Outline = new TMPMaterialCreator.OutlineInfo
                    {
                        Width = float.Parse(match.Groups[3].Value),
                        Color = HexToColor(match.Groups[4].Value)
                    };
                }

                // DropShadow: offsetX (5), offsetY (6), blur (7), hexColor (8)
                if (match.Groups[5].Success && match.Groups[6].Success &&
                    match.Groups[7].Success && match.Groups[8].Success)
                {
                    result.Shadow = new TMPMaterialCreator.ShadowInfo
                    {
                        DropShadow = true,
                        Offset = new Vector2(float.Parse(match.Groups[5].Value), float.Parse(match.Groups[6].Value)),
                        Blur = float.Parse(match.Groups[7].Value),
                        Color = HexToColor(match.Groups[8].Value)
                    };
                }

                return result;
            }

            // Try inner-shadow pattern
            match = InnerShadowRegex.Match(materialName);
            if (match.Success)
            {
                result.FontName = match.Groups[1].Value;
                result.Shadow = new TMPMaterialCreator.ShadowInfo
                {
                    DropShadow = false,
                    Offset = new Vector2(float.Parse(match.Groups[3].Value), float.Parse(match.Groups[4].Value)),
                    Blur = float.Parse(match.Groups[5].Value),
                    Color = HexToColor(match.Groups[6].Value)
                };
                return result;
            }

            return null;
        }

        /// <summary>
        /// Build a deterministic material name from the parsed Figma parameters and the
        /// given fontSize. This is the inverse of <see cref="Parse"/>.
        /// </summary>
        public string BuildMaterialName(float fontSize)
        {
            if (!HasShadow || Shadow.DropShadow)
            {
                string matName = $"{FontName} Size_{fontSize.ToString("F1")}";
                if (HasOutline)
                    matName = $"{matName} Outline_{Outline.Width.ToString("F1")}_{ColorUtility.ToHtmlStringRGBA(Outline.Color)}";
                if (HasShadow)
                    matName = $"{matName} DropShadow_X{Shadow.Offset.x.ToString("F1")}_Y{Shadow.Offset.y.ToString("F1")}_Blur{Shadow.Blur.ToString("F1")}_{ColorUtility.ToHtmlStringRGBA(Shadow.Color)}";
                return matName;
            }
            else
            {
                return $"{FontName} Size_{fontSize.ToString("F1")} InnerShadow_X{Shadow.Offset.x.ToString("F1")}_Y{Shadow.Offset.y.ToString("F1")}_Blur{Shadow.Blur.ToString("F1")}_{ColorUtility.ToHtmlStringRGBA(Shadow.Color)}";
            }
        }

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString($"#{hex}", out Color color))
                return color;
            return Color.white;
        }
    }
}
