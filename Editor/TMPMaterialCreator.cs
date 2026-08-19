using TMPro;
using UnityEngine;

namespace FigmaTMPStyler.Editor
{
    public class TMPMaterialCreator
    {
        public class OutlineInfo
        {
            public string StrokeAlign; // 当前仅支持 OUTSIDE 类型的 stroke
            public float Width;
            public UnityEngine.Color Color;
            public bool Valid => Width > 0 && Color != UnityEngine.Color.clear;

            public override string ToString()
            {
                return $"Width: {Width}, Color: {Color}, Align: {StrokeAlign}";
            }
        }

        public class ShadowInfo
        {
            public bool DropShadow; // true : drop; false : inner
            public Vector2 Offset;
            public float Blur;
            public float Spread;
            public float OutlineWidth;
            public UnityEngine.Color Color;

            public bool Valid => (Offset != Vector2.zero || Blur != 0) && Color != UnityEngine.Color.clear;

            public override string ToString()
            {
                return $"Drop: {DropShadow}, Offset: {Offset}, Blur: {Blur}, OutlineWidth: {OutlineWidth}, Color: {Color}";
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

            Material material = new Material(font.material);

            if (outline != null && outline.Valid)
            {
                float faceDilate = outline.Width / 2 / maxOutlineUnderCurrentFontSize;
                float outlineThickness = faceDilate;
                material.EnableKeyword("OUTLINE_ON");
                material.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineThickness);
                material.SetColor(ShaderUtilities.ID_OutlineColor, outline.Color);

                if (faceDilate > 1 || outlineThickness > 1)
                {
                    Debug.LogError($"Can not Create CORRECT Material outline effect (faceDilate->{faceDilate}, outlineThickness->{outlineThickness}) for outline->{outline}");
                }
            }
            else
            {
                material.DisableKeyword("OUTLINE_ON");
            }

            if (shadow != null && shadow.Valid)
            {
                material.EnableKeyword(shadow.DropShadow ? "UNDERLAY_ON" : "UNDERLAY_INNER");
                material.DisableKeyword(shadow.DropShadow ? "UNDERLAY_INNER" : "UNDERLAY_ON");

                if (!material.IsKeywordEnabled("OUTLINE_ON")) // set facedilate to make the cal more accurate
                {
                    float faceDilate = shadow.OutlineWidth / 2 / maxOutlineUnderCurrentFontSize;
                    material.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
                }

                ShaderUtilities.UpdateShaderRatios(material);
                float inputRatioC = material.GetFloat(ShaderUtilities.ID_ScaleRatio_C);
                float bestDiff = float.MaxValue;
                float bestRatioC = inputRatioC;
                for (int i = 0; i < 8; i++)
                {
                    SetShadowValues(material, shadow, inputRatioC, gradientScale, sizeScale);
                    ShaderUtilities.UpdateShaderRatios(material);
                    float resultRatioC = material.GetFloat(ShaderUtilities.ID_ScaleRatio_C);
                    float diff = Mathf.Abs((resultRatioC - inputRatioC) / inputRatioC);
                    if (diff < bestDiff)
                    {
                        bestRatioC = resultRatioC;
                        bestDiff = diff;
                    }
                    inputRatioC = resultRatioC;
                    if (diff < 0.05 || inputRatioC < 0.001f) break;
                }
                SetShadowValues(material, shadow, bestRatioC, gradientScale, sizeScale);
                ShaderUtilities.UpdateShaderRatios(material);
            }
            else
            {
                material.DisableKeyword("UNDERLAY_ON");
                material.DisableKeyword("UNDERLAY_INNER");
            }

            return material;
        }

        private void SetShadowValues(Material material, ShadowInfo shadow, float inputRatioC, float gradientScale, float sizeScale)
        {
            float maxShadowUnderCurrentFontSize = gradientScale * inputRatioC * sizeScale;
            // drop 和 inner 不一样
            Vector2 offset = shadow.Offset;

            float faceDilate = shadow.OutlineWidth / 2;
            if (shadow.DropShadow)
            {
                // 在  Drop 模式下，设置 ID_UnderlayDilate， 可以使阴影的起点与 figma 保持一致，还原更精确
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, faceDilate / maxShadowUnderCurrentFontSize); // 将 shadow 本体继续膨胀，以便与figma 对齐
            }
            else
            {
                // 在 inner 模式下，设置 ID_UnderlayDilate 无法改变阴影起点，阴影的起点在 facedilate 边缘上 
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0); 
                float magnitude = offset.magnitude;
                if (magnitude > 0.0001f)
                {
                    float targetMagnitude = Mathf.Max(0f, magnitude - faceDilate);
                    offset = offset * (targetMagnitude / magnitude);
                }
            }

            float shadowOffsetX = -Mathf.Sign(offset.x) * Mathf.Abs(offset.x) / maxShadowUnderCurrentFontSize;
            float shadowOffsetY = -Mathf.Sign(offset.y) * Mathf.Abs(offset.y) / maxShadowUnderCurrentFontSize;

            // figma 的 blur 是高斯卷积，tmp 的 softness 是 SDF 边缘的线性加宽，两者原理不同；
            // 这里只能对齐「宽度」（糊开的像素量），无法精确还原 figma 的模糊剖面。
            float shadowSoftness = shadow.Blur / maxShadowUnderCurrentFontSize;

            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffsetX);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffsetY);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shadowSoftness);
            
            material.SetColor(ShaderUtilities.ID_UnderlayColor, shadow.Color);
            if (!shadow.DropShadow)
            {
                material.SetColor(ShaderUtilities.ID_FaceColor, UnityEngine.Color.clear);
            }

            if (Mathf.Abs(shadowOffsetX) > 1 || Mathf.Abs(shadowOffsetY) > 1 || shadowSoftness > 1 ||
                shadowSoftness < 0)
            {
                Debug.LogError($"Can not Create CORRECT Material shadow effect (offset->({shadowOffsetX},{shadowOffsetY}), softness->{shadowSoftness}) for shadow->{shadow}, You may need adjust TMP Font");
            }
        }
    }


}
