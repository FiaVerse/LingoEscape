// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// Generates a <see cref="QuadBoundsDescriptor"/> for every quad provider
    /// </summary>
    [BurstCompile]
    internal struct QuadGenerationJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public NativeList<Length3.Calculated> LayoutProperties;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RenderIndex, UIBlock2DData> BlockData;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public ImageDataProvider ImageDataProvider;

        [ReadOnly]
        public NovaHashMap<DataStoreID, RotationSetSummary> RotationSets;
        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, SubQuadData> SubQuadData;

        private RotationSet rotationSet;

        public void Execute(int index)
        {
            if (!DirtyBatches.TryGetQuadProvider(ref RotationSets, ref index, out rotationSet, out DataStoreID batchRootID))
            {
                return;
            }

            VisualElementIndex visualElementIndex = rotationSet.QuadProviders[index];
            ref VisualElement visualElement = ref VisualElements[batchRootID].ElementAt(visualElementIndex);
            SubQuadData.ElementAt(visualElement.RenderIndex).Clear();
            ref RenderElement<BaseRenderInfo> baseInfo = ref BaseInfos.ElementAt(visualElement.DataStoreIndex);

            QuadBoundsDescriptor quadDescriptor = DoBlock(visualElementIndex, ref visualElement, ref baseInfo);
            rotationSet.BoundarySummary.Descriptors.ElementAt(index) = quadDescriptor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private QuadBoundsDescriptor DoBlock(VisualElementIndex visualElementIndex, ref VisualElement visualElement, ref RenderElement<BaseRenderInfo> baseInfo)
        {
            LayoutAccess.Calculated layoutProperties = LayoutAccess.Get(visualElement.DataStoreIndex, ref LayoutProperties);
            float2 size = layoutProperties.Size.Value.xy;

            ref UIBlock2DData data = ref BlockData.ElementAt(visualElement.RenderIndex);
            bool hasImage = ImageDataProvider.TryGetImageData(data.Image.ImageID, out ImageDescriptor imageDescriptor, out TextureDescriptor textureDescriptor);
            if (hasImage)
            {
                data.AdjustSizeForImage(ref size, imageDescriptor.AspectRatio);
            }

            float2 halfSize = Math.float2_Half * size;
            float halfMinBlockDimension = math.cmin(halfSize);

            ref float4x4 worldFromBlock = ref WorldFromLocalMatrices.ElementAt(visualElement.DataStoreIndex);

            float cornerRadius = data.GetCornerRadius(halfMinBlockDimension);
            float setFromBlockScale = Math.GetRoughScale(ref worldFromBlock) * Math.GetRoughScale(ref rotationSet.SetFromWorld);

            QuadBoundsDescriptor toRet = new QuadBoundsDescriptor()
            {
                RenderIndex = visualElement.RenderIndex,
                CornerRadius = setFromBlockScale * cornerRadius,
                VisualElementIndex = visualElementIndex,
                ZLayer = baseInfo.Val.ZIndex,
            };

            if (data.FillEnabled)
            {
                toRet.Flags |= QuadDescriptorFlags.BodyRenders;

                if (IsGuaranteedOpaque(ref data, hasImage, ref textureDescriptor))
                {
                    toRet.Flags |= QuadDescriptorFlags.BodyOccludes;
                }
            }


            float3 bl = Math.Transform(ref rotationSet.SetFromWorld, ref worldFromBlock, new float3(-halfSize, 0f));
            float3 tr = Math.Transform(ref rotationSet.SetFromWorld, ref worldFromBlock, new float3(halfSize, 0f));

            toRet.Bounds = new RotationSpaceBounds(bl.xy, tr.xy);

            if (!data.Border.Enabled)
            {
                return toRet;
            }

            toRet.Flags |= QuadDescriptorFlags.HasBorder;

            if (data.Border.Color.IsOpaque() && !data.RadialFill.EnabledAndNot360)
            {
                toRet.Flags |= QuadDescriptorFlags.BorderOccludes;
            }

            float borderWidth = data.Border.GetWidth(halfMinBlockDimension);
            data.Border.GetBorderRadii(cornerRadius, borderWidth, ref size, out float innerRadius, out float outerRadius);

            toRet.Border.BorderWidth = borderWidth * setFromBlockScale;
            toRet.Border.OuterRadius = outerRadius * setFromBlockScale;

            float2 borderHalfSize = Math.float2_Half * size;

            float3 blBorder = Math.Transform(ref rotationSet.SetFromWorld, ref worldFromBlock, new float3(-borderHalfSize, 0f));
            float3 trBorder = Math.Transform(ref rotationSet.SetFromWorld, ref worldFromBlock, new float3(borderHalfSize, 0f));
            toRet.Border.Bounds = new RotationSpaceBounds(blBorder.xy, trBorder.xy);

            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsGuaranteedOpaque(ref UIBlock2DData data, bool hasImage, ref TextureDescriptor textureDescriptor)
        {
            if (!data.Color.IsOpaque())
            {
                return false;
            }

            if (data.RadialFill.EnabledAndNot360)
            {
                return false;
            }

            if (data.Gradient.Enabled && !data.Gradient.Color.IsOpaque())
            {
                return false;
            }

            if (hasImage && textureDescriptor.HasAlphaChannel)
            {
                return false;
            }

            return true;
        }
    }
}

