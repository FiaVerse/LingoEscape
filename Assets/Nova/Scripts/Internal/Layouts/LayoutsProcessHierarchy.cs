// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Layouts
{
    internal partial class LayoutCore
    {
        [BurstCompile]
        internal struct CountDirtyRoots : INovaJobParallelFor
        {
            [ReadOnly]
            public NativeList<BatchGroupElement> BatchGroupElements;
            [ReadOnly]
            public NativeList<DataStoreIndex> DirtyLayoutElements;
            [ReadOnly]
            public NovaHashMap<DataStoreID, int> BatchRootIDToCounterIndex;

            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            [NativeDisableContainerSafetyRestriction]
            public NativeList<UnsafeAtomicCounter32> BatchRootDirtyCounts;
            public void Execute(int dirtyIndex)
            {
                DataStoreIndex globalIndex = DirtyLayoutElements[dirtyIndex];
                BatchGroupElement batchGroupElement = BatchGroupElements[globalIndex];
                DataStoreID batchRootID = batchGroupElement.BatchRootID;

                if (!batchRootID.IsValid)
                {
                    UnityEngine.Debug.LogError("Batch Root ID invalid");
                    return;
                }

                int batchRootCounterIndex = BatchRootIDToCounterIndex[batchRootID];
                BatchRootDirtyCounts.ElementAt(batchRootCounterIndex).Add(1);

                DataStoreIndex batchRootGlobalIndex = HierarchyLookup[batchRootID];
                DataStoreID batchRootParentID = Hierarchy[batchRootGlobalIndex].ParentID;
                while (batchRootParentID.IsValid)
                {
                    DataStoreIndex batchRootParentIndex = HierarchyLookup[batchRootParentID];
                    DataStoreID outerBatchRootID = BatchGroupElements[batchRootParentIndex].BatchRootID;
                    batchRootParentID = Hierarchy[batchRootParentIndex].ParentID;

                    int parentBatchRootCounterIndex = BatchRootIDToCounterIndex[outerBatchRootID];
                    BatchRootDirtyCounts.ElementAt(parentBatchRootCounterIndex).Add(1);
                }
            }
        }

        [BurstCompile]
        internal struct MarkRootsDirty : INovaJob
        {
            [ReadOnly]
            public NativeList<DataStoreID> AllBatchRoots;

            [ReadOnly]
            public NativeList<DataStoreID> DirtyBatchRoots;

            [ReadOnly]
            public NativeList<UnsafeAtomicCounter32> BatchRootDirtyCounts;

            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
            [ReadOnly]
            public NovaHashMap<DataStoreID, int> HierarchyRoots;

            [ReadOnly]
            public NativeList<BatchGroupElement> BatchGroupElements;
            [ReadOnly]
            public NovaHashMap<DataStoreID, int> BatchRootIDToCounterIndex;

            public NativeList<DataStoreID> DependentBatchRoots;

            public void Execute()
            {
                // find dirty batch roots dirtied by the hierarchy that are hierarchy roots
                int dirtyRootCount = DirtyBatchRoots.Length;
                for (int i = 0; i < dirtyRootCount; ++i)
                {
                    DataStoreID dirtyRootID = DirtyBatchRoots[i];

                    if (HierarchyRoots.TryGetValue(dirtyRootID, out int dirtyRootIndex))
                    {
                        DependentBatchRoots[dirtyRootIndex] = dirtyRootID;
                    }
                }

                // find dirty batch roots dirtied by layouts
                int rootCount = AllBatchRoots.Length;
                for (int index = 0; index < rootCount; ++index)
                {
                    DataStoreID batchRootID = AllBatchRoots[index];

                    DataStoreID batchingID = batchRootID;
                    int batchingIndex = index;

                    while (batchingID.IsValid)
                    {
                        unsafe
                        {
                            if (*BatchRootDirtyCounts[batchingIndex].Counter >= 1)
                            {
                                DependentBatchRoots[index] = batchRootID;
                                break;
                            }
                        }

                        DataStoreIndex batchRootIndex = HierarchyLookup[batchingID];
                        DataStoreID batchRootParentID = Hierarchy[batchRootIndex].ParentID;

                        if (!HierarchyLookup.TryGetValue(batchRootParentID, out DataStoreIndex batchRootParentIndex))
                        {
                            break;
                        }

                        batchingID = BatchGroupElements[batchRootParentIndex].BatchRootID;
                        batchingIndex = BatchRootIDToCounterIndex[batchingID];
                    }
                }

                // remove unset array elements
                for (int i = DependentBatchRoots.Length - 1; i >= 0; --i)
                {
                    if (!DependentBatchRoots[i].IsValid)
                    {
                        DependentBatchRoots.RemoveAtSwapBack(i);
                    }
                }
            }
        }
    }
}
