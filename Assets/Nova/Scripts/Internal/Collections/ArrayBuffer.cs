// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities.Extensions;
using System;

namespace Nova.Internal.Collections
{
    /// <summary>
    /// Similar to a List<T>, an array buffer will resize itself as new elements are added. It mainly 
    /// provides a way to directly access the underlying array elements for faster copying between managed
    /// arrays and native containers.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    internal class ArrayBuffer<T>
    {
        private const int StartingSize = 4;
        public int Count { get; private set; } = 0;
        public int Capacity => buffer.Length;

        private T[] buffer = new T[StartingSize];
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException("index", $"Attemping to get element at index: {index}. Index must be within range [0, {Count - 1}] for this ArrayBuffer instance.");
                }

                return buffer[index];
            }
            set
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException("index", $"Attemping to set element at index: {index}. Index must be within range [0, {Count - 1}] for this ArrayBuffer instance.");
                }

                buffer[index] = value;
            }
        }

        public void Add(T value)
        {
            if (buffer == null || Count == buffer.Length)
            {
                ResizeBuffer();
            }

            buffer[Count++] = value;
        }

        public void RemoveAtSwapBack(int index)
        {
            if (index < 0 || index >= Count)
            {
                return;
            }

            buffer[index] = buffer[--Count];
        }

        public void AddRange(ReadOnlyList<T> range)
        {
            if (buffer == null || Count + range.Count > buffer.Length)
            {
                ResizeBuffer(Count + range.Count);
            }

            range.CopyTo(buffer, Count);

            Count += range.Count;
        }

        public void Clear()
        {
            buffer.Memset(default);
            Count = 0;
        }

        public T[] GetUnderlyingArray()
        {
            return buffer;
        }

        private void ResizeBuffer(int newSize)
        {
            T[] temp = new T[newSize];
            Array.Copy(buffer, temp, buffer.Length);
            buffer = temp;
        }

        private void ResizeBuffer()
        {
            ResizeBuffer(2 * buffer.Length);
        }
    }
}
