// Copyright (c) Supernova Technologies LLC
//#define RUN_JOB
#define inline

using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace Nova.Internal.Layouts
{
    /// <summary>
    /// The engine for processing all layout node properties
    /// </summary>
    internal partial class LayoutEngine : EngineBaseGeneric<LayoutEngine>
    {
        public LayoutCache EngineCache;
        private NativeList<DataStoreID> inlineRootIDs;

        [FixedAddressValueType]
        private static LayoutCore.Build layoutBuildRunner;
        private static BurstedMethod<LayoutCore.Rebuild> rebuildLayout;

        private static LayoutCore.CountDirtyRoots countDirtyBatchesRunner;
        private static LayoutCore.MarkRootsDirty markDirtyBatchesRunner;
        private static LayoutCore.ConvertToTransforms convertToTransformsRunner;
        private static LayoutCore.ContentBounds contentBoundsRunner;
        private static LayoutCore.HierarchyBounds hierarchyBoundsRunner;
        private static LayoutCore.SpatialPartition spatialPartitionRunner;
        private static TransformSync.Read readTransformsRunner;
        private static TransformSync.UpdateMatrices updateMatricesRunner;
        private static TransformSync.Write writeTransformsRunner;

        public bool ShouldRunLayoutUpdate => LayoutDataStore.Instance.LayoutPropertiesNeedUpdate || HierarchyDataStore.Instance.IsDirty;

        private JobHandle TransformUpdateHandle;
        private JobHandle PartitionHandle;
        private JobHandle ContentBoundsHandle;

        public override void Dispose()
        {
            EngineCache.Dispose();
            inlineRootIDs.Dispose();
        }

        public override void Init()
        {
            Instance = this;

            unsafe
            {
                prepare = new BurstedMethod<BurstMethod>(LayoutCache.Prepare.Execute);
                rebuildLayout = new BurstedMethod<LayoutCore.Rebuild>(LayoutCore.GetDependenciesAndRun);
            }

            EngineCache.Init();
            inlineRootIDs = new NativeList<DataStoreID>(4, Allocator.Persistent);

            #region Init Job Structs
            convertToTransformsRunner = new LayoutCore.ConvertToTransforms()
            {
                CalculatedLengths = LayoutDataStore.Instance.CalculatedLengths,
                UncalculatedLengths = LayoutDataStore.Instance.LengthConfigs,
                MinMaxes = LayoutDataStore.Instance.LengthMinMaxes,
                PreviewSizesAvailable = NovaApplication.IsEditor,
                PreviewSizes = LayoutDataStore.Instance.Previews.PreviewSizes,
                Alignments = LayoutDataStore.Instance.Alignments,
                UseRotations = LayoutDataStore.Instance.UseRotations,

                TransformPositions = LayoutDataStore.Instance.TransformLocalPositions,
                TransformRotations = LayoutDataStore.Instance.TransformLocalRotations,
                TransformProxies = LayoutDataStore.Instance.TransformProxies,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,

                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,

                DirtyElementIndices = EngineCache.AllProcessedElements,
            };

            contentBoundsRunner = new LayoutCore.ContentBounds()
            {
                Lengths = LayoutDataStore.Instance.CalculatedLengths,
                UseRotations = LayoutDataStore.Instance.UseRotations,
                LocalPositions = LayoutDataStore.Instance.TransformLocalPositions,
                LocalRotations = LayoutDataStore.Instance.TransformLocalRotations,
                Alignments = LayoutDataStore.Instance.Alignments,

                DirectContentSizes = LayoutDataStore.Instance.DirectContentSizes,
                DirectContentOffsets = LayoutDataStore.Instance.DirectContentOffsets,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,

                DirtyIndices = EngineCache.AllProcessedElements,
            };

            hierarchyBoundsRunner = new LayoutCore.HierarchyBounds()
            {
                Lengths = LayoutDataStore.Instance.CalculatedLengths,
                LocalPositions = LayoutDataStore.Instance.TransformLocalPositions,
                LocalRotations = LayoutDataStore.Instance.TransformLocalRotations,
                LocalScales = LayoutDataStore.Instance.TransformLocalScales,

                TotalContentSizes = LayoutDataStore.Instance.TotalContentSizes,
                TotalContentOffsets = LayoutDataStore.Instance.TotalContentOffsets,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                DirtyIndices = EngineCache.AllProcessedElements,
            };

            layoutBuildRunner = new LayoutCore.Build()
            {
                PreviewSizesAvailable = NovaApplication.IsEditor,
                PreviewSizes = LayoutDataStore.Instance.Previews.PreviewSizes,
                LengthConfigs = LayoutDataStore.Instance.LengthConfigs,
                LengthRanges = LayoutDataStore.Instance.LengthMinMaxes,
                CalculatedLengths = LayoutDataStore.Instance.CalculatedLengths,

                AutoSizes = LayoutDataStore.Instance.AutoSizes,
                Alignments = LayoutDataStore.Instance.Alignments,
                UseRotations = LayoutDataStore.Instance.UseRotations,
                AspectRatios = LayoutDataStore.Instance.AspectRatios,

                AutoLayouts = LayoutDataStore.Instance.AutoLayouts,
                CalculatedSpacing = LayoutDataStore.Instance.CalculatedSpacing,
                FormerAutoLayoutAxes = LayoutDataStore.Instance.FormerAutoLayoutAxes,

                TransformProxies = LayoutDataStore.Instance.TransformProxies,
                TransformRotations = LayoutDataStore.Instance.TransformLocalRotations,
                TransformPositions = LayoutDataStore.Instance.TransformLocalPositions,
                UsingTransformPositions = LayoutDataStore.Instance.UsingTransformPositions,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,

                AncestorBuffer = LayoutDataStore.Instance.AncestorBuffer,
                DirtyLayouts = LayoutDataStore.Instance.AccessedLayouts,
                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,

                IndicesToProcess = EngineCache.AllProcessedElements,

                ShrinkSizeOverrides = LayoutDataStore.Instance.ShrinkSizeOverrides,
                ParentSizes = LayoutDataStore.Instance.ParentSizes,

                NeedsSecondPass = EngineCache.NeedsSecondPass,

                AutoLayoutTrackCache = EngineCache.AutoLayoutTrackCache,
                AutoLayoutRangeCache = EngineCache.AutoLayoutRangeCache,

                DirectContentSizes = LayoutDataStore.Instance.DirectContentSizes,
                DirectContentOffsets = LayoutDataStore.Instance.DirectContentOffsets,
            };

            readTransformsRunner = new TransformSync.Read()
            {
                LocalRotations = LayoutDataStore.Instance.TransformLocalRotations,
                LocalPositions = LayoutDataStore.Instance.TransformLocalPositions,
                LocalScales = LayoutDataStore.Instance.TransformLocalScales,
                WorldToLocalMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                LocalToWorldMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,

                UseRotations = LayoutDataStore.Instance.UseRotations,
                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,
                UsingTransformPositions = LayoutDataStore.Instance.UsingTransformPositions,
                ReceivedFullUpdate = LayoutDataStore.Instance.ReceivedFullEngineUpdate.AsReadOnly(),

                LayoutLengths = LayoutDataStore.Instance.LengthConfigs,
                LayoutLengthRanges = LayoutDataStore.Instance.LengthMinMaxes,
                CalculatedLengths = LayoutDataStore.Instance.CalculatedLengths,
                Alignments = LayoutDataStore.Instance.Alignments,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,

                PhysicalToSharedIndexMap = LayoutDataStore.Instance.PhysicalToSharedIndexMap,
                TransformProxies = LayoutDataStore.Instance.TransformProxies,
                DirtyCount = LayoutDataStore.Instance.NumLayoutsAccessed,

                DirtyTransformCount = LayoutDataStore.Instance.NumTransformsModified,
            };

            writeTransformsRunner = new TransformSync.Write()
            {
                TransformPositions = LayoutDataStore.Instance.TransformLocalPositions,
                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,

                TransformProxies = LayoutDataStore.Instance.TransformProxies,
                PhysicalToSharedTransformIndexMap = LayoutDataStore.Instance.PhysicalToSharedIndexMap,
                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,

                LocalToWorldMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
            };

            countDirtyBatchesRunner = new LayoutCore.CountDirtyRoots()
            {
                BatchGroupElements = HierarchyDataStore.Instance.BatchGroupTracker.BatchGroupElements,
                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,

                DirtyLayoutElements = LayoutDataStore.Instance.DirtyIndices,

                BatchRootIDToCounterIndex = HierarchyEngine.Instance.TrackedRootIDs,
            };

            markDirtyBatchesRunner = new LayoutCore.MarkRootsDirty()
            {
                AllBatchRoots = HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs,
                BatchGroupElements = HierarchyDataStore.Instance.BatchGroupTracker.BatchGroupElements,
                BatchRootIDToCounterIndex = HierarchyEngine.Instance.TrackedRootIDs,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,
            };

            spatialPartitionRunner = new LayoutCore.SpatialPartition()
            {
                SpatialPartitions = LayoutDataStore.Instance.SpatialPartitions,

                TotalContentSizes = LayoutDataStore.Instance.TotalContentSizes,
                TotalContentOffsets = LayoutDataStore.Instance.TotalContentOffsets,
                LocalPositions = LayoutDataStore.Instance.TransformLocalPositions,
                LocalRotations = LayoutDataStore.Instance.TransformLocalRotations,
                LocalScales = LayoutDataStore.Instance.TransformLocalScales,
                DirtyElementIndices = EngineCache.AllProcessedElements,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,
            };

            updateMatricesRunner = new TransformSync.UpdateMatrices()
            {
                TransformPositions = LayoutDataStore.Instance.TransformLocalPositions,
                TransformRotations = LayoutDataStore.Instance.TransformLocalRotations,
                TransformScales = LayoutDataStore.Instance.TransformLocalScales,

                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                HierarchyLookup = HierarchyDataStore.Instance.HierarchyLookup,
                BatchGroupElements = HierarchyDataStore.Instance.BatchGroupTracker.BatchGroupElements,

                TransformProxies = LayoutDataStore.Instance.TransformProxies,
                WorldToLocalMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                LocalToWorldMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,

                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,
            };

            #endregion
        }

        public void UpdateLayoutElement(DataStoreID elementID, bool secondPass)
        {
            unsafe
            {
                rebuildLayout.Method.Invoke(UnsafeUtility.AddressOf(ref layoutBuildRunner), UnsafeUtility.AddressOf(ref elementID), secondPass ? 1 : 0);
            }
        }

        public override void UpdateFirstPass(ref EngineUpdateInfo engineUpdateInfo)
        {
            if (!ShouldRunLayoutUpdate)
            {
                return;
            }

            JobHandle diffDependencies = DiffDirtyDependencies(ref EngineCache, ref engineUpdateInfo, engineUpdateInfo.EngineSequenceCompleteHandle);
            JobHandle getAllDirtyElements = CombineDirtyLists(ref EngineCache, diffDependencies);

            engineUpdateInfo.EngineSequenceCompleteHandle = UpdateLayoutProperties(getAllDirtyElements);
        }

        public override void UpdateSecondPass(ref EngineUpdateInfo engineUpdateInfo)
        {
            if (ShouldRunLayoutUpdate)
            {
                // do a full layout update
                PerformLayoutUpdate(ref engineUpdateInfo);
            }
            else if (LayoutDataStore.Instance.IsDirty)
            {
                PerformTransformUpdate(ref engineUpdateInfo);
            }
        }

        /// <summary>
        /// A complete pass of updating layout properties and transforms
        /// </summary>
        /// <param name="layoutCache"></param>
        /// <param name="engineUpdateInfo"></param>
        private void PerformLayoutUpdate(ref EngineUpdateInfo engineUpdateInfo)
        {
            JobHandle updateHandle = engineUpdateInfo.EngineSequenceCompleteHandle;

            if (LayoutDataStore.Instance.LayoutsNeedSecondPass)
            {
                updateHandle = UpdateLayoutProperties(updateHandle);
            }

            updateHandle = UpdateTransformProperties(ref EngineCache.AllProcessedElements, updateHandle);

            // Update sub hierarchy bounds per element
            JobHandle hierachyBoundsHandle = hierarchyBoundsRunner.NovaScheduleByRef(updateHandle);

            // Create spatial partitions
            PartitionHandle = CreateSpatialPartitions(ref EngineCache, hierachyBoundsHandle);

            // Update direct content bounds per element
            ContentBoundsHandle = contentBoundsRunner.NovaScheduleByRef(EngineCache.AllProcessedElements.Length, EqualWorkBatchSize, updateHandle);

            // Write values calculated by layout engine to transforms
            TransformUpdateHandle = WriteToPhysicalTransforms(updateHandle);

            // Get new set of elements whose matrices need to be updated, which will be a superset of elements whose layouts needed to be updated
            engineUpdateInfo.EngineSequenceCompleteHandle = UpdateTransformMatrices(ref EngineCache, TransformUpdateHandle);
        }

        /// <summary>
        /// A complete pass of just updating transform matrices, bypassing all the layout property work when it's not needed
        /// </summary>
        private void PerformTransformUpdate(ref EngineUpdateInfo engineUpdateInfo)
        {
            // only update transforms/matrices
            EngineCache.ProcessedRootIDs.Clear();
            EngineCache.ProcessedRootIDs.AddRange(HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs.List.AsArray());
            JobHandle allElements = CombineDirtyLists(ref EngineCache, engineUpdateInfo.EngineSequenceCompleteHandle);

            TransformUpdateHandle = WriteToPhysicalTransforms(allElements);

            engineUpdateInfo.EngineSequenceCompleteHandle = UpdateTransformMatrices(ref EngineCache);
        }

        public override JobHandle PreUpdate(JobHandle enginePreUpdateHandle)
        {
            LayoutDataStore.Instance.CacheDirtyCount();

            if (NovaApplication.IsEditor)
            {
                LayoutDataStore.Instance.TransformTracker.LockTransforms();
                LayoutDataStore.Instance.Previews.EditorOnly_TryRefresh();
            }

            enginePreUpdateHandle = ReadFromTransforms(enginePreUpdateHandle);
            enginePreUpdateHandle = LayoutDataStore.Instance.PreProcessDirtyState(enginePreUpdateHandle);
            EngineCache.PrepareForUpdate();
            return enginePreUpdateHandle;
        }

        public override void CompleteUpdate()
        {
            TransformUpdateHandle.Complete();
            PartitionHandle.Complete();
            ContentBoundsHandle.Complete();
        }

        public override void PostUpdate()
        {
            if (NovaApplication.IsEditor)
            {
                LayoutDataStore.Instance.TransformTracker.ReleaseTransforms();
            }

            LayoutDataStore.Instance.ClearDirtyState();
        }

        #region Run Jobs
        private JobHandle UpdateLayoutProperties(JobHandle dependency)
        {
            JobHandle layoutUpdate = dependency;

            layoutUpdate = layoutBuildRunner.NovaScheduleByRef(layoutUpdate);

            return layoutUpdate;
        }

        private JobHandle UpdateTransformProperties(ref NativeList<DataStoreIndex> dirtyElementIndices, JobHandle dependency)
        {
            // Convert layout info to transform positions
            JobHandle transformUpdate = ConvertLayoutsToTransformPositions(ref dirtyElementIndices, dependency);

            return transformUpdate;
        }

        private JobHandle ReadFromTransforms(JobHandle dependency)
        {
            return readTransformsRunner.ScheduleReadOnly(LayoutDataStore.Instance.PhysicalTransforms, EqualWorkBatchSize, dependency);
        }

        private JobHandle DiffDirtyDependencies(ref LayoutCache layoutCache, ref EngineUpdateInfo engineUpdateInfo, JobHandle dependency)
        {
            JobHandle diffDependencies = dependency;

            int dirtyLayoutCount = LayoutDataStore.Instance.DirtyIndices.Length;

            if (dirtyLayoutCount > 0)
            {
                countDirtyBatchesRunner.BatchRootDirtyCounts = layoutCache.DirtyRootCounts;
                diffDependencies = countDirtyBatchesRunner.NovaScheduleByRef(dirtyLayoutCount, EqualWorkBatchSize, diffDependencies);
            }

            markDirtyBatchesRunner.DirtyBatchRoots = engineUpdateInfo.RootsToUpdate;
            markDirtyBatchesRunner.HierarchyRoots = HierarchyEngine.Instance.TrackedRootIDs;
            markDirtyBatchesRunner.BatchRootDirtyCounts = layoutCache.DirtyRootCounts;
            markDirtyBatchesRunner.DependentBatchRoots = layoutCache.ProcessedRootIDs;

            diffDependencies = markDirtyBatchesRunner.NovaScheduleByRef(diffDependencies);

            return diffDependencies;
        }

        private JobHandle CombineDirtyLists(ref LayoutCache layoutCache, JobHandle dependency)
        {
            return HierarchyDataStore.Instance.GetDepthSortedHierarchy(ref layoutCache.ProcessedRootIDs, ref layoutCache.AllProcessedElements, dependency);
        }

        private JobHandle ConvertLayoutsToTransformPositions(ref NativeList<DataStoreIndex> dirtyElementIndices, JobHandle dependency)
        {
            convertToTransformsRunner.DirtyElementIndices = dirtyElementIndices;

            return convertToTransformsRunner.NovaScheduleByRef(dirtyElementIndices.Length, EqualWorkBatchSize, dependency);
        }

        private JobHandle CreateSpatialPartitions(ref LayoutCache layoutCache, JobHandle dependency)
        {
            return spatialPartitionRunner.NovaScheduleByRef(layoutCache.AllProcessedElements.Length, EqualWorkBatchSize, dependency);
        }

        private JobHandle WriteToPhysicalTransforms(JobHandle dependency)
        {
            return writeTransformsRunner.Schedule(LayoutDataStore.Instance.PhysicalTransforms, dependency);
        }

        private JobHandle UpdateTransformMatrices(ref LayoutCache cache, JobHandle dependency = default)
        {
            updateMatricesRunner.DepthSortedHierarchy = cache.AllProcessedElements;
            updateMatricesRunner.DirtyIndices = cache.LayoutDirtiedIndices;
            updateMatricesRunner.DirtyRootIDs = cache.MatrixDirtiedRootIDs;

            return updateMatricesRunner.NovaScheduleByRef(dependency);
        }
        #endregion
    }
}