// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// An abstract base class for custom components to synchronize a set of properties based on their transform hierarchy
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(UIBlockActivator))]
    public abstract class CoreBlock : MonoBehaviour, ICoreBlock, ISerializationCallbackReceiver
    {
        bool IHierarchyBlock.IsHierarchyRoot => Parent == null;
        bool IHierarchyBlock.IsBatchRoot => IsBatchRoot;
        int IHierarchyBlock.SiblingPriority => transform.GetSiblingIndex();

        [NonSerialized, HideInInspector]
        private bool childHandledHierarchyChange = false;
        [NonSerialized, HideInInspector]
        private bool childrenDirty = true;

        bool ICoreBlock.ChildHandledHierarchyChangeForParent { get => childHandledHierarchyChange; set => childHandledHierarchyChange = value; }
        bool ICoreBlock.ChildrenAreDirty { get => childrenDirty; set => childrenDirty = value; }
        IHierarchyBlock IHierarchyBlock.Root => HierarchyDataStore.Instance.GetHierarchyRoot(ID);
        IHierarchyBlock IHierarchyBlock.Parent => Self.Parent;

        internal bool Activated => activated;


        [NonSerialized, HideInInspector]
        private CoreBlock _parent = null;
        private CoreBlock Parent
        {
            get
            {
                return _parent;
            }
            set
            {
                if (!Self.Activated || value == _parent)
                {
                    return;
                }

                if (_parent != null && _parent.Self.IsRegistered && value == null)
                {
                    bool parentState = _parent.Self.ChildHandledHierarchyChangeForParent;

                    Self.UnregisterFromParent();

                    if (Deactivating && !parentState)
                    {
                        // if we are deactivating, we set this flag outside of a transform
                        // hierarchy change event, which means leaving it set will keep the
                        // parent in a stale state until a real transform hierarchy change
                        // occurs, and at that point it will be incorrect.
                        _parent.Self.ChildHandledHierarchyChangeForParent = false;
                    }
                }

                _parent = value;

                if (_parent != null)
                {
                    bool parentState = _parent.Self.ChildHandledHierarchyChangeForParent;

                    Self.EnsureRegisteredWithParent();

                    if (initializing && !parentState)
                    {
                        // if we are initializing, we set this flag outside of a transform
                        // hierarchy change event, which means leaving it set will keep the
                        // parent in a stale state until a real transform hierarchy change
                        // occurs, and at that point it will be incorrect.
                        _parent.Self.ChildHandledHierarchyChangeForParent = false;
                    }
                }

                HandleParentChanged();
            }
        }

        ICoreBlock ICoreBlock.Parent
        {
            get
            {
                return Parent;
            }
            set
            {
                Parent = value as CoreBlock;
            }
        }

        private protected ICoreBlock Self
        {
            get
            {
                Init();
                return this;
            }
        }

        private protected abstract bool IsBatchRoot { get; }

        bool IHierarchyActivatable.Activated => activated;
        bool IHierarchyActivatable.ActiveInHierarchy => gameObject.activeInHierarchy;
        bool IHierarchyActivatable.ActiveSelf => gameObject.activeSelf;
        bool IHierarchyActivatable.Deactivating => Deactivating;
        Transform ITransformProvider.Transform => transform;
        bool ITransformProvider.IsVirtual => false;
        bool ITransformProvider.TransformCanBeRegistered => NovaApplication.IsEditor ? transform.childCount > 0 || transform.parent != null : true;

        [NonSerialized, HideInInspector]
        private DataStoreID _id = DataStoreID.Invalid;
        [SerializeField, HideInInspector, NotKeyable]
        private DataStoreID sourceID = DataStoreID.Invalid;

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

        [NonSerialized, HideInInspector]
        private DataStoreIndex _index = DataStoreIndex.Invalid;
        DataStoreIndex IDataStoreElement.Index => _index;
        void IDataStoreElement.SetIndex(DataStoreIndex index) => _index = index;

        bool IDataStoreElement.IsRegistered
        {
            get
            {
                return _index.IsValid;
            }
        }

        string INamedElement.Name => name;


        internal int BlockCount => HierarchyDataStore.Instance.GetChildCount(ID);

        private protected bool Deactivating => deactivating;

        private protected abstract void EditorOnly_EnsureTransformRegistration();
        internal void EditorOnly_EnsureParent() => OnTransformParentChanged();
        internal void EditorOnly_EnsureChildren() => OnTransformChildrenChanged();

        private void OnTransformParentChanged()
        {
            if (!Activated)
            {
                return;
            }

            if (transform.parent != null &&
                transform.parent.TryGetComponent(out CoreBlock parentBlock) &&
                parentBlock.ShouldBeRegistered())
            {
                Parent = parentBlock;
            }
            else
            {
                Parent = null;
            }

            if (NovaApplication.IsEditor)
            {
                EditorOnly_EnsureTransformRegistration();
            }
        }

        private void OnTransformChildrenChanged()
        {
            if (!Activated)
            {
                return;
            }

            Self.RefreshChildren();

            if (NovaApplication.IsEditor)
            {
                EditorOnly_EnsureTransformRegistration();
            }
        }

        internal void SetAsFirstSibling()
        {
            Self.SetAsFirstSibling();
            transform.SetAsFirstSibling();
        }

        internal void SetAsLastSibling()
        {
            Transform parentTransform = transform.parent;
            Self.SetAsLastSibling(parentTransform == null ? 0 : parentTransform.childCount - 1);
            transform.SetAsLastSibling();
        }

        [NonSerialized, HideInInspector]
        private List<DataStoreID> childIDs = new List<DataStoreID>(0);
        List<DataStoreID> ICoreBlock.ChildIDs => childIDs;

        [NonSerialized, HideInInspector]
        private List<ICoreBlock> children = new List<ICoreBlock>(0);
        List<ICoreBlock> ICoreBlock.Children => children;

        /// <summary>
        /// Returns the internal list of Block IDs as an
        /// optimization to bypass all Block accessors when looping.
        /// </summary>
        /// <returns></returns>
        internal ReadOnlyList<DataStoreID> ChildBlockIDs
        {
            get
            {
                return childIDs.ToReadOnly();
            }
        }

        internal ReadOnlyList<ICoreBlock> ChildBlocks
        {
            get
            {
                return children.ToReadOnly();
            }
        }

        internal ICoreBlock GetChildBlock(int index)
        {
            ReadOnlyList<ICoreBlock> blocks = ChildBlocks;

            if (index < 0 || index >= blocks.Count)
            {
                throw new ArgumentOutOfRangeException("index", $"Expected: [0, {blocks.Count}) Received: {index}");
            }

            return blocks[index];
        }

        [NonSerialized, HideInInspector]
        private protected bool initialized = false;
        [NonSerialized, HideInInspector]
        private bool initializing = false;
        private protected void Init()
        {
            if (initializing || initialized)
            {
                // Ensures we don't invoke this path recursively.
                // Derived classes may try to access this.Node
                // while initializing
                return;
            }

            if (!activated)
            {
                return;
            }

            initializing = true;

            if (activator == null && TryGetComponent(out activator))
            {
                activator.Register(this);
            }

            if (NovaApplication.IsEditor)
            {
                if (NovaApplication.InPlayer(this))
                {
                    sourceID = ID;
                }
            }
            else
            {
                sourceID = ID;
            }

            // since we're unregistered, the previous state of these flags are stale,
            // so we need to reset them in order to register/init from the starting state
            childrenDirty = true;
            childHandledHierarchyChange = false;

            OnTransformParentChanged();

            Self.RegisterWithHierarchy();

            initialized = true;
            initializing = false;
        }

        void IDataStoreElement.Register() => Register();
        void IDataStoreElement.Unregister() => Unregister();
        private protected abstract void Register();
        private protected abstract void Unregister();
        internal abstract void CopyFromDataStore();
        internal abstract void CloneFromSource(DataStoreID sourceID);
        private protected abstract void HandleParentChanged();

        [NonSerialized]
        private bool deactivating = false;
        [NonSerialized]
        private bool activated = false;
        [NonSerialized]
        private UIBlockActivator activator = null;

        internal void EnsureRegistration()
        {
            Self.EnsureRegistration();
        }

        private void Awake()
        {
            if (TryGetComponent(out activator))
            {
                activator.Register(this);
            }
        }

        private protected virtual void OnDestroy()
        {
            // Ensure this gets called, will no-op if already deactivated
            ((IGameObjectActiveReceiver)this).HandleOnDisable();

            if (activator != null)
            {
                activator.Unregister(this);
            }
        }

        private protected abstract void OnBlockEnabled();
        private protected abstract void OnBlockDisabled();

        void IGameObjectActiveReceiver.HandleOnEnable()
        {
            if (activated)
            {
                return;
            }

            activated = true;

            Init();

            OnBlockEnabled();
        }

        void IGameObjectActiveReceiver.HandleOnDisable()
        {
            if (!activated)
            {
                return;
            }

            deactivating = true;

            CopyFromDataStore();
            sourceID = DataStoreID.Invalid;

            OnBlockDisabled();

            Self.UnregisterFromHierarchy();

            deactivating = false;
            activated = false;
            initialized = false;
            initializing = false;
        }

        /// <summary>
        /// Prevent users from inheriting
        /// </summary>
        internal CoreBlock() { }

        #region Serialization
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!NovaApplication.IsPlaying)
            {
                if (sourceID.IsValid)
                {
                    // If we somehow missed a scenario
                    // where we serialize a sourceID in
                    // play mode that gets saved somewhere
                    // this will clear it out
                    sourceID = DataStoreID.Invalid;
                }
                return;
            }

            if (sourceID.IsValid && sourceID != ID)
            {
                // cloned
                CloneFromSource(sourceID);

                // will get initialized on Init()
                sourceID = DataStoreID.Invalid;
            }
        }
        #endregion
    }
}
