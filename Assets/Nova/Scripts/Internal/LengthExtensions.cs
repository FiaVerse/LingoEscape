// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova
{
    internal static class LengthExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Internal.Length ToInternal(ref this Length length)
        {
            return ref UnsafeUtility.As<Length, Internal.Length>(ref length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Internal.Length2 ToInternal(ref this Length2 length)
        {
            return ref UnsafeUtility.As<Length2, Internal.Length2>(ref length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Internal.Length3.MinMax ToInternal(ref this MinMax3 minMax)
        {
            return ref UnsafeUtility.As<MinMax3, Internal.Length3.MinMax>(ref minMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Length ToPublic(ref this Internal.Length length)
        {
            return ref UnsafeUtility.As<Internal.Length, Length>(ref length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly Length.Calculated ToPublic(ref this Internal.Length.Calculated calc)
        {
            return ref UnsafeUtility.As<Internal.Length.Calculated, Length.Calculated>(ref calc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly Length2.Calculated ToPublic(ref this Internal.Length2.Calculated calc)
        {
            return ref UnsafeUtility.As<Internal.Length2.Calculated, Length2.Calculated>(ref calc);
        }
    }
}
