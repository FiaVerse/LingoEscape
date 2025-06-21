// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
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
    internal struct NavigationHit : IComparer<NavigationHit>, IHit
    {
        public DataStoreID ID;
        public float ProximityScore;
        public float TopLevelProximity;
        public RenderOrder RenderOrder;

        public float3 HitPoint;
        public float3 Normal;

        DataStoreID IHit.ID => ID;
        float3 IHit.HitPoint => HitPoint;
        float3 IHit.Normal => Normal;
        float IHit.Proximity => ProximityScore;

        public int Compare(NavigationHit x, NavigationHit y)
        {
            bool equalTopLevel = Math.ApproximatelyEqual(x.TopLevelProximity, y.TopLevelProximity);

            // For draw order, higher == drawn on top and renderqueue takes priority
            if (x.RenderOrder.RenderQueue != y.RenderOrder.RenderQueue || (equalTopLevel && Math.ApproximatelyEqual(x.ProximityScore, y.ProximityScore)))
            {
                return y.RenderOrder.CompareTo(x.RenderOrder);
            }
            else
            {
                return equalTopLevel ? x.ProximityScore.CompareTo(y.ProximityScore) : x.TopLevelProximity.CompareTo(y.TopLevelProximity);
            }
        }
    }

    [BurstCompile]
    internal unsafe struct NavigateToBounds : ICollisionTest<StructuredRay, NavigationHit>
    {
        public const float Epsilon = 0.001f;

        // value here is kind of arbitrary but seemed to
        // cover all test cases sufficiently.
        private const int MinimizeRadiusMaxSteps = 4;

        public Ray Ray;
        public int LayerMask;

        public VisualModifiers VisualModifiers;

        [ReadOnly]
        public RenderOrderCalculator RenderOrderCalculator;

        [ReadOnly]
        public NativeList<Length3.Calculated> LengthProperties;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
        [ReadOnly]
        public NativeList<HierarchyElement> Hierarchy;
        [ReadOnly]
        public NovaHashMap<DataStoreID, bool> NavigationScopeFlags;
        [ReadOnly]
        public NovaHashMap<DataStoreID, bool> NavigationNodeFlags;

        [ReadOnly]
        public NativeList<float3> Sizes;
        [ReadOnly]
        public NativeList<float3> Offsets;
        [ReadOnly]
        public NativeList<float4x4> WorldToLocalMatrices;
        [ReadOnly]
        public NativeList<float4x4> LocalToWorldMatrices;

        public bool UseTopLevelProximities;
        public NovaHashMap<DataStoreID, float> TopLevelProximities;

        [NativeDisableUnsafePtrRestriction]
        private float4x4* worldToLocalPtr;
        [NativeDisableUnsafePtrRestriction]
        private float4x4* localToWorldPtr;


        public bool FilterToNavNodes;
        public DataStoreID IgnoreID;
        public bool IgnoreAllClippedContent;
        private bool ignoreDescendants;

        public DataStoreID ScopeID;
        public bool ExcludeScope;

        private VisualModifierID allowedModifierID;

        private float3 zeroOffset;
        private StructuredRay worldRay;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructuredRay Init()
        {
            TopLevelProximities.Clear();

            worldRay = new StructuredRay(Ray);
            worldToLocalPtr = WorldToLocalMatrices.GetRawReadonlyPtr();
            localToWorldPtr = LocalToWorldMatrices.GetRawReadonlyPtr();
            zeroOffset = Math.float3_Zero;

            VisualModifierID baseModifierID = !IgnoreAllClippedContent && ScopeID.IsValid ?
                                              VisualModifiers.VisualModifierIDs[HierarchyLookup[ScopeID]] :
                                              VisualModifierID.Invalid;
            
            allowedModifierID = VisualModifierID.Invalid;

            if (baseModifierID.IsValid)
            {
                VisualModifierShaderData shaderData = VisualModifiers.ShaderData[baseModifierID];

                for (int i = 0; i < shaderData.Count; ++i)
                {
                    VisualModifierID modifierID = shaderData.ModifierIDs[i];
                    if (VisualModifiers.ClipInfo[modifierID].Clip)
                    {
                        allowedModifierID = modifierID;
                        break;
                    }
                }
            }

            // This is questionable...
            ignoreDescendants = IgnoreID.IsValid && IgnoreID != ScopeID && NavigationScopeFlags.TryGetValue(IgnoreID, out bool autoSelect) && autoSelect;

            return worldRay;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpatialPartitionMask GetCollisionMask(ref StructuredRay ray, DataStoreIndex index)
        {
            DataStoreID id = Hierarchy[index].ID;

            if (id == IgnoreID && ignoreDescendants)
            {
                return SpatialPartitionMask.Empty;
            }

            if (id != ScopeID && NavigationScopeFlags.TryGetValue(Hierarchy[index].ID, out bool autoSelect) && !autoSelect)
            {
                return SpatialPartitionMask.Empty;
            }

            SpatialPartitionMask mask = SpatialPartitionMask.Empty;

            float3 extents = Sizes[index] * Math.float3_Half;
            float3 halfExtents = extents * Math.float3_Half;

            float3 center = Offsets[index];

            // Octants flip z bit every other index. If the parent is 2D,
            // we only need to check intersections with even indices
            int increment = extents.z == 0 ? 2 : 1;

            UIBounds bounds = new UIBounds() { Extents = halfExtents };

            float3 origin = ray.Origin;

            for (int i = 0; i < SpatialPartitionMask.OctantCount; i += increment)
            {
                float3 octant = SpatialPartitionMask.Octants[i];
                bounds.Center = center + halfExtents * octant;

                bool contains = bounds.Contains(origin);
                bool inDirection = math.dot(bounds.Max - origin, ray.Direction) > 0;
                inDirection = inDirection ? true : math.dot(bounds.Min - origin, ray.Direction) > 0;

                mask[i] = contains || inDirection;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithMesh(ref StructuredRay ray, DataStoreIndex index, DataStoreID id, out NavigationHit hit)
        {
            hit = default;

            if (id == IgnoreID || (ExcludeScope && id == ScopeID) || (FilterToNavNodes && !NavigationNodeFlags.ContainsKey(id)))
            {
                return false;
            }

            int objectLayer = RenderOrderCalculator.BaseInfos[index].Val.GameObjectLayer;

            if (((1 << objectLayer) & LayerMask) == 0)
            {
                return false;
            }

            if (!TryGetProximity(index, ref ray, out float proximity, out float3 hitInWorldSpace, out float3 normalInWorldSpace))
            {
                return false;
            }

            float topLevelProximity = 0;

            if (UseTopLevelProximities && ScopeID.IsValid)
            {
                IsDescendantOf(index, ScopeID, out DataStoreID topLevelID);

                if (!TopLevelProximities.TryGetValue(topLevelID, out topLevelProximity))
                {
                    DataStoreIndex topLevelIndex = HierarchyLookup[topLevelID];
                    TransformCollidable(ref worldRay, topLevelIndex, out StructuredRay localRay);
                    TryGetProximity(topLevelIndex, ref localRay, out topLevelProximity, out _, out _);
                    TopLevelProximities.Add(topLevelID, topLevelProximity);
                }

            }

            hit.ID = id;
            hit.RenderOrder = RenderOrderCalculator.GetRenderOrder(index);
            hit.ProximityScore = proximity;
            hit.TopLevelProximity = topLevelProximity;
            hit.Normal = normalInWorldSpace;
            hit.HitPoint = hitInWorldSpace;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDescendantOf(DataStoreIndex descendantIndex, DataStoreID ancestorID, out DataStoreID topLevelID)
        {
            return Internal.Hierarchy.Hierarchy.NativeHierarchy.IsDescendantOf(ref Hierarchy, ref HierarchyLookup, descendantIndex, ancestorID, out topLevelID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetProximity(DataStoreIndex index, ref StructuredRay ray, out float proximity, out float3 hitInWorldSpace, out float3 normalInWorldSpace)
        {
            proximity = float.PositiveInfinity;
            hitInWorldSpace = Math.float3_Zero;
            normalInWorldSpace = Math.float3_Zero;

            LayoutAccess.Calculated layout = LayoutAccess.Get(index, ref LengthProperties);
            float3 size = Math.RoundToNearest(layout.Size.Value, Epsilon);

            UIBounds bounds = new UIBounds(size);

            float3 closestPoint = bounds.ClosestPoint(ray.Origin);
            float3 rayToPoint = math.select(closestPoint - ray.Origin, -ray.Origin, bounds.Extents == Math.Abs(ray.Origin));

            if (math.dot(ray.Direction, rayToPoint) <= 0)
            {
                return false;
            }

            float radius = GetMinProximityBubbleRadius(ref bounds, closestPoint, ref ray);

            float3 proximityPoint = ray.GetPoint(radius);

            float4x4 localToWorld = *(localToWorldPtr + index);
            hitInWorldSpace = math.transform(localToWorld, closestPoint);
            float3 proximityInWorldSpace = math.transform(localToWorld, proximityPoint);
            float3 normalInLocalSpace = UIBounds.NormalOnBounds(ref bounds, ref closestPoint);
            normalInWorldSpace = math.normalize(math.rotate(localToWorld, normalInLocalSpace));

            if (!IntersectsUnclippedMesh(index, hitInWorldSpace))
            {
                return false;
            }

            proximity = math.lengthsq(proximityInWorldSpace - worldRay.Origin);
            return true;
        }

        /// <summary>
        /// Determines the radius of the circle whose center
        /// sits along the provided ray and intersects the ray
        /// origin and the provided closest point
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetProximityBubbleRadius(ref float3 closestPoint, ref StructuredRay ray)
        {
            float3 rayToPoint = closestPoint - ray.Origin;

            float dot = math.dot(rayToPoint, ray.Direction);

            return Math.ApproximatelyZero(dot) ? 0.5f * math.length(rayToPoint) : 0.5f * math.lengthsq(rayToPoint) / dot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IntersectsUnclippedMesh(DataStoreIndex index, float3 hitInWorldSpace)
        {
            VisualModifierID modifierID = VisualModifiers.VisualModifierIDs[index];

            if (!modifierID.IsValid)
            {
                return true;
            }

            return RayToBounds.IntersectsUnclippedBounds(index, allowedModifierID, ref hitInWorldSpace, ref VisualModifiers, ref HierarchyLookup, ref LengthProperties, worldToLocalPtr);
        }

        /// <summary>
        /// Attempts to minimize the output of <see cref="GetProximityBubbleRadius(ref float3, ref StructuredRay)"/> 
        /// by adjusting the closest point. Limited to <see cref="MinimizeRadiusMaxSteps"/> iterations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMinProximityBubbleRadius(ref UIBounds bounds, float3 closestPoint, ref StructuredRay ray)
        {
            float radius = 0;
            float next = GetProximityBubbleRadius(ref closestPoint, ref ray);

            for (int steps = 1; !Math.ApproximatelyZero(radius - next) && steps < MinimizeRadiusMaxSteps; ++steps)
            {
                radius = next;
                closestPoint = bounds.ClosestPoint(ray.GetPoint(radius));
                next = GetProximityBubbleRadius(ref closestPoint, ref ray);
            }

            return next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CollidesWithContent(ref StructuredRay ray, DataStoreIndex index, DataStoreID id, out StructuredRay rayInLocalSpace)
        {
            rayInLocalSpace = default;

            float3 rayOriginInLocalSpace = Math.RoundToNearest(math.transform(*(worldToLocalPtr + index), ray.Origin), Epsilon);
            float3 rayDirInLocalSpace = Math.RoundToNearest(math.normalize(math.rotate(*(worldToLocalPtr + index), ray.Direction)), Epsilon);

            UIBounds bounds = new UIBounds(Offsets[index], Sizes[index]);

            float minDot = math.dot(rayDirInLocalSpace, bounds.Min - rayOriginInLocalSpace);
            float maxDot = math.dot(rayDirInLocalSpace, bounds.Max - rayOriginInLocalSpace);
            bool canIntersect = minDot > 0 || maxDot > 0 || bounds.Contains(rayOriginInLocalSpace);

            if (canIntersect)
            {
                rayInLocalSpace = new StructuredRay() { Origin = rayOriginInLocalSpace, Direction = rayDirInLocalSpace };
            }

            return canIntersect;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransformCollidable(ref StructuredRay ray, DataStoreIndex index, out StructuredRay rayInLocalSpace)
        {
            float4x4 worldToLocal = *(worldToLocalPtr + index);

            float3 rayOriginInLocalSpace = Math.RoundToNearest(math.transform(*(worldToLocalPtr + index), ray.Origin), Epsilon);
            float3 rayDirInLocalSpace = Math.RoundToNearest(math.normalize(math.rotate(*(worldToLocalPtr + index), ray.Direction)), Epsilon);

            rayInLocalSpace = new StructuredRay() { Origin = rayOriginInLocalSpace, Direction = rayDirInLocalSpace };
        }
    }

    internal struct UIBounds
    {
        public float3 Extents;
        public float3 Center;

        public float3 Min => Center - Extents;
        public float3 Max => Center + Extents;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float3 point, float radius = 0)
        {
            float3 radius3 = radius;
            return math.all(point + radius3 >= Min & point - radius3 <= Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 ClosestPoint(float3 point)
        {
            return Math.Clamp(point, Min, Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 ClosestPointOnSurface(float3 point)
        {
            float3 closest = ClosestPoint(point);
            float3 pointOnBounds = Center + Math.SignNonZero(closest) * Extents;

            float3 diff = Math.Abs(pointOnBounds - closest);
            float maxDiff = math.cmax(diff);

            if (maxDiff > 0)
            {
                closest = math.select(closest, pointOnBounds, diff < maxDiff);
            }

            return closest;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(UIBounds bounds)
        {
            return math.all(Max >= bounds.Min & Min <= bounds.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UIBounds(float3 center, float3 size)
        {
            Center = center;
            Extents = Math.float3_Half * size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UIBounds(float3 size)
        {
            Center = Math.float3_Zero;
            Extents = Math.float3_Half * size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalOnBounds(ref UIBounds bounds, ref float3 position)
        {
            if (bounds.Extents.z == 0)
            {
                return Math.Back;
            }

            float3 centerToPosition = position - bounds.Center;
            float3 size = Math.float3_Two * bounds.Extents;
            return NormalOnBounds(ref size, ref centerToPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalOnBounds(ref float3 boundsSize, ref float3 position)
        {
            if (boundsSize.z == 0)
            {
                return Math.Back;
            }

            float3 sign = Math.SignNonZero(position);
            float3 signedExtents = sign * Math.float3_Half * boundsSize;
            float3 normal = math.normalize(sign * Math.Mask(Math.ApproximatelyEqual3(ref position, ref signedExtents)));

            return normal;
        }
    }
}
