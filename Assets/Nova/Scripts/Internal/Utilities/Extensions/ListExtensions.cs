// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class ListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryPopBack<T>(this List<T> list, out T val)
        {
            if (list.Count > 0)
            {
                val = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                return true;
            }
            else
            {
                val = default;
                return false;
            }
        }

        public static ReadOnlyList<T> ToReadOnly<T>(this List<T> list)
        {
            return new ReadOnlyList<T>(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeElementsAndClear<T>(this List<T> list) where T : IDisposable
        {
            for (int i = 0; i < list.Count; ++i)
            {
                list[i].Dispose();
            }
            list.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetFromPoolOrInit<T>(this List<NativeList<T>> pool, ref NativeList<T> list) where T : unmanaged
        {
            if (!pool.TryPopBack(out list))
            {
                list.Init();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetFromPoolOrInit<K, V>(this List<NovaHashMap<K, V>> pool, ref NovaHashMap<K, V> map)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged
        {
            if (!pool.TryPopBack(out map))
            {
                map.Init();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetFromPoolOrInit<T>(this List<T> pool, ref T collection) where T : struct, IInitializable
        {
            if (!pool.TryPopBack(out collection))
            {
                collection.Init();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetFromPoolOrInit<T>(this List<T> pool, ref T collection, int capacity = 0) where T : struct, ICapacityInitializable
        {
            if (!pool.TryPopBack(out collection))
            {
                collection.Init(capacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnToPool<T>(this List<T> pool, ref T collection) where T : struct, IClearable
        {
            collection.Clear();
            pool.Add(collection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnToPool<T>(this List<NativeList<T>> pool, ref NativeList<T> list) where T : unmanaged
        {
            list.Clear();
            pool.Add(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnToPool<K, V>(this List<NovaHashMap<K, V>> pool, ref NovaHashMap<K, V> map)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged
        {
            map.Clear();
            pool.Add(map);
        }
    }
}

