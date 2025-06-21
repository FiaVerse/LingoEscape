// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Collections;

namespace Nova.Internal.Hierarchy
{
    internal struct ProxyContainer
    {
        public const int ExistingDistribution = -1;

        public NovaList<DataStoreID> ProxyIDs;
        public int DesiredChildrenPerProxy;
    }

    internal partial class Hierarchy
    {
        internal struct NativeHierarchy : IInitializable
        {
            public NativeList<HierarchyElement> Elements;
            public NativeDedupedList<DataStoreID> RootIDs;
            public NativeList<int> SiblingPriorities;
            public NovaHashMap<DataStoreID, ProxyContainer> VirtualProxies;
            public NovaHashMap<DataStoreID, DataStoreIndex> Lookup;
            public BatchGroupTracker BatchGroupTracker;

            private NativeList<NovaList<DataStoreIndex>> IndexListPool;

            public struct ReadOnly
            {
                [ReadOnly]
                public NativeList<HierarchyElement> Elements;
                [ReadOnly]
                public NovaHashMap<DataStoreID, ProxyContainer> VirtualProxies;
                [ReadOnly]
                public NovaHashMap<DataStoreID, DataStoreIndex> Lookup;
                public BatchGroupTracker.ReadOnly BatchGroupTracker;

                public DataStoreID GetParentID(DataStoreID childID, bool includeVirtualProxies = false)
                {
                    DataStoreID parentID = GetDirectParentID(childID);

                    if (!includeVirtualProxies)
                    {
                        while (IsVirtual(parentID) && Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
                        {
                            parentID = Elements[parentIndex].ParentID;
                        }
                    }

                    return parentID;
                }

                public DataStoreID GetDirectParentID(DataStoreID childID)
                {
                    if (Lookup.TryGetValue(childID, out DataStoreIndex childIndex))
                    {
                        if (childIndex < 0 || childIndex > Elements.Length)
                        {
                            return DataStoreID.Invalid;
                        }

                        return Elements[childIndex].ParentID;
                    }

                    return DataStoreID.Invalid;
                }

                public bool IsVirtual(DataStoreID elementID)
                {
                    if (Lookup.TryGetValue(elementID, out DataStoreIndex elementIndex))
                    {
                        return HasProxies(Elements[elementIndex].ParentID);
                    }

                    return false;
                }

                public bool HasProxies(DataStoreID elementID)
                {
                    return VirtualProxies.ContainsKey(elementID);
                }

                public bool IsDescendantOf(DataStoreIndex descendantIndex, DataStoreID ancestorID, out DataStoreID childID)
                {
                    return NativeHierarchy.IsDescendantOf(ref Elements, ref Lookup, descendantIndex, ancestorID, out childID);
                }

                public ReadOnly(in NativeHierarchy hierarchy)
                {
                    Elements = hierarchy.Elements;
                    VirtualProxies = hierarchy.VirtualProxies;
                    Lookup = hierarchy.Lookup;
                    BatchGroupTracker = hierarchy.BatchGroupTracker.AsReadOnly();
                }
            }

            public ReadOnly AsReadOnly() => new ReadOnly(this);

            public bool IsVirtual(DataStoreID elementID)
            {
                if (Lookup.TryGetValue(elementID, out DataStoreIndex elementIndex))
                {
                    return HasProxies(Elements[elementIndex].ParentID);
                }

                return false;
            }

            public bool HasProxies(DataStoreID elementID)
            {
                return VirtualProxies.ContainsKey(elementID);
            }

            /// <summary>
            /// Returns the ID of the closest ancestor which is not a virtual proxy element 
            /// (unless includeVirtualProxies == true). In most cases this will be the direct parent.
            /// </summary>
            /// <param name="childID"></param>
            /// <returns></returns>
            public DataStoreID GetParentID(DataStoreID childID, bool includeVirtualProxies = false)
            {
                DataStoreID parentID = GetDirectParentID(childID);

                if (!includeVirtualProxies)
                {
                    while (IsVirtual(parentID) && Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
                    {
                        parentID = Elements[parentIndex].ParentID;
                    }
                }

                return parentID;
            }

            /// <summary>
            /// Returns the ID of the closest ancestor which is not a virtual proxy element 
            /// (unless includeVirtualProxies == true). In most cases this will be the direct parent.
            /// </summary>
            /// <param name="childID"></param>
            /// <returns></returns>
            public DataStoreID GetParentID(DataStoreIndex childIndex, bool includeVirtualProxies = false)
            {
                DataStoreID parentID = GetDirectParentID(childIndex);

                if (!includeVirtualProxies)
                {
                    while (IsVirtual(parentID) && Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
                    {
                        parentID = Elements[parentIndex].ParentID;
                    }
                }

                return parentID;
            }

            public DataStoreID GetDirectParentID(DataStoreID childID)
            {
                if (Lookup.TryGetValue(childID, out DataStoreIndex childIndex))
                {
                    if (childIndex < 0 || childIndex > Elements.Length)
                    {
                        return DataStoreID.Invalid;
                    }

                    return Elements[childIndex].ParentID;
                }

                return DataStoreID.Invalid;
            }

            public DataStoreID GetDirectParentID(DataStoreIndex childIndex)
            {
                if (!childIndex.IsValid)
                {
                    return DataStoreID.Invalid;
                }

                return Elements.ElementAt(childIndex).ParentID;
            }

            public static bool IsDescendantOf(ref NativeList<HierarchyElement> elements, ref NovaHashMap<DataStoreID, DataStoreIndex> lookup, DataStoreIndex descendantIndex, DataStoreID ancestorID, out DataStoreID childID)
            {
                HierarchyElement child = elements[descendantIndex];

                if (child.ID == ancestorID)
                {
                    childID = child.ID;
                    return true;
                }

                while (child.ParentID.IsValid)
                {
                    if (child.ParentID == ancestorID)
                    {
                        childID = child.ID;
                        return true;
                    }

                    child = elements[lookup[child.ParentID]];
                }

                childID = DataStoreID.Invalid;
                return false;
            }

            public NovaList<DataStoreIndex> GetIndexList()
            {
                if (IndexListPool.Length == 0)
                {
                    return new NovaList<DataStoreIndex>(0, Allocator.Persistent);
                }

                NovaList<DataStoreIndex> list = IndexListPool.Last();
                IndexListPool.RemoveLast();

                list.Clear();

                return list;
            }

            public void ReturnIndexList(ref NovaList<DataStoreIndex> list)
            {
                IndexListPool.Add(list);
            }

            public void Init()
            {
                IndexListPool = new NativeList<NovaList<DataStoreIndex>>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
                Elements = new NativeList<HierarchyElement>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
                SiblingPriorities = new NativeList<int>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
                RootIDs = NativeDedupedList<DataStoreID>.Create(Constants.SomeElementsInitialCapacity);
                Lookup = new NovaHashMap<DataStoreID, DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
                VirtualProxies = new NovaHashMap<DataStoreID, ProxyContainer>(Constants.FewElementsInitialCapacity, Allocator.Persistent);
                BatchGroupTracker.Init();
            }

            public void Dispose()
            {
                VirtualProxies.Dispose();
                Lookup.Dispose();
                IndexListPool.DisposeListAndElements();
                SiblingPriorities.Dispose();
                Elements.DisposeListAndElements();
                RootIDs.Dispose();
                BatchGroupTracker.Dispose();
            }
        }
    }
}
