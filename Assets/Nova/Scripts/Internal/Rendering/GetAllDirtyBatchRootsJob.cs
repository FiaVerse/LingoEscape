// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct GetAllDirtyBatchRootsJob : INovaJob
    {
        public bool DirtyEverything;

        [ReadOnly]
        public NativeList<DataStoreID> HierarchyDirtiedRoots;
        [ReadOnly]
        public NativeList<DataStoreID> RenderingDirtiedRoots;
        [ReadOnly]
        public NativeList<DataStoreIndex> LayoutDirtiedElements;
        [ReadOnly]
        public NativeList<DataStoreID> CurrentBatchGroups;
        [ReadOnly]
        public NativeList<BatchGroupElement> BatchGroupElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [ReadOnly]
        public NativeList<DataStoreID> MatrixDirtiedRoots;
        [ReadOnly]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public NativeList<DataStoreIndex, ComputeBufferIndex> TransformIndices;

        public ComputeBufferReadOnlyAccess<TransformAndLightingData> RootFromNodeTransforms;


        public NativeList<DataStoreID> AllDirtiedRoots;

        public void Execute()
        {
            UpdateDirtyRoots();
        }

        private void UpdateDirtyRoots()
        {
            AllDirtiedRoots.Clear();

            if (DirtyEverything)
            {
                // Dirty all roots
                DirtyRange(ref CurrentBatchGroups);
            }
            else
            {
                DirtyRange(ref HierarchyDirtiedRoots);
                DirtyRange(ref RenderingDirtiedRoots);

                // Normally, when only the transform of a root has changed, we don't reprocess it. However,
                // in the case where the scale goes from zero to non-zero, we do need to reprocess it since
                // all of the bounds for the draw calls and whatnot will have a size of zero. This does that check
                for (int i = 0; i < MatrixDirtiedRoots.Length; ++i)
                {
                    DataStoreID id = MatrixDirtiedRoots[i];
                    if (!ShouldAddBatchRoot(id))
                    {
                        continue;
                    }

                    DataStoreIndex index = DataStoreIDToDataStoreIndex[id];

                    float4x4 worldFromLocal = WorldFromLocalMatrices[index];
                    float3 currentScale = Math.Scale(ref worldFromLocal);
                    if (math.any(currentScale == float3.zero))
                    {
                        // Scale is currently zero, don't mark dirty
                        continue;
                    }

                    float4x4 previousRootFromBlock = RootFromNodeTransforms[TransformIndices[index]].RootFromBlock;
                    float3 previousScale = Math.Scale(ref previousRootFromBlock);
                    if (math.any(math.isnan(previousScale) | previousScale == float3.zero))
                    {
                        // If the previous scale was NAN or zero, we need to mark this whole
                        // batch as dirty
                        AllDirtiedRoots.Add(id);
                    }
                }

                for (int i = 0; i < LayoutDirtiedElements.Length; ++i)
                {
                    BatchGroupElement batchGroupElement = BatchGroupElements[LayoutDirtiedElements[i]];
                    if (!ShouldAddBatchRoot(batchGroupElement.BatchRootID))
                    {
                        continue;
                    }
                    AllDirtiedRoots.Add(batchGroupElement.BatchRootID);
                }
            }
        }

        private void DirtyRange(ref NativeList<DataStoreID> range)
        {
            for (int i = 0; i < range.Length; ++i)
            {
                if (!ShouldAddBatchRoot(range[i]))
                {
                    continue;
                }
                AllDirtiedRoots.Add(range[i]);
            }
        }

        private bool ShouldAddBatchRoot(DataStoreID batchRootID)
        {
            if (AllDirtiedRoots.AsArray().Contains(batchRootID) || !CurrentBatchGroups.AsArray().Contains(batchRootID))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}

