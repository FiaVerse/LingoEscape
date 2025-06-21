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
    [BurstCompile]
    internal struct CoplanarSpaceBoundsJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<CoplanarSetID, CoplanarSet>> CoplanarSets;
        [ReadOnly]
        public NativeList<DataStoreIndex, CoplanarSetID> CoplanarSetIDs;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> LocalFromWorldMatrices;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [ReadOnly]
        public NativeList<VisualModifierID, ClipMaskInfo> ClipMaskData;
        [ReadOnly]
        public NovaHashMap<VisualModifierID, AABB> VisualModifierClipBounds;
        [ReadOnly]
        public NativeList<DataStoreIndex, VisualModifierID> VisualModifierIDs;
        [ReadOnly]
        public NovaHashMap<RenderIndex, ComputeBufferIndex> ShadowIndices;
        [ReadOnly]
        public NativeList<VisualModifierID, DataStoreID> ModifierToBlockID;
        [ReadOnly]
        public NativeList<Length3.Calculated> LayoutProperties;

        public BlockBounds Bounds;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Execute(int index)
        {
            if (!VisualElementUtilities.TryGetIndex(index, ref DirtyBatches, ref VisualElements, out DataStoreID batchRootID, out int indexIntoBatch))
            {
                return;
            }

            VisualElementIndex visualElementIndex = indexIntoBatch;
            ref VisualElement visualElement = ref VisualElements[batchRootID].ElementAt(visualElementIndex);

            switch (visualElement.Type)
            {
                case VisualType.UIBlock3D:
                {
                    LayoutAccess.Calculated layoutProperties = LayoutAccess.Get(visualElement.DataStoreIndex, ref LayoutProperties);
                    float3 halfSize = Math.float3_Half * layoutProperties.Size.Value;
                    AABB blockSpaceBounds = new AABB(-halfSize, halfSize);

                    ref float4x4 coplanarSetFromWorld = ref GetCoplanarSetFromWorld(ref batchRootID, ref visualElement);
                    ref float4x4 worldFromLocal = ref WorldFromLocalMatrices.ElementAt(visualElement.DataStoreIndex);
                    AABB coplanarSpaceBounds = AABB.Transform3D(ref coplanarSetFromWorld, ref worldFromLocal, ref blockSpaceBounds);
                    Bounds.Block.ElementAt(visualElement.DataStoreIndex).CoplanarSpaceBounds = coplanarSpaceBounds;
                    break;
                }
                case VisualType.DropShadow:
                {
                    ref AccentBounds bounds = ref Bounds.Shadow.ElementAt(ShadowIndices[visualElement.RenderIndex]);
                    ClampForClipSpace(ref visualElement, ref bounds.Inner, ref batchRootID);
                    if (!ClampForClipSpace(ref visualElement, ref bounds.Outer, ref batchRootID))
                    {
                        visualElement.SkipRendering = true;
                    }
                    break;
                }
                default:
                {
                    ref RenderBounds bounds = ref Bounds.Block.ElementAt(visualElement.DataStoreIndex);
                    if (!ClampForClipSpace(ref visualElement, ref bounds, ref batchRootID))
                    {
                        visualElement.SkipRendering = true;
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Returns whether or not should render
        /// </summary>
        /// <param name="visualElement"></param>
        /// <param name="worldSpaceCorners"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ClampForClipSpace(ref VisualElement visualElement, ref RenderBounds bounds, ref DataStoreID batchRootID)
        {
            bool toRet = true;
            VisualModifierID visualModifierID = VisualModifierIDs[visualElement.DataStoreIndex];
            if (visualModifierID.IsValid && ClipMaskData[visualModifierID].Clip)
            {
                DataStoreIndex visualModifierDataStoreIndex = DataStoreIDToDataStoreIndex[ModifierToBlockID[visualModifierID]];
                ref float4x4 modifierFromWorld = ref LocalFromWorldMatrices.ElementAt(visualModifierDataStoreIndex);
                float4x4 cornersInModifierSpace = math.mul(modifierFromWorld, bounds.WorldSpaceCorners);
                AABB boundsInModifierSpace = new AABB(ref cornersInModifierSpace);
                AABB clipBounds = VisualModifierClipBounds[visualModifierID];

                if (!clipBounds.Overlaps2D(ref boundsInModifierSpace))
                {
                    toRet = false;
                }

                boundsInModifierSpace.Clamp2D(ref clipBounds);
                cornersInModifierSpace = boundsInModifierSpace.GetCorners2D();
                bounds.WorldSpaceCorners = math.mul(WorldFromLocalMatrices.ElementAt(visualModifierDataStoreIndex), cornersInModifierSpace);
            }

            ref float4x4 coplanarSetFromWorld = ref GetCoplanarSetFromWorld(ref batchRootID, ref visualElement);
            float4x4 cornersInSetSpace = math.mul(coplanarSetFromWorld, bounds.WorldSpaceCorners);
            bounds.CoplanarSpaceBounds = new AABB(ref cornersInSetSpace);
            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref float4x4 GetCoplanarSetFromWorld(ref DataStoreID batchRootID, ref VisualElement visualElement)
        {
            CoplanarSetID coplanarSetID = CoplanarSetIDs[visualElement.DataStoreIndex];
            ref CoplanarSet coplanarSet = ref CoplanarSets[batchRootID].ElementAt((int)coplanarSetID);
            DataStoreIndex coplanarSetRootIndex = DataStoreIDToDataStoreIndex[coplanarSet.RootID];
            return ref LocalFromWorldMatrices.ElementAt(coplanarSetRootIndex);
        }
    }
}

