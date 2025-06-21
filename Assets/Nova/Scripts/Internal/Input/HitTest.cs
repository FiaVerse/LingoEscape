// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Nova.Internal.Input
{
    [BurstCompile]
    internal unsafe struct HitTest<TTest,TCollidable,THit> : IJob
        where TTest : struct, ICollisionTest<TCollidable, THit>
        where THit : unmanaged, IComparer<THit>
        where TCollidable : unmanaged
    {
        public TTest CollisionTest;

        public NativeList<DataStoreIndex> IndicesToProcess;
        public NativeList<THit> HitIndices;

        [ReadOnly]
        public NativeList<SpatialPartitionMask> SpatialPartitionMasks;
        [ReadOnly]
        public NativeList<DataStoreID> RootIDs;

        [ReadOnly]
        public NativeList<HierarchyElement> Hierarchy;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

        private TCollidable collidableInWorldSpace;

        private THit hit;

        public void Execute()
        {
            IndicesToProcess.Clear();
            HitIndices.Clear();

            collidableInWorldSpace = CollisionTest.Init();

            for (int i = 0; i < RootIDs.Length; ++i)
            {
                IndicesToProcess.Add(HierarchyLookup[RootIDs[i]]);
            }

            Traverse();

            HitIndices.Sort(default(THit));

            IndicesToProcess.Clear();
        }

        private void Traverse()
        {
            int countToProcess = IndicesToProcess.Length;
            for (int i = 0; i < countToProcess; ++i)
            {
                DataStoreIndex parentIndex = IndicesToProcess[i];
                HierarchyElement parentElement = Hierarchy[parentIndex];

                NovaList<DataStoreIndex> children = parentElement.Children;

                TCollidable collidableInTestSpace;
                if (children.Length > 0)
                {
                    if (!CollisionTest.CollidesWithContent(ref collidableInWorldSpace, parentIndex, parentElement.ID, out collidableInTestSpace))
                    {
                        continue;
                    }

                    SpatialPartitionMask parentHitMask = CollisionTest.GetCollisionMask(ref collidableInTestSpace, parentIndex);

                    // Adds to IndicesToProcess
                    countToProcess += AppendOverlappingChildren(ref children, parentHitMask);
                }
                else
                {
                    CollisionTest.TransformCollidable(ref collidableInWorldSpace, parentIndex, out collidableInTestSpace);
                }

                CheckHitMesh(ref collidableInTestSpace, parentIndex, parentElement.ID);
            }
        }

        private int AppendOverlappingChildren(ref NovaList<DataStoreIndex> children, SpatialPartitionMask parentHitMask)
        {
            int childCount = children.Length;

            int childrenToProcess = 0;
            for (int siblingIndex = 0; siblingIndex < childCount; ++siblingIndex)
            {
                DataStoreIndex childIndex = children[siblingIndex];
                if (!SpatialPartitionMasks[childIndex].Overlaps(parentHitMask))
                {
                    continue;
                }

                // queue child items to process
                IndicesToProcess.Add(childIndex);
                childrenToProcess++;
            }

            return childrenToProcess;
        }

        private void CheckHitMesh(ref TCollidable collidableInLocalSpace, DataStoreIndex elementIndex, DataStoreID elementID)
        {
            if (!CollisionTest.CollidesWithMesh(ref collidableInLocalSpace, elementIndex, elementID, out hit))
            {
                return;
            }

            HitIndices.Add(hit);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void Run(void* jobData)
        {
            UnsafeUtility.AsRef<HitTest<TTest, TCollidable, THit>>(jobData).Execute();
        }
    }
}
