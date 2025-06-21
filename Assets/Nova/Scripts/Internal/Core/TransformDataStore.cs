// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Utilities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Nova.Internal.Core
{
    internal partial interface ITransformProvider : IDataStoreElement
    {
        Transform Transform { get; }
        bool IsVirtual { get; }
    }

    internal struct TransformProxy
    {
        public bool IsVirtual;
        public int Index;

        public override string ToString()
        {
            return $"Virtual: {IsVirtual}, Index: {Index}";
        }
    }

    /// <summary>
    /// Base class for a data store which maintains a <see cref="TransformAccessArray"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract partial class TransformDataStore<TDataStore,T> : DataStore<TDataStore, T>
        where TDataStore : DataStore<TDataStore, T>
        where T : ITransformProvider
    {
        /// <summary>
        /// This buffer is effectively treated as output of the LayoutEngine. It should only be read immediately after
        /// the LayoutEngine has completed its work. Otherwise the data here will be out-of-date, and will cause bugs.
        /// </summary>
        public NativeList<float4x4> WorldToLocalMatrices;
        public NativeList<float4x4> LocalToWorldMatrices;

        public NativeList<TransformProxy> TransformProxies;

        public TransformAccessArray PhysicalTransforms;
        public NativeList<DataStoreIndex> PhysicalToSharedIndexMap;
        public NativeList<DataStoreIndex> VirtualToSharedIndexMap;
        public int TransformCount => TransformProxies.Length;

        public TransformRegistrationTracker TransformTracker { get; } = new TransformRegistrationTracker();

        protected override void Add(T val)
        {
            int index;
            if (val.IsVirtual)
            {
                // Just adding a dummy transform for now.
                // This will have real values after the 
                // LayoutEngine processes it.
                index = VirtualToSharedIndexMap.Length;
                VirtualToSharedIndexMap.Add(TransformProxies.Length);
            }
            else // real transform
            {
                index = PhysicalTransforms.length;
                PhysicalTransforms.Add(val.Transform);
                PhysicalToSharedIndexMap.Add(TransformProxies.Length);
            }

            TransformProxies.Add(new TransformProxy()
            {
                IsVirtual = val.IsVirtual,
                Index = index
            });

            if (NovaApplication.IsEditor)
            {
                TransformTracker.SetTransformTrackingState(val);
            }

            // Allocate a dummy matrix to maintain size. This will be modified by the layout engine
            WorldToLocalMatrices.Add(float4x4.identity);
            LocalToWorldMatrices.Add(float4x4.identity);
        }

        /// <summary>
        /// An optimization to allow the derived class to run the native Add() work in a burst method,
        /// since this base class has managed memory types and needs to handle them separately
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected int AddNonNativeOnly(T val)
        {
            int index;
            if (val.IsVirtual)
            {
                // Just adding a dummy transform for now.
                // This will have real values after the 
                // LayoutEngine processes it.
                index = VirtualToSharedIndexMap.Length;
            }
            else // real transform
            {
                index = PhysicalTransforms.length;
                PhysicalTransforms.Add(val.Transform);
            }

            return index;
        }

        public float4x4 GetLocalToWorldMatrix(T val)
        {
            if (!val.Index.IsValid)
            {
                return float4x4.identity;
            }

            return LocalToWorldMatrices.ElementAt(val.Index);
        }

        protected override void RemoveAtSwapBack(DataStoreID idToRemove, DataStoreIndex indexToRemove)
        {
            // Get the proxy of the transform, physical or virtual, to remove
            TransformProxy proxyToRemove = TransformProxies[indexToRemove];

            // remove the proxy at the requested index and swap in the one we just updated
            TransformProxies.RemoveAtSwapBack(indexToRemove);
            WorldToLocalMatrices.RemoveAtSwapBack(indexToRemove);
            LocalToWorldMatrices.RemoveAtSwapBack(indexToRemove);

            if (indexToRemove < TransformProxies.Length) // otherwise we removed last
            {
                TransformProxy proxySwappedFromBack = TransformProxies[indexToRemove];

                if (proxySwappedFromBack.IsVirtual)
                {
                    VirtualToSharedIndexMap[proxySwappedFromBack.Index] = indexToRemove;
                }
                else
                {
                    PhysicalToSharedIndexMap[proxySwappedFromBack.Index] = indexToRemove;
                }
            }

            int proxyIndexToUpdate = -1;
            if (proxyToRemove.IsVirtual)
            {
                // remove virtual transform
                VirtualToSharedIndexMap.RemoveAtSwapBack(proxyToRemove.Index);

                if (proxyToRemove.Index < VirtualToSharedIndexMap.Length) // otherwise we removed last
                {
                    // get the proxy index of the newly swapped in virtual transform
                    proxyIndexToUpdate = VirtualToSharedIndexMap[proxyToRemove.Index];
                }
            }
            else
            {
                if (NovaApplication.IsEditor)
                {
                    TransformTracker.Remove(idToRemove, PhysicalTransforms[proxyToRemove.Index]);
                }

                PhysicalTransforms.RemoveAtSwapBack(proxyToRemove.Index);
                PhysicalToSharedIndexMap.RemoveAtSwapBack(proxyToRemove.Index);

                if (proxyToRemove.Index < PhysicalToSharedIndexMap.Length) // otherwise we removed last
                {
                    // get the proxy index of the newly swapped in physical transform
                    proxyIndexToUpdate = PhysicalToSharedIndexMap[proxyToRemove.Index];
                }
            }

            if (proxyIndexToUpdate >= 0 && proxyIndexToUpdate < TransformProxies.Length)
            {
                TransformProxy swappedTransformProxy = TransformProxies[proxyIndexToUpdate];
                swappedTransformProxy.Index = proxyToRemove.Index;
                TransformProxies[proxyIndexToUpdate] = swappedTransformProxy;
            }
        }

        public Transform GetTransform(DataStoreIndex index)
        {
            if (!index.IsValid)
            {
                return null;
            }

            TransformProxy proxy = TransformProxies[index];

            if (proxy.IsVirtual)
            {
                return null;
            }

            return PhysicalTransforms[proxy.Index];
        }

        public override void Init()
        {
            base.Init();
            TransformProxies = new NativeList<TransformProxy>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            WorldToLocalMatrices = new NativeList<float4x4>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            LocalToWorldMatrices = new NativeList<float4x4>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            PhysicalTransforms = new TransformAccessArray(Constants.AllElementsInitialCapacity);
            PhysicalToSharedIndexMap = new NativeList<DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            VirtualToSharedIndexMap = new NativeList<DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent);

            if (NovaApplication.IsEditor)
            {
                TransformTracker.Init(this);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            TransformProxies.Dispose();
            WorldToLocalMatrices.Dispose();
            LocalToWorldMatrices.Dispose();
            PhysicalTransforms.Dispose();
            PhysicalToSharedIndexMap.Dispose();
            VirtualToSharedIndexMap.Dispose();

            if (NovaApplication.IsEditor)
            {
                TransformTracker.Dispose();
            }
        }
    }
}