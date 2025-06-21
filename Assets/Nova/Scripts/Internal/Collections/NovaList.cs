// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Common;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Collections
{
    internal unsafe struct NovaList<T> : IInitializable, IResizable where T : unmanaged
    {
        public UnsafeList<T> list;

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.Length;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => list.Resize(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T val) => list.Add(val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAtSwapBack(int index)
        {
            if (IndexIsValid(index))
            {
                list.RemoveAtSwapBack(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ref NovaList<T> toAdd)
        {
            if (toAdd.Length > 0)
            {
                list.AddRange(toAdd.Ptr, toAdd.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ref NativeList<T> toAdd)
        {
            if (toAdd.Length > 0)
            {
                list.AddRange(toAdd.GetRawPtr(), toAdd.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(T* ptr, int count)
        {
            if (count > 0)
            {
                list.AddRange(ptr, count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRangeNoResize(void* ptr, int length)
        {
            list.AddRangeNoResize(ptr, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if (IndexIsValid(index))
            {
                list.RemoveAt(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPopBack(out T val)
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
        public void Resize(int len)
        {
            Length = len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRangeReverse(ref NovaList<T> toAdd)
        {
            for (int i = toAdd.Length - 1; i >= 0; --i)
            {
                list.Add(toAdd[i]);
            }
        }

        public T* Ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.Ptr;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.Capacity;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => list.Capacity = value;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IndexIsValid(index))
                {
                    return list[index];
                }
                else
                {
                    return default;
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (IndexIsValid(index))
                {
                    list[index] = value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index)
        {
            if (IndexIsValid(index))
            {
                return ref list.ElementAt(index);
            }
            else
            {
                // Not sure what I can do here?
                return ref list.ElementAt(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, T val) => list.Insert(index, val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => list.Clear();

        public NovaList(int initialCapacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            list = new UnsafeList<T>(initialCapacity > 0 ? initialCapacity : 1, allocator == Allocator.Persistent ? NovaAllocator.Handle : allocator, options);
        }

        public void Dispose()
        {
            list.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IndexIsValid(int index)
        {
            if (index >= 0 && index < list.Length)
            {
                return true;
            }
            else
            {
                Debug.LogError($"Index out of range. Requested {index}, but list was length {list.Length}");
                return false;
            }
        }

        public RawPtrArrayWrapper<T> AsArray()
        {
            return new RawPtrArrayWrapper<T>(list.Ptr, list.Length);
        }

        public void Init()
        {
            list = new UnsafeList<T>(1, NovaAllocator.Handle, NativeArrayOptions.UninitializedMemory);
        }
    }


    internal static partial class NovaListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetIndexOf<T, U>(ref this NovaList<T> list, U item, out int index, int offset = 0) where T : unmanaged, IEquatable<U>
        {
            offset = math.max(0, offset);
            unsafe
            {
                int length = list.Length;
                T* listPtr = list.Ptr;

                for (int i = offset; i < length; ++i)
                {
                    if (listPtr[i].Equals(item))
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetIndexOf<T, C>(ref this NovaList<T> list, T item, C comparer, out int index, int offset = 0)
            where T : unmanaged
            where C : unmanaged, System.Collections.Generic.IComparer<T>
        {
            offset = math.max(0, offset);
            unsafe
            {
                int length = list.Length;
                T* listPtr = list.Ptr;

                for (int i = offset; i < length; ++i)
                {
                    if (comparer.Compare(listPtr[i], item) == 0)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void CopyFrom<T>(this ref NovaList<T> dest, T[] src, int count, int startDestIndex = 0, int startSrcIndex = 0) where T : unmanaged
        {
            if (src == null)
            {
                Debug.LogError("Tried to copy from null array");
                return;
            }

            if (dest.Length < (startDestIndex + count) || src.Length < (startSrcIndex + count))
            {
                Debug.LogError($"Tried to copy {count} elements with a source length of {src.Length} and dest length of {dest.Length}");
                return;
            }

            fixed (T* srcPtr = src)
            {
                MemoryUtils.MemCpy(dest.Ptr + startDestIndex, srcPtr + startSrcIndex, count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void CreateEmpty<T>(this ref NovaList<T> list) where T : unmanaged
        {
            list = new NovaList<T>(0, Allocator.Persistent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FillWithValue<T>(this ref NovaList<T> list, T val) where T : unmanaged
        {
            for (int i = 0; i < list.Length; ++i)
            {
                list[i] = val;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void MemClear<T>(this ref NovaList<T> list) where T : unmanaged
        {
            UnsafeUtility.MemClear(list.Ptr, sizeof(T) * list.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void Init<T>(this ref NovaList<T> list, int capacity = 0) where T : unmanaged
        {
            list = new NovaList<T>(capacity, Allocator.Persistent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void DisposeListAndElements<T>(this ref NovaList<T> list) where T : unmanaged, IDisposable
        {
            for (int i = 0; i < list.Length; ++i)
            {
                list[i].Dispose();
            }
            list.Clear();
            list.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void CopyTo<T>(this ref NovaList<T> list, T* dest) where T : unmanaged
        {
            UnsafeUtility.MemCpy(dest, list.Ptr, sizeof(T) * list.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T>(this ref NovaList<T> list) where T : unmanaged, IComparable<T>
        {
            list.list.Sort();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T, U>(this ref NovaList<T> list, U comparer) where T : unmanaged where U : System.Collections.Generic.IComparer<T>
        {
            list.list.Sort(comparer);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnAllToPool<T>(this ref NovaList<T> list, ref NovaList<T> pool) where T : unmanaged, IClearable
        {
            for (int i = 0; i < list.Length; ++i)
            {
                ref T elt = ref list.ElementAt(i);
                elt.Clear();
                pool.Add(elt);
            }
            list.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static NovaList<U> Reinterpret<T, U>(this NovaList<T> list) where T : unmanaged where U : unmanaged
        {
            return new NovaList<U>()
            {
                list = new UnsafeList<U>((U*)list.Ptr, list.Length),
            };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemove<T, U>(ref this NovaList<T> list, U item) where T : unmanaged, IEquatable<U>
        {
            if (!list.TryGetIndexOf(item, out int index))
            {
                return false;
            }
            list.RemoveAtSwapBack(index);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(this ref NovaList<T> list, int index1, int index2) where T : unmanaged
        {
            T temp = list.ElementAt(index1);
            list.ElementAt(index1) = list.ElementAt(index2);
            list.ElementAt(index2) = temp;
        }
    }
}

