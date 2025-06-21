// Copyright (c) Supernova Technologies LLC
//#define LOG_REGISTRATIONS
//#define VALIDATED_REGISTRATIONS
using Nova.Internal.Utilities;
using System.Collections.Generic;

namespace Nova.Internal.Core
{
    internal abstract class DataStore<TDataStore,T> : IDataStore, IFrameDirtyable
        where TDataStore : DataStore<TDataStore, T>
        where T : IDataStoreElement
    {

        public static TDataStore Instance { get; private set; } = null;

        public Dictionary<DataStoreID, T> Elements = new Dictionary<DataStoreID, T>();
        protected bool initialized = false;

        public int Count { get; private set; } = 0;

        public abstract bool IsDirty { get; }

        protected abstract void Add(T val);
        protected abstract void RemoveAtSwapBack(DataStoreID idToRemove, DataStoreIndex indexToRemove);

        protected abstract void CopyToStoreImpl(T val);
        protected abstract void CopyFromStoreImpl(T val);
        protected abstract void CloneImpl(T source, T destination);

        protected abstract bool TryGetIndex(DataStoreID id, out DataStoreIndex index);
        protected abstract DataStoreID GetID(DataStoreIndex index);

        public abstract void ClearDirtyState();

        public void CopyToStore(T val)
        {
            if (!initialized)
            {
                return;
            }

            if (!Instance.IsRegistered(val.UniqueID))
            {
                return;
            }

            Instance.CopyToStoreImpl(val);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public void CopyFromStore(T val)
        {
            if (!initialized)
            {
                return;
            }

            if (!Instance.IsRegistered(val.UniqueID))
            {
                return;
            }

            Instance.CopyFromStoreImpl(val);
        }

        public void Clone(DataStoreID sourceID, T destination)
        {
            if (!initialized)
            {
                return;
            }

            if (!Instance.Elements.TryGetValue(sourceID, out T source))
            {
                return;
            }

            Instance.CloneImpl(source, destination);
        }

        public bool IsRegisteredUnsafe(T val)
        {
            if (!initialized)
            {
                return false;
            }
            return val.Index.IsValid;
        }

        public bool IsRegistered(DataStoreID id)
        {
            if (!initialized)
            {
                return false;
            }

            return Elements.ContainsKey(id);
        }

        /// <summary>
        /// Registers the element and returns the index
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public void Register(T val)
        {
            if (!initialized)
            {
                return;
            }

            if (!val.UniqueID.IsValid)
            {
                // Attempting to register an element with an invalid ID
                return;
            }

            if (IsRegistered(val.UniqueID))
            {
                return;
            }


            Elements.Add(val.UniqueID, val);
            val.SetIndex(Elements.Count - 1);
            {
                Add(val);
            }
            Count += 1;

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public void Unregister(T val)
        {
            DataStoreID id = val.UniqueID;

            if (!initialized || !IsRegistered(id))
            {
                return;
            }

            if (!TryGetIndex(id, out DataStoreIndex index))
            {
                return;
            }

            {
                RemoveAtSwapBack(id, index);
            }

            val.SetIndex(DataStoreIndex.Invalid);
            Elements.Remove(id);

            if (index < Elements.Count)
            {
                if (Elements.TryGetValue(GetID(index), out val))
                {
                    val.SetIndex(index);
                }
            }

            Count -= 1;

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public virtual void Init()
        {
            Instance = (TDataStore)this;
            initialized = true;
        }

        public virtual void Dispose()
        {
            Instance = null;
            initialized = false;

            // If these aren't cleared, but we do an artificial dispose,
            // We can get into a state where unregistered blocks have valid indices
            foreach (var element in Elements)
            {
                if (element.Value == null)
                {
                    continue;
                }

                element.Value.SetIndex(DataStoreIndex.Invalid);
            }
        }
    }
}

