// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct OverlapJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, BatchZLayers> ZLayers;
        [ReadOnly]
        public NativeList<DataStoreIndex, CoplanarSetID> CoplanarSetIDs;
        [ReadOnly]
        public ComputeBufferIndices ComputeBufferIndices;
        [ReadOnly]
        public BlockBounds Bounds;
        [ReadOnly]
        public NovaHashMap<RenderIndex, ComputeBufferIndex> ShadowIndices;

        public OverlapElements OverlapElements;

        private NovaList<VisualElementIndex, VisualElement> visualElements;

        public void Execute(int index)
        {
            if (!VisualElementUtilities.TryGetIndex(index, ref DirtyBatches, ref VisualElements, out DataStoreID batchRootID, out int indexIntoLayers))
            {
                return;
            }

            BatchZLayers zLayers = ZLayers[batchRootID];
            if (!zLayers.TryGetReverseIterator(indexIntoLayers, out BatchZLayers.ReverseIterator iterator) ||
                !zLayers.TryGet(out VisualElementIndex visualElementIndex, ref iterator))
            {
                return;
            }

            visualElements = VisualElements[batchRootID];

            ref VisualElement visualElement = ref visualElements.ElementAt(visualElementIndex);
            if (visualElement.SkipRendering || visualElement.Type == VisualType.TextSubmesh || !visualElement.Type.Is2D())
            {
                // Skip text because we just use the bounds of the main mesh
                return;
            }

            if (visualElement.Type == VisualType.DropShadow)
            {
                ref AccentBounds elementBounds = ref Bounds.Shadow.ElementAt(ShadowIndices[visualElement.RenderIndex]);
                DoAccent(ref visualElement, ref zLayers, iterator, ref elementBounds);
            }
            else
            {
                DoBlock(ref visualElement, ref zLayers, iterator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoAccent(ref VisualElement visualElement, ref BatchZLayers zLayers, BatchZLayers.ReverseIterator iterator, ref AccentBounds accentBounds)
        {
            CoplanarSetID coplanarSetID = CoplanarSetIDs[visualElement.DataStoreIndex];
            ref NovaList<VisualElementIndex> overlapping = ref OverlapElements.Get(ref visualElement, ref ComputeBufferIndices);
            overlapping.Clear();

            while (zLayers.TryGet(out VisualElementIndex indexToCheck, ref iterator))
            {
                ref VisualElement elementToCheck = ref visualElements.ElementAt(indexToCheck);
                if (elementToCheck.SkipRendering ||
                    !elementToCheck.Type.Is2D() ||
                    coplanarSetID != CoplanarSetIDs[elementToCheck.DataStoreIndex])
                {
                    continue;
                }

                if (elementToCheck.Type == VisualType.DropShadow)
                {
                    ref AccentBounds boundsToCheck = ref Bounds.Shadow.ElementAt(ShadowIndices[elementToCheck.RenderIndex]);
                    if (!boundsToCheck.Overlaps2D(ref accentBounds))
                    {
                        continue;
                    }

                    overlapping.Add(indexToCheck);
                }
                else
                {
                    ref AABB boundsToCheck = ref Bounds.Block.ElementAt(elementToCheck.DataStoreIndex).CoplanarSpaceBounds;
                    if (!accentBounds.Overlaps2D(ref boundsToCheck))
                    {
                        continue;
                    }

                    overlapping.Add(indexToCheck);
                    if (boundsToCheck.Encapsulates2D(ref accentBounds.Outer.CoplanarSpaceBounds))
                    {
                        break;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoBlock(ref VisualElement visualElement, ref BatchZLayers zLayers, BatchZLayers.ReverseIterator iterator)
        {
            CoplanarSetID coplanarSetID = CoplanarSetIDs[visualElement.DataStoreIndex];
            ref AABB elementBounds = ref Bounds.Block.ElementAt(visualElement.DataStoreIndex).CoplanarSpaceBounds;
            ref NovaList<VisualElementIndex> overlapping = ref OverlapElements.Get(ref visualElement, ref ComputeBufferIndices);
            overlapping.Clear();

            while (zLayers.TryGet(out VisualElementIndex indexToCheck, ref iterator))
            {
                ref VisualElement elementToCheck = ref visualElements.ElementAt(indexToCheck);
                if (elementToCheck.SkipRendering ||
                    !elementToCheck.Type.Is2D() ||
                    coplanarSetID != CoplanarSetIDs[elementToCheck.DataStoreIndex])
                {
                    continue;
                }

                if (elementToCheck.Type == VisualType.DropShadow)
                {
                    ref AccentBounds boundsToCheck = ref Bounds.Shadow.ElementAt(ShadowIndices[elementToCheck.RenderIndex]);
                    if (!boundsToCheck.Overlaps2D(ref elementBounds))
                    {
                        continue;
                    }

                    overlapping.Add(indexToCheck);
                }
                else
                {
                    ref AABB boundsToCheck = ref Bounds.Block.ElementAt(elementToCheck.DataStoreIndex).CoplanarSpaceBounds;
                    if (!boundsToCheck.Overlaps2D(ref elementBounds))
                    {
                        continue;
                    }

                    overlapping.Add(indexToCheck);
                    if (boundsToCheck.Encapsulates2D(ref elementBounds))
                    {
                        break;
                    }
                }
            }
        }
    }
}
