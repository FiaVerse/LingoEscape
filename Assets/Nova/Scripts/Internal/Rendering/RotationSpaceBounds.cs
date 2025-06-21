// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    
    internal struct RotationSpaceBounds
    {
        public float2 BL;
        public float2 TR;

        public float2 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TR - BL;
        }

        public float LeftEdge
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BL.x;
        }

        public float RightEdge
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TR.x;
        }

        public float BottomEdge
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BL.y;
        }

        public float TopEdge
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TR.y;
        }

        /// <summary>
        /// Index => BL, TL, TR, BR
        /// </summary>
        public float2 GetCorner(int cornerIndex)
        {
            switch (cornerIndex)
            {
                case 0:
                    return BL;
                case 1:
                    return new float2(BL.x, TR.y);
                case 2:
                    return TR;
                default:
                    return new float2(TR.x, BL.y);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RotationSpaceBounds(float2 bl, float2 tr)
        {
            BL = bl;
            TR = tr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RotationSpaceBounds(float left, float bottom, float right, float top)
        {
            BL = new float2(left, bottom);
            TR = new float2(right, top);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool4 ApproxEqual(ref RotationSpaceBounds other)
        {
            bool4 toRet = default;
            toRet.xy = Math.ApproximatelyEqual2(ref BL, ref other.BL);
            toRet.zw = Math.ApproximatelyEqual2(ref TR, ref other.TR);
            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(ref RotationSpaceBounds other)
        {
            if (Math.ApproximatelyEqual(BL.x, other.BL.x))
            {
                return 0;
            }
            else
            {
                return BL.x.CompareTo(other.BL.x);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlap(ref RotationSpaceBounds other)
        {
            return !(math.any(BL.ApproximatelyGreaterThan(ref other.TR)) || math.any(other.BL.ApproximatelyGreaterThan(ref TR)));
        }
    }
}
