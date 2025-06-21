// Copyright (c) Supernova Technologies LLC
using System.Collections.Generic;


namespace Nova.Internal.Collections
{
    internal class ListPool<T> : CollectionPool<PooledList<T>, T> { }

    internal class PooledList<T> : List<T> { }

    internal class DictionaryPool<TKey,TValue> : CollectionPool<PooledDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>> { }

    internal class PooledDictionary<TKey,TValue> : Dictionary<TKey, TValue> { }

    internal class CollectionPool<TCollection,T> where TCollection : ICollection<T>, new()
    {
        private readonly static Queue<TCollection> collections = new Queue<TCollection>();

        public static TCollection Get()
        {
            if (collections.Count > 0)
            {
                TCollection collection = collections.Dequeue();
                collection.Clear();
                return collection;
            }

            return new TCollection();
        }

        public static void Release(TCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            collections.Enqueue(collection);
        }
    }
}
