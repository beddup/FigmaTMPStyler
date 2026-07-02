using TMPro;
using UnityEngine;

namespace Figma.TMPStyler.Editor
{
    public class TMPMaterialCreator
    {
        public class OutlineInfo
        {
            public float Width;
            public UnityEngine.Color Color;
            public bool Valid => Width > 0 && Color != UnityEngine.Color.clear;

            public override string ToString()
            {
                return $"Width: {Width}, Color: {Color}";
            }
        }

        public class ShadowInfo
        {
            public bool DropShadow; // true : drop; false : inner
            public Vector2 Offset;
            public float Blur;
            public float Spread;
            public UnityEngine.Color Color;

            public bool Valid => Offset != Vector2.zero && Color != UnityEngine.Color.clear;

            public override string ToString()
            {
                return $"Drop: {DropShadow}, Offset: {Offset}, Blur: {Blur}, Color: {Color}";
            }
        }

        public Material CreateTMPMaterial(TMP_FontAsset font, float textFontSize, OutlineInfo outline,
            ShadowInfo shadow)
        {
            // figma 默认是外描边，tmp 是 中间描边，为了达到外描边的效果
            // 需要先将 face 外扩描边宽度的一半
            float fontPointSize = font.faceInfo.pointSize;
            // float fontPadding = font.atlasPadding;

            float scaleRatioA = font.material.GetFloat(ShaderUtilities.ID_ScaleRatio_A);
            float gradientScale = font.material.GetFloat(ShaderUtilities.ID_GradientScale);

            float sizeScale = textFontSize / fontPointSize;
            
            float maxOutlineUnderCurrentFontSize = scaleRatioA * gradientScale * sizeScale;
            

            float outlineWidthPixel = outline != null && outline.Valid ? outline.Width : 0;
            float faceDilatePixel = outlineWidthPixel / 2;

            Material material = new Material(font.material);

            if (outline != null && outline.Valid)
            {
                float faceDilate = faceDilatePixel / maxOutlineUnderCurrentFontSize;
                float outlineThickness = faceDilate;
                material.EnableKeyword("OUTLINE_ON");
                material.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineThickness);
                material.SetColor(ShaderUtilities.ID_OutlineColor, outline.Color);
            }
            else
            {
                material.DisableKeyword("OUTLINE_ON");
            }

            if (shadow != null && shadow.Valid)
            {
                material.EnableKeyword(shadow.DropShadow ? "UNDERLAY_ON" : "UNDERLAY_INNER");

                float scaleRatioC = font.material.GetFloat(ShaderUtilities.ID_ScaleRatio_C);
                float maxShadowUnderCurrentFontSize = gradientScale * scaleRatioC * sizeScale;

                float shadowDilate = faceDilatePixel / maxShadowUnderCurrentFontSize;

                float shadowOffsetX = shadow.Offset.x == 0 ? 0
                    : -Mathf.Sign(shadow.Offset.x) * Mathf.Abs(shadow.Offset.x) / maxShadowUnderCurrentFontSize;
                float shadowOffsetY = shadow.Offset.y == 0 ? 0
                    : -Mathf.Sign(shadow.Offset.y) * Mathf.Abs(shadow.Offset.y) / maxShadowUnderCurrentFontSize;

                float shadowSoftness = shadow.Blur / maxShadowUnderCurrentFontSize;

                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, shadowDilate);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffsetX);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffsetY);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shadowSoftness);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, shadow.Color);
                if (!shadow.DropShadow)
                {
                    material.SetColor(ShaderUtilities.ID_FaceColor, UnityEngine.Color.clear);
                }

                // todo spread
            }
            else
            {
                material.DisableKeyword("UNDERLAY_ON");
                material.DisableKeyword("UNDERLAY_INNER");
            }

            return material;
        }
    }
}