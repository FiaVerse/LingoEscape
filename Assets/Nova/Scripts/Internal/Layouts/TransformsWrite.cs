// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Nova.Internal.Layouts
{
    internal static partial class TransformSync
    {
        /// <summary>
        /// The job for writing layout-calculated positions to Transforms
        /// </summary>
        [BurstCompile]
        internal struct Write : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeList<float3> TransformPositions;
            [WriteOnly, NativeDisableParallelForRestriction]
            public NativeList<float4x4> LocalToWorldMatrices;
            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
            [ReadOnly]
            public NativeList<DataStoreIndex> PhysicalToSharedTransformIndexMap;
            [ReadOnly]
            public NativeList<TransformProxy> TransformProxies;
            [ReadOnly]
            public NativeList<HierarchyDependency> DirtyDependencies;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int phyiscalTransformIndex, TransformAccess transform)
            {
                DataStoreIndex transformIndex = PhysicalToSharedTransformIndexMap[phyiscalTransformIndex];

                if (!DirtyDependencies[transformIndex].IsDirty)
                {
                    return;
                }

                HierarchyElement element = Hierarchy[transformIndex];

                float3 localPosition = TransformPositions[transformIndex];

                if (HierarchyLookup.TryGetValue(element.ParentID, out DataStoreIndex transformParentIndex) && TransformProxies[transformParentIndex].IsVirtual)
                {
                    // if parent is virtual offset by parent position
                    localPosition += TransformPositions[transformParentIndex];
                }

                transform.localPosition = localPosition;

                if (!transformParentIndex.IsValid)
                {
                    LocalToWorldMatrices[transformIndex] = transform.localToWorldMatrix;
                }
            }
        }

        [BurstCompile]
        internal struct UpdateMatrices : INovaJob
        {
            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
            [ReadOnly]
            public NativeList<BatchGroupElement> BatchGroupElements;

            [ReadOnly]
            public NativeList<TransformProxy> TransformProxies;
            [ReadOnly]
            public NativeList<DataStoreIndex> DepthSortedHierarchy;

            [ReadOnly]
            public NativeList<quaternion> TransformRotations;
            [ReadOnly]
            public NativeList<float3> TransformScales;
            [ReadOnly]
            public NativeList<float3> TransformPositions;

            [WriteOnly]
            public NativeList<DataStoreIndex> DirtyIndices;
            [WriteOnly]
            public NativeList<DataStoreID> DirtyRootIDs;

            public NativeList<float4x4> WorldToLocalMatrices;
            public NativeList<float4x4> LocalToWorldMatrices;

            public NativeList<HierarchyDependency> DirtyDependencies;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute()
            {
                int numMatricesToUpdate = DepthSortedHierarchy.Length;

                for (int dirtyIndex = 0; dirtyIndex < numMatricesToUpdate; ++dirtyIndex)
                {
                    DataStoreIndex transformIndex = DepthSortedHierarchy[dirtyIndex];

                    // since this was updated, mark dirty as we go down depth levels
                    ref HierarchyDependency dependency = ref DirtyDependencies.ElementAt(transformIndex);
                    HierarchyElement element = Hierarchy[transformIndex];

                    if (!HierarchyLookup.TryGetValue(element.ParentID, out DataStoreIndex parentTransformIndex))
                    {
                        if (dependency.IsDirty)
                        {
                            WorldToLocalMatrices.ElementAt(transformIndex) = math.inverse(LocalToWorldMatrices.ElementAt(transformIndex));
                            DirtyRootIDs.Add(element.ID);

                            if (dependency == HierarchyDependency.MaxDependencies)
                            {
                                DirtyIndices.Add(transformIndex);
                            }
                        }

                        continue;
                    }

                    if (!dependency.IsDirty && !DirtyDependencies[parentTransformIndex].IsDirty)
                    {
                        continue;
                    }

                    ref float4x4 worldToParent = ref WorldToLocalMatrices.ElementAt(parentTransformIndex);
                    float4x4 parentToChild;

                    if (TransformProxies[transformIndex].IsVirtual)
                    {
                        // virtual nodes don't have their own rotations or scales
                        parentToChild = float4x4.Translate(-TransformPositions[transformIndex]);
                    }
                    else
                    {
                        float3 localScale = TransformScales[transformIndex];

                        float4x4 parentToChildScale = float4x4.Scale(math.select(math.rcp(localScale), Math.float3_Zero, localScale == Math.float3_Zero));
                        float4x4 parentToChildRotation = new float4x4(math.inverse(TransformRotations[transformIndex]), Math.float3_Zero);
                        float4x4 parentToChildPosition = float4x4.Translate(-TransformPositions[transformIndex]);

                        // math.inverse will fail in this context when localScale == float3.zero, so instead we invert the TRS 
                        // components independently, ensure valid/finite scale values, and reconstruct the inverse matrix on our own. 
                        parentToChild = math.mul(parentToChildScale, math.mul(parentToChildRotation, parentToChildPosition));
                    }

                    float4x4 worldToLocal = math.mul(parentToChild, worldToParent);

                    WorldToLocalMatrices.ElementAt(transformIndex) = worldToLocal;
                    LocalToWorldMatrices.ElementAt(transformIndex) = math.inverse(worldToLocal);

                    dependency = HierarchyDependency.Max(dependency, HierarchyDependency.Self);

                    bool isNonHierarchyBatchRoot = BatchGroupElements[transformIndex].BatchRootID == element.ID;

                    if (isNonHierarchyBatchRoot)
                    {
                        DirtyRootIDs.Add(element.ID);
                    }

                    if (dependency.HasDirectDependencies)
                    {
                        DirtyIndices.Add(transformIndex);
                    }
                }
            }
        }
    }
}
