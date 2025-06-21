// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Utilities
{
    internal struct NativeDedupedList<T> : ICapacityInitializable where T : unmanaged, IEquatable<T>
    {
        public NativeList<T> List;
        private NovaHashMap<T, int> StoredValues;

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => List.Length;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => List[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(T val)
        {
            int length = List.Length;

            // This will only insert/update if val doesn't already
            // exist in the map. If it does already exist, it just 
            // returns false, so nothing will be added to the list.
            if (StoredValues.TryAdd(val, length))
            {
                List.Add(val);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(NovaList<T> toAdd)
        {
            for (int i = 0; i < toAdd.Length; ++i)
            {
                Add(toAdd[i]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T val)
        {
            if (!StoredValues.TryGetValue(val, out int index))
            {
                return false;
            }

            StoredValues.Remove(val);
            List.RemoveAtSwapBack(index);

            if (index < List.Length)
            {
                StoredValues[List[index]] = index;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T val)
        {
            return StoredValues.ContainsKey(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            List.Clear();
            StoredValues.Clear();
        }

        public void Dispose()
        {
            List.Dispose();
            StoredValues.Dispose();
        }

        public static NativeDedupedList<T> Create(int capacity = 16) => new NativeDedupedList<T>()
        {
            List = new NativeList<T>(capacity, Allocator.Persistent),
            StoredValues = new NovaHashMap<T, int>(capacity, Allocator.Persistent),
        };

        public void Init(int capacity = 0)
        {
            List.Init(capacity);
            StoredValues.Init(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NativeList<T>(NativeDedupedList<T> list) => list.List;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NovaHashMap<T, int>(NativeDedupedList<T> list) => list.StoredValues;
    }
}

