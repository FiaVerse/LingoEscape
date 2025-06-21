// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Nova.Internal.Layouts
{
    internal static partial class TransformSync
    {
        [BurstCompile]
        internal unsafe struct Read : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            [ReadOnly]
            public NativeList<bool> UseRotations;
            [ReadOnly]
            public NativeList<DataStoreIndex> PhysicalToSharedIndexMap;
            [ReadOnly]
            public NativeList<TransformProxy> TransformProxies;
            [ReadOnly]
            public NativeBitList.ReadOnly ReceivedFullUpdate;

            [NativeDisableParallelForRestriction]
            public NativeList<quaternion> LocalRotations;
            [NativeDisableParallelForRestriction]
            public NativeList<float3> LocalPositions;
            [NativeDisableParallelForRestriction]
            public NativeList<float3> LocalScales;
            [NativeDisableParallelForRestriction]
            public NativeList<float4x4> WorldToLocalMatrices;
            [NativeDisableParallelForRestriction]
            public NativeList<float4x4> LocalToWorldMatrices;
            [NativeDisableParallelForRestriction]
            public NativeList<HierarchyDependency> DirtyDependencies;
            [NativeDisableParallelForRestriction]
            public NativeList<bool> UsingTransformPositions;

            [NativeDisableParallelForRestriction]
            public NativeList<Length3> LayoutLengths;
            [NativeDisableParallelForRestriction]
            public NativeList<Length3.MinMax> LayoutLengthRanges;
            [NativeDisableParallelForRestriction]
            public NativeList<Length3.Calculated> CalculatedLengths;

            [NativeDisableParallelForRestriction]
            public NativeList<float3> Alignments;

            [NativeDisableContainerSafetyRestriction]
            public NativeReference<UnsafeAtomicCounter32> DirtyCount;

            [NativeDisableContainerSafetyRestriction]
            public NativeReference<UnsafeAtomicCounter32> DirtyTransformCount;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int physicalTransformChunkIndex, TransformAccess transform)
            {
                DataStoreIndex transformIndex = PhysicalToSharedIndexMap[physicalTransformChunkIndex];

                DataStoreID parentID = Hierarchy[transformIndex].ParentID;
                DataStoreIndex parentIndex = parentID.IsValid ? HierarchyLookup[parentID] : DataStoreIndex.Invalid;
                bool virtualParent = parentIndex.IsValid && TransformProxies[parentIndex].IsVirtual;

                // translation
                float3 newPosition = transform.localPosition;
                float3 oldPosition = virtualParent ? LocalPositions[parentIndex] + LocalPositions[transformIndex] : LocalPositions[transformIndex];
                bool positionChanged = !math.all(Math.ApproximatelyEqual3(ref oldPosition, ref newPosition));

                // rotation
                quaternion newRotation = transform.localRotation;
                ref quaternion currentRotation = ref LocalRotations.ElementAt(transformIndex);
                bool rotationChanged = !math.all(Math.ApproximatelyEqual4(ref currentRotation.value, ref newRotation.value));

                // scale
                float3 newScale = transform.localScale;
                ref float3 currentScale = ref LocalScales.ElementAt(transformIndex);
                bool scaleChanged = !math.all(Math.ApproximatelyEqual3(ref currentScale, ref newScale));

                // diff
                HierarchyDependency dirtyDependency = positionChanged || rotationChanged || scaleChanged ? HierarchyDependency.Parent : HierarchyDependency.None;

                if (!parentIndex.IsValid)
                {
                    ref float4x4 rootLocalToWorld = ref LocalToWorldMatrices.ElementAt(transformIndex);
                    float4x4 newLocalToWorld = transform.localToWorldMatrix;

                    if (rotationChanged || scaleChanged || !newLocalToWorld.Equals(rootLocalToWorld))
                    {
                        rootLocalToWorld = newLocalToWorld;
                        WorldToLocalMatrices.ElementAt(transformIndex) = math.inverse(rootLocalToWorld);

                        dirtyDependency = HierarchyDependency.Max(dirtyDependency, HierarchyDependency.Self);
                    }
                }

                if (positionChanged && !virtualParent && ReceivedFullUpdate[transformIndex]) // only process when parent is not virtual and after the first registered frame
                {
                    DirtyTransformCount.GetRawPtrWithoutChecks()->Add(1);
                    UsingTransformPositions[transformIndex] = true;
                    LocalPositions[transformIndex] = newPosition;
                }

                if (rotationChanged)
                {
                    currentRotation = newRotation;
                }

                if (scaleChanged)
                {
                    currentScale = newScale;
                }

                if (dirtyDependency.IsDirty)
                {
                    ref HierarchyDependency currentDependency = ref DirtyDependencies.ElementAt(transformIndex);

                    currentDependency = HierarchyDependency.Max(currentDependency, dirtyDependency);

                    DirtyCount.GetRawPtrWithoutChecks()->Add(1);
                }
            }
        }
    }
}
