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
        [BurstCompile]
        public struct ContentBounds : INovaJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeList<float3> DirectContentSizes;
            [NativeDisableParallelForRestriction]
            public NativeList<float3> DirectContentOffsets;

            [ReadOnly]
            public NativeList<Length3.Calculated> Lengths;
            [ReadOnly]
            public NativeList<float3> LocalPositions;
            [ReadOnly]
            public NativeList<quaternion> LocalRotations;
            [ReadOnly]
            public NativeList<bool> UseRotations;
            [ReadOnly]
            public NativeList<float3> Alignments;
            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NativeList<DataStoreIndex> DirtyIndices;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int index)
            {
                Execute(DirtyIndices[index]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(DataStoreIndex parentIndex, bool calculateChildPositions = false)
            {
                HierarchyElement parentElement = Hierarchy[parentIndex];

                float3 minContentPoint = float.MaxValue;
                float3 maxContentPoint = float.MinValue;

                int childCount = parentElement.Children.Length;

                for (int i = 0; i < childCount; ++i)
                {
                    DataStoreIndex childIndex = parentElement.Children[i];

                    float3x2 directBounds = default;
                    if (calculateChildPositions)
                    {
                        CalculateBounds(childIndex, parentIndex, out directBounds);
                    }
                    else
                    {
                        CalculateBounds(childIndex, out directBounds);
                    }

                    minContentPoint = math.min(directBounds.c0, minContentPoint);
                    maxContentPoint = math.max(directBounds.c1, maxContentPoint);
                }

                if (childCount == 0)
                {
                    minContentPoint = Math.float3_Zero;
                    maxContentPoint = Math.float3_Zero;
                }

                float3 directContentSize = maxContentPoint - minContentPoint;
                float3 directContentOffset = (maxContentPoint + minContentPoint) * Math.float3_Half;

                DirectContentSizes[parentIndex] = directContentSize;
                DirectContentOffsets[parentIndex] = directContentOffset;
            }

            /// <summary>
            /// Gets the min/max bounds of the given child's hierarchy content
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CalculateBounds(DataStoreIndex childIndex, out float3x2 childBounds)
            {
                LayoutAccess.Calculated childLayout = LayoutAccess.Get(childIndex, ref Lengths);
                Math.ToFloat3x2(childLayout.GetRotatedSize(ref LocalRotations, ref UseRotations) + childLayout.Margin.Size, out float3x2 rotatedChildSize);
                Math.ToFloat3x2(LocalPositions[childIndex] - childLayout.Margin.Offset, out float3x2 contentCenterOffset);
                childBounds = (rotatedChildSize * Math.Extents) + contentCenterOffset;
            }

            /// <summary>
            /// Gets the min/max bounds of the given child's hierarchy content
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CalculateBounds(DataStoreIndex childIndex, DataStoreIndex parentIndex, out float3x2 childBounds)
            {
                LayoutAccess.Calculated childLayout = LayoutAccess.Get(childIndex, ref Lengths);
                LayoutAccess.Calculated parentLayout = LayoutAccess.Get(parentIndex, ref Lengths);

                Math.ToFloat3x2(childLayout.GetRotatedSize(ref LocalRotations, ref UseRotations), out float3x2 rotatedChildSize);

                float3 position = LayoutUtils.LayoutOffsetToLocalPosition(childLayout.Position.Value, rotatedChildSize.c0 + childLayout.Margin.Size, childLayout.Margin.Offset, parentLayout.PaddedSize, parentLayout.Padding.Offset, Alignments[childIndex]);
                Math.ToFloat3x2(position, out float3x2 contentCenterOffset);
                childBounds = (rotatedChildSize * Math.Extents) + contentCenterOffset;
            }
        }

        [BurstCompile]
        public struct HierarchyBounds : INovaJob
        {
            public NativeList<float3> TotalContentSizes;
            public NativeList<float3> TotalContentOffsets;

            [ReadOnly]
            public NativeList<quaternion> LocalRotations;
            [ReadOnly]
            public NativeList<float3> LocalPositions;
            [ReadOnly]
            public NativeList<float3> LocalScales;
            [ReadOnly]
            public NativeList<Length3.Calculated> Lengths;
            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NativeList<DataStoreIndex> DirtyIndices;

            public void Execute()
            {
                int dirtyCount = DirtyIndices.Length;

                for (int i = dirtyCount - 1; i >= 0; --i)
                {
                    DataStoreIndex parentIndex = DirtyIndices[i];

                    HierarchyElement parentElement = Hierarchy[parentIndex];

                    float3 minHierarchyPoint = float.MaxValue;
                    float3 maxHierarchyPoint = float.MinValue;

                    int childCount = parentElement.Children.Length;

                    for (int j = 0; j < childCount; ++j)
                    {
                        DataStoreIndex childIndex = parentElement.Children[j];

                        GetBounds(childIndex, out float3x2 hierarchyBounds);

                        minHierarchyPoint = math.min(hierarchyBounds.c0, minHierarchyPoint);
                        maxHierarchyPoint = math.max(hierarchyBounds.c1, maxHierarchyPoint);
                    }

                    if (childCount == 0)
                    {
                        minHierarchyPoint = Math.float3_Zero;
                        maxHierarchyPoint = Math.float3_Zero;
                    }

                    Math.ToFloat3x2(LayoutAccess.Get(parentIndex, ref Lengths).Size.Value, out float3x2 parentSize3x2);
                    float3x2 parentExtents = parentSize3x2 * Math.Extents;

                    minHierarchyPoint = math.min(parentExtents[0], minHierarchyPoint);
                    maxHierarchyPoint = math.max(parentExtents[1], maxHierarchyPoint);

                    TotalContentSizes[parentIndex] = maxHierarchyPoint - minHierarchyPoint;
                    TotalContentOffsets[parentIndex] = (maxHierarchyPoint + minHierarchyPoint) * Math.float3_Half;
                }
            }

            /// <summary>
            /// Gets the min/max bounds of the given child's hierarchy content
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void GetBounds(DataStoreIndex childIndex, out float3x2 hierarchyPoints)
            {
                quaternion rotation = LocalRotations[childIndex];
                float3 scale = LocalScales[childIndex];

                Math.ToFloat3x2(LayoutUtils.RotateSize(TotalContentSizes[childIndex], rotation) * scale, out float3x2 rotatedContentSize);
                float3 hierarchyLocalPosition = LocalPositions[childIndex] + math.rotate(rotation, TotalContentOffsets[childIndex] * scale);

                Math.ToFloat3x2(hierarchyLocalPosition, out float3x2 hierarchyCenterOffset);
                hierarchyPoints = (rotatedContentSize * Math.Extents) + hierarchyCenterOffset;
            }
        }
    }
}