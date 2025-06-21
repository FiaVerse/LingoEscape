// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Unity.Collections;
using Unity.Jobs;

namespace Nova.Internal.Hierarchy
{
    internal class HierarchyEngine : EngineBaseGeneric<HierarchyEngine>
    {
        // Maps a root ID to an index in the BatchGroupTracker's list of batch roots.
        // Only valid in the process of an engine update.
        public NovaHashMap<DataStoreID, int> TrackedRootIDs;
        public JobHandle HierarchyUpdateHandle;

        private bool ShouldRunUpdate => HierarchyDataStore.Instance.IsDirty;

        private Hierarchy.SetupHierarchy setupRunner;
        private Hierarchy.AssignBatchRoots assignRootsRunner;

        public override void Dispose()
        {
            TrackedRootIDs.Dispose();
        }

        public override void Init()
        {
            Instance = this;

            TrackedRootIDs = new NovaHashMap<DataStoreID, int>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);

            #region Init Job Structs
            setupRunner = new Hierarchy.SetupHierarchy()
            {
                BatchRootIDs = HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs,
                Lookup = HierarchyDataStore.Instance.HierarchyLookup,

                RootIndexMap = TrackedRootIDs,
            };

            assignRootsRunner = new Hierarchy.AssignBatchRoots()
            {
                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,

                BatchRoots = TrackedRootIDs,
                BatchGroupElements = HierarchyDataStore.Instance.BatchGroupTracker.BatchGroupElements,
            };
            #endregion
        }

        public override void UpdateFirstPass(ref EngineUpdateInfo engineUpdateInfo)
        {
            if (!ShouldRunUpdate)
            {
                return;
            }

            // Setup
            setupRunner.HierarchySize = HierarchyDataStore.Instance.Elements.Count;
            engineUpdateInfo.EngineSequenceCompleteHandle = setupRunner.NovaScheduleByRef();

            // Get Dirty Elements
            engineUpdateInfo.EngineSequenceCompleteHandle = HierarchyDataStore.Instance.PopulateWithDirtyBatchElements(ref engineUpdateInfo, engineUpdateInfo.EngineSequenceCompleteHandle);

            // Assign Batch Roots
            assignRootsRunner.DirtyToDataStoreIndices = engineUpdateInfo.ElementsToUpdate;
            engineUpdateInfo.EngineSequenceCompleteHandle = assignRootsRunner.ScheduleByRef(engineUpdateInfo.ElementsToUpdate, EqualWorkBatchSize, engineUpdateInfo.EngineSequenceCompleteHandle);

            HierarchyUpdateHandle = engineUpdateInfo.EngineSequenceCompleteHandle;
        }

        public override void PostUpdate()
        {
            HierarchyDataStore.Instance.ClearDirtyState();
        }
    }
}
