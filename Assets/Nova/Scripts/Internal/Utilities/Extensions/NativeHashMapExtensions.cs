// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Common;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class NovaHashMapExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static V GetAndClear<K, V>(this ref NovaHashMap<K, V> NovaHashMap, K key)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged, IClearable
        {
            V list = NovaHashMap[key];
            list.Clear();
            return list;
        }

        public static V GetAndResize<K, V>(this ref NovaHashMap<K, V> NovaHashMap, K key, int newSize)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged, IResizable
        {
            V list = NovaHashMap[key];
            list.Length = newSize;
            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddEmpty<K, V>(this ref NovaHashMap<K, V> map, K key)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged, IInitializable
        {
            V newVal = new V();
            newVal.Init();
            map.Add(key, newVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Init<K, V>(this ref NovaHashMap<K, V> map, int capacity = 4)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged
        {
            map = new NovaHashMap<K, V>(capacity, Allocator.Persistent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAndRemove<K, V>(this ref NovaHashMap<K, V> map, K key, out V val)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged
        {
            if (map.TryGetValue(key, out val))
            {
                map.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

