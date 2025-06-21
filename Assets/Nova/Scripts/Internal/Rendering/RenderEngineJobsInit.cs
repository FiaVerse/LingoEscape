// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal unsafe partial class RenderEngine : EngineBaseGeneric<RenderEngine>
    {
        private NativeList<HierarchyElement> Hierarchy => HierarchyDataStore.Instance.Hierarchy;
        private NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex => HierarchyDataStore.Instance.HierarchyLookup;
        private NativeList<BatchGroupElement> BatchGroupElements => HierarchyDataStore.Instance.BatchGroupTracker.BatchGroupElements;
        private NativeList<RenderElement<BaseRenderInfo>> BaseInfos => RenderingDataStore.Instance.Common.BaseInfos;
        private RenderingDataStore DataStore => RenderingDataStore.Instance;

        private void InitJobStructs()
        {
            ShaderDataJob = new ShaderDataJob()
            {
                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,
                DirtiedByRendering = DataStore.DirtyState.DirtyShaderData,
                BaseInfos = BaseInfos,
                LayoutProperties = LayoutDataStore.Instance.CalculatedLengths,
                UIBlock2DData = DataStore.UIBlock2DData.BlockData,
                TextNodeData = DataStore.TextBlockData.BlockData,
                UIBlock3DData = DataStore.UIBlock3DData.BlockData,
                ComputeBufferIndices = DataStore.ComputeBufferIndices,
                SurfaceData = DataStore.SurfaceData.Data,
                Hierarchy = Hierarchy,
                LayoutLengths = LayoutDataStore.Instance.LengthConfigs,
                LengthMinMaxes = LayoutDataStore.Instance.LengthMinMaxes,
                AutoSizes = LayoutDataStore.Instance.AutoSizes,
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                ImageDataProvider = DataStore.ImageData.DataProvider,
                PackDataProvider = DataStore.ImageData.PackDataProvider,
                HiddenElements = DataStore.Common.HiddenElements,

                UIBlock2DShaderData = DataStore.UIBlock2DData.ShaderData.GetAccess(),
                ShadowInstanceShaderData = DataStore.UIBlock2DData.Shadow.ComputeBuffer.GetAccess(),
                ShadowQuadShaderData = DataStore.UIBlock2DData.ShadowQuadShaderData.GetAccess(),
                TextPerVertShaderData = DataStore.TextBlockData.PerCharShaderData.GetAccess(),
                UIBlock3DShaderData = DataStore.UIBlock3DData.ShaderData.GetAccess(),
                LightingShaderData = DataStore.Common.TransformAndLightingData.GetAccess(),
            };

            WorldBoundsJob = new WorldSpaceBoundsJob()
            {
                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,
                DirtiedByRendering = DataStore.DirtyState.DirtyShaderData,
                LayoutLengths = LayoutDataStore.Instance.LengthConfigs,
                LengthMinMaxes = LayoutDataStore.Instance.LengthMinMaxes,
                AutoSizes = LayoutDataStore.Instance.AutoSizes,
                BaseInfos = BaseInfos,
                LayoutProperties = LayoutDataStore.Instance.CalculatedLengths,
                ShadowIndices = DataStore.UIBlock2DData.Shadow.Indices,
                TextData = DataStore.TextBlockData.BlockData,
                UIBlock2DData = DataStore.UIBlock2DData.BlockData,
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                ImageDataProvider = DataStore.ImageData.DataProvider,

                Bounds = DataStore.Bounds,
            };

            TextBlockSizeChangeDetectionJob = new TextBlockSizeChangeDetectionJob()
            {
                Hierarchy = Hierarchy,
                BlockData = DataStore.TextBlockData.BlockData,
                CalculatedLayoutLengths = LayoutDataStore.Instance.CalculatedLengths,
                RenderIndexToDataStoreIndex = DataStore.TextBlockData.DataStoreIndices,
                LengthMinMaxes = LayoutDataStore.Instance.LengthMinMaxes,
                LayoutLengths = LayoutDataStore.Instance.LengthConfigs,
                AutoSizes = LayoutDataStore.Instance.AutoSizes,

                DirtiedMargins = DataStore.TextBlockData.DirtiedMargins,
                Margins = DataStore.TextBlockData.Margins,
                ShrinkMask = DataStore.TextBlockData.ShrinkMask,
                ForceSizeOverrideCall = DataStore.TextBlockData.ForceSizeOverrideCall,
            };

            RenderBoundsJob = new CoplanarSpaceBoundsJob()
            {
                DirtyBatches = allDirtyRoots,
                VisualElements = BatchGroupDataStore.VisualElements,
                CoplanarSets = BatchGroupDataStore.CoplanarSets,
                CoplanarSetIDs = DataStore.Common.CoplanarSetIDs,
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                LocalFromWorldMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex,
                ClipMaskData = DataStore.VisualModifierTracker.Data,
                VisualModifierIDs = DataStore.Common.VisualModifierIDs,
                VisualModifierClipBounds = DataStore.VisualModifierTracker.ClipBounds,
                ShadowIndices = DataStore.UIBlock2DData.Shadow.Indices,
                ModifierToBlockID = DataStore.VisualModifierTracker.ModifierToBlockID,
                LayoutProperties = LayoutDataStore.Instance.CalculatedLengths,

                Bounds = DataStore.Bounds,
            };

            GetAllDirtyBatchRootsJob = new GetAllDirtyBatchRootsJob()
            {
                RenderingDirtiedRoots = DataStore.DirtyState.DirtyBatchRoots.List,
                LayoutDirtiedElements = LayoutEngine.Instance.EngineCache.LayoutDirtiedIndices,
                CurrentBatchGroups = HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs,
                BatchGroupElements = BatchGroupElements,
                DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex,
                MatrixDirtiedRoots = LayoutEngine.Instance.EngineCache.MatrixDirtiedRootIDs,
                TransformIndices = DataStore.Common.TransformIndices,
                RootFromNodeTransforms = DataStore.Common.TransformAndLightingData.GetReadonlyAccess(),
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,

                AllDirtiedRoots = allDirtyRoots,
            };

            RootFromBlockMatrixJob = new RootFromBlockMatrixJob()
            {
                DirtyBatchRoots = allDirtyRoots,
                DirtyDependencies = LayoutDataStore.Instance.DirtyDependencies,
                BatchGroupElements = BatchGroupElements,
                LocalFromWorldMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex,
                TransformIndices = DataStore.Common.TransformIndices,
                TransformLocalPositions = LayoutDataStore.Instance.TransformLocalPositions,
                TransformLocalRotations = LayoutDataStore.Instance.TransformLocalRotations,

                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                RootFromNodeTransforms = DataStore.Common.TransformAndLightingData.GetAccess(),
            };

            OverlapJob = new OverlapJob()
            {
                DirtyBatches = allDirtyRoots,
                VisualElements = BatchGroupDataStore.VisualElements,
                ZLayers = BatchGroupDataStore.ZLayers,
                CoplanarSetIDs = DataStore.Common.CoplanarSetIDs,
                ComputeBufferIndices = DataStore.ComputeBufferIndices,
                Bounds = DataStore.Bounds,
                ShadowIndices = DataStore.UIBlock2DData.Shadow.Indices,

                OverlapElements = DataStore.OverlapElements,
            };

            DrawCallJob = new DrawCallArbitrationJob()
            {
                DirtyBatchGroups = allDirtyRoots,
                BaseInfos = BaseInfos,
                CoplanarSetIDs = DataStore.Common.CoplanarSetIDs,
                Bounds = DataStore.Bounds,
                CoplanarSets = BatchGroupDataStore.CoplanarSets,
                VisualElements = BatchGroupDataStore.VisualElements,
                ZLayers = BatchGroupDataStore.ZLayers,
                TextData = DataStore.TextBlockData.BlockData,
                ComputeBufferIndices = DataStore.ComputeBufferIndices,
                DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex,
                VisualModifierClipBounds = DataStore.VisualModifierTracker.ClipBounds,
                LocalFromWorldMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                OverlapElements = DataStore.OverlapElements,
                ModifierToBlockID = DataStore.VisualModifierTracker.ModifierToBlockID,

                MinDrawCalls = BatchGroupDataStore.MinDrawCalls,
                DrawCallSummaries = BatchGroupDataStore.DrawCallSummaries,
            };

            RenderOrderJob = new RenderOrderJob()
            {
                BatchesToProcess = allDirtyRoots,
                Hierarchy = Hierarchy,
                BaseInfos = BaseInfos,
                BatchGroupElements = BatchGroupElements,
                DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex,
                UIBlock2DData = DataStore.UIBlock2DData.BlockData,
                UIBlock3DData = DataStore.UIBlock3DData.BlockData,
                TextNodeData = DataStore.TextBlockData.BlockData,
                LayoutProperties = LayoutDataStore.Instance.CalculatedLengths,
                SurfaceData = DataStore.SurfaceData.Data,
                VisualModifierIDs = DataStore.Common.VisualModifierIDs,
                CoplanarSetRoots = coplanarSetRoots,
                RotationSetRoots = rotationSetRoots,
                ImageDataProvider = DataStore.ImageData.DataProvider,
                PackDataProvider = DataStore.ImageData.PackDataProvider,
                HiddenElements = DataStore.Common.HiddenElements,
                BlockToVisualModifierID = DataStore.VisualModifierTracker.BlockToModifierID,

                ZLayers = BatchGroupDataStore.ZLayers,
                ZLayerCounts = BatchGroupDataStore.ZLayerCounts,
                VisualModifierClipBounds = DataStore.VisualModifierTracker.ClipBounds,
                OrderInZLayer = DataStore.Common.OrderInZLayer,
                SortingProcessQueues = BatchGroupDataStore.SortingProcessQueues,
                DrawCallSummaries = BatchGroupDataStore.DrawCallSummaries,
                VisualElements = BatchGroupDataStore.VisualElements,
                CoplanarSetIDs = DataStore.Common.CoplanarSetIDs,
                RotationSets = BatchGroupDataStore.RotationSets,
                RotationSetIDs = DataStore.Common.RotationSetIDs,
                ContainedSortGroups = BatchGroupDataStore.ContainedSortGroups,
                ParentVisualModifier = DataStore.VisualModifierTracker.ParentModifier,
                ContainedVisualModifers = BatchGroupDataStore.ContainedVisualModifers,
            };

            QuadGenerationJob = new QuadGenerationJob()
            {
                DirtyBatches = allDirtyRoots,
                BaseInfos = BaseInfos,
                BlockData = DataStore.UIBlock2DData.BlockData,
                LayoutProperties = LayoutDataStore.Instance.CalculatedLengths,
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                VisualElements = BatchGroupDataStore.VisualElements,
                ImageDataProvider = DataStore.ImageData.DataProvider,

                RotationSets = BatchGroupDataStore.RotationSets,
                SubQuadData = DataStore.UIBlock2DData.SubQuadData,
            };

            QuadProcessJob = new QuadProcessJob()
            {
                DirtyBatches = allDirtyRoots,
                RotationSets = BatchGroupDataStore.RotationSets,
                VisualElements = BatchGroupDataStore.VisualElements,
                VisualModifierIDs = DataStore.Common.VisualModifierIDs,

                SubQuadData = DataStore.UIBlock2DData.SubQuadData,
            };

            SubQuadOverlapProcessJob = new SubQuadShaderDataJob()
            {
                DirtyBatches = allDirtyRoots,
                ComputeBufferIndices = DataStore.UIBlock2DData.ComputeBufferIndices,
                VisualElements = BatchGroupDataStore.VisualElements,
                RotationSets = BatchGroupDataStore.RotationSets,
                LocalFromWorldMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                UIBlock2DData = DataStore.UIBlock2DData.BlockData,
                ImageDataProvider = DataStore.ImageData.DataProvider,

                SubQuadData = DataStore.UIBlock2DData.SubQuadData,
                ProcessingData = subQuadProcessingData,
            };

            SubQuadVertCopyJob = new SubQuadVertCopyJob()
            {
                DirtyBatches = allDirtyRoots,
                SubQuadData = DataStore.UIBlock2DData.SubQuadData,
                VisualElements = BatchGroupDataStore.VisualElements,

                SubQuadBuffers = BatchGroupDataStore.SubQuadBuffers,
                DrawCallSummaries = BatchGroupDataStore.DrawCallSummaries,
            };

            VisualElementCountJob = new VisualElementCountJob()
            {
                DirtyBatchRoots = allDirtyRoots,
                VisualElements = BatchGroupDataStore.VisualElements,
                RotationSets = BatchGroupDataStore.RotationSets,

                VisualElementCount = updateCounts,
            };

            RenderSetFilterJob = new RenderSetFilterJob()
            {
                AllBatchRootIDs = HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs,
                BatchGroupElements = BatchGroupElements,
                DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex,
                Hierarchy = Hierarchy,
                LocalFromWorldMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                PotentialCoplanarSetRoots = potentialCoplanarSetRoots,
                PotentialRotationSetRoots = potentialRotationSetRoots,
                DirtyRoots = allDirtyRoots,

                CoplanarSetRoots = coplanarSetRoots,
                RotationSetRoots = rotationSetRoots,
                CoplanarSets = BatchGroupDataStore.CoplanarSets,
                RotationSets = BatchGroupDataStore.RotationSets,
            };

            FinalizeJob = new RenderingFinalizeJob()
            {
                HierarchyRootIDs = HierarchyDataStore.Instance.HierarchyRootIDs,
                ContainedSortGroups = BatchGroupDataStore.ContainedSortGroups,
                DrawCallSummaries = BatchGroupDataStore.DrawCallSummaries,
                DirtyBatchRoots = allDirtyRoots,
                Hierarchy = HierarchyDataStore.Instance.Hierarchy,
                DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex,
                BaseInfos = BaseInfos,
                LayoutProperties = LayoutDataStore.Instance.CalculatedLengths,
                LocalFromWorldMatrices = LayoutDataStore.Instance.WorldToLocalMatrices,
                WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices,
                ModifierToBlockID = DataStore.VisualModifierTracker.ModifierToBlockID,
                UIBlock2DData = DataStore.UIBlock2DData.BlockData,
                VisualModifierData = DataStore.VisualModifierTracker.Data,
                VisualModifierRenderData = DataStore.VisualModifierTracker.RenderData,
                VisualModifierShaderData = DataStore.VisualModifierTracker.ShaderData,
                SortGroupInfos = DataStore.RootDataStore.SortGroupInfos,
                CachedMaterials = MaterialCache.CachedMaterials,
                CachedShaders = MaterialCache.CachedShaders,
                ScreenSpaceCameraTargets = DataStore.RootDataStore.ScreenSpaceCameraTargets,
                ParentVisualModifier = DataStore.VisualModifierTracker.ParentModifier,
                RootVisualModifierOverride = BatchGroupDataStore.RootVisualModifierOverride,
                VisualModifierIDs = DataStore.Common.VisualModifierIDs,
                BatchGroupElements = BatchGroupElements,
                ContainedVisualModifers = BatchGroupDataStore.ContainedVisualModifers,

                ProcessingQueue = sortGroupProcessingQueue,
                SortGroupHierarchyInfo = SortGroupHierarchyInfo,
                UpdatedVisualModifiers = updatedVisualModifiers,
                MaterialAssignments = BatchGroupDataStore.MaterialAssignments,
                MaterialsToAdd = MaterialCache.MaterialsToAdd,
                ShadersToAdd = MaterialCache.ShadersToAdd,
                ProcessedSortGroupInfos = processedSortGroupInfos,
            };
        }
    }
}