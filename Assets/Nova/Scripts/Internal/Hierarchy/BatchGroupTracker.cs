// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Hierarchy
{
    internal static class DepthLevel
    {
        public const int Invalid = -1;
    }

    internal struct BatchGroupElement
    {
        public DataStoreID BatchRootID;
        public int HierarchyDepthLevel;


        public static readonly BatchGroupElement Invalid = new BatchGroupElement()
        {
            BatchRootID = DataStoreID.Invalid,
            HierarchyDepthLevel = -1,
        };

        public static readonly BatchGroupElement Unassigned = new BatchGroupElement()
        {
            BatchRootID = DataStoreID.Invalid,
            HierarchyDepthLevel = -1
        };
    }

    /// <summary>
    /// A collection of Batch Groups for a set of Data Stores
    /// </summary>
    internal struct BatchGroupTracker : IInitializable, IFrameDirtyable
    {
        private NativeReference<bool> isDirty;
        public bool IsDirty => isDirty.Value && BatchRootIDs.Length > 0;

        public NativeList<BatchGroupElement> BatchGroupElements;
        public NativeList<HierarchyDependency> DirtyDependencies;

        /// This is the list of currently active batch root ids. 
        /// NOTE: An item that is in the <see cref="dirtyRootIDs"/> list may not be in here
        /// if it was removed
        /// </summary>
        public NativeDedupedList<DataStoreID> BatchRootIDs;
        private NativeDedupedList<DataStoreID> dirtyRootIDs;
        public ReadOnly AsReadOnly() => new ReadOnly(this);

        /// <summary>
        /// Adds a new batch root if it doesn't already exist and creates a new batch group
        /// </summary>
        /// <param name="rootID"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBatchRoot(DataStoreID rootID, DataStoreIndex formerBatchParentIndex)
        {
            if (!BatchRootIDs.Add(rootID))
            {
                return;
            }

            MarkRootDirty(rootID);
            MarkContainingBatchDirty(formerBatchParentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearDirtyState()
        {
            dirtyRootIDs.Clear();
            isDirty.Value = false;

            unsafe
            {
                UnsafeUtility.MemSet(DirtyDependencies.GetRawPtr(), 0, DirtyDependencies.Length * UnsafeUtility.SizeOf<HierarchyDependency>());
            }
        }

        /// <summary>
        /// Removes the given batch group if it's being tracked. This just removes the given ID as
        /// a Batch Root, which is separate from removing the ID in its entirety from this Collection.
        /// </summary>
        /// <param name="rootID"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveBatchRoot(DataStoreID rootID, DataStoreIndex hierarchyParentIndex)
        {
            if (!BatchRootIDs.Remove(rootID))
            {
                return;
            }

            MarkRootDirty(rootID);
            MarkContainingBatchDirty(hierarchyParentIndex);
        }

        /// <summary>
        /// Allocates an empty slot for a new element -- allows us to track
        /// the proper size of total element count being split into batch groups
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEmpty()
        {
            BatchGroupElements.Add(BatchGroupElement.Unassigned);
            DirtyDependencies.Add(HierarchyDependency.MaxDependencies);

            isDirty.Value = true;
        }

        /// <summary>
        /// Removes the element at the given index from this Collection. If the element
        /// was a Batch Root ID, that must be explicitly removed separately from this call.
        /// </summary>
        /// <param name="dataStoreIndex"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAtSwapBack(DataStoreIndex dataStoreIndex)
        {
            BatchGroupElement elementToRemove = BatchGroupElements[dataStoreIndex];

            BatchGroupElements.RemoveAtSwapBack(dataStoreIndex);
            DirtyDependencies.RemoveAtSwapBack(dataStoreIndex);

            MarkRootDirty(elementToRemove.BatchRootID);

            isDirty.Value = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkContainingBatchDirty(DataStoreIndex elementIndex)
        {
            if (!elementIndex.IsValid)
            {
                return;
            }

            DirtyDependencies[elementIndex] = HierarchyDependency.MaxDependencies;

            MarkRootDirty(BatchGroupElements[elementIndex].BatchRootID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkChildDirty(DataStoreIndex childIndex, DataStoreIndex newParentIndex)
        {
            MarkContainingBatchDirty(childIndex);
            MarkContainingBatchDirty(newParentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkRootDirty(DataStoreID rootID)
        {
            if (!rootID.IsValid)
            {
                return;
            }

            dirtyRootIDs.Add(rootID);
            isDirty.Value = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            BatchGroupElements = new NativeList<BatchGroupElement>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            DirtyDependencies = new NativeList<HierarchyDependency>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            BatchRootIDs = NativeDedupedList<DataStoreID>.Create(Constants.SomeElementsInitialCapacity);

            isDirty = new NativeReference<bool>(Allocator.Persistent);
            dirtyRootIDs = NativeDedupedList<DataStoreID>.Create(Constants.SomeElementsInitialCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            BatchGroupElements.Dispose();
            DirtyDependencies.Dispose();
            BatchRootIDs.Dispose();

            isDirty.Dispose();
            dirtyRootIDs.Dispose();
        }

        public struct ReadOnly
        {
            [ReadOnly]
            public NativeList<BatchGroupElement> BatchGroupElements;
            [ReadOnly]
            public NativeDedupedList<DataStoreID> BatchRootIDs;

            [ReadOnly]
            public NativeDedupedList<DataStoreID> DirtyRootIDs;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnly(in BatchGroupTracker tracker)
            {
                BatchGroupElements = tracker.BatchGroupElements;
                BatchRootIDs = tracker.BatchRootIDs;
                DirtyRootIDs = tracker.dirtyRootIDs;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe void PopulateWithDirtyRoots(ref NativeList<DataStoreID> rootsToUpdate)
            {
                int dirtyCount = DirtyRootIDs.Length;
                for (int i = 0; i < dirtyCount; ++i)
                {
                    DataStoreID rootID = DirtyRootIDs[i];

                    if (!BatchRootIDs.Contains(rootID))
                    {
                        continue;
                    }

                    rootsToUpdate.Add(rootID);

                }
            }
        }
    }
}
