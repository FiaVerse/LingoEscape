// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct WorldSpaceBoundsJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NovaHashMap<DataStoreIndex, int> DirtiedByRendering;
        [ReadOnly]
        public NativeList<HierarchyDependency> DirtyDependencies;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RenderIndex, TextBlockData> TextData;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<Length3.MinMax> LengthMinMaxes;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<AutoSize3> AutoSizes;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<Length3> LayoutLengths;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public NativeList<Length3.Calculated> LayoutProperties;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RenderIndex, UIBlock2DData> UIBlock2DData;
        [ReadOnly]
        public NovaHashMap<RenderIndex, ComputeBufferIndex> ShadowIndices;
        [ReadOnly]
        public ImageDataProvider ImageDataProvider;

        public BlockBounds Bounds;

        public void Execute(int index)
        {
            DataStoreIndex dataStoreIndex = index;

            if (!DirtiedByRendering.ContainsKey(dataStoreIndex) &&
                DirtyDependencies[dataStoreIndex] == HierarchyDependency.None)
            {
                // Not dirty
                return;
            }

            ref RenderElement<BaseRenderInfo> baseInfo = ref BaseInfos.ElementAt(dataStoreIndex);
            switch (baseInfo.Val.BlockType)
            {
                case BlockType.Text:
                    DoText(dataStoreIndex, ref baseInfo);
                    break;
                case BlockType.UIBlock2D:
                    DoUIBlock2D(dataStoreIndex, ref baseInfo);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoText(DataStoreIndex dataStoreIndex, ref RenderElement<BaseRenderInfo> baseInfo)
        {
            ref TextBlockData textNodeData = ref TextData.ElementAt(baseInfo.RenderIndex);
            LayoutAccess.Properties layout = LayoutAccess.Get(dataStoreIndex, ref LayoutLengths);
            layout.WrapMinMaxes(ref LengthMinMaxes);
            layout.WrapAutoSizes(ref AutoSizes);

            float2 offset = textNodeData.GetPositionalOffset(layout.AutoSize.Shrink.xy);
            AABB blockSpaceBounds = AABB.Translate2D(ref offset, ref textNodeData.TextBounds);
            ref float4x4 worldFromLocal = ref WorldFromLocalMatrices.ElementAt(dataStoreIndex);
            float4x4 cornersInWorldSpace = math.mul(worldFromLocal, blockSpaceBounds.GetCorners2D());
            Bounds.Block.ElementAt(dataStoreIndex).WorldSpaceCorners = cornersInWorldSpace;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoUIBlock2D(DataStoreIndex dataStoreIndex, ref RenderElement<BaseRenderInfo> baseInfo)
        {
            float2 size = GetSize(dataStoreIndex).xy;
            ref UIBlock2DData data = ref UIBlock2DData.ElementAt(baseInfo.RenderIndex);
            if (ImageDataProvider.TryGetImageData(data.Image.ImageID, out ImageDescriptor imageDescriptor))
            {
                data.AdjustSizeForImage(ref size, imageDescriptor.AspectRatio);
            }

            float2 halfSize = Math.float2_Half * size;

            float halfMinBlockDimension = math.cmin(halfSize.xy);
            float bodyCornerRadius = data.GetCornerRadius(halfMinBlockDimension);

            if (data.Border.Enabled)
            {
                float borderWidth = data.Border.GetWidth(halfMinBlockDimension);
                data.Border.ModifySizeForBorder(ref bodyCornerRadius, ref size, borderWidth);
                halfSize = Math.float2_Half * size;
            }

            AABB blockSpaceBounds = new AABB(-halfSize, halfSize);
            ref float4x4 worldFromLocal = ref WorldFromLocalMatrices.ElementAt(dataStoreIndex);
            float4x4 worldSpaceCorners = math.mul(worldFromLocal, blockSpaceBounds.GetCorners2D());
            Bounds.Block.ElementAt(dataStoreIndex).WorldSpaceCorners = worldSpaceCorners;

            if (!data.Shadow.HasOuterShadow)
            {
                return;
            }

            AccentBounds accentBounds = new AccentBounds();
            float2 offset = data.Shadow.GetOffset(ref size);

            float2 innerTouchPoint = halfSize.xy - Math.OneMinusSin45 * bodyCornerRadius;
            AABB inner = new AABB(-innerTouchPoint, innerTouchPoint);
            inner = AABB.Translate2D(ref offset, ref inner);
            accentBounds.Inner.WorldSpaceCorners = math.mul(worldFromLocal, inner.GetCorners2D());

            float sizeIncrease = math.csum(data.Shadow.GetWidths(halfMinBlockDimension));
            float2 outerHalfSize = halfSize.xy + sizeIncrease;
            AABB outer = new AABB(-outerHalfSize, outerHalfSize);
            outer = AABB.Translate2D(ref offset, ref outer);
            accentBounds.Outer.WorldSpaceCorners = math.mul(worldFromLocal, outer.GetCorners2D());

            Bounds.Shadow.ElementAt(ShadowIndices[baseInfo.RenderIndex]) = accentBounds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 GetSize(DataStoreIndex dataStoreIndex)
        {
            LayoutAccess.Calculated layoutProperties = LayoutAccess.Get(dataStoreIndex, ref LayoutProperties);
            return layoutProperties.Size.Value;
        }
    }
}

