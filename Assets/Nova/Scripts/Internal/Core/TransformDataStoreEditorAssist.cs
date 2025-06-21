// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Internal.Core
{
    internal partial interface ITransformProvider
    {
        bool TransformCanBeRegistered { get; }
    }

    internal abstract partial class TransformDataStore<TDataStore,T>
    {
        public class TransformRegistrationTracker
        {
            private HashList<DataStoreID> untrackedTransforms = null;
            private HashSet<int> transformInstanceIDs = null;
            private TransformDataStore<TDataStore, T> owner = null;

            public void LockTransforms()
            {
                if (!NovaApplication.IsEditor)
                {
                    return;
                }

                for (int i = 0; i < untrackedTransforms.Count; ++i)
                {
                    ITransformProvider provider = owner.Elements[untrackedTransforms[i]];
                    int transformIndex = owner.TransformProxies[provider.Index].Index;
                    owner.PhysicalTransforms[transformIndex] = provider.Transform;
                }
            }

            public void ReleaseTransforms()
            {
                if (!NovaApplication.IsEditor)
                {
                    return;
                }

                for (int i = 0; i < untrackedTransforms.Count; ++i)
                {
                    ITransformProvider provider = owner.Elements[untrackedTransforms[i]];
                    int transformIndex = owner.TransformProxies[provider.Index].Index;
                    owner.PhysicalTransforms[transformIndex] = null;
                }
            }

            public void SetTransformTrackingState(ITransformProvider provider)
            {
                if (!NovaApplication.IsEditor)
                {
                    return;
                }

                if (provider.IsVirtual || !provider.Index.IsValid)
                {
                    return;
                }

                int index = owner.TransformProxies[provider.Index].Index;

                Transform newTransform = provider.TransformCanBeRegistered ? provider.Transform : null;
                Transform currentTransform = owner.PhysicalTransforms[index];

                if (currentTransform == newTransform)
                {
                    return;
                }

                bool wasTracking = currentTransform != null;

                owner.PhysicalTransforms[index] = newTransform;

                if (wasTracking)
                {
                    untrackedTransforms.Add(provider.UniqueID);
                }
                else
                {
                    untrackedTransforms.Remove(provider.UniqueID);
                }
            }

            public void Add(ITransformProvider provider)
            {
                if (!NovaApplication.IsEditor)
                {
                    return;
                }

                Transform transform = provider.Transform;

                if (transform != null)
                {
                    transformInstanceIDs.Add(transform.GetInstanceID());
                }

                SetTransformTrackingState(provider);
            }

            public void Remove(DataStoreID idToRemove, Transform transform)
            {
                if (!NovaApplication.IsEditor)
                {
                    return;
                }

                if (transform != null)
                {
                    transformInstanceIDs.Remove(transform.GetInstanceID());
                }

                untrackedTransforms.Remove(idToRemove);
            }

            public bool Tracking(Transform transform)
            {
                if (!NovaApplication.IsEditor)
                {
                    return false;
                }

                if (transform == null)
                {
                    return false;
                }

                return transformInstanceIDs.Contains(transform.GetInstanceID());
            }

            public void Init(TransformDataStore<TDataStore, T> transformDataStore)
            {
                if (!NovaApplication.IsEditor)
                {
                    return;
                }

                owner = transformDataStore;
                untrackedTransforms = new HashList<DataStoreID>();
                transformInstanceIDs = new HashSet<int>();
            }

            public void Dispose()
            {
                if (!NovaApplication.IsEditor)
                {
                    return;
                }

                untrackedTransforms.Clear();
                transformInstanceIDs.Clear();
            }
        }
    }
}
