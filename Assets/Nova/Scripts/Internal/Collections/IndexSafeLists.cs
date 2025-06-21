// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Common;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova.Internal.Collections
{
    internal interface IIndex<T> : IComparable<T>, IEquatable<T> where T : IIndex<T>
    {
        int Index { get; }
    }

    /// <summary>
    /// Wrapper around a NativeList<T> that provides type safety of the index type
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    
    internal unsafe struct NativeList<K,V> : ICapacityInitializable, IResizable
        where K : unmanaged, IIndex<K>
        where V : unmanaged
    {
        [NativeDisableContainerSafetyRestriction]
        private NativeList<V> list;

        public NativeList<V> UnderlyingList
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.Length;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                list.Length = value;
            }
        }

        public void Clear()
        {
            list.Clear();
        }

        public V this[K index]
        {
            get
            {
                return list[index.Index];
            }
            set
            {
                list[index.Index] = value;
            }
        }

        public bool TryGetPointerAt(K index, out V* ptr)
        {
            if (index.Index >= list.Length)
            {
                Debug.LogError($"Tried to read at index {index.Index}, length was {list.Length}");
                ptr = null;
                return false;
            }
            ptr = list.GetRawPtr() + index.Index;
            return true;
        }

        public ref V ElementAt(K index)
        {
            return ref list.ElementAt(index.Index);
        }

        public void RemoveAtSwapBack(K index)
        {
            list.RemoveAtSwapBack(index.Index);
        }

        public void AddRef(ref V val)
        {
            list.Add(val);
        }

        public void Add(V val)
        {
            list.Add(val);
        }

        public void Dispose()
        {
            list.Dispose();
        }

        public void Init(int capacity = 0)
        {
            list.Init(capacity);
        }
    }

    /// <summary>
    /// Wrapper around a NovaList<T> that provides type safety of the index type
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    internal unsafe struct NovaList<K,V> : ICapacityInitializable, IResizable
    where K : unmanaged, IIndex<K>
    where V : unmanaged
    {
        [NativeDisableContainerSafetyRestriction]
        public NovaList<V> UnderlyingList;

        public V* Ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UnderlyingList.Ptr;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UnderlyingList.Length;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                UnderlyingList.Length = value;
            }
        }

        public void Clear()
        {
            UnderlyingList.Clear();
        }

        public void Resize(int len)
        {
            UnderlyingList.Resize(len);
        }

        public V this[K index]
        {
            get
            {
                return UnderlyingList[index.Index];
            }
            set
            {
                UnderlyingList[index.Index] = value;
            }
        }

        public bool TryGetPointerAt(K index, out V* ptr)
        {
            if (index.Index >= UnderlyingList.Length)
            {
                Debug.LogError($"Tried to read at index {index.Index}, length was {UnderlyingList.Length}");
                ptr = null;
                return false;
            }
            ptr = UnderlyingList.Ptr + index.Index;
            return true;
        }

        public ref V ElementAt(K index)
        {
            return ref UnderlyingList.ElementAt(index.Index);
        }

        public void RemoveAtSwapBack(K index)
        {
            UnderlyingList.RemoveAtSwapBack(index.Index);
        }

        public void AddRef(ref V val)
        {
            UnderlyingList.Add(val);
        }

        public void Add(V val)
        {
            UnderlyingList.Add(val);
        }

        public void Dispose()
        {
            UnderlyingList.Dispose();
        }

        public void Init(int capacity = 0)
        {
            UnderlyingList.Init(capacity);
        }
    }

    internal static partial class NovaListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetIndexOf<K, V, U>(ref this NovaList<K, V> list, U item, out int index, int offset = 0)
            where K : unmanaged, IIndex<K>
            where V : unmanaged, IEquatable<U>
        {
            return list.UnderlyingList.TryGetIndexOf(item, out index, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void MemClear<K, V>(ref this NovaList<K, V> list)
            where K : unmanaged, IIndex<K>
            where V : unmanaged
        {
            UnsafeUtility.MemClear(list.Ptr, sizeof(V) * list.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void DisposeListAndElements<K, V>(this ref NativeList<K, V> list) where K : unmanaged, IIndex<K> where V : unmanaged, IDisposable
        {
            for (int i = 0; i < list.Length; ++i)
            {
                list.UnderlyingList.ElementAt(i).Dispose();
            }
            list.Clear();
            list.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void DisposeListAndElements<K, V>(this ref NovaList<K, V> list) where K : unmanaged, IIndex<K> where V : unmanaged, IDisposable
        {
            for (int i = 0; i < list.Length; ++i)
            {
                list.UnderlyingList.ElementAt(i).Dispose();
            }
            list.Clear();
            list.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnAllToPool<K, V>(this ref NovaList<K, V> list, ref NovaList<V> pool)
            where K : unmanaged, IIndex<K>
            where V : unmanaged, IClearable
        {
            list.UnderlyingList.ReturnAllToPool(ref pool);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void AddEmpty<K, V>(ref this NovaList<K, V> list)
            where K : unmanaged, IIndex<K>
            where V : unmanaged, IInitializable
        {
            V toAdd = default;
            toAdd.Init();
            list.Add(toAdd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void AddEmpty<K, V>(ref this NativeList<K, V> list)
            where K : unmanaged, IIndex<K>
            where V : unmanaged, IInitializable
        {
            V toAdd = default;
            toAdd.Init();
            list.Add(toAdd);
        }
    }
}

