// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Internal
{
    [Serializable]
    internal class VirtualBlockModule : IDisposable
    {
        public static readonly VirtualBlockModule Empty = new VirtualBlockModule(null);

        private static Dictionary<DataStoreID, VirtualBlockModule> modules = new Dictionary<DataStoreID, VirtualBlockModule>();

        public bool IsValid => owner != null;
        public CoreBlock Owner => owner;

        [SerializeReference, HideInInspector]
        private List<VirtualBlock> virtualBlocks = null;
        [NonSerialized, HideInInspector]
        private List<DataStoreID> virtualBlockIDs = null;
        [SerializeField, HideInInspector]
        private int childrenPerVirtualBlock;
        [SerializeField, HideInInspector]
        private CoreBlock owner = null;

        public void EnsureIDs()
        {
            if (virtualBlocks == null)
            {
                return;
            }

            if (virtualBlockIDs == null)
            {
                virtualBlockIDs = new List<DataStoreID>();
            }

            virtualBlockIDs.Clear();
            for (int i = 0; i < virtualBlocks.Count; ++i)
            {
                virtualBlockIDs.Add(virtualBlocks[i].ID);
            }

            modules[owner.ID] = this;
        }

        public VirtualBlockModule(CoreBlock owner)
        {
            this.owner = owner;

            if (owner != null)
            {
                modules.Add(owner.ID, this);
            }
        }

        public ReadOnlyList<DataStoreID> IDs
        {
            get
            {
                return virtualBlockIDs == null ? ReadOnlyList<DataStoreID>.Empty : virtualBlockIDs.ToReadOnly();
            }
        }

        public ReadOnlyList<VirtualBlock> Blocks
        {
            get
            {
                return virtualBlocks == null ? ReadOnlyList<VirtualBlock>.Empty : virtualBlocks.ToReadOnly();
            }
        }

        public int Count => virtualBlocks == null ? 0 : virtualBlocks.Count;

        public void AddVirtualBlock(VirtualBlock virtualBlock, int childrenPerBlock, bool insertAtFirstPosition)
        {
            if (!IsValid)
            {
                return;
            }

            if (virtualBlocks == null)
            {
                virtualBlocks = new List<VirtualBlock>();
            }

            if (virtualBlockIDs == null || virtualBlockIDs.Count != virtualBlocks.Count)
            {
                EnsureIDs();
            }

            childrenPerVirtualBlock = childrenPerBlock;

            if (owner.Activated)
            {
                HierarchyDataStore.Instance.ParentVirtualProxy(virtualBlock, owner.ID, childrenPerBlock, insertAtFirstPosition);
            }

            // Set sibling priority after updating hierarchy data store
            // just because name gets cached there, which will query the hierarchy,
            // so setting the priority first will cache a stale value
            if (insertAtFirstPosition)
            {
                virtualBlocks.Insert(0, virtualBlock);
                virtualBlockIDs.Insert(0, virtualBlock.ID);

                // Everything has shifted, need to update. Ideally this
                // only happens in relatively small batches.
                for (int i = 0; i < virtualBlocks.Count; ++i)
                {
                    virtualBlocks[i].SetSiblingPriority(i);
                }
            }
            else
            {
                virtualBlocks.Add(virtualBlock);
                virtualBlockIDs.Add(virtualBlock.ID);
                virtualBlock.SetSiblingPriority(virtualBlockIDs.Count - 1);
            }
        }

        public void MoveVirtualBlock(VirtualBlock virtualBlock, int newIndex)
        {
            if (virtualBlocks == null)
            {
                return;
            }

            int currentIndex = virtualBlock.SiblingPriority;
            if (currentIndex == newIndex)
            {
                return;
            }

            if (virtualBlocks[currentIndex] != virtualBlock)
            {
                Debug.LogError($"Virtual Block not found at current index, {currentIndex}.");

                return;
            }

            if (owner.Activated)
            {
                HierarchyDataStore.Instance.SetVirtualProxyIndex(((IDataStoreElement)virtualBlock).Index, owner.ID, newIndex);
            }

            virtualBlocks.RemoveAt(currentIndex);
            virtualBlockIDs.RemoveAt(currentIndex);

            virtualBlocks.Insert(newIndex, virtualBlock);
            virtualBlockIDs.Insert(newIndex, virtualBlock.ID);

            // Everything has shifted, need to update. Ideally this
            // only happens in relatively small batches.
            for (int i = Mathf.Min(currentIndex, newIndex); i < virtualBlocks.Count; ++i)
            {
                virtualBlocks[i].SetSiblingPriority(i);
            }
        }

        public void RemoveVirtualBlock(VirtualBlock virtualBlock)
        {
            if (virtualBlocks == null)
            {
                return;
            }

            if (!owner.Activated)
            {
                return;
            }

            int index = virtualBlocks.LastIndexOf(virtualBlock);

            if (index < 0)
            {
                return;
            }

            virtualBlocks.RemoveAt(index);
            virtualBlockIDs.RemoveAt(index);

            HierarchyDataStore.Instance.UnparentVirtualProxy(virtualBlock);

            // Everything has shifted, need to update. Ideally this
            // only happens in relatively small batches.
            for (int i = index; i < virtualBlocks.Count; ++i)
            {
                virtualBlocks[i].SetSiblingPriority(i);
            }

            // Set sibling priority after updating hierarchy data store
            // just because name gets cached there, which will query the hierarchy,
            // so setting the priority first will cache a stale value
            virtualBlock.SetSiblingPriority(VirtualBlock.InvalidPriority);
        }

        public void RedistributeChildren(int childrenPerBlock)
        {
            if (virtualBlocks == null)
            {
                return;
            }

            if (!owner.Activated)
            {
                return;
            }

            HierarchyDataStore.Instance.AdjustChildProxyDistribution(owner.ID, childrenPerBlock);
        }

        public void HandleOwnerDisabled()
        {
            if (virtualBlocks == null)
            {
                return;
            }

            for (int i = virtualBlocks.Count - 1; i >= 0; --i)
            {
                VirtualBlock vBlock = virtualBlocks[i];
                vBlock.CopyFromDataStore();
                HierarchyDataStore.Instance.UnparentVirtualProxy(vBlock);
                vBlock.SetSiblingPriority(VirtualBlock.InvalidPriority);
                vBlock.Dispose();
            }
        }

        public void HandleOwnerEnabled()
        {
            if (virtualBlocks == null)
            {
                return;
            }

            for (int i = 0; i < virtualBlocks.Count; ++i)
            {
                VirtualBlock vBlock = virtualBlocks[i];
                vBlock.Init();
                HierarchyDataStore.Instance.ParentVirtualProxy(vBlock, owner.ID, childrenPerVirtualBlock, insertAtFirstPosition: false);
                vBlock.SetSiblingPriority(i);
            }
        }

        public void Dispose()
        {
            if (owner != null)
            {
                modules.Remove(owner.ID);
            }

            if (virtualBlocks == null)
            {
                return;
            }

            for (int i = virtualBlocks.Count - 1; i >= 0; --i)
            {
                VirtualBlock virtualBlock = virtualBlocks[i];
                RemoveVirtualBlock(virtualBlock);
                virtualBlock.Dispose();
            }
        }

        public static VirtualBlockModule Clone(DataStoreID ownerSourceID, CoreBlock newOwner)
        {
            if (!modules.TryGetValue(ownerSourceID, out VirtualBlockModule ownerModule))
            {
                return Empty;
            }

            return ownerModule.CloneForOwner(newOwner);
        }

        private VirtualBlockModule CloneForOwner(CoreBlock newOwner)
        {
            if (virtualBlocks == null)
            {
                return Empty;
            }

            VirtualBlockModule source = new VirtualBlockModule(newOwner);

            for (int i = 0; i < virtualBlocks.Count; ++i)
            {
                source.AddVirtualBlock(virtualBlocks[i].Clone(), childrenPerVirtualBlock, insertAtFirstPosition: false);
            }

            return source;
        }
    }
}
