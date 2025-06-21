// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Layouts
{
    internal partial class LayoutEngine
    {
        private static BurstedMethod<BurstMethod> prepare;

        public struct LayoutCache : IInitializable
        {
            public NativeList<DataStoreIndex> LayoutDirtiedIndices;
            public NativeList<DataStoreID> MatrixDirtiedRootIDs;

            public NativeList<DataStoreIndex> AllProcessedElements;

            private NativeList<int> underlyingRootCountsUnatomic;
            public NativeList<UnsafeAtomicCounter32> DirtyRootCounts;
            public NativeList<DataStoreID> ProcessedRootIDs;

            public NativeList<LayoutCore.ExpandableTrack> AutoLayoutTrackCache;
            public NativeList<LayoutCore.ExpandableRange> AutoLayoutRangeCache;

            public NativeList<bool> NeedsSecondPass;
            public NativeList<int> ProcessingStack;

            private Prepare prepareRunner;

            public void PrepareForUpdate()
            {
                prepareRunner.BatchRootIDs = HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs;

                unsafe
                {
                    prepare.Method.Invoke(UnsafeUtility.AddressOf(ref prepareRunner));
                }
            }

            public void Init()
            {
                AutoLayoutTrackCache = new NativeList<LayoutCore.ExpandableTrack>(Constants.FewElementsInitialCapacity, Allocator.Persistent);
                AutoLayoutRangeCache = new NativeList<LayoutCore.ExpandableRange>(Constants.FewElementsInitialCapacity, Allocator.Persistent);

                AllProcessedElements = new NativeList<DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
                LayoutDirtiedIndices = new NativeList<DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
                MatrixDirtiedRootIDs = new NativeList<DataStoreID>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

                NeedsSecondPass = new NativeList<bool>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
                ProcessingStack = new NativeList<int>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

                underlyingRootCountsUnatomic = new NativeList<int>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
                DirtyRootCounts = new NativeList<UnsafeAtomicCounter32>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
                ProcessedRootIDs = new NativeList<DataStoreID>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);

                prepareRunner = new Prepare()
                {
                    AllProcessedElements = AllProcessedElements,
                    DirtyNonRootElements = LayoutDirtiedIndices,
                    MovedRootIDs = MatrixDirtiedRootIDs,

                    UnderlyingRootCountsUnatomic = underlyingRootCountsUnatomic,
                    DirtyRootCounts = DirtyRootCounts,
                    DirtyRoots = ProcessedRootIDs,
                };
            }

            public void Dispose()
            {
                AutoLayoutTrackCache.Dispose();
                AutoLayoutRangeCache.Dispose();

                AllProcessedElements.Dispose();
                LayoutDirtiedIndices.Dispose();
                MatrixDirtiedRootIDs.Dispose();
                NeedsSecondPass.Dispose();
                ProcessingStack.Dispose();

                underlyingRootCountsUnatomic.Dispose();
                DirtyRootCounts.Dispose();
                ProcessedRootIDs.Dispose();
            }

            [BurstCompile]
            public struct Prepare : INovaJob
            {
                public NativeList<DataStoreIndex> AllProcessedElements;
                public NativeList<DataStoreID> MovedRootIDs;
                public NativeList<DataStoreIndex> DirtyNonRootElements;
                public NativeList<int> UnderlyingRootCountsUnatomic;
                public NativeList<UnsafeAtomicCounter32> DirtyRootCounts;
                public NativeList<DataStoreID> DirtyRoots;

                [ReadOnly]
                public NativeList<DataStoreID> BatchRootIDs;

                public unsafe void Execute()
                {
                    AllProcessedElements.Clear();
                    MovedRootIDs.Clear();
                    DirtyNonRootElements.Clear();

                    int batchRootCount = BatchRootIDs.Length;

                    UnderlyingRootCountsUnatomic.Length = batchRootCount;
                    DirtyRootCounts.Length = batchRootCount;
                    DirtyRoots.Length = batchRootCount;

                    int* unatomicPtr = UnderlyingRootCountsUnatomic.GetRawPtr();
                    for (int i = 0; i < batchRootCount; ++i)
                    {
                        DirtyRoots[i] = DataStoreID.Invalid;
                        UnderlyingRootCountsUnatomic[i] = 0;
                        DirtyRootCounts[i] = new UnsafeAtomicCounter32(unatomicPtr + i);
                    }
                }

                [BurstCompile]
                [MonoPInvokeCallback(typeof(BurstMethod))]
                public static unsafe void Execute(void* jobData)
                {
                    UnsafeUtility.AsRef<Prepare>(jobData).Execute();
                }
            }
        }
    }
}
