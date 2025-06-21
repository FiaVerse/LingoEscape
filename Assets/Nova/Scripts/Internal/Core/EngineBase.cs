// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Common;
using Nova.Internal.Utilities;
using System;
using Unity.Collections;
using Unity.Jobs;

namespace Nova.Internal.Core
{
    /// <summary>
    /// Base class that engines can inherit from to be used with the 
    /// <see cref="EngineManager"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class EngineBaseGeneric<T> : EngineBase where T : EngineBaseGeneric<T>
    {
        public static T Instance = null;
    }

    internal struct EngineUpdateInfo : IInitializable
    {
        /// <summary>
        /// The subset of dirty roots that are still 'active'
        /// </summary>
        public NativeList<DataStoreID> RootsToUpdate;

        /// <summary>
        /// The set of element indices that either need to be
        /// assigned to a batch group or whose batch root was
        /// dirtied this frame
        /// </summary>
        public NativeList<DataStoreIndex> ElementsToUpdate;

        /// <summary>
        /// The job handle tracking sequential engine work to complete 
        /// </summary>
        public JobHandle EngineSequenceCompleteHandle;

        public void Init()
        {
            RootsToUpdate = new NativeList<DataStoreID>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
            ElementsToUpdate = new NativeList<DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
        }

        public void Clear()
        {
            RootsToUpdate.Clear();
            ElementsToUpdate.Clear();
            EngineSequenceCompleteHandle = default(JobHandle);
        }

        public void Dispose()
        {
            RootsToUpdate.Dispose();
            ElementsToUpdate.Dispose();
        }
    }

    /// <summary>
    /// Base class for an engine. Don't inherit from this directly,
    /// inherit from <see cref="EngineBaseGeneric{T}"/>
    /// </summary>
    internal abstract class EngineBase : IDisposable
    {
        public const int EqualWorkBatchSize = 32;

        public abstract void Init();
        public abstract void Dispose();

        public virtual JobHandle PreUpdate(JobHandle enginePreUpdateHandle) { return enginePreUpdateHandle; }
        public virtual void UpdateFirstPass(ref EngineUpdateInfo updateInfo) { }
        public virtual void UpdateSecondPass(ref EngineUpdateInfo updateInfo) { }
        public virtual void CompleteUpdate() { }
        public virtual void PostUpdate() { }
    }
}

