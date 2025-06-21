// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Internal.DataBinding
{
    internal enum PrefabRetrieval
    {
        Success,
        TypeMatchFailed,
        FailedToCreate
    }

    internal class PrefabPool<TKey>
    {
        #region Static
        private static Dictionary<Type, DataBinder> dataBinders = new Dictionary<Type, DataBinder>()
        {
            {typeof(object), new DataBinder<object>() }
        };

        internal static DataBinder GetDataBinder(Type t)
        {
            if (dataBinders.TryGetValue(t, out DataBinder db))
            {
                return db;
            }

            return dataBinders[typeof(object)];
        }
        #endregion

        /// <summary>
        /// The pool under which to store pooled prefabs
        /// </summary>
        private Transform root = null;

        private List<ItemView> listItemPrefabs = new List<ItemView>();

        private Dictionary<ItemView, Stack<ItemView>> prefabPools = new Dictionary<ItemView, Stack<ItemView>>();
        private Dictionary<DataStoreID, (ItemView Prefab, TKey Key)> pagedIn = new Dictionary<DataStoreID, (ItemView Prefab, TKey Key)>();
        private HashList<ItemView> pagedOutItems = new HashList<ItemView>();
        private Dictionary<DataStoreID, TKey> pagedOutKeys = new Dictionary<DataStoreID, TKey>();

        private List<Type> typesWithProviders = new List<Type>();
        private Dictionary<Type, MulticastDelegate> prefabProviders = new Dictionary<Type, MulticastDelegate>();
        private Dictionary<Type, List<Type>> prefabToDataTypeBinders = new Dictionary<Type, List<Type>>();

        private Dictionary<int, ItemView> detachedPrefabsToSourcePrefabs = new Dictionary<int, ItemView>();

        private bool initialized = false;

        public void AddPrefabToDataTypeMapping<TPrefab, TData>()
        {
            Type prefabType = typeof(TPrefab);

            if (!prefabToDataTypeBinders.TryGetValue(prefabType, out List<Type> dataTypes))
            {
                dataTypes = new List<Type>();
                prefabToDataTypeBinders[prefabType] = dataTypes;
            }

            Type dataType = typeof(TData);
            if (!dataTypes.Contains(dataType))
            {
                dataTypes.Add(dataType);

                if (!dataBinders.ContainsKey(dataType))
                {
                    dataBinders.Add(dataType, new DataBinder<TData>());
                }
            }
        }

        public void RemovePrefabToDataTypeMapping<TPrefab, TData>()
        {
            Type prefabType = typeof(TPrefab);

            if (!prefabToDataTypeBinders.TryGetValue(prefabType, out List<Type> dataTypes))
            {
                return;
            }

            dataTypes.Remove(typeof(TData));
        }

        public void AddListItemPrefabProvider<TData>(PrefabProviderCallback<TKey> eventHandler)
        {
            Type dataType = typeof(TData);

            if (prefabProviders.ContainsKey(dataType))
            {
                Debug.LogError($"A different Prefab Provider for the given data type [{typeof(TData)}] is already registered.");
                return;
            }

            prefabProviders.Add(dataType, eventHandler);
            typesWithProviders.Add(dataType);
        }

        /// <summary>
        /// Might want to change this to unregister via the callback?
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        public void RemoveListItemPrefabProvider<TData>()
        {
            Type dataType = typeof(TData);

            if (!prefabProviders.ContainsKey(dataType))
            {
                return;
            }

            prefabProviders.Remove(dataType);
            typesWithProviders.Remove(dataType);
        }

        public PrefabRetrieval GetPrefabInstance(TKey key, Type dataType, out ItemView prefabInstance, out Type userDataType)
        {
            prefabInstance = null;
            ItemView prefabSource = GetSourcePrefab(key, dataType, out userDataType);

            if (prefabSource == null)
            {
                return PrefabRetrieval.TypeMatchFailed;
            }

            if (!prefabPools.TryGetValue(prefabSource, out Stack<ItemView> prefabPool))
            {
                prefabPool = new Stack<ItemView>();
                prefabPools[prefabSource] = prefabPool;
            }

            // If prefabs in the pool were unexpectedly destroyed, try to handle that
            while (prefabInstance == null && prefabPool.Count > 0)
            {
                prefabInstance = prefabPool.Pop();
            }

            if (prefabInstance == null)
            {
                // We cleared the pool but all the elements were invalid, try creating a new one
                try
                {
                    prefabInstance = ItemView.Instantiate(prefabSource, root);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Instantiating prefab {prefabSource.name} failed with: {e}", root);
                    return PrefabRetrieval.FailedToCreate;
                }
            }
            else
            {
                pagedOutItems.Remove(prefabInstance);
                pagedOutKeys.Remove(prefabInstance.UIBlock.ID);
            }

            pagedIn.Add(prefabInstance.UIBlock.ID, (prefabSource, key));

            if (!prefabInstance.gameObject.activeSelf)
            {
                try
                {
                    prefabInstance.gameObject.SetActive(true);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Activating prefab failed with: {e}", root);
                }
            }

            return PrefabRetrieval.Success;
        }

        public void ReturnPrefabInstance(ItemView prefabInstance, TKey key)
        {
            // null check to handle the item being unexpectedly destroyed
            if (prefabInstance == null)
            {
                return;
            }

            if (!pagedIn.TryGetValue(prefabInstance.UIBlock.ID, out var vals))
            {
                // if the prefab source was destroyed, we don't
                // know how to pool this... just destroy it
                try
                {
                    GameObject.Destroy(prefabInstance.gameObject);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                return;
            }

            pagedIn.Remove(prefabInstance.UIBlock.ID);

            Stack<ItemView> prefabPool = prefabPools[vals.Prefab];
            pagedOutItems.Add(prefabInstance);
            pagedOutKeys.Add(prefabInstance.UIBlock.ID, key);
            prefabPool.Push(prefabInstance);
        }

        /// <summary>
        /// Handles when an object has been destroyed
        /// </summary>
        /// <param name="id"></param>
        public void Remove(DataStoreID id)
        {
            pagedIn.Remove(id);
        }

        public bool TryDetachInstance(ItemView prefabInstance, Transform newParent)
        {
            // null check to handle the item being unexpectedly destroyed
            if (prefabInstance == null)
            {
                return false;
            }

            if (!pagedIn.TryGetValue(prefabInstance.UIBlock.ID, out var vals))
            {
                return false;
            }
            pagedIn.Remove(prefabInstance.UIBlock.ID);

            if (prefabInstance.transform.parent == root)
            {
                try
                {
                    prefabInstance.transform.parent = newParent;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Reparenting detached item failed with: {e}");
                }
            }

            detachedPrefabsToSourcePrefabs.Add(prefabInstance.GetInstanceID(), vals.Prefab);
            return true;
        }

        public bool TryReattachInstance(ItemView prefabInstance)
        {
            if (prefabInstance == null || !detachedPrefabsToSourcePrefabs.TryGetValue(prefabInstance.GetInstanceID(), out ItemView prefabSource))
            {
                return false;
            }

            if (!prefabPools.TryGetValue(prefabSource, out Stack<ItemView> prefabPool))
            {
                return false;
            }

            if (prefabInstance.gameObject.activeSelf)
            {
                try
                {
                    prefabInstance.gameObject.SetActive(false);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (prefabInstance.transform.parent != root)
            {
                try
                {
                    prefabInstance.transform.parent = root;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            prefabPool.Push(prefabInstance);
            detachedPrefabsToSourcePrefabs.Remove(prefabInstance.GetInstanceID());

            return true;
        }

        public void FinalizePoolForCurrentFrame()
        {
            for (int i = 0; i < pagedOutItems.Count; ++i)
            {
                ItemView instance = pagedOutItems[i];

                if (instance == null)
                {
                    // destroyed by the user, don't try to handle
                    continue;
                }

                if (instance.gameObject.activeSelf)
                {
                    try
                    {
                        instance.gameObject.SetActive(false);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            pagedOutItems.Clear();
            pagedOutKeys.Clear();
        }

        public bool IsCorrectPrefabType(TKey key, ItemView prefabInstance, Type dataType, out Type matchedUserDataType)
        {
            ItemView possibleSource = GetSourcePrefab(key, dataType, out matchedUserDataType);

            if (!pagedIn.TryGetValue(prefabInstance.UIBlock.ID, out var vals))
            {
                return false;
            }

            return vals.Prefab == possibleSource;
        }

        public bool TryGetKey(DataStoreID id, out TKey key)
        {
            if (pagedIn.TryGetValue(id, out var vals))
            {
                key = vals.Key;
                return true;
            }

            return pagedOutKeys.TryGetValue(id, out key);
        }

        private ItemView GetSourcePrefab(TKey key, Type dataType, out Type matchedDataType)
        {
            ItemView prefab = null;

            // First check to see if there is a matching provider which provides
            // a valid prefab
            for (int i = 0; i < typesWithProviders.Count; ++i)
            {
                if (!typesWithProviders[i].IsAssignableFrom(dataType))
                {
                    continue;
                }

                if (!prefabProviders.TryGetValue(dataType, out MulticastDelegate callback) ||
                    !(callback is PrefabProviderCallback<TKey> prefabProvider))
                {
                    continue;
                }

                try
                {
                    if (!prefabProvider.Invoke(key, out prefab))
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Prefab provider failed with: {ex}");
                    continue;
                }

                if (prefab == null)
                {
                    string typeName = dataType == null ? "Null" : dataType.Name;
                    Debug.LogWarning($"Prefab Provider returned \"true\" but prefab object was null. Falling back to default prefab for type {typeName}.");
                    continue;
                }

                // Valid match found
                matchedDataType = dataType;
                return prefab;
            }

            matchedDataType = typeof(object);

            for (int i = 0; i < listItemPrefabs.Count; ++i)
            {
                ItemView listItemPrefab = listItemPrefabs[i];

                if (listItemPrefab == null)
                {
                    continue;
                }

                if (!listItemPrefab.HasVisuals)
                {
                    Debug.LogError($"The {nameof(ItemView)}.{nameof(ItemView.Visuals)} property must be non-null in order to work correctly.", root);
                    continue;
                }

                Type prefabType = listItemPrefab.TypeOfVisuals;
                if (!prefabToDataTypeBinders.TryGetValue(prefabType, out List<Type> mappedDataTypes))
                {
                    continue;
                }

                for (int j = 0; j < mappedDataTypes.Count; ++j)
                {
                    Type mappedDataType = mappedDataTypes[j];

                    if (mappedDataType.IsAssignableFrom(dataType) &&
                        matchedDataType.IsAssignableFrom(mappedDataType))
                    {
                        matchedDataType = mappedDataType;
                        prefab = listItemPrefabs[i];
                    }
                }
            }

            return prefab;
        }

        public Type GetUserDataType(ItemView prefabInstance, Type dataType)
        {
            Type matchedDataType = typeof(object);
            Type prefabType = prefabInstance.TypeOfVisuals;

            if (prefabToDataTypeBinders.TryGetValue(prefabType, out List<Type> mappedDataTypes))
            {
                for (int j = 0; j < mappedDataTypes.Count; ++j)
                {
                    Type mappedDataType = mappedDataTypes[j];

                    if (mappedDataType.IsAssignableFrom(dataType) &&
                        matchedDataType.IsAssignableFrom(mappedDataType))
                    {
                        matchedDataType = mappedDataType;
                    }
                }
            }

            return matchedDataType;
        }

        public void DestroyPooledPrefabs()
        {
            foreach (var prefabPool in prefabPools)
            {
                Stack<ItemView> pool = prefabPool.Value;

                if (pool == null)
                {
                    continue;
                }

                while (pool.Count > 0)
                {
                    try
                    {
                        GameObject.Destroy(pool.Pop().gameObject);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            prefabPools.Clear();
            detachedPrefabsToSourcePrefabs.Clear();
        }

        public void Init(Transform prefabOwner, List<ItemView> prefabSources)
        {
            if (initialized)
            {
                return;
            }

            root = prefabOwner;

            for (int i = 0; i < prefabSources.Count; ++i)
            {
                ItemView listItemPrefab = prefabSources[i];

                if (listItemPrefab == null)
                {
                    continue;
                }

                if (!listItemPrefab.HasVisuals)
                {
                    Debug.LogError($"[{listItemPrefab.name}] The {nameof(ItemView)}.{nameof(ItemView.Visuals)} property must be non-null in order to work correctly.", listItemPrefab);
                    continue;
                }

                if (listItemPrefabs.Contains(listItemPrefab))
                {
                    // Duplicate
                    continue;
                }

                listItemPrefabs.Add(listItemPrefab);
                prefabPools.Add(listItemPrefab, new Stack<ItemView>());
            }

            initialized = true;
        }
    }
}
