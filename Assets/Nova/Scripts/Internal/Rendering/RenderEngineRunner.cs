// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct RenderEngineRunner : IInitializable
    {
        public NativeList<DataStoreID> DirtyRoots;
        public NovaHashMap<DataStoreID, int> TrackedRootIDs;

        public BatchGroupDataStore BatchGroupData;
        public CameraSorting.Runner CameraSorter;

        public NativeList<DataStoreID> RemovedBatchRoots;
        public NativeList<DataStoreID> AddedBatchRoots;

        private void EnsureBatchRoots()
        {
            RemoveDestroyedBatchRoots();
            AddNewBatchRoots();
        }

        private void AddNewBatchRoots()
        {
            AddedBatchRoots.Clear();

            for (int i = 0; i < DirtyRoots.Length; ++i)
            {
                DataStoreID dirtyRootID = DirtyRoots[i];
                if (BatchGroupData.Contains(dirtyRootID))
                {
                    continue;
                }

                AddedBatchRoots.Add(dirtyRootID);
                BatchGroupData.Add(dirtyRootID);
                CameraSorter.AddBatchGroup(dirtyRootID);
            }
        }

        private void RemoveDestroyedBatchRoots()
        {
            RemovedBatchRoots.Clear();

            for (int i = 0; i < BatchGroupData.KnownBatchRoots.Length; ++i)
            {
                DataStoreID dataStoreID = BatchGroupData.KnownBatchRoots[i];
                if (!TrackedRootIDs.ContainsKey(dataStoreID))
                {
                    RemovedBatchRoots.Add(dataStoreID);
                }
            }

            for (int i = 0; i < RemovedBatchRoots.Length; ++i)
            {
                DataStoreID dataStoreID = RemovedBatchRoots[i];
                BatchGroupData.Remove(dataStoreID);
                CameraSorter.RemoveBatchGroup(dataStoreID);
            }
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void EnsureBatchRoots(void* data)
        {
            UnsafeUtility.AsRef<RenderEngineRunner>(data).EnsureBatchRoots();
        }

        public void Init()
        {
            RemovedBatchRoots.Init();
            AddedBatchRoots.Init();
        }

        public void Dispose()
        {
            RemovedBatchRoots.Dispose();
            AddedBatchRoots.Dispose();
        }
    }
}

