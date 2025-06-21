// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct RootFromBlockMatrixJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatchRoots;
        [ReadOnly]
        public NativeList<HierarchyDependency> DirtyDependencies;
        [ReadOnly]
        public NativeList<BatchGroupElement> BatchGroupElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> LocalFromWorldMatrices;
        [ReadOnly]
        public NativeList<DataStoreIndex, ComputeBufferIndex> TransformIndices;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float3> TransformLocalPositions;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<quaternion> TransformLocalRotations;
        [NativeDisableParallelForRestriction]
        public NativeList<float4x4> WorldFromLocalMatrices;

        [NativeDisableParallelForRestriction]
        public ComputeBufferAccess<TransformAndLightingData> RootFromNodeTransforms;

        public NativeList<DataStoreIndex>.ParallelWriter PotentialCoplanarSetRoots;
        public NativeList<DataStoreIndex>.ParallelWriter PotentialRotationSetRoots;

        public void Execute(int index)
        {
            DataStoreIndex dataStoreIndex = index;
            BatchGroupElement batchGroupElement = BatchGroupElements[dataStoreIndex];

            HierarchyDependency hierarchyDependency = DirtyDependencies[dataStoreIndex];
            if (hierarchyDependency.IsDirty)
            {
                // Update worldFromLocal
                ref float4x4 worldFromLocal = ref WorldFromLocalMatrices.ElementAt(dataStoreIndex);
                DataStoreIndex batchRootDataStoreIndex = DataStoreIDToDataStoreIndex[batchGroupElement.BatchRootID];
                ref float4x4 batchRootFromWorld = ref LocalFromWorldMatrices.ElementAt(batchRootDataStoreIndex);
                RootFromNodeTransforms.ElementAt(TransformIndices[dataStoreIndex]).RootFromBlock = math.mul(batchRootFromWorld, worldFromLocal);
            }

            // Check if coplanar or rotation root
            if (!DirtyBatchRoots.AsArray().Contains(batchGroupElement.BatchRootID))
            {
                // No need to process non-dirty batch groups
                return;
            }

            ref float3 localPos = ref TransformLocalPositions.ElementAt(dataStoreIndex);
            ref quaternion localRotation = ref TransformLocalRotations.ElementAt(dataStoreIndex);

            if (!Math.ApproximatelyZero(localPos.z) || !localRotation.RotationMaintainsCoplanarity())
            {
                PotentialCoplanarSetRoots.AddNoResize(dataStoreIndex);
                PotentialRotationSetRoots.AddNoResize(dataStoreIndex);
                return;
            }

            if (!Math.ApproximatelyIdentity(ref localRotation))
            {
                PotentialRotationSetRoots.AddNoResize(dataStoreIndex);
                return;
            }
        }
    }
}
