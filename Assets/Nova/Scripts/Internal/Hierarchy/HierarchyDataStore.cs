// Copyright (c) Supernova Technologies LLC
//#define AGGRESSIVE_INDEX_GETTERS

#define CACHE_NAME

using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Unity.Collections;
using UnityEngine;
using static Nova.Internal.Hierarchy.Hierarchy;

namespace Nova.Internal.Hierarchy
{
    internal class HierarchyDataStore : DataStore<HierarchyDataStore, IHierarchyBlock>
    {
        public IndexIDMapper IDToIndexMap { get; private set; } = new IndexIDMapper();
        public ref NativeDedupedList<DataStoreID> HierarchyRootIDs => ref hierarchy.HierarchyFastButUnsafe.RootIDs;

        private Hierarchy hierarchy = new Hierarchy();

        public ref NativeHierarchy.ReadOnly ReadOnlyHierarchy => ref hierarchy.ReadOnlyHierarchy;
        public ref NativeHierarchy HierarchyFastButUnsafe => ref hierarchy.HierarchyFastButUnsafe;
        public ref NativeList<HierarchyElement> Hierarchy => ref hierarchy.Elements;
        public ref NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup => ref hierarchy.Lookup;
        public ref BatchGroupTracker BatchGroupTracker => ref hierarchy.BatchGroupTracker;

        protected override bool TryGetIndex(DataStoreID id, out DataStoreIndex index) => IDToIndexMap.TryGetIndex(id, out index);
        protected override DataStoreID GetID(DataStoreIndex index) => IDToIndexMap.ToID(index);

        public override bool IsDirty => BatchGroupTracker.IsDirty;

        public override void ClearDirtyState() => BatchGroupTracker.ClearDirtyState();

        protected override void Add(IHierarchyBlock val)
        {

            int siblingPriority = val.SiblingPriority;
            IDToIndexMap.Add(val.UniqueID, Count);

            DataStoreID parentID = DataStoreID.Invalid;

            IHierarchyBlock parent = val.Parent;
            if (parent != null)
            {
                if (parent.IsRegistered)
                {
                    parentID = parent.UniqueID;
                }
                else
                {
                    Debug.LogError($"Tried to register {val.Name} but parent {parent.Name} was not registered.");
                }
            }

            hierarchy.Add(val.UniqueID, parentID, siblingPriority, val.IsVirtual);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        /// <summary>
        /// Registers with parent and returns the newly parented node's sibling index
        /// </summary>
        /// <param name="child"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public int RegisterWithParent(IHierarchyBlock child, IHierarchyBlock parent)
        {
            bool parentRegistered = IsRegisteredUnsafe(parent);
            bool childRegistered = IsRegisteredUnsafe(child);

            if (!parentRegistered || !childRegistered)
            {
                Debug.LogError("Called RegisterWithParent when parent or child was not registered");
                return -1;
            }

            DataStoreID childID = child.UniqueID;
            DataStoreID newParentID = parent.UniqueID;

            int addedItemSiblingIndex = hierarchy.SetParent(child.Index, newParentID, child.SiblingPriority);

            RemoveHierarchyRoot(childID);

            EditModeUtils.QueueEditorUpdateNextFrame();

            return addedItemSiblingIndex;
        }

        /// <summary>
        /// Only call if when parent is actually null. Otherwise just register
        /// with the new parent, and the unregister will be taken care of.
        /// </summary>
        /// <param name="child"></param>
        public void UnregisterFromParent(IHierarchyBlock child)
        {
            if (!IsRegisteredUnsafe(child))
            {
                Debug.LogError("Called UnregisterFromParent when child was not registered");
                return;
            }

            DataStoreID childID = child.UniqueID;
            DataStoreIndex childIndex = child.Index;
            DataStoreID parentID = hierarchy.GetParentID(childIndex);
            if (!parentID.IsValid)
            {
                return;
            }

            hierarchy.Unparent(childIndex, childID);

            if (!child.Deactivating)
            {
                AddHierarchyRoot(childID);
            }

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public void ParentVirtualProxy(IHierarchyBlock virtualProxy, DataStoreID newParentID, int childrenPerProxy, bool insertAtFirstPosition)
        {
            bool proxyRegistered = IsRegisteredUnsafe(virtualProxy);

            if (!proxyRegistered)
            {
                Debug.LogError("Called ReparentVirtualProxy when virtual element was not registered");
                return;
            }

            DataStoreID virtualProxyID = virtualProxy.UniqueID;
            DataStoreID formerParentID = hierarchy.GetParentID(virtualProxyID);
            if (formerParentID == newParentID)
            {
                // Nothing to do, already shadowing parent
                return;
            }

            hierarchy.AddVirtualProxyToParent(virtualProxyID, newParentID, childrenPerProxy, insertAtFirstPosition);
        }

        public void SetVirtualProxyIndex(DataStoreIndex virtualProxyIndex, DataStoreID parentID, int newProxyIndex)
        {
            if (!virtualProxyIndex.IsValid)
            {
                Debug.LogError("Called SetVirtualProxyIndex when virtual element was not registered");
                return;
            }

            hierarchy.UpdateVirtualProxyIndex(virtualProxyIndex, parentID, newProxyIndex);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public void UnparentVirtualProxy(IHierarchyBlock virtualProxy)
        {
            bool proxyRegistered = IsRegisteredUnsafe(virtualProxy);

            if (!proxyRegistered)
            {
                Debug.LogError("Called UnparentVirtualProxy when virtual element was not registered");
                return;
            }

            DataStoreID virtualProxyID = virtualProxy.UniqueID;
            hierarchy.RemoveVirtualProxyFromParent(virtualProxyID);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public void AdjustChildProxyDistribution(DataStoreID parentID, int childrenPerProxy)
        {
            hierarchy.AdjustChildProxyDistribution(parentID, childrenPerProxy);
        }

        protected override void RemoveAtSwapBack(DataStoreID idToRemove, DataStoreIndex indexToRemove)
        {
            hierarchy.Remove(indexToRemove, idToRemove);
            RemoveHierarchyRoot(idToRemove);

            IDToIndexMap.RemoveAtSwapBack(indexToRemove);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        private void AddHierarchyRoot(DataStoreID rootID)
        {
            hierarchy.AddHierarchyRoot(rootID);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        private void RemoveHierarchyRoot(DataStoreID rootID)
        {
            ICoreBlock root = Elements[rootID] as ICoreBlock;

            // if root is null, means the removed element is a virtual block
            bool isBatchRoot = root != null && root.IsBatchRoot;

            hierarchy.RemoveHierarchyRoot(rootID, isBatchRoot);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        /// <summary>
        /// This should never be called unless the Node this RootID
        /// belongs to has IsBatchRoot == true. This method doesn't check
        /// because currently this only gets called in 1 place.
        /// </summary>
        /// <param name="rootID"></param>
        public void AddBatchRoot(DataStoreID rootID)
        {
            if (!IsRegistered(rootID))
            {
                return;
            }

            hierarchy.AddBatchRoot(rootID);

            EditModeUtils.QueueEditorUpdateNextFrame();

        }

        public void RemoveBatchRoot(DataStoreID rootID)
        {
            if (!IsRegistered(rootID))
            {
                return;
            }

            if (!HierarchyRootIDs.Contains(rootID))
            {
                hierarchy.RemoveBatchRoot(rootID);
            }

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public IHierarchyBlock GetHierarchyRoot(DataStoreID nodeID)
        {
            DataStoreID rootID = hierarchy.GetHierarchyRootID(nodeID);

            if (Elements.TryGetValue(rootID, out IHierarchyBlock root))
            {
                return root;
            }

            return null;
        }

        public IHierarchyBlock GetHierarchyParent(DataStoreID nodeID)
        {
            DataStoreID parentID = hierarchy.GetParentID(nodeID, includeVirtualProxies: true);
            if (Elements.TryGetValue(parentID, out IHierarchyBlock parent))
            {
                return parent;
            }

            return null;
        }

        public bool IsDescendantOf(DataStoreIndex descendantIndex, DataStoreID ancestorID, out IHierarchyBlock directChild)
        {
            if (!hierarchy.IsDescendantOf(descendantIndex, ancestorID, out DataStoreID directChildID))
            {
                directChild = null;
                return false;
            }

            directChild = Elements[directChildID];
            return true;
        }

        public NovaList<DataStoreIndex> GetChildIndices(DataStoreIndex parentIndex)
        {
            return hierarchy.Elements[parentIndex].Children;
        }

        public Unity.Jobs.JobHandle GetDepthSortedHierarchy(ref NativeList<DataStoreID> elementIDs, ref NativeList<DataStoreIndex> depthSortedIndicesToPopulate, Unity.Jobs.JobHandle dependency)
        {
            return hierarchy.GetDepthSortedHierarchy(ref elementIDs, ref depthSortedIndicesToPopulate, dependency);
        }

        public int GetChildCount(DataStoreID nodeID)
        {
            return hierarchy.GetChildCount(nodeID);
        }

        /// <summary>
        /// Not the most optimal way of tracking/getting sibling indices, but we're just using
        /// them in the editors right now, so it seems ok to do this search at the moment.
        /// </summary>
        /// <param name="nodeID"></param>
        /// <returns>The sibling index of the given node in the Hierarchy</returns>
        public int GetSiblingIndex(DataStoreID nodeID)
        {
            return hierarchy.GetSiblingIndex(nodeID);
        }

        public void SetAsFirstSibling(DataStoreID parentID, DataStoreID childID)
        {
            hierarchy.SetAsFirstSibling(parentID, childID);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public void SetAsLastSibling(DataStoreID parentID, DataStoreID childID, int newSiblingPriority)
        {
            hierarchy.SetAsLastSibling(parentID, childID, newSiblingPriority);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public void UpdateChildOrder(DataStoreID parentID, ReadOnlyList<DataStoreID> newChildOrder, ReadOnlyList<int> newChildPriorities)
        {
            hierarchy.UpdateChildOrder(parentID, newChildOrder, newChildPriorities);

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        public Unity.Jobs.JobHandle PopulateWithDirtyBatchElements(ref EngineUpdateInfo infoToPopulate, Unity.Jobs.JobHandle dependency)
        {
            return hierarchy.PopulateWithDirtyBatchElements(ref infoToPopulate, dependency);
        }

        public override void Init()
        {
            base.Init();
            IDToIndexMap.Init();
            hierarchy.Init();
        }

        public override void Dispose()
        {
            base.Dispose();
            IDToIndexMap.Dispose();
            hierarchy.Dispose();
        }

        protected override void CopyToStoreImpl(IHierarchyBlock val)
        {

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        protected override void CopyFromStoreImpl(IHierarchyBlock val) { }
        protected override void CloneImpl(IHierarchyBlock source, IHierarchyBlock destination) { }
    }
}
