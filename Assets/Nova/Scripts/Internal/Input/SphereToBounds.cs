// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Input
{
    internal struct StructuredSphere
    {
        public float3 Position;
        public float Radius;
    }

    [BurstCompile]
    internal unsafe struct SphereToBounds : ICollisionTest<StructuredSphere, PointerHit>
    {
        public StructuredSphere Sphere;
        public int LayerMask;

        public VisualModifiers VisualModifiers;

        [ReadOnly]
        public RenderOrderCalculator RenderOrderCalculator;

        [ReadOnly]
        public NativeList<Length3.Calculated> LengthProperties;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

        [ReadOnly]
        public NativeList<float3> Sizes;
        [ReadOnly]
        public NativeList<float3> Offsets;
        [ReadOnly]
        public NativeList<float4x4> WorldToLocalMatrices;
        [ReadOnly]
        public NativeList<float4x4> LocalToWorldMatrices;

        [NativeDisableUnsafePtrRestriction]
        private float4x4* worldToLocalPtr;
        [NativeDisableUnsafePtrRestriction]
        private float4x4* localToWorldPtr;

        private float3 zeroOffset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructuredSphere Init()
        {
            worldToLocalPtr = WorldToLocalMatrices.GetRawReadonlyPtr();
            localToWorldPtr = LocalToWorldMatrices.GetRawReadonlyPtr();
            zeroOffset = Math.float3_Zero;

            return Sphere;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpatialPartitionMask GetCollisionMask(ref StructuredSphere colliderLocalSpace, DataStoreIndex index)
        {
            SpatialPartitionMask mask = SpatialPartitionMask.Empty;

            float3 extents = Sizes[index] * 0.5f;
            float3 halfExtents = extents * 0.5f;
            float3 center = Offsets[index];

            int increment = extents.z == 0 ? 2 : 1;

            for (int i = 0; i < SpatialPartitionMask.OctantCount; i += increment)
            {
                float3 octant = SpatialPartitionMask.Octants[i];
                UIBounds bounds = new UIBounds(center + (halfExtents * octant), extents);
                mask[i] = bounds.Contains(colliderLocalSpace.Position, colliderLocalSpace.Radius);
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithMesh(ref StructuredSphere colliderLocalSpace, DataStoreIndex index, DataStoreID id, out PointerHit hit)
        {
            hit = default;

            int objectLayer = RenderOrderCalculator.BaseInfos[index].Val.GameObjectLayer;

            if (((1 << objectLayer) & LayerMask) == 0)
            {
                return false;
            }

            UIBounds bounds = new UIBounds(LayoutAccess.Get(index, ref LengthProperties).Size.Value);

            bool intersectsMesh = bounds.Contains(colliderLocalSpace.Position, colliderLocalSpace.Radius);
            bool intersectsClipMesh = intersectsMesh;

            float worldSpaceDistance = float.MaxValue;
            float3 hitInWorldSpace = Math.float3_NaN;
            float3 normalInWorldSpace = Math.float3_NaN;

            if (intersectsMesh)
            {
                float4x4 localToWorld = *(localToWorldPtr + index);
                float3 hitInLocalSpace = bounds.ClosestPoint(colliderLocalSpace.Position);
                float3 normalInLocalSpace = UIBounds.NormalOnBounds(ref bounds, ref hitInLocalSpace);
                hitInWorldSpace = math.transform(localToWorld, hitInLocalSpace);
                normalInWorldSpace = math.normalize(math.rotate(localToWorld, normalInLocalSpace));
                worldSpaceDistance = math.length(Sphere.Position - hitInWorldSpace);

                intersectsClipMesh = RayToBounds.IntersectsUnclippedBounds(index, VisualModifierID.Invalid, ref hitInWorldSpace, ref VisualModifiers, ref HierarchyLookup, ref LengthProperties, worldToLocalPtr);
            }

            if (intersectsClipMesh)
            {
                hit.ID = id;
                hit.RenderOrder = RenderOrderCalculator.GetRenderOrder(index);
                hit.HitPoint = hitInWorldSpace;
                hit.Normal = normalInWorldSpace;
                hit.Distance = worldSpaceDistance;
            }

            return intersectsClipMesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithContent(ref StructuredSphere colliderWorldSpace, DataStoreIndex index, DataStoreID id, out StructuredSphere colliderLocalSpace)
        {
            UIBounds bounds = new UIBounds(Offsets[index], Sizes[index]);

            float3 colliderCenterInLocalSpace = math.transform(*(worldToLocalPtr + index), colliderWorldSpace.Position);
            float radiusInLocalSpace = colliderWorldSpace.Radius * math.cmin(Math.Scale(ref UnsafeUtility.AsRef<float4x4>(worldToLocalPtr + index)));

            if (bounds.Contains(colliderCenterInLocalSpace, radiusInLocalSpace))
            {
                colliderLocalSpace = new StructuredSphere() { Position = colliderCenterInLocalSpace, Radius = radiusInLocalSpace };
                return true;
            }

            colliderLocalSpace = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransformCollidable(ref StructuredSphere colliderWorldSpace, DataStoreIndex index, out StructuredSphere colliderLocalSpace)
        {
            float3 colliderCenterInLocalSpace = math.transform(*(worldToLocalPtr + index), colliderWorldSpace.Position);
            float radiusInLocalSpace = colliderWorldSpace.Radius * math.cmin(Math.Scale(ref UnsafeUtility.AsRef<float4x4>(worldToLocalPtr + index)));

            colliderLocalSpace = new StructuredSphere() { Position = colliderCenterInLocalSpace, Radius = radiusInLocalSpace };
        }
    }
}
