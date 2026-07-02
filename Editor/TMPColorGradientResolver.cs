using TMPro;
using UnityEngine;
using UnityEditor;
using System.IO;
using FigmaClient;

namespace FigmaTMPStyler.Editor
{
    public class TMPColorGradientResolver
    {
        
        public static TMP_ColorGradient GetTextGradientColorPreset(Fill fill, string presetAssetFolder)
        {
            if (fill.type == "GRADIENT_LINEAR")
            {
                var startPos = fill.gradientHandlePositions[0];
                var endPos = fill.gradientHandlePositions[1];
                bool isVetical = startPos.x == endPos.x && startPos.y != endPos.y;
                var startColor = fill.gradientStops[0].color.ToColor();
                var endColor = fill.gradientStops[1].color.ToColor();

                string presetPrefix = isVetical ? "Vertical" : "Horizontal";
                string presetName = $"{presetPrefix}_{ColorUtility.ToHtmlStringRGBA(startColor)}-{ColorUtility.ToHtmlStringRGBA(endColor)}";
                string presetAssetPath = Path.Combine(presetAssetFolder, $"{presetName}.asset");
                
                TMP_ColorGradient gradientPreset = AssetDatabase.LoadAssetAtPath<TMP_ColorGradient>(presetAssetPath);

                if (gradientPreset == null)
                {
                    gradientPreset = ScriptableObject.CreateInstance<TMP_ColorGradient>();
                    gradientPreset.colorMode = isVetical ? ColorMode.VerticalGradient : ColorMode.HorizontalGradient;
                    gradientPreset.topLeft = startColor;
                    gradientPreset.topRight = isVetical ? startColor : endColor;
                    gradientPreset.bottomLeft = isVetical ? endColor : startColor;
                    gradientPreset.bottomRight = endColor;

                    AssetDatabase.CreateAsset(gradientPreset, presetAssetPath);
                    AssetDatabase.Refresh();
                }
                return gradientPreset;
            }
            Debug.LogError("Only support LINEAR GRADIENT");
            return null;

        }

    }
}
