// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal struct BatchGroupDataStore : IInitializable
    {
        public NativeList<DataStoreID> KnownBatchRoots;

        public NovaHashMap<DataStoreID, NovaList<RenderHierarchyElement>> SortingProcessQueues;
        public NovaHashMap<DataStoreID, BatchZLayers> ZLayers;
        public NovaHashMap<DataStoreID, ZLayerCounts> ZLayerCounts;
        public NovaHashMap<DataStoreID, DrawCallSummary> DrawCallSummaries;
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, DrawCallID>> MinDrawCalls;
        public NovaHashMap<DataStoreID, NovaList<CoplanarSetID, CoplanarSet>> CoplanarSets;
        public NovaHashMap<DataStoreID, RotationSetSummary> RotationSets;
        public NovaHashMap<DataStoreID, NovaList<SubQuadVert>> SubQuadBuffers;
        public NovaHashMap<DataStoreID, NovaList<DataStoreID>> ContainedSortGroups;
        public NovaHashMap<DataStoreID, NovaList<DrawCallDescriptorID, MaterialCacheIndex>> MaterialAssignments;
        /// <summary>
        /// If the batch group is nested inside of a clip mask, this should be used as the "override" for
        /// draw calls that don't have an intra-batchgroup visual modifier
        /// </summary>
        public NovaHashMap<DataStoreID, VisualModifierShaderData> RootVisualModifierOverride;
        public NovaHashMap<DataStoreID, NovaList<VisualModifierID>> ContainedVisualModifers;

        #region Pools
        private NativeList<NovaList<RenderHierarchyElement>> sortingProcessQueuesPool;
        private NativeList<BatchZLayers> zLayersPool;
        private NativeList<ZLayerCounts> zLayerCountsPool;
        private NativeList<DrawCallSummary> drawCallSummariesPool;
        private NativeList<NovaList<VisualElementIndex, VisualElement>> visualElementsPool;
        private NativeList<NovaList<VisualElementIndex, DrawCallID>> minDrawCallsPool;
        private NativeList<NovaList<CoplanarSetID, CoplanarSet>> coplanarSetsPool;
        private NativeList<RotationSetSummary> rotationSetsPool;
        private NativeList<NovaList<SubQuadVert>> subQuadBuffersPool;
        private NativeList<NovaList<DataStoreID>> containedSortGroupsPool;
        private NativeList<NovaList<VisualModifierID>> containedVisualModifiersPool;
        private NativeList<NovaList<DrawCallDescriptorID, MaterialCacheIndex>> materialAssignmentsPool;
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(DataStoreID dataStoreID) => DrawCallSummaries.ContainsKey(dataStoreID);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(DataStoreID dataStoreID)
        {
            KnownBatchRoots.Add(dataStoreID);

            SortingProcessQueues.Add(dataStoreID, sortingProcessQueuesPool.GetFromPoolOrInit());
            ZLayers.Add(dataStoreID, zLayersPool.GetFromPoolOrInit());
            ZLayerCounts.Add(dataStoreID, zLayerCountsPool.GetFromPoolOrInit());
            DrawCallSummaries.Add(dataStoreID, drawCallSummariesPool.GetFromPoolOrInit());
            VisualElements.Add(dataStoreID, visualElementsPool.GetFromPoolOrInit());
            MinDrawCalls.Add(dataStoreID, minDrawCallsPool.GetFromPoolOrInit());
            CoplanarSets.Add(dataStoreID, coplanarSetsPool.GetFromPoolOrInit());
            RotationSets.Add(dataStoreID, rotationSetsPool.GetFromPoolOrInit());
            SubQuadBuffers.Add(dataStoreID, subQuadBuffersPool.GetFromPoolOrInit());
            ContainedSortGroups.Add(dataStoreID, containedSortGroupsPool.GetFromPoolOrInit());
            ContainedVisualModifers.Add(dataStoreID, containedVisualModifiersPool.GetFromPoolOrInit());
            MaterialAssignments.Add(dataStoreID, materialAssignmentsPool.GetFromPoolOrInit());
            RootVisualModifierOverride.Add(dataStoreID, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(DataStoreID dataStoreID)
        {
            if (!KnownBatchRoots.TryGetIndexOf(dataStoreID, out int index))
            {
                return;
            }

            KnownBatchRoots.RemoveAtSwapBack(index);

            sortingProcessQueuesPool.ReturnToPoolNonRef(SortingProcessQueues[dataStoreID]);
            SortingProcessQueues.Remove(dataStoreID);
            zLayersPool.ReturnToPoolNonRef(ZLayers[dataStoreID]);
            ZLayers.Remove(dataStoreID);
            zLayerCountsPool.ReturnToPoolNonRef(ZLayerCounts[dataStoreID]);
            ZLayerCounts.Remove(dataStoreID);
            drawCallSummariesPool.ReturnToPoolNonRef(DrawCallSummaries[dataStoreID]);
            DrawCallSummaries.Remove(dataStoreID);
            visualElementsPool.ReturnToPoolNonRef(VisualElements[dataStoreID]);
            VisualElements.Remove(dataStoreID);
            minDrawCallsPool.ReturnToPoolNonRef(MinDrawCalls[dataStoreID]);
            MinDrawCalls.Remove(dataStoreID);
            coplanarSetsPool.ReturnToPoolNonRef(CoplanarSets[dataStoreID]);
            CoplanarSets.Remove(dataStoreID);
            rotationSetsPool.ReturnToPoolNonRef(RotationSets[dataStoreID]);
            RotationSets.Remove(dataStoreID);
            subQuadBuffersPool.ReturnToPoolNonRef(SubQuadBuffers[dataStoreID]);
            SubQuadBuffers.Remove(dataStoreID);
            containedSortGroupsPool.ReturnToPoolNonRef(ContainedSortGroups[dataStoreID]);
            ContainedSortGroups.Remove(dataStoreID);
            containedVisualModifiersPool.ReturnToPoolNonRef(ContainedVisualModifers[dataStoreID]);
            ContainedVisualModifers.Remove(dataStoreID);
            materialAssignmentsPool.ReturnToPoolNonRef(MaterialAssignments[dataStoreID]);
            MaterialAssignments.Remove(dataStoreID);
            RootVisualModifierOverride.Remove(dataStoreID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            KnownBatchRoots.Init();

            SortingProcessQueues.Init();
            ZLayers.Init();
            ZLayerCounts.Init();
            VisualElements.Init();
            MinDrawCalls.Init();
            DrawCallSummaries.Init();
            CoplanarSets.Init();
            RotationSets.Init();
            SubQuadBuffers.Init();
            ContainedSortGroups.Init();
            ContainedVisualModifers.Init();
            MaterialAssignments.Init();
            RootVisualModifierOverride.Init();

            sortingProcessQueuesPool.Init();
            zLayersPool.Init();
            zLayerCountsPool.Init();
            drawCallSummariesPool.Init();
            visualElementsPool.Init();
            minDrawCallsPool.Init();
            coplanarSetsPool.Init();
            rotationSetsPool.Init();
            subQuadBuffersPool.Init();
            containedSortGroupsPool.Init();
            containedVisualModifiersPool.Init();
            materialAssignmentsPool.Init();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            KnownBatchRoots.Dispose();

            SortingProcessQueues.Dispose();
            ZLayers.Dispose();
            ZLayerCounts.Dispose();
            VisualElements.Dispose();
            MinDrawCalls.Dispose();
            DrawCallSummaries.Dispose();
            CoplanarSets.Dispose();
            RotationSets.Dispose();
            SubQuadBuffers.Dispose();
            ContainedSortGroups.Dispose();
            ContainedVisualModifers.Dispose();
            MaterialAssignments.Dispose();
            RootVisualModifierOverride.Dispose();

            sortingProcessQueuesPool.DisposeListAndElements();
            zLayersPool.DisposeListAndElements();
            zLayerCountsPool.DisposeListAndElements();
            drawCallSummariesPool.DisposeListAndElements();
            visualElementsPool.DisposeListAndElements();
            minDrawCallsPool.DisposeListAndElements();
            coplanarSetsPool.DisposeListAndElements();
            rotationSetsPool.DisposeListAndElements();
            subQuadBuffersPool.DisposeListAndElements();
            containedSortGroupsPool.DisposeListAndElements();
            containedVisualModifiersPool.DisposeListAndElements();
            materialAssignmentsPool.DisposeListAndElements();
        }
    }

}
