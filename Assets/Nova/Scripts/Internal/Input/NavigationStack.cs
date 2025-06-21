// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using System;
using System.Collections.Generic;

namespace Nova.Internal.Input
{
    internal class NavigationStack<T> where T : class, IUIBlock
    {
        public struct Key : IEquatable<Key>
        {
            public uint Value;
            public int Layers;

            public Key(uint value, int layers)
            {
                Value = value;
                Layers = layers;
            }

            public static implicit operator Key(uint value)
            {
                return new Key() { Value = value };
            }

            public bool Equals(Key other)
            {
                return Value == other.Value;
            }
        }

        private Dictionary<uint, List<T>> keyToElements = new Dictionary<uint, List<T>>();
        private Dictionary<T, HashList<Key>> elementToUnorderedKeys = new Dictionary<T, HashList<Key>>();

        public void Push(uint key, T element, int layerMask)
        {
            if (!keyToElements.TryGetValue(key, out List<T> elements))
            {
                elements = new List<T>();
                keyToElements.Add(key, elements);
            }

            if (!elementToUnorderedKeys.TryGetValue(element, out HashList<Key> keys))
            {
                keys = CollectionPool<HashList<Key>, Key>.Get();
                elementToUnorderedKeys[element] = keys;
            }

            if (!keys.Contains(key))
            {
                keys.Add(new Key(key, layerMask));
            }

            // Ensure no duplicates;
            elements.Remove(element);
            elements.Add(element);
        }

        public T Pop(uint key)
        {
            T top = Top(key);

            if (top != null)
            {
                Remove(key, top);
            }

            return top;
        }

        public void Remove(uint key, T element)
        {
            if (!keyToElements.TryGetValue(key, out List<T> elements))
            {
                return;
            }

            if (elementToUnorderedKeys.TryGetValue(element, out HashList<Key> keys))
            {
                keys.Remove(key);

                if (keys.Count == 0)
                {
                    elementToUnorderedKeys.Remove(element);
                    CollectionPool<HashList<Key>, Key>.Release(keys);
                }
            }

            elements.Remove(element);
        }

        public void Clear(uint key)
        {
            if (!keyToElements.TryGetValue(key, out List<T> elements))
            {
                return;
            }

            for (int i = 0; i < elements.Count; ++i)
            {
                T element = elements[i];

                if (elementToUnorderedKeys.TryGetValue(element, out HashList<Key> keys))
                {
                    keys.Remove(key);

                    if (keys.Count == 0)
                    {
                        elementToUnorderedKeys.Remove(element);
                        CollectionPool<HashList<Key>, Key>.Release(keys);
                    }
                }
            }

            elements.Clear();
        }

        public T Top(uint key)
        {
            if (!keyToElements.TryGetValue(key, out List<T> elements) || elements.Count == 0)
            {
                return null;
            }

            for (int i = elements.Count - 1; i >= 0; i--)
            {
                T element = elements[i];

                if (element != null)
                {
                    return element;
                }

                elements.RemoveAt(i);
            }

            return null;
        }

        public T Top(uint key, int offsetFromTop)
        {
            if (!keyToElements.TryGetValue(key, out List<T> elements) || elements.Count <= offsetFromTop)
            {
                return null;
            }

            return elements[elements.Count - 1 - offsetFromTop];
        }

        public int GetCount(uint key)
        {
            if (!keyToElements.TryGetValue(key, out List<T> elements))
            {
                return 0;
            }

            return elements.Count;
        }

        public ReadOnlyList<Key> GetControls(T element)
        {
            if (elementToUnorderedKeys.TryGetValue(element, out HashList<Key> keys))
            {
                return keys.List;
            }

            return ReadOnlyList<Key>.Empty;
        }
    }
}