// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// Goes through every quad in the rotation set and assigns render under
    /// </summary>
    [BurstCompile]
    internal struct QuadProcessJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [ReadOnly]
        public NovaHashMap<DataStoreID, RotationSetSummary> RotationSets;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public NativeList<DataStoreIndex, VisualModifierID> VisualModifierIDs;

        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, SubQuadData> SubQuadData;

        private BoundarySummary boundarySummary;
        private NovaList<VisualElementIndex, VisualElement> visualElements;

        public void Execute(int index)
        {
            if (!TryGetRotationSetToProcess(index, out RotationSetID rotationSetID, out RotationSetSummary rotationSetSummary, out DataStoreID batchRootID))
            {
                return;
            }

            visualElements = VisualElements[batchRootID];

            ref RotationSet rotationSet = ref rotationSetSummary.Sets.ElementAt(rotationSetID);
            boundarySummary = rotationSet.BoundarySummary;
            boundarySummary.Descriptors.Sort();

            for (int i = 0; i < boundarySummary.Descriptors.Length; ++i)
            {
                ref QuadBoundsDescriptor descriptor = ref boundarySummary.Descriptors.ElementAt(i);
                SubQuadData.ElementAt(descriptor.RenderIndex).SortedIndex = i;
                AssignOverlaps(ref descriptor, i);
                boundarySummary.InProgress.Add(new ValuePair<QuadBoundsDescriptor, int>(descriptor, i));
            }

            boundarySummary.InProgress.Clear();

            rotationSet.BoundarySummary = boundarySummary;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignOverlaps(ref QuadBoundsDescriptor descriptor, int index)
        {
            VisualModifierID visualModifierID = VisualModifierIDs[visualElements[descriptor.VisualElementIndex].DataStoreIndex];

            for (int i = 0; i < boundarySummary.InProgress.Length; ++i)
            {
                ref ValuePair<QuadBoundsDescriptor, int> inProgress = ref boundarySummary.InProgress.ElementAt(i);

                if (inProgress.Item1.Bounds.RightEdge <= descriptor.Bounds.LeftEdge)
                {
                    // The inprogress quad has ended
                    boundarySummary.InProgress.RemoveAtSwapBack(i--);
                    continue;
                }

                if (VisualModifierIDs[visualElements[inProgress.Item1.VisualElementIndex].DataStoreIndex] != visualModifierID)
                {
                    continue;
                }

                if (descriptor.RendersUnder(ref inProgress.Item1))
                {
                    TryAssignRenderUnder(ref descriptor, ref inProgress.Item1, inProgress.Item2);
                }
                else
                {
                    TryAssignRenderUnder(ref inProgress.Item1, ref descriptor, index);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryAssignRenderUnder(ref QuadBoundsDescriptor rendersUnder, ref QuadBoundsDescriptor rendersOnTop, int index)
        {
            if (!rendersOnTop.HasAnyOcclusion || !rendersUnder.MaxRenderBounds.Overlap(ref rendersOnTop.MaxOcclusionBounds))
            {
                return;
            }

            SubQuadData.ElementAt(rendersUnder.RenderIndex).RendersUnder.Add(index);
        }

        private bool TryGetRotationSetToProcess(int index, out RotationSetID rotationSetID, out RotationSetSummary rotationSetSummary, out DataStoreID batchRootID)
        {
            for (int i = 0; i < DirtyBatches.Length; ++i)
            {
                batchRootID = DirtyBatches[i];
                rotationSetSummary = RotationSets[batchRootID];

                if (index >= rotationSetSummary.SetCount)
                {
                    index -= rotationSetSummary.SetCount;
                    continue;
                }

                rotationSetID = index;
                return true;
            }

            rotationSetSummary = default;
            batchRootID = DataStoreID.Invalid;
            rotationSetID = default;
            return false;
        }
    }
}

