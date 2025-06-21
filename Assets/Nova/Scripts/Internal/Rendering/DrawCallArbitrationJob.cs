// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
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
    internal unsafe struct DrawCallArbitrationJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatchGroups;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, BatchZLayers> ZLayers;
        [ReadOnly]
        public NativeList<DataStoreIndex, CoplanarSetID> CoplanarSetIDs;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [ReadOnly]
        public BlockBounds Bounds;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<CoplanarSetID, CoplanarSet>> CoplanarSets;
        [ReadOnly]
        public NativeList<RenderIndex, TextBlockData> TextData;
        [ReadOnly]
        public ComputeBufferIndices ComputeBufferIndices;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> LocalFromWorldMatrices;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public NovaHashMap<VisualModifierID, AABB> VisualModifierClipBounds;
        [ReadOnly]
        public OverlapElements OverlapElements;
        [ReadOnly]
        public NativeList<VisualModifierID, DataStoreID> ModifierToBlockID;

        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, DrawCallSummary> DrawCallSummaries;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, DrawCallID>> MinDrawCalls;

        private DataStoreID batchRootID;
        private DrawCallSummary drawCallSummary;
        private BatchZLayers zLayers;
        private NovaList<VisualElementIndex, VisualElement> visualElements;
        private NovaList<VisualElementIndex, DrawCallID> assignedDrawCalls;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute(int index)
        {
            batchRootID = DirtyBatchGroups[index];
            drawCallSummary = DrawCallSummaries[batchRootID];
            zLayers = ZLayers[batchRootID];
            visualElements = VisualElements[batchRootID];
            assignedDrawCalls = MinDrawCalls.GetAndResize(batchRootID, visualElements.Length);
            assignedDrawCalls.MemClear();

            if (!zLayers.TryGetVal(0, out VisualElementIndex visualElementIndex, out BatchZLayers.Iterator iterator))
            {
                return;
            }

            do
            {
                ProcessElement(visualElementIndex);
            } while (zLayers.TryGetNext(out visualElementIndex, ref iterator));

            drawCallSummary.UpdateIndexBounds();
            UpdateCoplanarCountsAndBounds();

            DrawCallSummaries[batchRootID] = drawCallSummary;
            MinDrawCalls[batchRootID] = assignedDrawCalls;
        }

        /// <summary>
        /// Updates all of the CoplanarSet render bounds (by encapsulating all of the contained draw calls)
        /// and updates the count of transparent draw calls contained.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCoplanarCountsAndBounds()
        {
            NovaList<CoplanarSetID, CoplanarSet> coplanarSets = CoplanarSets[batchRootID];

            for (int i = 0; i < drawCallSummary.DrawCalls.Length; ++i)
            {
                ref DrawCall drawCall = ref drawCallSummary.DrawCalls.ElementAt(i);
                ref DrawCallDescriptor descriptor = ref drawCallSummary.DrawCallDescriptors.ElementAt(drawCall.DescriptorID);
                if (!descriptor.DrawCallType.Is2D())
                {
                    continue;
                }

                ref CoplanarSet coplanarSet = ref coplanarSets.ElementAt(drawCall.CoplanarSetID);

                if (descriptor.VisualModifierID.IsValid &&
                    VisualModifierClipBounds.TryGetValue(descriptor.VisualModifierID, out AABB clipBounds))
                {
                    // It's for a clip type, so clamp the bounds of the draw call
                    DataStoreIndex coplanarSetRootIndex = DataStoreIDToDataStoreIndex[coplanarSet.RootID];
                    DataStoreIndex modifierDataStoreIndex = DataStoreIDToDataStoreIndex[ModifierToBlockID[descriptor.VisualModifierID]];
                    ref float4x4 modifierFromWorld = ref LocalFromWorldMatrices.ElementAt(modifierDataStoreIndex);
                    ref float4x4 worldFromCoplanarSet = ref WorldFromLocalMatrices.ElementAt(coplanarSetRootIndex);
                    AABB boundsInModifierSpace = AABB.Transform2D(ref modifierFromWorld, ref worldFromCoplanarSet, ref drawCall.CoplanarSpaceRenderBounds);
                    boundsInModifierSpace.Clamp2D(ref clipBounds);

                    ref float4x4 worldFromModifier = ref WorldFromLocalMatrices.ElementAt(modifierDataStoreIndex);
                    ref float4x4 coplanarSetFromWorld = ref LocalFromWorldMatrices.ElementAt(coplanarSetRootIndex);
                    drawCall.CoplanarSpaceRenderBounds = AABB.Transform2D(ref coplanarSetFromWorld, ref worldFromModifier, ref boundsInModifierSpace);
                }

                drawCall.TransparentDrawCallOrderInCoplanarSet = coplanarSet.TransparentDrawCallCount++;
                coplanarSet.CoplanarSpaceRenderBounds.Encapsulate(ref drawCall.CoplanarSpaceRenderBounds);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessElement(VisualElementIndex visualElementIndex)
        {
            ref VisualElement visualElement = ref visualElements.ElementAt(visualElementIndex);
            if (visualElement.SkipRendering)
            {
                return;
            }

            CoplanarSetID coplanarSetID = CoplanarSetIDs[visualElement.DataStoreIndex];
            DrawCallID minDrawCall = GetMinDrawCall(ref visualElement);
            if (!drawCallSummary.TryGetMatchingDrawCall(visualElement.DrawCallDescriptorID, CoplanarSetIDs[visualElement.DataStoreIndex], minDrawCall, out DrawCallID matching))
            {
                matching = drawCallSummary.AddDrawCall(visualElement.DrawCallDescriptorID, coplanarSetID);
            }

            ref AABB bounds = ref GetBounds(ref visualElement);
            RenderIndex renderIndex = BaseInfos[visualElement.DataStoreIndex].RenderIndex;
            switch (visualElement.Type)
            {
                case VisualType.DropShadow:
                    ComputeBufferIndex shaderIndex = ComputeBufferIndices.Shadow[renderIndex];
                    shaderIndex *= Constants.QuadsPerAccent;
                    for (int i = 0; i < Constants.QuadsPerAccent; ++i)
                    {
                        drawCallSummary.AddElementWithIndex(shaderIndex + i, ref matching, ref bounds);
                    }
                    break;
                case VisualType.TextBlock:
                case VisualType.TextSubmesh:
                {
                    NovaList<ComputeBufferIndex> textShaderIndices = ComputeBufferIndices.Text[renderIndex];
                    ref DrawCallDescriptor descriptor = ref drawCallSummary.DrawCallDescriptors.ElementAt(visualElement.DrawCallDescriptorID);
                    TextBlockData textData = TextData[renderIndex];
                    textData.GetInstanceSliceForSubmesh(descriptor.Text.MaterialID, out int startIndex, out int count);
                    for (int i = 0; i < count; ++i)
                    {
                        drawCallSummary.AddElementWithIndex(textShaderIndices[startIndex + i], ref matching, ref bounds);
                    }
                    break;
                }
                case VisualType.UIBlock2D:
                    drawCallSummary.AddElementNoIndex(ref matching, ref bounds, ref visualElementIndex);
                    break;
                case VisualType.UIBlock3D:
                    drawCallSummary.AddElementWithIndex(ComputeBufferIndices.UIBlock3D[renderIndex], ref matching, ref bounds);
                    break;
                default:
                    Debug.LogError("Unknown visual type");
                    break;
            }

            assignedDrawCalls[visualElementIndex] = matching;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref AABB GetBounds(ref VisualElement visualElement)
        {
            if (visualElement.Type == VisualType.DropShadow)
            {
                return ref Bounds.Shadow.ElementAt(ComputeBufferIndices.Shadow[visualElement.RenderIndex]).Outer.CoplanarSpaceBounds;
            }
            else
            {
                return ref Bounds.Block.ElementAt(visualElement.DataStoreIndex).CoplanarSpaceBounds;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DrawCallID GetMinDrawCall(ref VisualElement visualElement)
        {
            DrawCallID toRet = DrawCallID.Invalid;
            ref NovaList<VisualElementIndex> dependencies = ref OverlapElements.Get(ref visualElement, ref ComputeBufferIndices);
            for (int i = 0; i < dependencies.Length; ++i)
            {
                toRet = DrawCallID.Max(ref toRet, ref assignedDrawCalls.ElementAt(dependencies[i]));
            }
            return toRet;
        }
    }
}

