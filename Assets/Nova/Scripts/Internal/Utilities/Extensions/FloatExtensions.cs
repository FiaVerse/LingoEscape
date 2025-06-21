// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class FloatExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ClampPositive(this float2 f)
        {
            return math.max(f, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 ClampPositive(ref this float4 f)
        {
            return math.max(f, 0);
        }

        /// <summary>
        /// Multiplies (scales) f by s, returns zero if product is NaN or Infinity
        /// </summary>
        /// <param name="f"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MultipliedBy(this float f, float s)
        {
            // float.PositiveInfinity * 0 and float.NegativeInfinity * 0 both
            // return NaN, so this is a bit of an optimization

            float result = f * s;
            return float.IsNaN(result * 0) ? 0 : result;
        }
    }
}