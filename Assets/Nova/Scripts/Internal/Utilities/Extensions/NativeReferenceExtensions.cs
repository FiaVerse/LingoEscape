// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class NativeReferenceExtensions
    {
        public static unsafe ref T Ref<T>(this NativeReference<T> reference) where T : unmanaged
        {
            return ref UnsafeUtility.AsRef<T>(reference.GetRawPtr());
        }

        public static unsafe void Init<T>(ref this NativeReference<T> reference) where T : unmanaged
        {
            reference = new NativeReference<T>(Allocator.Persistent);
        }

        public static unsafe void Init<T>(ref this NativeReference<T> reference, T initalValue) where T : unmanaged
        {
            reference = new NativeReference<T>(initalValue, Allocator.Persistent);
        }
    }
}
