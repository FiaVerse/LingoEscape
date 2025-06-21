// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Unity.Burst;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct VisualElementCountJob : INovaJob
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatchRoots;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, RotationSetSummary> RotationSets;

        [WriteOnly]
        public NativeReference<RenderEngineUpdateCounts> VisualElementCount;

        public void Execute()
        {
            RenderEngineUpdateCounts counts = default;
            for (int i = 0; i < DirtyBatchRoots.Length; ++i)
            {
                DataStoreID batchRootID = DirtyBatchRoots[i];
                counts.VisualElementCount += VisualElements[batchRootID].Length;

                RotationSetSummary rotationSet = RotationSets[batchRootID];
                counts.RotationSetCount += rotationSet.SetCount;
                counts.QuadProviderCount += rotationSet.QuadProviderCount;
            }
            VisualElementCount.Value = counts;
        }
    }
}

