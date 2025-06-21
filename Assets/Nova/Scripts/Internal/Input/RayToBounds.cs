// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Input
{
    internal interface IHit
    {
        DataStoreID ID { get; }
        float3 HitPoint { get; }
        float3 Normal { get; }
        float Proximity { get; }
    }

    internal struct PointerHit : IComparer<PointerHit>, IHit
    {
        public DataStoreID ID;
        public float Distance;
        public float3 HitPoint;
        public float3 Normal;
        public RenderOrder RenderOrder;

        DataStoreID IHit.ID => ID;
        float3 IHit.HitPoint => HitPoint;
        float3 IHit.Normal => Normal;
        float IHit.Proximity => Distance;

        public int Compare(PointerHit x, PointerHit y)
        {
            // For draw order, higher == drawn on top and renderqueue takes priority
            if (x.RenderOrder.RenderQueue != y.RenderOrder.RenderQueue || Math.ApproximatelyEqual(x.Distance, y.Distance))
            {
                return y.RenderOrder.CompareTo(x.RenderOrder);
            }
            else
            {
                return x.Distance.CompareTo(y.Distance);
            }
        }
    }

    internal struct VisualModifiers
    {
        [ReadOnly]
        public NativeList<DataStoreIndex, VisualModifierID> VisualModifierIDs;
        [ReadOnly]
        public NativeList<VisualModifierID, DataStoreID> ModifierToBlockID;
        [ReadOnly]
        public NativeList<VisualModifierID, VisualModifierShaderData> ShaderData;
        [ReadOnly]
        public NativeList<VisualModifierID, ClipMaskInfo> ClipInfo;
    }

    [BurstCompile]
    internal unsafe struct RayToBounds : ICollisionTest<StructuredRay, PointerHit>
    {
        public UnityEngine.Ray Ray;
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

        public bool IncludeInvisibleContent;

        [NativeDisableUnsafePtrRestriction]
        private float4x4* worldToLocalPtr;
        [NativeDisableUnsafePtrRestriction]
        private float4x4* localToWorldPtr;

        private float3 zeroOffset;

        private StructuredRay worldRay;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructuredRay Init()
        {
            worldRay = new StructuredRay(Ray);
            worldToLocalPtr = WorldToLocalMatrices.GetRawReadonlyPtr();
            localToWorldPtr = LocalToWorldMatrices.GetRawReadonlyPtr();
            zeroOffset = Math.float3_Zero;

            return worldRay;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpatialPartitionMask GetCollisionMask(ref StructuredRay ray, DataStoreIndex index)
        {
            SpatialPartitionMask mask = SpatialPartitionMask.Full;

            Math.ToFloat3x3(Sizes[index], out float3x3 size);
            Math.ToFloat3x3(Offsets[index], out float3x3 center);

            int axes = size.c1.z == 0 ? 2 : 3;

            for (int i = 0; i < axes; ++i)
            {
                bool2 intersections = Math.IntersectsBoundsHalves(ref ray.Origin3x3, ref ray.RcpDirection3x3, ref center, ref size, axis: i);
                mask.SetAxis(axisIndex: i, positiveHalf: false, value: intersections.x);
                mask.SetAxis(axisIndex: i, positiveHalf: true, value: intersections.y);
            }

            return mask;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithMesh(ref StructuredRay ray, DataStoreIndex index, DataStoreID id, out PointerHit hit)
        {
            hit = default;

            int objectLayer = RenderOrderCalculator.BaseInfos[index].Val.GameObjectLayer;

            if (((1 << objectLayer) & LayerMask) == 0)
            {
                return false;
            }

            float3 size = LayoutAccess.Get(index, ref LengthProperties).Size.Value;

            bool intersectsMesh = Math.IntersectsBounds(ref ray.Origin3x2, ref ray.RcpDirection3x2, ref zeroOffset, ref size, out float localSpaceDistance);
            bool intersectsUnclippedMesh = intersectsMesh;

            float worldSpaceDistance = localSpaceDistance;
            float3 hitInWorldSpace = Math.float3_NaN;
            float3 normalInWorldSpace = Math.float3_NaN;

            if (intersectsMesh)
            {
                float4x4 localToWorld = *(localToWorldPtr + index);
                float3 hitInLocalSpace = ray.GetPoint(localSpaceDistance);
                float3 normalInLocalSpace = UIBounds.NormalOnBounds(ref size, ref hitInLocalSpace);
                hitInWorldSpace = math.transform(localToWorld, hitInLocalSpace);
                normalInWorldSpace = math.normalize(math.rotate(localToWorld, normalInLocalSpace));
                worldSpaceDistance = math.length(worldRay.Origin - hitInWorldSpace);

                if (!IncludeInvisibleContent)
                {
                    intersectsUnclippedMesh = IntersectsUnclippedBounds(index, VisualModifierID.Invalid, ref hitInWorldSpace, ref VisualModifiers, ref HierarchyLookup, ref LengthProperties, worldToLocalPtr);
                }
            }

            if (intersectsUnclippedMesh)
            {
                hit.ID = id;
                hit.RenderOrder = RenderOrderCalculator.GetRenderOrder(index);
                hit.HitPoint = hitInWorldSpace;
                hit.Normal = normalInWorldSpace;
                hit.Distance = worldSpaceDistance;
            }

            return intersectsUnclippedMesh;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithContent(ref StructuredRay ray, DataStoreIndex index, DataStoreID id, out StructuredRay rayInLocalSpace)
        {
            float3 size = Sizes[index];
            float3 center = Offsets[index];

            float3 rayOriginInLocalSpace = math.transform(*(worldToLocalPtr + index), ray.Origin);
            float3 rayDirInLocalSpace = math.normalize(math.rotate(*(worldToLocalPtr + index), ray.Direction));

            if (Math.CanIntersectBounds(ref rayOriginInLocalSpace, ref rayDirInLocalSpace, ref center, ref size))
            {
                rayInLocalSpace = new StructuredRay(rayOriginInLocalSpace, rayDirInLocalSpace);
                return Math.IntersectsBounds(ref rayInLocalSpace.Origin3x2, ref rayInLocalSpace.RcpDirection3x2, ref center, ref size, out _);
            }

            rayInLocalSpace = default;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransformCollidable(ref StructuredRay ray, DataStoreIndex index, out StructuredRay rayInLocalSpace)
        {
            float3 rayOriginInLocalSpace = math.transform(*(worldToLocalPtr + index), ray.Origin);
            float3 rayDirInLocalSpace = math.normalize(math.rotate(*(worldToLocalPtr + index), ray.Direction));
            rayInLocalSpace = new StructuredRay(rayOriginInLocalSpace, rayDirInLocalSpace);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool IntersectsUnclippedBounds(DataStoreIndex index, VisualModifierID topLevelModifier, ref float3 hitInWorldSpace, ref VisualModifiers visualModifiers, ref NovaHashMap<DataStoreID, DataStoreIndex> hierarchy, ref NativeList<Length3.Calculated> lengths, float4x4* worldToLocalPtr)
        {
            VisualModifierID modifierID = visualModifiers.VisualModifierIDs[index];

            if (!modifierID.IsValid)
            {
                return true;
            }

            VisualModifierShaderData shaderData = visualModifiers.ShaderData[modifierID];
            
            for (int i = 0; i < shaderData.Count; ++i)
            {
                VisualModifierID nestedModifierID = shaderData.ModifierIDs[i];

                if (nestedModifierID == topLevelModifier)
                {
                    break;
                }

                if (!visualModifiers.ClipInfo[nestedModifierID].Clip)
                {
                    continue;
                }

                DataStoreIndex modifierIndex = hierarchy[visualModifiers.ModifierToBlockID[nestedModifierID]];

                float2 hitInClipSpace = math.transform(*(worldToLocalPtr + modifierIndex), hitInWorldSpace).xy;
                float2 extentsInClipSpace = LayoutAccess.Get(modifierIndex, ref lengths).Size.Value.xy * Math.float2_Half;

                if (!WithinBounds(hitInClipSpace, extentsInClipSpace))
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WithinBounds(float2 localSpacePosition, float2 extents)
        {
            float2 max = extents;
            float2 min = -max;

            // Using these utilities instead of operators because was
            // running into some imperceptible comparison issues with Navigation.
            bool2 withinMin = Math.ApproximatelyGreaterThan(ref localSpacePosition, ref min);
            bool2 withinMax = Math.ApproximatelyLessThan(ref localSpacePosition, ref max);

            return math.all(new bool4(withinMin, withinMax));
        }
    }
}
