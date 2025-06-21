// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Nova.Internal.Hierarchy.Hierarchy;

namespace Nova.Internal.DataBinding
{
    internal enum ViewState { Partial, InView, OutOfView };

    internal struct ViewItem
    {
        public Vector3 Size;
        public ViewState State;
    }

    [BurstCompile]
    internal struct CheckInView : IJob
    {
        [WriteOnly]
        public NovaHashMap<DataStoreID, ViewItem> ViewItems;

        [ReadOnly]
        public NativeList<Length3.Calculated> Lengths;

        [ReadOnly]
        public NativeList<bool> UseRotations;
        [ReadOnly]
        public NativeList<quaternion> TransformRotations;

        [ReadOnly]
        public NativeHierarchy.ReadOnly Hierarchy;

        public float OutOfViewDistance;

        public DataStoreID ParentID;
        public int ScrollAxis;
        public float ScrollDirection;
        public float ScrollAmount;
        public int Alignment;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute()
        {
            DataStoreIndex parentIndex = Hierarchy.Lookup[ParentID];
            NovaList<DataStoreIndex> children = Hierarchy.Elements[parentIndex].Children;

            LayoutAccess.Calculated parent = LayoutAccess.Get(parentIndex, ref Lengths);
            float3 viewportSize = parent.Size.Value;
            float3 inflatedViewportSize = math.select(viewportSize, Math.float3_Epsilon, viewportSize == Math.float3_Zero);
            inflatedViewportSize[ScrollAxis] = viewportSize[ScrollAxis] + 2 * OutOfViewDistance;

            float parentPaddedSize = parent.PaddedSize[ScrollAxis];
            float parentPaddingOffset = parent.Padding.Offset[ScrollAxis];

            Bounds viewportBounds = new Bounds(Vector3.zero, inflatedViewportSize);
            float parentMin = viewportBounds.min[ScrollAxis];
            float parentMax = viewportBounds.max[ScrollAxis];

            ViewItems.Clear();

            int childCount = children.Length;

            for (int i = 0; i < childCount; ++i)
            {
                DataStoreIndex childIndex = children[i];

                LayoutAccess.Calculated childLayout = LayoutAccess.Get(childIndex, ref Lengths);

                float3 childLayoutSize = childLayout.GetLayoutSize(ref TransformRotations, ref UseRotations);
                float childSize = childLayoutSize[ScrollAxis];
                float childPos = childLayout.Position[ScrollAxis].Value;
                float childMarginOffset = childLayout.Margin.Offset[ScrollAxis];

                // Transform positions from the engine may be stale, so we need to recalculate from the layout values
                float pos = LayoutUtils.LayoutOffsetToLocalPosition(childPos, childSize, parentPaddedSize, childMarginOffset + parentPaddingOffset, Alignment) + ScrollAmount;

                float childMin = pos - 0.5f * childSize;
                float childMax = pos + 0.5f * childSize;

                float scrollInPointOnChild = ScrollDirection < 0 ? childMax : childMin;
                float scrollOutPointOnChild = ScrollDirection < 0 ? childMax : childMin;

                float scrollInPointOnViewport = ScrollDirection < 0 ? parentMax : parentMin;
                float scrollOutPointOnViewport = ScrollDirection < 0 ? parentMin : parentMax;

                float distanceOut = (scrollOutPointOnChild - scrollOutPointOnViewport) * ScrollDirection;
                float distanceIn = (scrollInPointOnChild - scrollInPointOnViewport) * ScrollDirection;

                bool completelyOutOfView = distanceOut > 0;
                bool completelyInView = distanceIn >= 0;

                ViewItems.Add(Hierarchy.Elements[childIndex].ID, new ViewItem()
                {
                    Size = childLayoutSize,
                    State = completelyOutOfView ? ViewState.OutOfView :
                            completelyInView ? ViewState.InView :
                            ViewState.Partial,
                });
            }
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void Run(void* jobData)
        {
            UnsafeUtility.AsRef<CheckInView>(jobData).Execute();
        }
    }

}
