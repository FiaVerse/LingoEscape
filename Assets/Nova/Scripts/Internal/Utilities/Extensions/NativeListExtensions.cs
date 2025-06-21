// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class NativeListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetIndexOf<TList, TFind>(ref this NativeList<TList> list, TFind item, out int index) where TList : unmanaged, IEquatable<TFind>
        {
            index = Unity.Collections.NativeArrayExtensions.IndexOf(list.AsArray(), item);
            return index >= 0 && index < list.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryPopBack<T>(ref this NativeList<T> list, out T val) where T : unmanaged
        {
            if (list.Length > 0)
            {
                val = list[list.Length - 1];
                list.RemoveAtSwapBack(list.Length - 1);
                return true;
            }
            else
            {
                val = default;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void AddRange<T>(ref this NativeList<T> list, ref NovaList<T> novaList) where T : unmanaged
        {
            list.AddRange(novaList.Ptr, novaList.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void AddRangeReverse<T>(ref this NativeList<T> list, ref NovaList<T> novaList) where T : unmanaged
        {
            for (int i = novaList.Length - 1; i >= 0; --i)
            {
                list.Add(novaList.ElementAt(i));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Insert<T>(ref this NativeList<T> list, int index, T item) where T : unmanaged
        {
            unsafe
            {
                list.InsertRange(index, &item, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InsertRange<T>(this ref NativeList<T> list, int index, T* items, int count) where T : unmanaged
        {
            // Inserting at end same as an add
            if (index == list.Length)
            {
                list.AddRange(items, count);
                return;
            }

            if (index < 0 || index > list.Length)
            {
                Debug.LogError($"Expected within range [0, {list.Length}) but got {index}");
                return;
            }

            // add a dummy to resize as necessary
            list.Length += count;

            int sizeOf = UnsafeUtility.SizeOf<T>();

            T* ptr = list.GetRawPtr();
            T* src = ptr + index;
            T* dest = src + count;
            UnsafeUtility.MemMove(dest, src, sizeOf * (list.Length - index - count));// minus count becase we just added empty elements
            UnsafeUtility.MemCpy(src, items, count * sizeOf);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Last<T>(this ref NativeList<T> list) where T : unmanaged
        {
            return list[list.Length - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveLast<T>(this ref NativeList<T> list) where T : unmanaged
        {
            list.RemoveAt(list.Length - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Init<T>(this ref NativeList<T> list, int capacity = 0) where T : unmanaged
        {
            list = new NativeList<T>(capacity, Allocator.Persistent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this ref NativeList<T> list, ref NativeList<T> copyFrom) where T : unmanaged
        {
            list.Length = copyFrom.Length;
            list.AsArray().CopyFrom(copyFrom.AsArray());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeListAndElements<T>(this ref NativeList<T> list) where T : unmanaged, IDisposable
        {
            for (int i = 0; i < list.Length; ++i)
            {
                list[i].Dispose();
            }
            list.Clear();
            list.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeElementsAndClear<T>(this ref NativeList<T> list) where T : unmanaged, IDisposable
        {
            for (int i = 0; i < list.Length; ++i)
            {
                list[i].Dispose();
            }
            list.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetFromPoolOrInit<T>(this NativeList<T> list) where T : unmanaged, IInitializable
        {
            if (!list.TryPopBack(out T val))
            {
                val.Init();
            }
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NovaList<T> GetFromPoolOrInit<T>(this NativeList<NovaList<T>> list) where T : unmanaged
        {
            if (!list.TryPopBack(out NovaList<T> val))
            {
                val.Init();
            }
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NovaList<T, V> GetFromPoolOrInit<T, V>(this NativeList<NovaList<T, V>> list)
                where T : unmanaged, IIndex<T>
                where V : unmanaged
        {
            if (!list.TryPopBack(out NovaList<T, V> val))
            {
                val.Init();
            }
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnToPool<T>(this NativeList<T> list, ref T val) where T : unmanaged, IClearable
        {
            val.Clear();
            list.Add(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnToPoolNonRef<T>(this NativeList<T> list, T val) where T : unmanaged, IClearable
        {
            val.Clear();
            list.Add(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnToPool<T>(this NativeList<NovaList<T>> list, ref NovaList<T> val) where T : unmanaged
        {
            val.Clear();
            list.Add(val);
        }
    }
}

