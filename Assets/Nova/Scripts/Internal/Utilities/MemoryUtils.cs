// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Utilities
{
    internal unsafe static class MemoryUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemCpy<T>(T* dest, T* src, int count = 1) where T : unmanaged
        {
            UnsafeUtility.MemCpy(dest, src, count * sizeof(T));
        }

        public static void MemCpy<T>(ref NativeArray<T> dest, ref NativeArray<T> src, int count) where T : unmanaged
        {
            T* destPtr = (T*)dest.GetUnsafePtr();
            T* srcPtr = (T*)src.GetUnsafeReadOnlyPtr();
            MemCpy(destPtr, srcPtr, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TTo Reinterpret<TFrom, TTo>(this ref TFrom from) where TFrom : unmanaged where TTo : unmanaged
        {
            return ref UnsafeUtility.As<TFrom, TTo>(ref from);
        }
    }
}

