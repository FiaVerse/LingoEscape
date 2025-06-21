// Copyright (c) Supernova Technologies LLC
#define USE_HIERARCHY_JOBS

using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Nova.Internal.Hierarchy
{
    [BurstCompile]
    internal partial class Hierarchy : IInitializable, IDisposable
    {
        private NativeHierarchy hierarchy = default(NativeHierarchy);
        private NativeHierarchy.ReadOnly readOnlyHiearchy = default(NativeHierarchy.ReadOnly);

        public ref NativeHierarchy.ReadOnly ReadOnlyHierarchy => ref readOnlyHiearchy;
        public ref NativeHierarchy HierarchyFastButUnsafe => ref hierarchy;
        public ref NativeList<HierarchyElement> Elements => ref hierarchy.Elements;
        public ref NovaHashMap<DataStoreID, DataStoreIndex> Lookup => ref hierarchy.Lookup;
        public ref BatchGroupTracker BatchGroupTracker => ref hierarchy.BatchGroupTracker;

        private ArrayBuffer<DataStoreID> childIDBufferManaged = new ArrayBuffer<DataStoreID>();
        private ArrayBuffer<int> childPriorityBufferManaged = new ArrayBuffer<int>();
        private NativeList<DataStoreID> idBufferNative = default(NativeList<DataStoreID>);
        private NativeList<int> childPriorityBufferNative = default(NativeList<int>);
        private NativeList<DataStoreIndex> indexBufferNative = default(NativeList<DataStoreIndex>);

        private unsafe static BurstedMethod<BurstFunction> runBursted;

        [FixedAddressValueType]
        private static AddToHierarchyJob addToHierarchyData;
        [FixedAddressValueType]
        private static RemoveFromHierarchyJob removeFromHierarchyData;
        [FixedAddressValueType]
        private static AddBatchRootJob addBatchRootData;
        [FixedAddressValueType]
        private static RemoveBatchRootJob removeBatchRootData;
        [FixedAddressValueType]
        private static AddHierarchyRootJob addHierarchyRootData;
        [FixedAddressValueType]
        private static RemoveHierarchyRootJob removeHierarchyRootData;
        [FixedAddressValueType]
        private static SetParentJob setParentData;
        [FixedAddressValueType]
        private static UnparentJob unparentData;
        [FixedAddressValueType]
        private static SetSiblingIndexJob setSiblingIndexData;
        [FixedAddressValueType]
        private static GetChildrenOrSiblingsJob getChildrenOrSiblingsData;
        [FixedAddressValueType]
        private static AddVirtualProxyJob addVirtualProxyData;
        [FixedAddressValueType]
        private static UpdateVirtualProxyIndexJob updateVirtualProxyData;
        [FixedAddressValueType]
        private static RemoveVirtualProxyJob removeVirtualProxyData;
        [FixedAddressValueType]
        private static AppendAndRedistributeChildrenJob appendAndRedistributeData;
        [FixedAddressValueType]
        private static GetDirtyElements getDirtyBatchElementsData;
        [FixedAddressValueType]
        private static GetRootID getRootIDData;
        [FixedAddressValueType]
        private static SetChildOrderJob setChildOrderData;
        [FixedAddressValueType]
        private static IsDescendantOfJob isDescendantOfData;

        private EngineUtils.Listify<DataStoreID, DataStoreIndex> idsToIndicesRunner;

        [FixedAddressValueType]
        private static CombineSubtrees combineSubtreesRunner;

        public void Add(DataStoreID nodeID, DataStoreID parentID, int siblingPriority, bool isVirtual)
        {
            addToHierarchyData.IDToAdd = nodeID;
            addToHierarchyData.SiblingPriority = siblingPriority;
            addToHierarchyData.ParentID = parentID;
            addToHierarchyData.IsVirtual = isVirtual;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref addToHierarchyData), (int)Function.AddToHierarchy);
            }
        }

        /// <summary>
        /// Registers with parent and returns the newly parented node's sibling index
        /// </summary>
        public int SetParent(DataStoreIndex childIndex, DataStoreID parentID, int siblingPriority)
        {
            if (!parentID.IsValid)
            {
                Debug.LogError("Called SetParent when parent was not found.");
                return -1;
            }

            setParentData.ChildIndex = childIndex;
            setParentData.ParentID = parentID;
            setParentData.SiblingPriority_In = siblingPriority;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref setParentData), (int)Function.SetParent);
            }

            return setParentData.SiblingIndex_Out;
        }

        public void Unparent(DataStoreIndex childIndex, DataStoreID childID)
        {
            DataStoreID formerParentID = hierarchy.GetParentID(childIndex);

            unparentData.ChildID = childID;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref unparentData), (int)Function.Unparent);
            }
            AdjustChildProxyDistribution(formerParentID);
        }

        public void Remove(DataStoreIndex indexToRemove, DataStoreID idToRemove)
        {
            DataStoreID formerParentID = hierarchy.GetParentID(indexToRemove);

            removeFromHierarchyData.IDToRemove = idToRemove;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref removeFromHierarchyData), (int)Function.RemoveFromHierarchy);
            }
            AdjustChildProxyDistribution(formerParentID);
        }

        public void AddHierarchyRoot(DataStoreID rootIDToAdd)
        {
            addHierarchyRootData.RootIDToAdd = rootIDToAdd;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref addHierarchyRootData), (int)Function.AddHierarchyRoot);
            }
        }

        public void RemoveHierarchyRoot(DataStoreID rootIDToRemove, bool isBatchRoot)
        {
            removeHierarchyRootData.RootIDToRemove = rootIDToRemove;
            removeHierarchyRootData.IsBatchRoot = isBatchRoot;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref removeHierarchyRootData), (int)Function.RemoveHierarchyRoot);
            }
        }

        public void RemoveBatchRoot(DataStoreID rootIDToRemove)
        {
            removeBatchRootData.RootIDToRemove = rootIDToRemove;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref removeBatchRootData), (int)Function.RemoveBatchRoot);
            }
        }

        public void AddBatchRoot(DataStoreID rootIDToAdd)
        {
            addBatchRootData.RootIDToAdd = rootIDToAdd;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref addBatchRootData), (int)Function.AddBatchRoot);
            }
        }

        public void SetAsLastSibling(DataStoreID parentID, DataStoreID childID, int newSiblingPriority)
        {
            setSiblingIndexData.ParentID = parentID;
            setSiblingIndexData.ChildID = childID;
            setSiblingIndexData.NewSiblingPriority = newSiblingPriority;
            setSiblingIndexData.LastSibling = true;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref setSiblingIndexData), (int)Function.SetSiblingIndex);
            }
        }

        public void SetAsFirstSibling(DataStoreID parentID, DataStoreID childID)
        {
            setSiblingIndexData.ParentID = parentID;
            setSiblingIndexData.ChildID = childID;
            setSiblingIndexData.NewSiblingPriority = 0;
            setSiblingIndexData.LastSibling = false;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref setSiblingIndexData), (int)Function.SetSiblingIndex);
            }
        }

        public void UpdateChildOrder(DataStoreID parentID, ReadOnlyList<DataStoreID> newChildOrder, ReadOnlyList<int> newChildSiblingPriorities)
        {
            if (!hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
            {
                return;
            }

            if (newChildOrder.Count == 0)
            {
                Debug.LogError($"Called UpdateChildOrder with zero children.");
                return;
            }

            childIDBufferManaged.Clear();
            childIDBufferManaged.AddRange(newChildOrder);

            childPriorityBufferManaged.Clear();
            childPriorityBufferManaged.AddRange(newChildSiblingPriorities);

            idBufferNative.Length = childIDBufferManaged.Count;
            NativeArray<DataStoreID>.Copy(childIDBufferManaged.GetUnderlyingArray(), 0, idBufferNative.AsArray(), 0, childIDBufferManaged.Count);

            childPriorityBufferNative.Length = childIDBufferManaged.Count;
            NativeArray<int>.Copy(childPriorityBufferManaged.GetUnderlyingArray(), 0, childPriorityBufferNative.AsArray(), 0, childIDBufferManaged.Count);

            idsToIndicesRunner.Run();

            setChildOrderData.ParentID = parentID;
            setChildOrderData.ParentIndex = parentIndex;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref setChildOrderData), (int)Function.SetChildOrder);
            }
        }

        public void AdjustChildProxyDistribution(DataStoreID parentID, int childrenPerProxy = -1)
        {
            if (!hierarchy.VirtualProxies.TryGetValue(parentID, out ProxyContainer proxyContainer))
            {
                return;
            }

            indexBufferNative.Clear();


            if (childrenPerProxy > 0)
            {
                proxyContainer.DesiredChildrenPerProxy = childrenPerProxy;
            }

            appendAndRedistributeData.ProxyContainer = proxyContainer;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref appendAndRedistributeData), (int)Function.AppendAndRedistributeChildren);
            }
        }

        public void AddVirtualProxyToParent(DataStoreID virtualProxyID, DataStoreID parentID, int childrenPerProxy, bool insertAtFirstPosition)
        {
            bool parentFound = hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex);

            if (!parentFound)
            {
                Debug.LogError("Called AddVirtualProxyToParent when parent was not found.");
                return;
            }

            HierarchyElement parentElement = hierarchy.Elements[parentIndex];

            indexBufferNative.Clear();

            addVirtualProxyData.VirtualProxyID = virtualProxyID;
            addVirtualProxyData.ParentElement = parentElement;
            addVirtualProxyData.ChildrenPerProxy = childrenPerProxy;
            addVirtualProxyData.FirstSibling = insertAtFirstPosition;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref addVirtualProxyData), (int)Function.AddVirtualProxy);
            }
        }

        public void UpdateVirtualProxyIndex(DataStoreIndex virtualProxyIndex, DataStoreID parentID, int newProxyIndex)
        {
            HierarchyElement virtualProxy = hierarchy.Elements.ElementAt(virtualProxyIndex);
            DataStoreID virtualProxyID = virtualProxy.ID;
            bool parentMatch = virtualProxy.ParentID == parentID;

            if (!parentMatch || !parentID.IsValid)
            {
                Debug.LogError("Called SetVirtualProxyIndex when proxy is not parented to the requested parent.");
                return;
            }

            DataStoreIndex parentIndex = hierarchy.Lookup[parentID];
            HierarchyElement parentElement = hierarchy.Elements.ElementAt(parentIndex);

            indexBufferNative.Clear();

            updateVirtualProxyData.VirtualProxyID = virtualProxyID;
            updateVirtualProxyData.ParentElement = parentElement;
            updateVirtualProxyData.NewProxyIndex = newProxyIndex;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref updateVirtualProxyData), (int)Function.UpdateVirtualProxyIndex);
            }
        }

        public void RemoveVirtualProxyFromParent(DataStoreID virtualProxyID)
        {
            DataStoreID parentID = hierarchy.GetDirectParentID(virtualProxyID);
            bool parentFound = hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex);

            if (!parentFound)
            {
                Debug.LogError("Called RemoveVirtualProxyFromParent when parent was not found.");
                return;
            }

            HierarchyElement parentElement = hierarchy.Elements[parentIndex];

            indexBufferNative.Clear();

            removeVirtualProxyData.VirtualProxyID = virtualProxyID;
            removeVirtualProxyData.ParentElement = parentElement;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref removeVirtualProxyData), (int)Function.RemoveVirtualProxy);
            }
        }

        /// <summary>
        /// Returns the hierarchy root ID of the given child ID
        /// </summary>
        /// <param name="childID"></param>
        /// <returns></returns>
        public DataStoreID GetHierarchyRootID(DataStoreID childID)
        {
            getRootIDData.DescendentID = childID;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref getRootIDData), (int)Function.GetRootID);
            }

            return getRootIDData.RootIDOutput.Length == 0 ? DataStoreID.Invalid : getRootIDData.RootIDOutput[0];
        }

        /// <summary>
        /// Returns the ID of the closest ancestor which is not a shadow element 
        /// (unless includeShadowParents == true). In most cases this will be the direct parent.
        /// </summary>
        /// <param name="childID"></param>
        /// <returns></returns>
        public DataStoreID GetParentID(DataStoreID childID, bool includeVirtualProxies = false)
        {
            return hierarchy.GetParentID(childID, includeVirtualProxies);
        }

        /// <summary>
        /// Returns the ID of the closest ancestor which is not a shadow element 
        /// (unless includeShadowParents == true). In most cases this will be the direct parent.
        /// </summary>
        /// <param name="childIndex"></param>
        /// <returns></returns>
        public DataStoreID GetParentID(DataStoreIndex childIndex, bool includeVirtualProxies = false)
        {
            return hierarchy.GetParentID(childIndex, includeVirtualProxies);
        }

        public JobHandle GetDepthSortedHierarchy(ref NativeList<DataStoreID> elementIDs, ref NativeList<DataStoreIndex> subHierarchyToPopulate, JobHandle dependency)
        {
            combineSubtreesRunner.SubRootIDs = elementIDs;
            combineSubtreesRunner.DepthSortedHierarchyToPopulate = subHierarchyToPopulate;

            return combineSubtreesRunner.NovaScheduleByRef(dependency);
        }

        private static int GetChildCount(ref NovaList<DataStoreID> parentIDs, ref NativeHierarchy hierarchy)
        {
            int activeChildCount = 0;

            for (int i = 0; i < parentIDs.Length; ++i)
            {
                if (hierarchy.Lookup.TryGetValue(parentIDs[i], out DataStoreIndex parentIndex))
                {
                    activeChildCount += hierarchy.Elements[parentIndex].ChildCount;
                }
            }

            return activeChildCount;
        }

        public int GetChildCount(DataStoreID parentID)
        {
            if (hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
            {
                return hierarchy.Elements[parentIndex].ChildCount;
            }

            return 0;
        }

        /// <summary>
        /// Not the most optimal way of tracking/getting sibling indices, but we're just using
        /// them in the editors right now, so it seems ok to do this search at the moment.
        /// </summary>
        /// <param name="elementID"></param>
        /// <returns>The sibling index of the given node in the Hierarchy</returns>
        public int GetSiblingIndex(DataStoreID elementID)
        {
            getChildrenOrSiblingsData.ParentID = DataStoreID.Invalid;
            getChildrenOrSiblingsData.ChildID = elementID;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref getChildrenOrSiblingsData), (int)Function.GetChildrenOrSiblings);
            }
            return getChildrenOrSiblingsData.SiblingIndex_Out;
        }

        public bool IsDescendantOf(DataStoreIndex descendantIndex, DataStoreID ancestorID, out DataStoreID childID)
        {
            isDescendantOfData.DescendantIndex = descendantIndex;
            isDescendantOfData.AncestorID = ancestorID;

            unsafe
            {
                runBursted.Method.Invoke(UnsafeUtility.AddressOf(ref isDescendantOfData), (int)Function.IsDescendantOf);
            }

            childID = isDescendantOfData.OUT_DirectChildID;
            return isDescendantOfData.OUT_Result;
        }

        public JobHandle PopulateWithDirtyBatchElements(ref EngineUpdateInfo infoToPopulate, JobHandle dependency)
        {
            if (!hierarchy.BatchGroupTracker.IsDirty)
            {
                return dependency;
            }

            getDirtyBatchElementsData.ElementsToUpdate = infoToPopulate.ElementsToUpdate;
            getDirtyBatchElementsData.RootsToUpdate = infoToPopulate.RootsToUpdate;
            return getDirtyBatchElementsData.NovaScheduleByRef(dependency);
        }

        public void Init()
        {
            hierarchy.Init();
            readOnlyHiearchy = hierarchy.AsReadOnly();

            idBufferNative = new NativeList<DataStoreID>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
            indexBufferNative = new NativeList<DataStoreIndex>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
            childPriorityBufferNative = new NativeList<int>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);

            InitJobRunners();
            InitBurstMethods();
        }

        private void InitJobRunners()
        {
            addToHierarchyData = new AddToHierarchyJob()
            {
                Hierarchy = hierarchy,
                ChildCache = indexBufferNative,
            };

            removeFromHierarchyData = new RemoveFromHierarchyJob()
            {
                Hierarchy = hierarchy,
            };

            addBatchRootData = new AddBatchRootJob()
            {
                Hierarchy = hierarchy,
            };

            removeBatchRootData = new RemoveBatchRootJob()
            {
                Hierarchy = hierarchy,
            };

            addHierarchyRootData = new AddHierarchyRootJob()
            {
                Hierarchy = hierarchy,
            };

            removeHierarchyRootData = new RemoveHierarchyRootJob()
            {
                Hierarchy = hierarchy,
            };

            setParentData = new SetParentJob()
            {
                Hierarchy = hierarchy,
                ChildCache = indexBufferNative,
            };

            unparentData = new UnparentJob()
            {
                Hierarchy = hierarchy,
            };

            setSiblingIndexData = new SetSiblingIndexJob()
            {
                Hierarchy = hierarchy,
                ChildIndexCache = indexBufferNative,
            };

            getChildrenOrSiblingsData = new GetChildrenOrSiblingsJob()
            {
                Hierarchy = hierarchy,
                Children = indexBufferNative,
            };

            addVirtualProxyData = new AddVirtualProxyJob()
            {
                Hierarchy = hierarchy,
                ChildCache = indexBufferNative,
            };

            updateVirtualProxyData = new UpdateVirtualProxyIndexJob()
            {
                Hierarchy = hierarchy,
                ChildCache = indexBufferNative,
            };

            removeVirtualProxyData = new RemoveVirtualProxyJob()
            {
                Hierarchy = hierarchy,
                ChildCache = indexBufferNative,
            };

            appendAndRedistributeData = new AppendAndRedistributeChildrenJob()
            {
                Hierarchy = hierarchy,
                Children = indexBufferNative,
            };

            idsToIndicesRunner = new EngineUtils.Listify<DataStoreID, DataStoreIndex>()
            {
                Map = hierarchy.Lookup,
                KeysToListify = idBufferNative,
                Listified = indexBufferNative,
            };

            combineSubtreesRunner = new CombineSubtrees()
            {
                Hierarchy = readOnlyHiearchy,
            };

            getDirtyBatchElementsData = new GetDirtyElements()
            {
                Hierarchy = readOnlyHiearchy,
            };

            getRootIDData = new GetRootID()
            {
                Hierarchy = readOnlyHiearchy,
                RootIDOutput = idBufferNative,
            };

            setChildOrderData = new SetChildOrderJob()
            {
                Hierarchy = hierarchy,
                ChildIndexCache = indexBufferNative,
                ChildPriorityCache = childPriorityBufferNative,
            };

            isDescendantOfData = new IsDescendantOfJob()
            {
                Hierarchy = readOnlyHiearchy,
            };
        }

        private unsafe void InitBurstMethods()
        {
            runBursted = new BurstedMethod<BurstFunction>(Run);
        }

        public void Dispose()
        {
            hierarchy.Dispose();
            idBufferNative.Dispose();
            indexBufferNative.Dispose();
            childPriorityBufferNative.Dispose();
        }
    }
}
