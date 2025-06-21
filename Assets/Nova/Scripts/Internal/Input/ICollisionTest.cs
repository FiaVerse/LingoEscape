// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Nova.Internal.Input
{
    /// <summary>
    /// A ray stored in a few different structures for faster access and to reduce common calculations
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct StructuredRay : IRay
    {
        public static readonly StructuredRay Forward = new StructuredRay(Math.float3_Zero, Math.Forward);

        [FieldOffset(0)]
        public float3 Direction;

        [FieldOffset(3 * sizeof(float))]
        public float3 Origin;
        [FieldOffset(6 * sizeof(float))]
        public float3x2 Origin3x2;
        [FieldOffset(3 * sizeof(float))]
        public float3x3 Origin3x3; // intentionally overlaps Origin
        
        [FieldOffset(12 * sizeof(float))]
        public float3 RcpDirection;
        [FieldOffset(15 * sizeof(float))]
        public float3x2 RcpDirection3x2;
        [FieldOffset(12 * sizeof(float))]
        public float3x3 RcpDirection3x3; // intentionally overlaps RcpDirection

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructuredRay(BasicRay ray) : this(ray.Origin, ray.Direction) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructuredRay(float3 rayOrigin, float3 rayDirection)
        {
            RcpDirection3x3 = default;
            Origin3x3 = default;

            Direction = rayDirection;
            Origin = rayOrigin;
            RcpDirection = Math.float3_One / Direction;

            Math.ToFloat3x2(Origin, out Origin3x2);
            Math.ToFloat3x2(RcpDirection, out RcpDirection3x2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetPoint(float distance)
        {
            return Origin + (Direction * distance);
        }
    }

    internal struct BasicRay : IRay
    {
        public float3 Origin;
        public float3 Direction;

        public BasicRay(float3 origin, float3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetPoint(float distance)
        {
            return Origin + Direction * distance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BasicRay(UnityEngine.Ray ray)
        {
            return new BasicRay() { Origin = ray.origin, Direction = ray.direction };
        }
    }

    internal interface ICollisionTest<TCollidable,TResult> where TCollidable : unmanaged where TResult : unmanaged
    {
        TCollidable Init();
        SpatialPartitionMask GetCollisionMask(ref TCollidable collidableInTestSpace, DataStoreIndex index);
        bool CollidesWithContent(ref TCollidable collidableInWorldSpace, DataStoreIndex index, DataStoreID id, out TCollidable collidableInTestSpace);
        bool CollidesWithMesh(ref TCollidable collidableInTestSpace, DataStoreIndex index, DataStoreID id, out TResult hit);
        void TransformCollidable(ref TCollidable inWorldSpace, DataStoreIndex index, out TCollidable collidableInTestSpace);
    }

    internal interface IRay { }
}
