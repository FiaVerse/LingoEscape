// Copyright (c) Supernova Technologies LLC
//#define DEBUG_LOG

using Nova.Internal.Utilities.Extensions;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;

namespace Nova.Internal.Collections
{
    internal class HashList<T> : ICollection<T>
    {
        private List<T> list = null;
        private Dictionary<T, int> StoredValues = null;

        public ReadOnlyList<T> List => list.ToReadOnly();

        public int Count => list.Count;

        public bool IsReadOnly => ((ICollection<T>)list).IsReadOnly;

        public T this[int index] => list[index];

        public void Add(T value)
        {
            if (Contains(value))
            {
                return;
            }

            int length = list.Count;

            StoredValues.Add(value, length);
            list.Add(value);

        }

        public void AddRange(List<T> values)
        {
            for (int i = 0; i < values.Count; ++i)
            {
                Add(values[i]);
            }
        }

        public void AddRange(HashList<T> values)
        {
            AddRange(values.list);
        }

        public void AddRange(T[] values)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                Add(values[i]);
            }
        }

        public bool Remove(T value)
        {
            if (!StoredValues.TryGetValue(value, out int index))
            {
                return false;
            }

            RemoveAt(index);

            return true;
        }

        public void RemoveAt(int index)
        {
            T value = list[index];
            StoredValues.Remove(value);
            list.RemoveAtSwapBack(index);

            if (index < list.Count)
            {
                StoredValues[list[index]] = index;
            }

        }

        public bool Contains(T value)
        {
            return StoredValues.ContainsKey(value);
        }

        public int IndexOf(T value)
        {
            if (StoredValues.TryGetValue(value, out int index))
            {
                return index;
            }

            return -1;
        }

        public void Clear()
        {
            list.Clear();
            StoredValues.Clear();
        }

        public void CopyTo(List<T> dest)
        {
            dest.Clear();
            dest.AddRange(list);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)list).GetEnumerator();
        }

        public HashList(IEqualityComparer<T> comparer)
        {
            StoredValues = new Dictionary<T, int>(comparer);
            list = new List<T>();
        }

        public HashList(int defaultCapacity = 4)
        {
            StoredValues = new Dictionary<T, int>(defaultCapacity);
            list = new List<T>(defaultCapacity);
        }

        public HashList() : this(defaultCapacity: 4) { }
    }
}

