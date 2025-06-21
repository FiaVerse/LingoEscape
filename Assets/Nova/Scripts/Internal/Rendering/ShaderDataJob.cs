// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal unsafe struct ShaderDataJob : INovaJobParallelFor
    {
        public bool DirtyEverything;

        [ReadOnly]
        public NovaHashMap<DataStoreIndex, int> DirtiedByRendering;
        [ReadOnly]
        public NativeList<HierarchyDependency> DirtyDependencies;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [ReadOnly]
        public NativeList<Length3.Calculated> LayoutProperties;
        [ReadOnly]
        public TexturePackDataProvider PackDataProvider;
        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, TextBlockData> TextNodeData;
        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, UIBlock2DData> UIBlock2DData;
        [ReadOnly]
        public NativeList<RenderIndex, UIBlock3DData> UIBlock3DData;
        [ReadOnly]
        public ComputeBufferIndices ComputeBufferIndices;
        [NativeDisableParallelForRestriction]
        public NativeList<DataStoreIndex, Surface> SurfaceData;
        [ReadOnly]
        public NativeList<HierarchyElement> Hierarchy;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<Length3.MinMax> LengthMinMaxes;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<Length3> LayoutLengths;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<AutoSize3> AutoSizes;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public ImageDataProvider ImageDataProvider;
        [ReadOnly]
        public NovaHashMap<DataStoreID, byte> HiddenElements;

        [NativeDisableParallelForRestriction]
        public ComputeBufferAccess<UIBlock2DShaderData> UIBlock2DShaderData;
        [NativeDisableParallelForRestriction]
        public ComputeBufferAccess<PerInstanceDropShadowShaderData> ShadowInstanceShaderData;
        [NativeDisableParallelForRestriction]
        public ComputeBufferAccess<AllQuadsDropShadowShaderData> ShadowQuadShaderData;
        [NativeDisableParallelForRestriction]
        public ComputeBufferAccess<PerCharacterTextShaderData> TextPerVertShaderData;
        [NativeDisableParallelForRestriction]
        public ComputeBufferAccess<UIBlock3DShaderData> UIBlock3DShaderData;
        [NativeDisableParallelForRestriction]
        public ComputeBufferAccess<TransformAndLightingData> LightingShaderData;

        private DataStoreIndex dataStoreIndex;
        private DataStoreID dataStoreID;
        private bool dirtiedByRendering;

        public void Execute(int index)
        {
            dataStoreIndex = index;

            dirtiedByRendering = DirtiedByRendering.ContainsKey(dataStoreIndex);
            bool dirtiedByLayouts = DirtyDependencies[dataStoreIndex] >= HierarchyDependency.Self;
            if (!dirtiedByRendering && !dirtiedByLayouts && !DirtyEverything)
            {
                return;
            }

            dataStoreID = Hierarchy[dataStoreIndex].ID;

            RenderElement<BaseRenderInfo> baseInfo = BaseInfos[dataStoreIndex];

            if (!baseInfo.Val.Visible)
            {
                return;
            }

            if (NovaApplication.ConstIsEditor && HiddenElements.ContainsKey(dataStoreID))
            {
                return;
            }

            switch (baseInfo.Val.BlockType)
            {
                case BlockType.UIBlock2D:
                    DoUIBlock2D(ref baseInfo);
                    break;
                case BlockType.UIBlock3D:
                    DoUIBlock3D(ref baseInfo);
                    break;
                case BlockType.Text:
                    DoTextBlock(ref baseInfo);
                    break;
            }

            ref Surface surfaceData = ref SurfaceData.ElementAt(dataStoreIndex);
            if (surfaceData.HasShaderData)
            {
                ComputeBufferIndex lightingIndex = ComputeBufferIndices.TransformAndLighting[dataStoreIndex];
                ref LightingShaderDataUnion shaderData = ref LightingShaderData.ElementAt(lightingIndex).Lighting;
                switch (surfaceData.LightingModel)
                {
                    case LightingModel.Lambert:
                        break;
                    case LightingModel.BlinnPhong:
                        shaderData.BlinnPhong.Specular = surfaceData.Specular;
                        shaderData.BlinnPhong.Gloss = surfaceData.Gloss;
                        break;
                    case LightingModel.Standard:
                        shaderData.Standard.Smoothness = surfaceData.Smoothness;
                        shaderData.Standard.Metallic = surfaceData.Metallic;
                        break;
                    case LightingModel.StandardSpecular:
                        shaderData.StandardSpecular.SpecularColor.Set(ref surfaceData.SpecularColor);
                        shaderData.StandardSpecular.Smoothness = surfaceData.Smoothness;
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoUIBlock3D(ref RenderElement<BaseRenderInfo> baseInfo)
        {
            UIBlock3DData data = UIBlock3DData[baseInfo.RenderIndex];
            ref UIBlock3DShaderData shaderData = ref UIBlock3DShaderData.ElementAt(ComputeBufferIndices.UIBlock3D[baseInfo.RenderIndex]);

            float3 nodeSize = LayoutAccess.Get(dataStoreIndex, ref LayoutProperties).Size.Value;
            float minXY = 0.5f * math.cmin(nodeSize.xy);
            float clampedCornerRadius = data.GetCornerRadius(minXY);
            float clampedEdgeRadius = data.GetEdgeRadius(nodeSize.z, clampedCornerRadius);

            shaderData.Color.Set(ref data.Color);
            shaderData.CornerRadius = clampedCornerRadius;
            shaderData.EdgeRadius = clampedEdgeRadius;
            shaderData.Size = nodeSize;
            AssignTransformIndex(ref shaderData.TransformIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoTextBlock(ref RenderElement<BaseRenderInfo> baseInfo)
        {
            ref TextBlockData textNodeData = ref TextNodeData.ElementAt(baseInfo.RenderIndex);

            int quadCount = textNodeData.QuadCount;
            if (quadCount == 0)
            {
                return;
            }

            // Pretty ugly, but this is the same logic TMP uses to determine if the scale has changed.
            // But they don't fire any events whenever they make an update due to scale changing, so we
            // manually determine that ourselves
            float lossyYScale = math.length(WorldFromLocalMatrices.ElementAt(dataStoreIndex).c1.xyz);
            lossyYScale = math.isnan(lossyYScale) ? 0f : lossyYScale;

            if (dirtiedByRendering)
            {
                UpdateTextScale(ref baseInfo, 1f);
            }
            else
            {
                // The text data hasn't changed, but the scale has, so we need to update the uvs
                float delta = Math.Abs(lossyYScale / textNodeData.LossyYScale);
                delta = math.isnan(delta) ? 0f : delta;
                UpdateTextScale(ref baseInfo, delta);
                return;
            }

            NovaList<ComputeBufferIndex> shaderIndices = ComputeBufferIndices.Text[baseInfo.RenderIndex];
            if (quadCount != shaderIndices.Length)
            {
                Debug.LogError("Text vert count didn't match shader indices length");
            }

            LayoutAccess.Properties layout = LayoutAccess.Get(dataStoreIndex, ref LayoutLengths);
            layout.WrapMinMaxes(ref LengthMinMaxes);
            layout.WrapAutoSizes(ref AutoSizes);

            float3 positionalOffset = new float3(textNodeData.GetPositionalOffset(layout.AutoSize.Shrink.xy), 0f);
            int length = math.min(shaderIndices.Length, quadCount);
            for (int i = 0; i < length; ++i)
            {
                ref PerCharacterTextShaderData vertData = ref TextPerVertShaderData.ElementAt(shaderIndices[i]);
                vertData.TransformIndex = (uint)ComputeBufferIndices.TransformAndLighting[dataStoreIndex];
                textNodeData.SetCharShaderData(i, ref vertData, ref positionalOffset);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTextScale(ref RenderElement<BaseRenderInfo> baseInfo, float delta)
        {
            NovaList<ComputeBufferIndex> shaderIndices = ComputeBufferIndices.Text[baseInfo.RenderIndex];
            for (int i = 0; i < shaderIndices.Length; ++i)
            {
                ref PerCharacterTextShaderData vertData = ref TextPerVertShaderData.ElementAt(shaderIndices[i]);
                vertData.ApplyScaleDelta(delta);
            }
        }

        private void AssignTransformIndex(ref ShaderIndex dest)
        {
            dest = (uint)ComputeBufferIndices.TransformAndLighting[dataStoreIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoUIBlock2D(ref RenderElement<BaseRenderInfo> baseInfo)
        {
            ref UIBlock2DData data = ref UIBlock2DData.ElementAt(baseInfo.RenderIndex);
            ref UIBlock2DShaderData shaderData = ref UIBlock2DShaderData.ElementAt(ComputeBufferIndices.UIBlock2D[baseInfo.RenderIndex]);

            AssignTransformIndex(ref shaderData.TransformIndex);

            float2 nodeSize = LayoutAccess.Get(dataStoreIndex, ref LayoutProperties).Size.Value.xy;

            bool hasImage = ImageDataProvider.TryGetImageData(data.Image.ImageID, out ImageDescriptor imageDescriptor, out TextureDescriptor textureDescriptor);
            if (hasImage)
            {
                data.AdjustSizeForImage(ref nodeSize, imageDescriptor.AspectRatio);
            }

            float2 nodeHalfSize = Math.float2_Half * nodeSize;
            float halfMinBlockDimension = math.cmin(nodeHalfSize);
            float bodyCornerRadius = data.GetCornerRadius(halfMinBlockDimension);

            if (hasImage && data.Image.Mode == ImagePackMode.Packed && PackDataProvider.TryGetSlice(imageDescriptor.TextureID, out TexturePackSlice slice))
            {
                // We always want to set the slice, even if the pack only has a count of 1 since this
                // block may not get dirtied later when another image gets added to the pack
                shaderData.TexturePackSlice = slice.index;
            }

            // This needs to happen before the border size adjustment
            shaderData.CornerRadius = bodyCornerRadius;

            if (data.FillEnabled)
            {
                shaderData.PrimaryColor.Set(ref data.Color);
                if (data.Gradient.Enabled)
                {
                    shaderData.GradientColor.Set(ref data.Gradient.Color);
                }
                else
                {
                    shaderData.GradientColor = shaderData.PrimaryColor;
                }
            }
            else
            {
                shaderData.PrimaryColor = ShaderColor.Transparent;
                shaderData.GradientColor = ShaderColor.Transparent;
            }

            float2 gradientSize = data.Gradient.GetSize(ref nodeSize);
            float2 gradientCenter = data.Gradient.GetCenter(ref nodeSize);
            shaderData.GradientCenter = gradientCenter;
            shaderData.GradientSizeReciprocal = math.select(float2.zero, Math.float2_One / gradientSize, gradientSize > float2.zero);

            math.sincos(math.radians(data.Gradient.Rotation), out shaderData.GradientRotationSinCos.x, out shaderData.GradientRotationSinCos.y);

            shaderData.QuadSize = nodeSize;

            if (data.RadialFill.EnabledAndNot360)
            {
                shaderData.RadialFillCenter = data.RadialFill.GetCenter(ref nodeSize);

                float rotation = -data.RadialFill.Rotation;
                float fillAngle = data.RadialFill.FillAngle;
                if (fillAngle < 0f)
                {
                    rotation -= fillAngle;
                    fillAngle = -fillAngle;
                }
                shaderData.RadialFillRotation = math.radians(rotation);
                shaderData.RadialFillAngle = -math.radians(fillAngle);
            }

            if (data.Border.Enabled)
            {
                shaderData.BorderColor.Set(ref data.Border.Color);
                shaderData.BorderWidth = data.Border.GetWidth(halfMinBlockDimension);

                data.Border.ModifySizeForBorder(ref bodyCornerRadius, ref nodeSize, shaderData.BorderWidth);
            }

            float2 shadowWidths = data.Shadow.GetWidths(halfMinBlockDimension);
            if (data.Shadow.HasInnerShadow)
            {
                shaderData.ShadowColor.Set(ref data.Shadow.Color);
                shaderData.ShadowOffset = data.Shadow.GetOffset(ref nodeSize);
                shaderData.ShadowWidth = shadowWidths.x;
                shaderData.ShadowBlur = shadowWidths.y;
            }
            else
            {
                shaderData.ShadowColor = shaderData.PrimaryColor;
                shaderData.ShadowOffset = float2.zero;
                shaderData.ShadowWidth = 0;
                shaderData.ShadowBlur = 0;
            }

            if (data.Shadow.HasOuterShadow)
            {
                if (!ComputeBufferIndices.Shadow.TryGetValue(baseInfo.RenderIndex, out ComputeBufferIndex shadowShaderIndex))
                {
                    Debug.LogError("Failed to get shadow shader index");
                    return;
                }

                if (!ShadowQuadShaderData.TryGetPointerAt(shadowShaderIndex, out AllQuadsDropShadowShaderData* perQuadShaderDataPtr))
                {
                    Debug.LogError("Failed to get shadow shader pointer");
                    return;
                }

                ref PerInstanceDropShadowShaderData perInstanceShadowData = ref ShadowInstanceShaderData.ElementAt(shadowShaderIndex);
                perInstanceShadowData = new PerInstanceDropShadowShaderData()
                {
                    Offset = data.Shadow.GetOffset(ref nodeSize),
                    BlockClipRadius = bodyCornerRadius,
                    HalfBlockQuadSize = Math.float2_Half * nodeSize,
                    Width = shadowWidths.x,
                    Blur = shadowWidths.y,
                    // We only want to soften the inner edges of the drop shadow when the body block
                    // isn't rendering, otherwise it results in a small gap between the body and shadow
                    EdgeSoftenMask = data.FillEnabled ? 0f : 1f,
                };
                perInstanceShadowData.Color.Set(ref data.Shadow.Color);

                AssignTransformIndex(ref perInstanceShadowData.TransformIndex);

                perInstanceShadowData.RadialFillCenter = shaderData.RadialFillCenter;
                perInstanceShadowData.RadialFillRotation = shaderData.RadialFillRotation;
                perInstanceShadowData.RadialFillAngle = shaderData.RadialFillAngle;

                DoOuterShadow((PerQuadDropShadowShaderData*)perQuadShaderDataPtr, ref data, ref nodeSize, ref shadowWidths, bodyCornerRadius);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoOuterShadow(PerQuadDropShadowShaderData* dropShadowData, ref UIBlock2DData data, ref float2 nodeSize, ref float2 shadowWidths, float bodyCornerRadius)
        {
            float2 nodeHalfSize = Math.float2_Half * nodeSize;

            float4 bodyEdgeXPositions = new float4(nodeHalfSize.xx * PosNeg, float2.zero);
            float4 bodyEdgeYPositions = new float4(float2.zero, nodeHalfSize.yy * PosNeg);
            float2 sizeInsideBodyCornerCircles = nodeSize - 2 * bodyCornerRadius;
            float2 bodyCornerCirclePosition = nodeHalfSize - bodyCornerRadius;

            float totalShadowWidth = math.csum(shadowWidths);
            float dropShadowRadius = bodyCornerRadius + totalShadowWidth;

            float2 dropShadowOffset = data.Shadow.GetOffset(ref nodeSize);

            float leftRightEdgeHeightReduction = Math.Abs(dropShadowOffset.y);
            // First do the edges. The edge structures are
            // Right, Left, Top, Bottom
            float4 dropShadowEdgeWidths = new float4(totalShadowWidth + PosNeg * dropShadowOffset.x, sizeInsideBodyCornerCircles.xx - Math.Abs(dropShadowOffset.x));
            dropShadowEdgeWidths = dropShadowEdgeWidths.ClampPositive();
            float4 dropShadowEdgeHeights = new float4(sizeInsideBodyCornerCircles.y - leftRightEdgeHeightReduction, totalShadowWidth + PosNeg * dropShadowOffset.y);
            dropShadowEdgeHeights = dropShadowEdgeHeights.ClampPositive();

            float4 dropShadowEdgeXPositions = new float4(bodyEdgeXPositions.xy + HalfPosNeg * dropShadowEdgeWidths.xy, .5f * dropShadowOffset.x);
            float4 dropShadowEdgeYPositions = new float4(.5f * dropShadowOffset.y, bodyEdgeYPositions.zw + HalfPosNeg * dropShadowEdgeHeights.zw);

            float2 absDropShadowOffset = Math.Abs(dropShadowOffset);
            float2 dropShadowOffsetSign = math.sign(dropShadowOffset);

            // Corners: TR, TL, BR, BL
            bool4 xOffsetMatchesCornerSign = dropShadowOffsetSign.x == PosNegPosNeg;
            bool4 yOffsetMatchesCornerSign = dropShadowOffsetSign.y == PosPosNegNeg;

            // If the offset is larger than the bodies size inside circles, we need to adjust the size increase/positional offset
            // to account for this
            float2 offsetPastSizeInsizeBodyCorners = (absDropShadowOffset - sizeInsideBodyCornerCircles).ClampPositive();

            // Only offset the corners being shifted towards the center
            float4 dropShadowCornerHorizontalOffset = math.select(dropShadowOffset.x, offsetPastSizeInsizeBodyCorners.x * PosNegPosNeg, xOffsetMatchesCornerSign);
            float4 dropShadowCornerWidthIncrease = math.select(float4.zero, absDropShadowOffset.x - offsetPastSizeInsizeBodyCorners.x, xOffsetMatchesCornerSign);

            float4 dropShadowCornerVerticalOffset = math.select(dropShadowOffset.y, offsetPastSizeInsizeBodyCorners.y * PosPosNegNeg, yOffsetMatchesCornerSign);
            float4 dropShadowCornerHeightIncrease = math.select(float4.zero, absDropShadowOffset.y - offsetPastSizeInsizeBodyCorners.y, yOffsetMatchesCornerSign);

            float4 dropShadowRadius4 = new float4(dropShadowRadius);
            float4 dropShadowCornerWidths = dropShadowRadius4 + dropShadowCornerWidthIncrease;
            float4 dropShadowCornerHeights = dropShadowRadius4 + dropShadowCornerHeightIncrease;

            float4 dropShadowCornerXPositions = PosNegPosNeg * (bodyCornerCirclePosition.xxxx + Math.float4_Half * dropShadowCornerWidths) + dropShadowCornerHorizontalOffset;
            float4 dropShadowCornerYPositions = PosPosNegNeg * (bodyCornerCirclePosition.yyyy + Math.float4_Half * dropShadowCornerHeights) + dropShadowCornerVerticalOffset;

            // LR Edges
            for (int i = 0; i < 2; ++i)
            {
                dropShadowData->PositionInNode = new float2(dropShadowCornerXPositions[i], dropShadowEdgeYPositions[i]);
                dropShadowData->QuadSize = new float2(dropShadowCornerWidths[i], dropShadowEdgeHeights[i]);
                dropShadowData += 1;
            }

            // TB Edges
            for (int i = 2; i < 4; ++i)
            {
                // Subtract 1 here because corners are 
                dropShadowData->PositionInNode = new float2(dropShadowEdgeXPositions[i], dropShadowCornerYPositions[i - 1]);
                dropShadowData->QuadSize = new float2(dropShadowEdgeWidths[i], dropShadowCornerHeights[i - 1]);
                dropShadowData += 1;
            }

            // Corners
            for (int i = 0; i < 4; ++i)
            {
                dropShadowData->PositionInNode = new float2(dropShadowCornerXPositions[i], dropShadowCornerYPositions[i]);
                dropShadowData->QuadSize = new float2(dropShadowCornerWidths[i], dropShadowCornerHeights[i]);
                dropShadowData += 1;
            }
        }

        private static readonly float2 PosNeg = new float2(1f, -1f);
        private static readonly float2 HalfPosNeg = Math.float2_Half * PosNeg;
        private static readonly float4 PosNegPosNeg = new float4(PosNeg, PosNeg);
        private static readonly float4 PosPosNegNeg = new float4(1, 1, -1, -1);
    }
}

