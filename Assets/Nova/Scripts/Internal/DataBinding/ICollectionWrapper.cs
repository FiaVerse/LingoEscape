// Copyright (c) Supernova Technologies LLC
using System;
using System.Collections.Generic;

namespace Nova.Internal.DataBinding
{
    internal class ListWrapper<TData> : ListWrapper
    {
        #region Static
        private static Queue<ListWrapper<TData>> pool = new Queue<ListWrapper<TData>>();

        public static ListWrapper<TData> Wrap(IList<TData> source)
        {
            ListWrapper<TData> wrapper = pool.Count > 0 ? pool.Dequeue() : new ListWrapper<TData>();

            wrapper.Source = source;
            return wrapper;
        }
        #endregion

        public IList<TData> Source;

        public override bool IsEmpty => Count == 0;

        public override Type GetElementType() => typeof(TData);

        public override IList<T> GetSource<T>() => Source as IList<T>;

        public override int Count
        {
            get
            {
                if (Source == null)
                {
                    return 0;
                }

                try
                {
                    // Try/Catch because this Count
                    // could be a user-written implementation
                    // of the IList<T> interface
                    return Source.Count;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                    return 0;
                }
            }
        }

        public override bool TryGet<T>(int index, out T value)
        {
            value = default;

            if (IsEmpty)
            {
                return false;
            }

            if (Source is IList<T> typedSource)
            {
                try
                {
                    if (index >= 0 && index < Count)
                    {
                        // Try/Catch because this indexer
                        // could be a user-written implementation
                        // of the IList<T> interface
                        value = typedSource[index];
                        return true;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                    return false;
                }

            }

            TData val = Get(index);

            if (val is T valAsT)
            {
                value = valAsT;
                return true;
            }
            else
            {
                // if val is null, that's fine.
                // if val is non-null but not type T, then we have a type mismatch
                return val == null;
            }
        }

        public override Type GetDataType(int key)
        {
            Type listElementType = typeof(TData);

            if (listElementType.IsValueType || !TryGet(key, out TData val) || val == null)
            {
                // If the TData is a value type, or we couldn't get the value at the key,
                // just return TData
                return listElementType;
            }

            return val.GetType();
        }

        public TData Get(int index)
        {
            try
            {
                if (index >= 0 && index < Count)
                {
                    // Try/Catch because this indexer
                    // could be a user-written implementation
                    // of the IList<T> interface
                    return Source[index];
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            return default;
        }

        public override void Dispose()
        {
            pool.Enqueue(this);
        }
    }

    /// <summary>
    /// Base class for List collection wrapper
    /// </summary>
    internal abstract class ListWrapper : ICollectionWrapper<int>, IDisposable
    {
        public abstract bool IsEmpty { get; }

        public abstract int Count { get; }
        public abstract Type GetDataType(int index);
        public abstract bool TryGet<TData>(int index, out TData value);
        public abstract Type GetElementType();
        public abstract IList<T> GetSource<T>();

        public virtual void Dispose() { }
    }

    /// <summary>
    /// Wraps an arbitrary data structure with a key type of <see cref="TKey"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    internal interface ICollectionWrapper<TKey>
    {
        public int Count { get; }
        Type GetDataType(TKey key);
        bool TryGet<TData>(TKey key, out TData value);
    }
}
