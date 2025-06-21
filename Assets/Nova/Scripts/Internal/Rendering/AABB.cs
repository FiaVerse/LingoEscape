// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct AABB : System.IEquatable<AABB>
    {
        public float3 Min;
        public float3 Max;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetCenter() => Math.float3_Half * (Min + Max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetSize() => Max - Min;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bounds ToBounds() => new Bounds(GetCenter(), GetSize());

        private float2 mins2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Min.xy;
        }

        private float2 maxes2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Max.xy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clamp2D(ref AABB clampTo)
        {
            Min.xy = math.clamp(Min.xy, clampTo.Min.xy, clampTo.Max.xy);
            Max.xy = math.clamp(Max.xy, clampTo.Min.xy, clampTo.Max.xy);
        }

        /// <summary>
        /// Returns starting at bottom-left (min, min), going clockwise
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4x4 GetCorners2D()
        {
            return new float4x4(
                new float4(Min.xy, 0, 1f),
                new float4(Min.x, Max.y, 0, 1f),
                new float4(Max.xy, 0, 1f),
                new float4(Max.x, Min.y, 0, 1f)
                );
        }

        public void GetCorners3D(out float4x4 minZ, out float4x4 maxZ)
        {
            minZ = maxZ = GetCorners2D();
            minZ += float4x4.Translate(new float3(0, 0, Min.z));
            maxZ += float4x4.Translate(new float3(0, 0, Max.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABB(float3 min, float3 max)
        {
            this.Min = min;
            this.Max = max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABB(float2 min, float2 max)
        {
            this.Min = new float3(min, 0f);
            this.Max = new float3(max, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABB(Bounds bounds)
        {
            Min = bounds.min;
            Max = bounds.max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABB(ref float4x4 corners)
        {
            float4 minPoint = Math.MinOverColumns(ref corners);
            float4 maxPoint = Math.MaxOverColumns(ref corners);
            this.Min = minPoint.xyz;
            this.Max = maxPoint.xyz;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps2D(ref AABB other)
        {
            // This is equivalent to checking if one rectangle is completely to the left
            // or completely above the other. If neither of those is true, then it they overlap
            float4 mins = new float4(mins2, other.mins2);
            float4 maxs = new float4(other.maxes2, maxes2);
            return !math.any(mins.ApproximatelyGreaterThan(ref maxs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Encapsulates2D(ref AABB other)
        {
            // This is equivalent to checking if the this max is greater than
            // other max, and other min is greater than this min
            float4 x = new float4(maxes2, other.mins2);
            float4 y = new float4(other.maxes2, mins2);
            return math.all(x.ApproximatelyGreaterThan(ref y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encapsulate(ref AABB other)
        {
            Min = math.min(Min, other.Min);
            Max = math.max(Max, other.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Transform2D(ref float4x4 transform, ref AABB bounds)
        {
            float4x4 transformed = math.mul(transform, bounds.GetCorners2D());
            return new AABB(ref transformed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Transform2D(ref float4x4 second, ref float4x4 first, ref AABB bounds)
        {
            float4x4 combined = math.mul(second, first);
            return Transform2D(ref combined, ref bounds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Transform3D(ref float4x4 transform, ref AABB bounds)
        {
            bounds.GetCorners3D(out float4x4 minZ, out float4x4 maxZ);
            float4x4 transformedA = math.mul(transform, minZ);
            float4x4 transformedB = math.mul(transform, maxZ);

            float4 min = math.min(Math.MinOverColumns(ref transformedA), Math.MinOverColumns(ref transformedB));
            float4 max = math.max(Math.MaxOverColumns(ref transformedA), Math.MaxOverColumns(ref transformedB));
            return new AABB(min.xyz, max.xyz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Transform3D(ref float4x4 second, ref float4x4 first, ref AABB bounds)
        {
            float4x4 combined = math.mul(second, first);
            return Transform3D(ref combined, ref bounds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Translate2D(ref float2 translation, ref AABB bounds)
        {
            float3 translation3D = new float3(translation, 0f);
            return new AABB(bounds.Min + translation3D, bounds.Max + translation3D);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(AABB other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        public override string ToString()
        {
            return $"({Min} => {Max}";
        }
    }
}
