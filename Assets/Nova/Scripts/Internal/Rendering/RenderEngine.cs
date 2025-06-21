// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Camera;

namespace Nova.Internal.Rendering
{
    internal struct RenderEngineUpdateCounts
    {
        public int VisualElementCount;
        public int RotationSetCount;
        public int QuadProviderCount;
    }

    internal unsafe partial class RenderEngine : EngineBaseGeneric<RenderEngine>
    {
        public MeshProvider MeshProvider { get; private set; } = null;
        private NativeReference<RenderEngineUpdateCounts> updateCounts;
        private RenderEngineUpdateCounts* updateCountsPtr = null;

        private Dictionary<DataStoreID, BatchGroupRenderer> batchGroupRenderers = new Dictionary<DataStoreID, BatchGroupRenderer>(Constants.SomeElementsInitialCapacity);

        #region Runners
        [FixedAddressValueType]
        private static RenderEngineRunner engineRunner = default;
        private static BurstedMethod<BurstMethod> ensureRoots;

        [FixedAddressValueType]
        private static CameraSorting.Runner cameraSorter = default;
        private static BurstedMethod<BurstMethod> sort;

        private static BurstedMethod<TextBlockSizeChangeDetectionJob.DetectChanges> textBlockUpdate;
        #endregion

        #region Cached
        private CameraCallback _precullHandler = null;
        private CameraCallback PrecullHandler
        {
            get
            {
                if (_precullHandler == null)
                {
                    _precullHandler = RenderCamera;
                }
                return _precullHandler;
            }
        }

        private System.Action<ScriptableRenderContext, Camera> _beginCameraRenderHandler = null;
        private System.Action<ScriptableRenderContext, Camera> BeginCameraRenderHandler
        {
            get
            {
                if (_beginCameraRenderHandler == null)
                {
                    _beginCameraRenderHandler = HandleCameraRenderBegin;
                }
                return _beginCameraRenderHandler;
            }
        }
        #endregion

        public BatchGroupDataStore BatchGroupDataStore;

        private bool dirtyEverything = false;
        private NativeList<SubQuadProcessingData> subQuadProcessingData;
        private NativeList<DataStoreID> allDirtyRoots;
        private NativeList<DataStoreIndex> potentialCoplanarSetRoots;
        private NativeList<DataStoreIndex> potentialRotationSetRoots;
        private NovaHashMap<DataStoreID, CoplanarSetID> coplanarSetRoots;
        private NovaHashMap<DataStoreID, RotationSetID> rotationSetRoots;
        public NovaHashMap<DataStoreID, SortGroupHierarchyInfo> SortGroupHierarchyInfo;
        private NativeList<DataStoreID> sortGroupProcessingQueue;
        private NativeList<VisualModifierID> updatedVisualModifiers;
        private NativeList<DataStoreID> batchRootsInPrefabStage;
        /// <summary>
        /// Since children of screen space sort groups inherit the roots renderqueue setting
        /// </summary>
        private NovaHashMap<DataStoreID, SortGroupInfo> processedSortGroupInfos;

        private static readonly ProfilerMarker textRebuildMarker = new ProfilerMarker("TMP Mesh Building");

        public bool EditorOnly_TexturesHaveBeenReprocessed { get; set; } = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NovaList<DrawCallID, CameraSorting.ProcessedDrawCall> GetDrawCallBounds(DataStoreID batchGroupID) => cameraSorter.GetDrawCallBounds(batchGroupID);

        public RenderOrderCalculator HierarchyRenderOrderCalculator => new RenderOrderCalculator()
        {
            BatchGroupElements = BatchGroupElements,
            BaseInfos = BaseInfos,
            ZLayerCounts = BatchGroupDataStore.ZLayerCounts,
            OrderInZLayer = DataStore.Common.OrderInZLayer,
            SortGroupInfos = processedSortGroupInfos,
        };

        private bool subscribedToCameraEvents = false;
        private void SubscribeToCamera()
        {
            if (subscribedToCameraEvents)
            {
                return;
            }

            subscribedToCameraEvents = true;
            // We subscribe to both events here because the user could switch the pipelines at runtime
            RenderPipelineManager.beginCameraRendering += BeginCameraRenderHandler;
            Camera.onPreCull += PrecullHandler;
        }

        private void UnsubscribeFromCamera()
        {
            if (!subscribedToCameraEvents)
            {
                return;
            }

            subscribedToCameraEvents = false;
            // We subscribe to both events here because the user could switch the pipelines at runtime
            RenderPipelineManager.beginCameraRendering -= BeginCameraRenderHandler;
            Camera.onPreCull -= PrecullHandler;
        }

        public override void PostUpdate()
        {
            MaterialCache.EnsureMaterials();

#pragma warning disable CS0162 // Unreachable code detected
            if (NovaApplication.ConstIsEditor)
            {
                if (EditorOnly_TexturesHaveBeenReprocessed)
                {
                    foreach (BatchGroupRenderer renderer in batchGroupRenderers.Values)
                    {
                        renderer.EditorOnly_HandleTexturesReprocessed();
                    }
                    EditorOnly_TexturesHaveBeenReprocessed = false;
                }
            }
#pragma warning restore CS0162 // Unreachable code detected

            // Update the visual modifier shader data
            for (int i = 0; i < updatedVisualModifiers.Length; ++i)
            {
                VisualModifierID visualModifierID = updatedVisualModifiers[i];

                ManagedVisualModifierShaderData managedData = DataStore.VisualModifierTracker.ManagedShaderData[visualModifierID];
                managedData.CopyFrom(DataStore.VisualModifierTracker.ShaderData[visualModifierID]);
            }

            // Update the override visual modifiers
            for (int i = 0; i < allDirtyRoots.Length; ++i)
            {
                DataStoreID rootID = allDirtyRoots[i];
                VisualModifierShaderData visualModifierShaderData = BatchGroupDataStore.RootVisualModifierOverride[rootID];
                batchGroupRenderers[rootID].OverrideVisualModifierData.CopyFrom(visualModifierShaderData);
            }

            {
                for (int i = 0; i < allDirtyRoots.Length; ++i)
                {
                    batchGroupRenderers[allDirtyRoots[i]].ClearDrawCalls();
                }

                DataStore.ImageTracker.NotifySubscribers();

                UpdateComputeBuffers();

                for (int i = 0; i < allDirtyRoots.Length; ++i)
                {
                    batchGroupRenderers[allDirtyRoots[i]].UpdateDrawCalls();
                }

                NativeList<DataStoreID> movedRoots = LayoutEngine.Instance.EngineCache.MatrixDirtiedRootIDs;
                for (int i = 0; i < movedRoots.Length; ++i)
                {
                    batchGroupRenderers[movedRoots[i]].UpdateRootMatrices();
                }
            }

            allDirtyRoots.Clear();
            DataStore.ClearDirtyState();
            dirtyEverything = false;

            if (batchGroupRenderers.Count > 0)
            {
                SubscribeToCamera();
            }
        }

        public unsafe void ProcessTextBlocks(ref NativeList<DataStoreIndex> dirtyIndices)
        {
            TextBlockSizeChangeDetectionJob.DirtyElements dirtyElements = new TextBlockSizeChangeDetectionJob.DirtyElements()
            {
                BaseInfos = DataStore.Common.BaseInfos,
                DirtyIndices = dirtyIndices,
            };

            textBlockUpdate.Method.Invoke(UnsafeUtility.AddressOf(ref TextBlockSizeChangeDetectionJob), UnsafeUtility.AddressOf(ref dirtyElements));

            UpdateResizedTextBlocks();
        }

        public void EnsureTextMeshes()
        {
            if (LayoutEngine.Instance.ShouldRunLayoutUpdate)
            {
                UpdateResizedTextBlocks();
            }

            DataStore.PreProcessDirtyState();
        }

        private void UpdateResizedTextBlocks()
        {
            NativeList<ValuePair<DataStoreID, float2>> forceSizeOverrideCall = DataStore.TextBlockData.ForceSizeOverrideCall;
            if (forceSizeOverrideCall.Length > 0)
            {
                for (int i = 0; i < forceSizeOverrideCall.Length; ++i)
                {
                    // See comment on <see cref="TextBlockDataStore.ShrinkMask"/>
                    var pair = forceSizeOverrideCall[i];
                    if (!DataStore.Elements.TryGetValue(pair.Item1, out IRenderBlock renderBlock))
                    {
                        continue;
                    }

                    LayoutDataStore.Instance.UpdateShrinkSizeOverride(renderBlock as ILayoutBlock, pair.Item2);
                }

                DataStore.TextBlockData.ForceSizeOverrideCall.Clear();
            }

            NativeList<ValuePair<DataStoreID, TextMargin>> resizedTextBlocks = DataStore.TextBlockData.DirtiedMargins;

            int resizedBlockCount = resizedTextBlocks.Length;

            if (resizedBlockCount == 0)
            {
                // don't need to loop and don't want to add the profiler marker
                return;
            }

            using (textRebuildMarker.Auto())
            {
                for (int i = 0; i < resizedBlockCount; ++i)
                {
                    ref ValuePair<DataStoreID, TextMargin> resized = ref resizedTextBlocks.ElementAt(i);
                    ITextBlock textBlock = DataStore.Elements[resized.Item1] as ITextBlock;
                    textBlock.UpdateMeshSize(ref resized.Item2);
                }
            }

            DataStore.TextBlockData.DirtiedMargins.Clear();
        }

        private void UpdateComputeBuffers()
        {
            UpdateComputeBuffer(DataStore.Common.TransformAndLightingData, ShaderPropertyIDs.TransformsAndLighting, ComputeBufferUpdateType.All);

            UpdateComputeBuffer(DataStore.UIBlock2DData.ShaderData, ShaderPropertyIDs.ShaderData, ComputeBufferUpdateType.UIBlock2D);
            UpdateComputeBuffer(DataStore.UIBlock2DData.ShadowQuadShaderData, ShaderPropertyIDs.ShaderData, ComputeBufferUpdateType.DropShadow);
            UpdateComputeBuffer(DataStore.UIBlock2DData.Shadow.ComputeBuffer, ShaderPropertyIDs.PerBlockData, ComputeBufferUpdateType.DropShadow);

            UpdateComputeBuffer(DataStore.UIBlock3DData.ShaderData, ShaderPropertyIDs.ShaderData, ComputeBufferUpdateType.UIBlock3D);

            UpdateComputeBuffer(DataStore.TextBlockData.PerCharShaderData, ShaderPropertyIDs.ShaderData, ComputeBufferUpdateType.Text);
        }

        private void UpdateComputeBuffer<TCPU, TGPU>(NovaComputeBuffer<TCPU, TGPU> buffer, int shaderPropertyID, ComputeBufferUpdateType updateType)
            where TCPU : unmanaged
            where TGPU : unmanaged
        {
            if (!buffer.UpdateComputeBuffer())
            {
                return;
            }

            for (int i = 0; i < BatchGroupDataStore.KnownBatchRoots.Length; ++i)
            {
                batchGroupRenderers[BatchGroupDataStore.KnownBatchRoots[i]].UpdateComputeBuffer(buffer, shaderPropertyID, updateType);
            }
        }

        public override JobHandle PreUpdate(JobHandle enginePreUpdateHandle)
        {
            DataStore.ImageTracker.PreUpdate();

#pragma warning disable CS0162 // Unreachable code detected
            if (NovaApplication.ConstIsEditor)
            {
                // The user can change the color space in project settings, but (as far as I can tell)
                // there are no events or indication that that has happened, so we unfortunately need to check every frame
                if (SystemSettings.ColorSpace != QualitySettings.activeColorSpace)
                {
                    SystemSettings.Init();
                    dirtyEverything = true;
                }
            }
#pragma warning restore CS0162 // Unreachable code detected


            return enginePreUpdateHandle;
        }

        public override void UpdateFirstPass(ref EngineUpdateInfo engineUpdateInfo)
        {
            if (LayoutEngine.Instance.ShouldRunLayoutUpdate)
            {
                TextBlockSizeChangeDetectionJob.NovaScheduleByRef(engineUpdateInfo.EngineSequenceCompleteHandle).Complete();
            }
        }

        public override void UpdateSecondPass(ref EngineUpdateInfo engineUpdateInfo)
        {
            if (!HierarchyDataStore.Instance.IsDirty &&
                !LayoutDataStore.Instance.IsDirty &&
                !DataStore.IsDirty &&
                !dirtyEverything)
            {
                // Need to run this regardless, because if everything was disabled, we might need to do some cleanup
                DataStore.ImageTracker.UpdatePacks();
                return;
            }

            EnsureRoots(ref engineUpdateInfo.RootsToUpdate);

            if (Hierarchy.Length > potentialCoplanarSetRoots.Capacity)
            {
                potentialCoplanarSetRoots.Capacity = Hierarchy.Length;
                potentialRotationSetRoots.Capacity = Hierarchy.Length;
            }

            potentialCoplanarSetRoots.Clear();
            potentialRotationSetRoots.Clear();

            ShaderDataJob.DirtyEverything = dirtyEverything;
            GetAllDirtyBatchRootsJob.DirtyEverything = dirtyEverything;
            GetAllDirtyBatchRootsJob.HierarchyDirtiedRoots = engineUpdateInfo.RootsToUpdate;
            RootFromBlockMatrixJob.PotentialCoplanarSetRoots = potentialCoplanarSetRoots.AsParallelWriter();
            RootFromBlockMatrixJob.PotentialRotationSetRoots = potentialRotationSetRoots.AsParallelWriter();


            JobHandle getDirtyRootsJob = GetAllDirtyBatchRootsJob.NovaScheduleByRef(engineUpdateInfo.EngineSequenceCompleteHandle);

            JobHandle rootFromNodeJob = RootFromBlockMatrixJob.NovaScheduleByRef(Hierarchy.Length, 64, getDirtyRootsJob);
            JobHandle renderSetFilterJob = RenderSetFilterJob.NovaScheduleByRef(rootFromNodeJob);

            JobHandle worldSpaceBoundsJob = WorldBoundsJob.NovaScheduleByRef(Hierarchy.Length, 32, rootFromNodeJob);
            JobHandle shaderDataJob = ShaderDataJob.NovaScheduleByRef(Hierarchy.Length, 32, rootFromNodeJob);

            JobHandle renderOrderJob = RenderOrderJob.ScheduleByRef(allDirtyRoots, 1, renderSetFilterJob);
            JobHandle visualElementCountJob = VisualElementCountJob.NovaScheduleByRef(renderOrderJob);

            JobHandle quadGenJob = QuadGenerationJob.ScheduleByRef(&updateCountsPtr->QuadProviderCount, 64, visualElementCountJob);
            JobHandle quadProcessJob = QuadProcessJob.ScheduleByRef(&updateCountsPtr->RotationSetCount, 1, quadGenJob);
            JobHandle subQuadOverlapJob = SubQuadOverlapProcessJob.ScheduleByRef(&updateCountsPtr->QuadProviderCount, 32, quadProcessJob);

            JobHandle renderBoundsJob = RenderBoundsJob.ScheduleByRef(&updateCountsPtr->VisualElementCount, 32, JobHandle.CombineDependencies(worldSpaceBoundsJob, visualElementCountJob));
            JobHandle overlapJob = OverlapJob.ScheduleByRef(&updateCountsPtr->VisualElementCount, 32, renderBoundsJob);
            JobHandle drawCallArbitrationjob = DrawCallJob.ScheduleByRef(allDirtyRoots, 1, overlapJob);

            JobHandle subQuadShaderDataJob = SubQuadVertCopyJob.ScheduleByRef(allDirtyRoots, 1, JobHandle.CombineDependencies(drawCallArbitrationjob, subQuadOverlapJob));

            FinalizeJob.CurrentMaterialCount = MaterialCache.NextMaterialIndex;
            FinalizeJob.CurrentShaderCount = MaterialCache.NextShaderIndex;
            JobHandle finalizeJob = FinalizeJob.NovaScheduleByRef(renderOrderJob);

            JobHandle combinedHandle = JobHandle.CombineDependencies(subQuadShaderDataJob, shaderDataJob, finalizeJob);

            DataStore.ImageTracker.UpdatePacks();
            combinedHandle.Complete();
        }

        private void EnsureRoots(ref NativeList<DataStoreID> dirtyRoots)
        {
            engineRunner.DirtyRoots = dirtyRoots;
            engineRunner.TrackedRootIDs = HierarchyEngine.Instance.TrackedRootIDs;

            unsafe
            {
                ensureRoots.Method.Invoke(UnsafeUtility.AddressOf(ref engineRunner));
            }

            for (int i = 0; i < engineRunner.RemovedBatchRoots.Length; ++i)
            {
                DataStoreID dataStoreID = engineRunner.RemovedBatchRoots[i];

                if (batchGroupRenderers.TryGetValue(dataStoreID, out BatchGroupRenderer batchGroupRenderer))
                {
                    batchGroupRenderer.Dispose();
                    batchGroupRenderers.Remove(dataStoreID);
                }
            }

            for (int i = 0; i < engineRunner.AddedBatchRoots.Length; ++i)
            {
                DataStoreID dataStoreID = engineRunner.AddedBatchRoots[i];
                batchGroupRenderers.Add(dataStoreID, new BatchGroupRenderer(dataStoreID));
            }
        }

        private void HandleCameraRenderBegin(ScriptableRenderContext arg1, Camera cam)
        {
            RenderCamera(cam);
        }

        private void RenderCamera(Camera cam)
        {
            if (HierarchyDataStore.Instance.Elements.Count == 0)
            {
                UnsubscribeFromCamera();
                return;
            }

            try
            {
#pragma warning disable CS0162 // Unreachable code detected
                if (NovaApplication.ConstIsEditor)
                {
                    switch (cam.cameraType)
                    {
                        case CameraType.Game:
                        case CameraType.SceneView:
                        case CameraType.VR:
                            break;
                        default:
                            return;
                    }
                }
#pragma warning restore CS0162 // Unreachable code detected

                cameraSorter.Camera = new CameraSorting.CameraConfig(cam);

                cameraSorter.BatchRootsToSort = GetBatchRootsToRender(cam);

                unsafe
                {
                    sort.Method.Invoke(UnsafeUtility.AddressOf(ref cameraSorter));
                }

                {
                    for (int i = 0; i < cameraSorter.BatchRootsToRender.Length; ++i)
                    {
                        if (!batchGroupRenderers.TryGetValue(cameraSorter.BatchRootsToRender[i], out BatchGroupRenderer renderer))
                        {
                            continue;
                        }

                        renderer.Render(cam);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Nova RenderCamera failed with: {e}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeList<DataStoreID> GetBatchRootsToRender(Camera camera)
        {
            if (!NovaApplication.IsEditor)
            {
                return HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs;
            }

            if (!PrefabStageUtils.TryGetCurrentStageRoot(out GameObject prefabRoot))
            {
                return HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs;
            }

            // We need to filter the batch roots to only those in the current camera's context
            batchRootsInPrefabStage.Clear();

            for (int i = 0; i < HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs.Length; ++i)
            {
                DataStoreID batchRootID = HierarchyDataStore.Instance.BatchGroupTracker.BatchRootIDs[i];

                if (!BatchGroupDataStore.Contains(batchRootID))
                {
                    // It is a new batch root and the engines haven't run yet
                    continue;
                }

                IUIBlock block = HierarchyDataStore.Instance.Elements[batchRootID] as IUIBlock;
                if (block as MonoBehaviour == null)
                {
                    continue;
                }

                Transform uiBlockTransform = block.Transform;

                bool isPartOfPrefab = uiBlockTransform.gameObject == prefabRoot || uiBlockTransform.IsChildOf(prefabRoot.transform);

                if ((camera.cameraType == CameraType.Game && !isPartOfPrefab) ||
                    (camera.cameraType == CameraType.SceneView && isPartOfPrefab))
                {
                    batchRootsInPrefabStage.Add(batchRootID);
                }
            }

            return batchRootsInPrefabStage;
        }

        public void DirtyEverything()
        {
            dirtyEverything = true;
        }

        #region Init and Cleanup
        public override void Init()
        {
            Instance = this;

            unsafe
            {
                ensureRoots = new BurstedMethod<BurstMethod>(RenderEngineRunner.EnsureBatchRoots);
                sort = new BurstedMethod<BurstMethod>(CameraSorting.Runner.DoSort);
                textBlockUpdate = new BurstedMethod<TextBlockSizeChangeDetectionJob.DetectChanges>(TextBlockSizeChangeDetectionJob.Run);
            }

            SystemSettings.Init();
            MeshProvider = new MeshProvider();
            updateCounts.Init();
            updateCountsPtr = updateCounts.GetRawReadonlyPtr();
            allDirtyRoots.Init(Constants.SomeElementsInitialCapacity);

            BatchGroupDataStore.Init();

            potentialCoplanarSetRoots.Init();
            potentialRotationSetRoots.Init();
            coplanarSetRoots.Init();
            rotationSetRoots.Init();

            SortGroupHierarchyInfo.Init();
            sortGroupProcessingQueue.Init();
            updatedVisualModifiers.Init();
            processedSortGroupInfos.Init(Constants.SomeElementsInitialCapacity);

            subQuadProcessingData.Init(JobsUtility.MaxJobThreadCount);
            subQuadProcessingData.Length = JobsUtility.MaxJobThreadCount;
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            {
                subQuadProcessingData.ElementAt(i).Init();
            }

            NovaSettings.OnRenderSettingsChanged += DirtyEverything;

#pragma warning disable CS0162 // Unreachable code detected
            if (NovaApplication.ConstIsEditor)
            {
                batchRootsInPrefabStage.Init();
            }
#pragma warning restore CS0162 // Unreachable code detected


            MaterialCache.Init();
            InitJobStructs();

            cameraSorter.Init();

            cameraSorter.LocalFromWorldMatrices = LayoutDataStore.Instance.WorldToLocalMatrices;
            cameraSorter.WorldFromLocalMatrices = LayoutDataStore.Instance.LocalToWorldMatrices;
            cameraSorter.DataStoreIDToDataStoreIndex = DataStoreIDToDataStoreIndex;
            cameraSorter.DrawCallSummaries = BatchGroupDataStore.DrawCallSummaries;
            cameraSorter.SortGroupInfos = processedSortGroupInfos;
            cameraSorter.CoplanarSets = BatchGroupDataStore.CoplanarSets;
            cameraSorter.SortGroupHierarchyInfo = SortGroupHierarchyInfo;
            cameraSorter.ScreenSpaceCameraTargets = DataStore.RootDataStore.ScreenSpaceCameraTargets;
            cameraSorter.ScreenSpaceAdditionalCameras = DataStore.RootDataStore.ScreenSpaceAdditionalCameras;

            engineRunner = new RenderEngineRunner()
            {
                BatchGroupData = BatchGroupDataStore,
                CameraSorter = cameraSorter,
            };
            engineRunner.Init();

            ShaderPropertyIDs.Init();
        }

        public override void Dispose()
        {
            Instance = null;

            MeshProvider.Dispose();

            updateCounts.Dispose();
            allDirtyRoots.Dispose();

            potentialCoplanarSetRoots.Dispose();
            potentialRotationSetRoots.Dispose();
            coplanarSetRoots.Dispose();
            rotationSetRoots.Dispose();

            SortGroupHierarchyInfo.Dispose();
            updatedVisualModifiers.Dispose();
            sortGroupProcessingQueue.Dispose();
            processedSortGroupInfos.Dispose();

            subQuadProcessingData.DisposeListAndElements();

            batchGroupRenderers.DisposeValues();

            while (!BatchGroupDataStore.KnownBatchRoots.IsEmpty)
            {
                DataStoreID dataStoreID = BatchGroupDataStore.KnownBatchRoots.Last();
                BatchGroupDataStore.Remove(dataStoreID);
                cameraSorter.RemoveBatchGroup(dataStoreID);
            }

            BatchGroupDataStore.Dispose();
            cameraSorter.Dispose();
            engineRunner.Dispose();

            RenderPipelineManager.beginCameraRendering -= BeginCameraRenderHandler;
            Camera.onPreCull -= PrecullHandler;

            NovaSettings.OnRenderSettingsChanged -= DirtyEverything;

            if (NovaApplication.IsEditor)
            {
                batchRootsInPrefabStage.Dispose();
            }

            MaterialCache.Dispose();
        }
        #endregion
    }
}


