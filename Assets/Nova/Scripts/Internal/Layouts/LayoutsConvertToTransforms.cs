// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
    internal partial class LayoutCore
    {
        /// <summary>
        /// A Job for calculating the local position (one axis at a time) of a Transform attached to a LayoutNode, based on the node's layout properties
        /// </summary>
        [BurstCompile]
        internal struct ConvertToTransforms : INovaJobParallelFor
        {
            [ReadOnly]
            public NativeList<Length3.Calculated> CalculatedLengths;
            [ReadOnly]
            public NativeList<Length3> UncalculatedLengths;
            [ReadOnly]
            public NativeList<Length3.MinMax> MinMaxes;
            [ReadOnly]
            public NativeList<bool> UseRotations;
            [ReadOnly]
            public NativeList<float3> Alignments;
            [ReadOnly]
            public NativeList<quaternion> TransformRotations;
            [ReadOnly]
            public NativeList<TransformProxy> TransformProxies;

            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
            [ReadOnly]
            public NativeList<DataStoreIndex> DirtyElementIndices;

            [ReadOnly]
            public NativeList<HierarchyDependency> DirtyDependencies;

            public bool PreviewSizesAvailable;
            [ReadOnly]
            public NovaHashMap<DataStoreID, PreviewSize> PreviewSizes;

            [NativeDisableParallelForRestriction]
            public NativeList<float3> TransformPositions;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int dirtyIndex)
            {
                Execute(DirtyElementIndices[dirtyIndex]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(DataStoreIndex layoutIndex, bool forceRun = false)
            {
                HierarchyElement child = Hierarchy[layoutIndex];
                DataStoreID parentID = child.ParentID;

                DataStoreIndex parentLayoutIndex = DataStoreIndex.Invalid;

                if (parentID.IsValid)
                {
                    parentLayoutIndex = HierarchyLookup[parentID];

                    if (!forceRun)
                    {
                        bool parentIsVirtual = TransformProxies[parentLayoutIndex].IsVirtual;
                        HierarchyDependency dependencies = DirtyDependencies[layoutIndex];

                        if (!dependencies.HasDirectDependencies && !parentIsVirtual &&
                            DirtyDependencies[parentLayoutIndex] <= HierarchyDependency.Parent)
                        {
                            return;
                        }
                    }
                }

                float3 parentSize = GetParentSize(parentLayoutIndex);

                float3 parentPaddingOffset = Math.float3_Zero;

                if (parentLayoutIndex.IsValid)
                {
                    // parent props
                    LayoutAccess.Calculated parentLayout = LayoutAccess.Get(parentLayoutIndex, ref CalculatedLengths);
                    parentPaddingOffset = parentLayout.Padding.Offset;
                }

                // child props
                LayoutAccess.Calculated childLayout = LayoutAccess.Get(layoutIndex, ref CalculatedLengths);
                float3 rotatedChildSize = childLayout.GetRotatedSize(ref TransformRotations, ref UseRotations);
                float3 localPosition = GetLocalPosition(ref childLayout, ref rotatedChildSize, ref parentSize, parentPaddingOffset, ref Alignments);

                if (Math.ValidAndFinite(ref localPosition))
                {
                    TransformPositions[layoutIndex] = localPosition;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float3 GetLocalPosition(ref LayoutAccess.Calculated childLayout, ref float3 rotatedChildSize, ref float3 paddedParentSize, float3 parentPaddingCenter, ref NativeList<float3> alignments)
            {
                LengthBounds.Calculated childMargin = childLayout.Margin;
                float3 alignment = alignments[childLayout.Index];
                float3 totalChildSize = rotatedChildSize + childMargin.Size;
                return LayoutUtils.LayoutOffsetToLocalPosition(childLayout.Position.Value, totalChildSize, childMargin.Offset, paddedParentSize, parentPaddingCenter, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float3 GetParentSize(DataStoreIndex parentIndex)
            {
                if (!parentIndex.IsValid)
                {
                    return Math.float3_Zero;
                }

                LayoutAccess.Calculated parentLayout = LayoutAccess.Get(parentIndex, ref CalculatedLengths);
                return parentLayout.PaddedSize;
            }
        }
    }
}
