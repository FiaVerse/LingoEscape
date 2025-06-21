// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Nova.Internal.Hierarchy
{
    internal partial class Hierarchy
    {
        [BurstCompile]
        internal struct AssignBatchRoots : INovaJobParallelFor, IJob
        {
            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            [ReadOnly]
            public NovaHashMap<DataStoreID, int> BatchRoots;

            [ReadOnly]
            public NativeList<DataStoreIndex> DirtyToDataStoreIndices;

            [NativeDisableParallelForRestriction]
            public NativeList<BatchGroupElement> BatchGroupElements;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe void Execute()
            {
                int dirtyIndexCount = DirtyToDataStoreIndices.Length;
                for (int i = 0; i < dirtyIndexCount; ++i)
                {
                    Execute(i);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe void Execute(int dirtyElementIndex)
            {
                DataStoreIndex elementIndex = DirtyToDataStoreIndices[dirtyElementIndex];
                HierarchyElement element = Hierarchy[elementIndex];

                bool hasChildren = element.ChildCount > 0;
                bool hasParent = element.ParentID.IsValid;

                bool elementIsBatchRoot = BatchRoots.ContainsKey(element.ID);
                if (!elementIsBatchRoot)
                {
                    if (!hasParent)
                    {
                        // this must be a redirector element (virtual node)

                        ref BatchGroupElement batchGroupElement = ref BatchGroupElements.ElementAt(elementIndex);
                        batchGroupElement.BatchRootID = DataStoreID.Invalid;
                        batchGroupElement.HierarchyDepthLevel = DepthLevel.Invalid;

                        return;
                    }

                    if (!hasChildren)
                    {
                        // leaf node -- parent will update
                        return;
                    }
                }

                TraverseUpwards(element.ID, out DataStoreID batchRootID, out int depthInHierarchy);

                if (elementIsBatchRoot)
                {
                    ref BatchGroupElement batchGroupElement = ref BatchGroupElements.ElementAt(elementIndex);
                    batchGroupElement.BatchRootID = element.ID;
                    batchGroupElement.HierarchyDepthLevel = depthInHierarchy;
                }

                UpdateChildren(elementIndex, batchRootID, depthInHierarchy);
            }

            /// <summary>
            /// Get the depth level of the given element relative to the hierarchy root and find which batch it belongs to.
            /// </summary>
            /// <param name="elementPtr"></param>
            /// <param name="depthLevel"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void TraverseUpwards(DataStoreID elementID, out DataStoreID batchRootID, out int depthLevel)
            {
                depthLevel = -1;
                batchRootID = DataStoreID.Invalid;

                while (!batchRootID.IsValid && HierarchyLookup.TryGetValue(elementID, out DataStoreIndex globalIndex))
                {
                    HierarchyElement element = Hierarchy[globalIndex];

                    depthLevel++;
                    batchRootID = !batchRootID.IsValid && BatchRoots.ContainsKey(elementID) ? elementID : batchRootID;

                    elementID = element.ParentID;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void UpdateChildren(DataStoreIndex parentIndex, DataStoreID batchRootID, int parentDepthLevel)
            {
                HierarchyElement parent = Hierarchy[parentIndex];

                int childDepth = parentDepthLevel + 1;
                int childCount = parent.Children.Length;
                for (int i = 0; i < childCount; ++i)
                {
                    DataStoreIndex childIndex = parent.Children[i];
                    DataStoreID childID = Hierarchy[childIndex].ID;

                    ref BatchGroupElement batchGroupChild = ref BatchGroupElements.ElementAt(childIndex);

                    batchGroupChild.HierarchyDepthLevel = childDepth;

                    if (!BatchRoots.ContainsKey(childID))
                    {
                        batchGroupChild.BatchRootID = batchRootID;
                    }
                }
            }
        }
    }
}
