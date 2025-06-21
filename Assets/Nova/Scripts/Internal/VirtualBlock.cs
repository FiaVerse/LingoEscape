// Copyright (c) Supernova Technologies LLC
#define CACHE_NAME

using Nova.Internal;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using System;
using System.Diagnostics;
using UnityEngine;

namespace Nova
{
    
    [Serializable]
    internal abstract class VirtualBlock : IHierarchyBlock, IDisposable
    {
        public const int InvalidPriority = -1;

        bool IHierarchyBlock.IsBatchRoot => false;
        bool IHierarchyBlock.IsHierarchyRoot => Self.IsRegistered && Self.Parent == null;
        IHierarchyBlock IHierarchyBlock.Root => HierarchyDataStore.Instance.GetHierarchyRoot(ID);
        IHierarchyBlock IHierarchyBlock.Parent => HierarchyDataStore.Instance.GetHierarchyParent(ID);

        private int siblingPriority = InvalidPriority;
        public int SiblingPriority => siblingPriority;

        private protected IHierarchyBlock Self
        {
            get
            {
                return this;
            }
        }

        public void SetSiblingPriority(int index)
        {
            siblingPriority = index;
        }

        bool IHierarchyActivatable.Activated => true;
        bool IHierarchyActivatable.ActiveInHierarchy => Self.Parent == null || Self.Parent.ActiveInHierarchy;
        bool IHierarchyActivatable.ActiveSelf => true;
        bool IHierarchyActivatable.Deactivating => BeingDestroyed;
        Transform ITransformProvider.Transform => null;
        bool ITransformProvider.IsVirtual => true;
        bool ITransformProvider.TransformCanBeRegistered => true;

        [NonSerialized]
        private DataStoreID _id = DataStoreID.Invalid;
        DataStoreID IDataStoreElement.UniqueID => ID;
        internal DataStoreID ID
        {
            get
            {
                if (!_id.IsValid)
                {
                    _id = DataStoreID.Create();
                }

                return _id;
            }
        }

        [NonSerialized]
        private DataStoreIndex _index;
        DataStoreIndex IDataStoreElement.Index => _index;
        void IDataStoreElement.SetIndex(DataStoreIndex index) => _index = index;

        bool IDataStoreElement.IsRegistered => HierarchyDataStore.Instance.IsRegisteredUnsafe(this);

        string INamedElement.Name => Self.Parent != null ? $"{Self.Parent.Name}.Virtual({Self.SiblingPriority})" : "Detached Virtual Block";


        internal int BlockCount => HierarchyDataStore.Instance.GetChildCount(ID);

        [field: NonSerialized]
        private protected bool BeingDestroyed { get; private set; } = false;

        [NonSerialized]
        private protected bool initialized = false;
        [NonSerialized]
        private bool initializing = false;
        public void Init()
        {
            if (initializing || initialized)
            {
                // Ensures we don't invoke this path recursively.
                // Derived classes may try to access this.Node
                // while initializing
                return;
            }

            initializing = true;


            BeingDestroyed = false;

            Self.Register();

            initialized = true;
            initializing = false;
        }

        void IDataStoreElement.Register() => Register();
        void IDataStoreElement.Unregister() => Unregister();

        private protected abstract void Register();
        private protected abstract void Unregister();
        internal abstract void CopyFromDataStore();
        private protected abstract void CloneFromDataStore(DataStoreID sourceID);
        private protected abstract void HandleParentChanged();
        internal abstract VirtualBlock Clone();

        public void Dispose()
        {
            BeingDestroyed = true;

            Self.Unregister();

            initialized = false;
            initializing = false;
        }

    }
}
