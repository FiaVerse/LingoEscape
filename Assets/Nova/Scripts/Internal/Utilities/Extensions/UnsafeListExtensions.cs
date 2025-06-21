// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class UnsafeListExtensions
    {
        /// <summary>
        /// Copied from https://github.com/needle-mirror/com.unity.collections/blob/4ca52cd58cdb97ec256450986971a6e9d7cd7796/Unity.Collections/UnsafeList.cs
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="index"></param>
        /// <param name="item"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Insert<T>(ref this UnsafeList<T> list, int index, T item) where T : unmanaged
        {
            ref UnsafeList<T> listRef = ref list;

            // Inserting at end same as an add
            if (index == listRef.Length)
            {
                listRef.Add(item);
                unsafe
                {
                    list.Ptr = listRef.Ptr;
                    list.Length = listRef.Length;
                    list.Capacity = listRef.Capacity;
                }
                return;
            }

            if (index < 0 || index > listRef.Length)
            {
                Debug.LogError($"Expected within range [0, {listRef.Length}) but got {index}");
                return;
            }

            // add a dummy to resize as necessary
            listRef.Add(default);

            unsafe
            {
                int sizeOf = UnsafeUtility.SizeOf<T>();
                T* ptr = listRef.Ptr;
                T* src = ptr + index;
                T* dest = src + 1;
                UnsafeUtility.MemMove(dest, src, sizeOf * (listRef.Length - index - 1)); // minus 1 becase we just added an empty element
            }

            listRef[index] = item;

            unsafe
            {
                list.Ptr = listRef.Ptr;
                list.Length = listRef.Length;
                list.Capacity = listRef.Capacity;
            }

            return;
        }
    }
}
