// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)

namespace Nova.Internal.Rendering
{
    internal struct TextMargin
    {
        public float4 Value;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !math.any(math.isnan(Value));
        }

        public override string ToString() => Value.ToString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TextMargin a, TextMargin b) => !(a == b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TextMargin a, TextMargin b) => Math.ApproximatelyEqual(ref a.Value, ref b.Value);

        /// <summary>
        /// If hugging, the size of the margin is max size, if it is set, otherwise zero
        /// </summary>
        /// <param name="maxDimensions"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetMarginSizeAssumeHugging(float2 maxDimensions)
        {
            return math.select(float2.zero, maxDimensions, maxDimensions < Math.float2_PositiveInfinity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TextMargin GetMarginAssumeHugging(float2 maxDimensions)
        {
            return FromMarginSize(GetMarginSizeAssumeHugging(maxDimensions));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetMarginSize(float2 layoutSize, float2 maxDimensions, bool2 shrinkMask)
        {
            float2 marginSizeIfHugging = GetMarginSizeAssumeHugging(maxDimensions);
            return math.select(layoutSize, marginSizeIfHugging, shrinkMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TextMargin GetMargin(float2 layoutSize, float2 maxDimensions, bool2 shrinkMask)
        {
            return FromMarginSize(GetMarginSize(layoutSize, maxDimensions, shrinkMask));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TextMargin FromMarginSize(float2 marginSize) => new TextMargin()
        {
            Value = (-Math.float2_Half * marginSize).xyxy,
        };

        public static readonly TextMargin Invalid = new TextMargin()
        {
            Value = Math.float4_NAN
        };
    }
}
