// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Input
{
    internal struct EdgeHit : IComparer<EdgeHit>
    {
        public bool ParentSpace;
        public float HitDistance;
        public float DistanceToHitBounds;
        public float3 HitPointWorldSpace;
        public float3 HitBoundsCenter;
        public float3 HitBoundsSize;
        public DataStoreID ID;
        public int HierarchyDepthLevel;

        public int Compare(EdgeHit x, EdgeHit y)
        {
            float xBoundsDist = Math.Abs(x.DistanceToHitBounds);
            float yBoundsDist = Math.Abs(y.DistanceToHitBounds);

            float xHitDist = Math.Abs(x.HitDistance);
            float yHitDist = Math.Abs(y.HitDistance);

            if (Math.Abs(xHitDist - yHitDist) < Math.Epsilon)
            {
                if (Math.Abs(xBoundsDist - yBoundsDist) < Math.Epsilon)
                {
                    return x.HierarchyDepthLevel.CompareTo(y.HierarchyDepthLevel);
                }

                return xBoundsDist.CompareTo(yBoundsDist);
            }

            return xHitDist.CompareTo(yHitDist);
        }

        public override string ToString()
        {
            return $"{ID}, {HierarchyDepthLevel}, Hit: {HitDistance.ToString("F5")}, Edge: {DistanceToHitBounds.ToString("F5")}";
        }
    }

    [BurstCompile]
    internal unsafe struct RayToEdge : ICollisionTest<StructuredRay, EdgeHit>
    {
        public const float Precision = 100;

        [ReadOnly]
        public NativeList<Length3.Calculated> LengthProperties;
        [ReadOnly]
        public NativeList<float3> Sizes;
        [ReadOnly]
        public NativeList<float3> Offsets;
        [ReadOnly]
        public NativeList<float4x4> WorldToLocalMatrices;
        [ReadOnly]
        public NativeList<float4x4> LocalToWorldMatrices;
        [ReadOnly]
        public NativeList<HierarchyElement> Hierarchy;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
        [ReadOnly]
        public NativeList<BatchGroupElement> BatchGroupElements;

        [ReadOnly]
        public NativeList<quaternion> TransformLocalRotations;
        [ReadOnly]
        public NativeList<float3> TransformLocalPositions;
        [ReadOnly]
        public NativeList<float3> TransformLocalScales;
        [ReadOnly]
        public NativeList<bool> UseRotations;

        [ReadOnly]
        public NovaHashMap<DataStoreID, bool> FilterRootIDs;

        public Ray Ray;
        public float4x4 WorldToViewport;

        [NativeDisableUnsafePtrRestriction]
        private float4x4* worldToLocalPtr;
        [NativeDisableUnsafePtrRestriction]
        private float4x4* localToWorldPtr;

        private float3 emptySize;
        private float3 localSpaceOrigin;

        private float4x4 localToWorld;
        private float4x4 worldToLocal;

        private float3 rotatedSize;
        private float3 worldSize;
        private float3 localPosition;
        private quaternion localRotation;

        private bool hasParent;
        private DataStoreIndex parentIndex;
        private float4x4 parentLocalToWorld;

        private StructuredRay rayInWorldSpace;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructuredRay Init()
        {
            rayInWorldSpace = new StructuredRay(Ray);

            // just being explicit
            emptySize = Math.float3_Zero;
            localSpaceOrigin = Math.float3_Zero;

            worldToLocalPtr = WorldToLocalMatrices.GetRawReadonlyPtr();
            localToWorldPtr = LocalToWorldMatrices.GetRawReadonlyPtr();

            return rayInWorldSpace;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpatialPartitionMask GetCollisionMask(ref StructuredRay ray, DataStoreIndex index)
        {
            return SpatialPartitionMask.Full;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithMesh(ref StructuredRay ray, DataStoreIndex index, DataStoreID id, out EdgeHit hit)
        {
            hit = default;

            if (FilterRootIDs.ContainsKey(id))
            {
                return false;
            }

            LayoutAccess.Calculated layout = LayoutAccess.Get(index, ref LengthProperties);

            float3 worldSpaceExtents = 0.5f * worldSize;
            float3 worldSpaceCenter = localToWorld.c3.xyz;

            if (!Math.OverlapsViewport(ref WorldToViewport, worldSpaceCenter - worldSpaceExtents, worldSpaceCenter + worldSpaceExtents))
            {
                return false;
            }

            hit.HierarchyDepthLevel = DepthLevel.Invalid;

            float distance = float.MaxValue;
            float3 hitPointLocalSpace = Math.float3_Zero;
            float distanceToHitBounds = float.MaxValue;
            bool intersects = false;

            float4x4 parentLocalToWorld = float4x4.identity;

            float3 size = layout.Size.Value;
            float3 center = localSpaceOrigin;

            float3 intersectionCenter = center;

            // Test required intersections in local space
            if (Math.IntersectsBoundsPlanes(ref ray.Origin3x2, ref ray.RcpDirection3x2, ref center, ref size, out float distanceToSize))
            {
                intersects = true;
                distance = distanceToSize;

                hitPointLocalSpace = ray.GetPoint(distance);

                distanceToHitBounds = GetDistanceFromHitToBounds(ref localToWorld, center, size, hitPointLocalSpace);

                hit.HitBoundsSize = size;
            }

            if (Math.IntersectsBoundsPlanes(ref ray.Origin3x2, ref ray.RcpDirection3x2, ref center, ref emptySize, out float distanceToCenter))
            {
                intersects = true;

                float dist = distance;
                distance = Math.MinAbs(distanceToCenter, distance);

                if (dist != distance)
                {
                    hitPointLocalSpace = ray.GetPoint(distance);

                    distanceToHitBounds = GetDistanceFromHitToBounds(ref localToWorld, center, emptySize, hitPointLocalSpace);
                }
            }

            if (!layout.Padding.Size.Equals(Math.float3_Zero))
            {
                // padded size/offset
                float3 paddedCenter = center + layout.Padding.Offset;
                float3 paddedSize = size - layout.Padding.Size;

                if (Math.IntersectsBoundsPlanes(ref ray.Origin3x2, ref ray.RcpDirection3x2, ref paddedCenter, ref paddedSize, out float distanceToPadding))
                {
                    intersects = true;

                    float dist = distance;
                    distance = Math.MinAbs(distanceToPadding, distance);

                    if (dist != distance)
                    {
                        hitPointLocalSpace = ray.GetPoint(distance);

                        distanceToHitBounds = GetDistanceFromHitToBounds(ref localToWorld, center, size, hitPointLocalSpace);

                        intersectionCenter = paddedCenter;
                        hit.HitBoundsSize = paddedSize;
                    }
                }
            }

            // move ray into parent space if it exists and is applicable
            bool testingInParentSpace = math.any(rotatedSize != size) && hasParent;

            // test required intersections in parent space
            if (testingInParentSpace)
            {
                TransformRay(ref rayInWorldSpace, ref UnsafeUtility.AsRef<float4x4>(worldToLocalPtr + parentIndex), out ray);

                center = TransformLocalPositions[index];

                float3 scaledSize = rotatedSize * TransformLocalScales[index];

                if (Math.IntersectsBoundsPlanes(ref ray.Origin3x2, ref ray.RcpDirection3x2, ref center, ref scaledSize, out float distanceToRotatedSize))
                {
                    intersects = true;

                    float dist = distance;
                    distance = Math.MinAbs(distanceToRotatedSize, distance);

                    if (dist != distance)
                    {
                        hitPointLocalSpace = ray.GetPoint(distance);

                        distanceToHitBounds = GetDistanceFromHitToBounds(ref parentLocalToWorld, center, scaledSize, hitPointLocalSpace);

                        hit.ParentSpace = true;
                        intersectionCenter = center;
                        hit.HitBoundsSize = scaledSize;
                    }
                }
            }

            if (!layout.Margin.Size.Equals(Math.float3_Zero))
            {
                float3 scaledLayoutSize = rotatedSize * TransformLocalScales[index] + layout.Margin.Size;
                float3 marginCenter = center - layout.Margin.Offset;

                // margin size/offset
                if (Math.IntersectsBoundsPlanes(ref ray.Origin3x2, ref ray.RcpDirection3x2, ref marginCenter, ref scaledLayoutSize, out float distanceToMargin))
                {
                    intersects = true;

                    float dist = distance;
                    distance = Math.MinAbs(distanceToMargin, distance);

                    if (dist != distance)
                    {
                        hitPointLocalSpace = ray.GetPoint(distance);

                        distanceToHitBounds = GetDistanceFromHitToBounds(ref parentLocalToWorld, marginCenter, scaledLayoutSize, hitPointLocalSpace);

                        hit.ParentSpace = testingInParentSpace;
                        intersectionCenter = marginCenter;
                        hit.HitBoundsSize = scaledLayoutSize;
                    }
                }
            }

            if (intersects)
            {
                float3 hitPointWorldSpace = math.transform(hit.ParentSpace ? parentLocalToWorld : localToWorld, math.round(hitPointLocalSpace * Precision) / Precision);

                hit.ID = id;

                hit.HitDistance = math.length(hitPointWorldSpace - rayInWorldSpace.Origin);
                hit.HitPointWorldSpace = hitPointWorldSpace;
                hit.HierarchyDepthLevel = BatchGroupElements[index].HierarchyDepthLevel;
                hit.DistanceToHitBounds = distanceToHitBounds;
                hit.HitBoundsCenter = intersectionCenter;
            }

            return intersects;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetDistanceFromHitToBounds(ref float4x4 localToWorld, float3 center, float3 size, float3 hitPosition)
        {
            UIBounds bounds = new UIBounds(center, size);

            return math.length(math.transform(localToWorld, (float3)bounds.ClosestPoint(hitPosition)) - math.transform(localToWorld, hitPosition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithContent(ref StructuredRay ray, DataStoreIndex index, DataStoreID id, out StructuredRay rayInLocalSpace)
        {
            rayInLocalSpace = default;
            if (FilterRootIDs.ContainsKey(id))
            {
                return false;
            }

            LayoutAccess.Calculated layout = LayoutAccess.Get(index, ref LengthProperties);

            worldToLocal = *(worldToLocalPtr + index);
            localToWorld = *(localToWorldPtr + index);

            float3 offset = Offsets[index] - layout.Margin.Offset;
            float3 center = Math.TotalTranslation(ref localToWorld, offset);

            float3 scale = Math.Scale(ref localToWorld);

            hasParent = HierarchyLookup.TryGetValue(Hierarchy[index].ParentID, out parentIndex);

            float3 parentScale = Math.float3_One;

            if (hasParent)
            {
                parentLocalToWorld = *(localToWorldPtr + parentIndex);
                parentScale = Math.Scale(ref parentLocalToWorld);
            }

            rotatedSize = layout.GetRotatedSize(ref TransformLocalRotations, ref UseRotations);
            worldSize = (scale * rotatedSize) + (parentScale * layout.Margin.Size);

            float3 size = math.max(Sizes[index] * scale, worldSize);
            float3 scaledExtents = Math.float3_Half * size;

            if (Math.OverlapsViewport(ref WorldToViewport, center - scaledExtents, center + scaledExtents))
            {
                TransformRay(ref ray, ref worldToLocal, out rayInLocalSpace);

                return Math.IntersectsBoundsPlanes(ref rayInLocalSpace.Origin3x2, ref rayInLocalSpace.RcpDirection3x2, ref localSpaceOrigin, ref size, out _);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TransformRay(ref StructuredRay fromRay, ref float4x4 fromTo, out StructuredRay toRay)
        {
            float3 rayOriginInToSpace = math.transform(fromTo, fromRay.Origin);
            float3 rayDirInToSpace = math.normalize(math.rotate(fromTo, fromRay.Direction));

            rayDirInToSpace = math.normalize(math.select(Math.float3_Zero, rayDirInToSpace, rayDirInToSpace == Math.MaxComponentAbs(rayDirInToSpace)));

            // snap to zero and one if it's close enough, otherwise we'll get some test failures
            float3 one = Math.float3_One;
            rayDirInToSpace = math.select(rayDirInToSpace, Math.float3_Zero, Math.ApproximatelyZero3(ref rayDirInToSpace));
            rayDirInToSpace = math.select(rayDirInToSpace, math.sign(rayDirInToSpace) * one, Math.ApproximatelyEqual3(ref rayDirInToSpace, ref one));

            // create ray in new "To" space
            toRay = new StructuredRay(rayOriginInToSpace, rayDirInToSpace);
        }

        public void TransformCollidable(ref StructuredRay ray, DataStoreIndex index, out StructuredRay rayInLocalSpace)
        {
            LayoutAccess.Calculated layout = LayoutAccess.Get(index, ref LengthProperties);

            worldToLocal = *(worldToLocalPtr + index);
            localToWorld = *(localToWorldPtr + index);

            float3 scale = Math.Scale(ref localToWorld);

            hasParent = HierarchyLookup.TryGetValue(Hierarchy[index].ParentID, out parentIndex);

            float3 parentScale = Math.float3_One;

            if (hasParent)
            {
                parentLocalToWorld = *(localToWorldPtr + parentIndex);
                parentScale = Math.Scale(ref parentLocalToWorld);
            }

            rotatedSize = layout.GetRotatedSize(ref TransformLocalRotations, ref UseRotations);
            worldSize = (scale * rotatedSize) + (parentScale * layout.Margin.Size);

            TransformRay(ref ray, ref worldToLocal, out rayInLocalSpace);
        }
    }
}
