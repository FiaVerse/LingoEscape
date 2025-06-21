// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal partial class RenderingDataStore : DataStore<RenderingDataStore, IRenderBlock>
    {
        public RenderingDirtyState DirtyState;

        public CommonData Common;
        public UIBlock2DDataStore UIBlock2DData;
        public TextBlockDataStore TextBlockData;
        public UIBlock3DDataStore UIBlock3DData;
        public SurfaceDataStore SurfaceData;
        public ImageDataStore ImageData;
        public RenderRootDataStore RootDataStore;

        #region Runners
        [FixedAddressValueType]
        private static DataStoreRunner runner;
        private static BurstedMethod<BurstMethod> register;
        private static BurstedMethod<BurstMethod> unregister;
        private static BurstedMethod<BurstMethod> preprocess;
        #endregion

        public HashSet<DataStoreID> ScreenSpaceRoots = new HashSet<DataStoreID>();
        public List<IScreenSpace> ScreenSpaces = new List<IScreenSpace>();
        public VisualModifierTracker VisualModifierTracker = new VisualModifierTracker();
        public Dictionary<TextMaterialID, Material> TextMaterials = new Dictionary<TextMaterialID, Material>();

        public ImageTracker ImageTracker = null;

        public void UpdateScreenSpaces()
        {
            for (int i = 0; i < ScreenSpaces.Count; ++i)
            {
                var screenSpace = ScreenSpaces[i];
                if (screenSpace == null)
                {
                    continue;
                }

                screenSpace.Update();
            }
        }

        public bool IsNonHierarchyBatchRoot(DataStoreID dataStoreID)
        {
            return RootDataStore.Roots.TryGetValue(dataStoreID, out RenderRootType rootType) && rootType != RenderRootType.Hierarchy;
        }

        public override bool IsDirty => DirtyState.IsDirty;

        public unsafe void PreProcessDirtyState()
        {
            runner.ContainedSortGroups = RenderEngine.Instance.BatchGroupDataStore.ContainedSortGroups;
            preprocess.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
        }

        public override void ClearDirtyState()
        {
            DirtyState.Clear();
            ImageTracker.ClearDirtyState();
            UIBlock2DData.ClearDirtyState();
            UIBlock3DData.ClearDirtyState();
            TextBlockData.ClearDirtyState();
            SurfaceData.ClearDirtyState();

            Common.TransformAndLightingData.ClearDirtyState();
        }

        public void HandleNodeParentChanged(DataStoreID dataStoreID, bool isHierarchyRoot)
        {
            if (!IsRegistered(dataStoreID))
            {
                return;
            }

            bool isStoredAsBatchRoot = RootDataStore.Roots.TryGetValue(dataStoreID, out RenderRootType rootType);

            if (isHierarchyRoot && !isStoredAsBatchRoot)
            {
                // Became a hierarchy root
                RootDataStore.AddHierarchyRoot(dataStoreID);
            }
            else if (!isHierarchyRoot && isStoredAsBatchRoot && rootType == RenderRootType.Hierarchy)
            {
                RootDataStore.RemoveHierarchyRoot(dataStoreID);
            }
        }

        public void RegisterOrUpdateScreenSpace(DataStoreID dataStoreID, IScreenSpace screenSpace)
        {
            if (ScreenSpaceRoots.Add(dataStoreID))
            {
                ScreenSpaces.Add(screenSpace);
            }
            RootDataStore.AddScreenSpaceRoot(dataStoreID, screenSpace);
            DirtyChildSortGroups(dataStoreID);
        }

        public void UnregisterScreenSpaceRoot(DataStoreID dataStoreID, IScreenSpace screenSpace)
        {
            if (ScreenSpaceRoots.Remove(dataStoreID))
            {
                ScreenSpaces.Remove(screenSpace);
            }

            RootDataStore.RemoveScreenSpaceRoot(dataStoreID);
            DirtyChildSortGroups(dataStoreID);
        }

        public void RemoveSortGroup(DataStoreID dataStoreID)
        {
            if (!RootDataStore.Roots.TryGetValue(dataStoreID, out RenderRootType rootType) ||
                rootType != RenderRootType.SortGroup)
            {
                return;
            }

            if (!Elements.TryGetValue(dataStoreID, out IRenderBlock renderNode))
            {
                return;
            }

            HierarchyDataStore.Instance.RemoveBatchRoot(dataStoreID);
            RootDataStore.RemoveSortGroup(dataStoreID);
            DirtyState.DirtyBatchRoots.Add(dataStoreID);

            if (renderNode.IsHierarchyRoot)
            {
                // Add it back as a hierarchy root
                RootDataStore.AddHierarchyRoot(dataStoreID);
            }

            if (ScreenSpaceRoots.Contains(dataStoreID))
            {
                // If this is a screen space target, dirty the children. This is pretty overkill since the draw calls
                // and things don't need to be processed.
                DirtyChildSortGroups(dataStoreID);
            }
        }

        private void DirtyChildSortGroups(DataStoreID dataStoreID)
        {
            if (RenderEngine.Instance.BatchGroupDataStore.ContainedSortGroups.TryGetValue(dataStoreID, out var containedSortGroups))
            {
                // If this is a screen space target, dirty the children. This is pretty overkill since the draw calls
                // and things don't need to be processed.
                DirtyState.DirtyBatchRoots.AddRange(containedSortGroups);
            }
        }

        public void AddOrUpdateSortGroup(DataStoreID dataStoreID, SortGroupInfo newInfo)
        {
            bool isTracked = RootDataStore.Roots.TryGetValue(dataStoreID, out RenderRootType rootType);

            if (!isTracked || rootType == RenderRootType.Hierarchy)
            {
                // Either wasn't tracked at all, or changed from hierarchy to sort group
                RootDataStore.AddSortGroup(dataStoreID, ref newInfo);
                DirtyState.DirtyBatchRoots.Add(dataStoreID);
                HierarchyDataStore.Instance.AddBatchRoot(dataStoreID);

                if (ScreenSpaceRoots.Contains(dataStoreID))
                {
                    // If this is a screen space target, dirty the children. This is pretty overkill since the draw calls
                    // and things don't need to be processed.
                    DirtyChildSortGroups(dataStoreID);
                }

                return;
            }

            if (isTracked && rootType == RenderRootType.SortGroup)
            {
                // Already tracked as a sort group, just update the info
                if (RootDataStore.SortGroupInfos[dataStoreID].Equals(newInfo))
                {
                    // Info matches, nothing to do
                    return;
                }

                RootDataStore.SortGroupInfos[dataStoreID] = newInfo;
                DirtyState.DirtyBatchRoots.Add(dataStoreID);

                if (ScreenSpaceRoots.Contains(dataStoreID))
                {
                    // If this is a screen space target, dirty the children. This is pretty overkill since the draw calls
                    // and things don't need to be processed.
                    DirtyChildSortGroups(dataStoreID);
                }

                return;
            }
        }

        protected override bool TryGetIndex(DataStoreID id, out DataStoreIndex index) => HierarchyDataStore.Instance.IDToIndexMap.TryGetIndex(id, out index);
        protected override DataStoreID GetID(DataStoreIndex index) => HierarchyDataStore.Instance.IDToIndexMap.ToID(index);

        #region Access
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UIBlock2DData Access(IUIBlock2D block)
        {
            if (!block.Index.IsValid)
            {
                return ref block.RenderData;
            }

            RenderIndex renderIndex = Common.BaseInfos.ElementAt(block.Index).RenderIndex;
            return ref UIBlock2DData.Access(block, renderIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UIBlock3DData Access(IUIBlock3D block)
        {
            if (!block.Index.IsValid)
            {
                return ref block.RenderData;
            }

            RenderIndex renderIndex = Common.BaseInfos.ElementAt(block.Index).RenderIndex;
            return ref UIBlock3DData.Access(block, renderIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TextBlockData Access(IRenderBlock<TextBlockData> block, int submeshCount)
        {
            RenderIndex renderIndex = Common.BaseInfos.ElementAt(block.Index).RenderIndex;
            return ref TextBlockData.Access(block.UniqueID, renderIndex, submeshCount);
        }

        public ref Surface AccessSurface(IRenderBlock block)
        {
            if (!block.Index.IsValid)
            {
                // Not registered, return the existing data on the block
                return ref block.Surface;
            }

            return ref SurfaceData.Access(block);
        }
        #endregion

        public void CopyBaseInfoToStore(IRenderBlock renderNode)
        {
            if (!initialized)
            {
                return;
            }

            if (!Instance.IsRegistered(renderNode.UniqueID))
            {
                return;
            }

            ref RenderElement<BaseRenderInfo> currentElement = ref Common.BaseInfos.ElementAt(renderNode.Index);
            ref BaseRenderInfo newInfo = ref renderNode.BaseRenderInfo;
            if (!currentElement.Val.Equals(ref newInfo))
            {
                DirtyState.DirtyBaseInfos.Add(renderNode.UniqueID);
                currentElement.Val = newInfo;
            }

            EditModeUtils.QueueEditorUpdateNextFrame();
        }

        protected unsafe override void CopyToStoreImpl(IRenderBlock renderNode)
        {
            DataStoreID dataStoreID = renderNode.UniqueID;
            DataStoreIndex dataStoreIndex = renderNode.Index;
            ref RenderElement<BaseRenderInfo> currentElement = ref Common.BaseInfos.ElementAt(dataStoreIndex);
            ref BaseRenderInfo newInfo = ref renderNode.BaseRenderInfo;

            SurfaceData.Access(renderNode);

            if (!currentElement.Val.Equals(ref newInfo))
            {
                DirtyState.DirtyBaseInfos.Add(renderNode.UniqueID);
                currentElement.Val = newInfo;
            }

            switch (renderNode)
            {
                case IUIBlock2D block2D:
                    UIBlock2DData.Access(block2D, currentElement.RenderIndex);
                    break;
                case IUIBlock3D block3D:
                    UIBlock3DData.Access(block3D, currentElement.RenderIndex);
                    break;

            }
        }

        protected override void CopyFromStoreImpl(IRenderBlock val)
        {
            DataStoreIndex dataStoreIndex = val.Index;
            val.Surface = SurfaceData.GetUpToDateInfo(dataStoreIndex);
            switch (val)
            {
                case IRenderBlock<UIBlock2DData> uiNode2D:
                {
                    RenderIndex renderIndex = Common.BaseInfos[val.Index].RenderIndex;
                    uiNode2D.RenderData = UIBlock2DData.GetUpToDateInfo(renderIndex);
                    break;
                }
                case IRenderBlock<UIBlock3DData> uiNode3D:
                {
                    RenderIndex renderIndex = Common.BaseInfos[val.Index].RenderIndex;
                    uiNode3D.RenderData = UIBlock3DData.GetUpToDateInfo(renderIndex);
                    break;
                }
            }
        }

        protected override void CloneImpl(IRenderBlock source, IRenderBlock destination)
        {
            DataStoreIndex dataStoreIndex = source.Index;
            destination.Surface = SurfaceData.GetUpToDateInfo(dataStoreIndex);

            RenderElement<BaseRenderInfo> renderElement = Common.BaseInfos[source.Index];
            destination.BaseRenderInfo = renderElement.Val;

            switch (destination)
            {
                case IRenderBlock<UIBlock2DData> uiNode2D:
                {
                    RenderIndex renderIndex = renderElement.RenderIndex;
                    uiNode2D.RenderData = UIBlock2DData.GetUpToDateInfo(renderIndex);
                    uiNode2D.RenderData.Image.ImageID = ImageID.Invalid;
                    break;
                }
                case IRenderBlock<UIBlock3DData> uiNode3D:
                {
                    RenderIndex renderIndex = renderElement.RenderIndex;
                    uiNode3D.RenderData = UIBlock3DData.GetUpToDateInfo(renderIndex);
                    break;
                }
            }
        }

        protected override void Add(IRenderBlock renderNode)
        {
            runner.IsHierarchyRoot = renderNode.IsHierarchyRoot;
            runner.DataStoreIndex = renderNode.Index;
            runner.DataStoreID = renderNode.UniqueID;
            runner.BaseInfo = renderNode.BaseRenderInfo;
            runner.Surface = renderNode.Surface;
            switch (renderNode)
            {
                case IRenderBlock<UIBlock2DData> uiNode2D:
                    runner.BlockData.Block2D = uiNode2D.RenderData;
                    break;
                case IRenderBlock<UIBlock3DData> uiNode3D:
                    runner.BlockData.Block3D = uiNode3D.RenderData;
                    break;
            }

            unsafe
            {
                register.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
            }
        }

        protected override void RemoveAtSwapBack(DataStoreID idToRemove, DataStoreIndex indexToRemove)
        {
            runner.DataStoreIndex = indexToRemove;
            runner.DataStoreID = idToRemove;
            unsafe
            {
                unregister.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
            }

            if (RootDataStore.Roots.TryGetValue(idToRemove, out RenderRootType rootType))
            {
                if (rootType == RenderRootType.Hierarchy)
                {
                    RootDataStore.RemoveHierarchyRoot(idToRemove);
                }
                else if (rootType == RenderRootType.SortGroup)
                {
                    RootDataStore.RemoveSortGroup(idToRemove);
                    HierarchyDataStore.Instance.RemoveBatchRoot(idToRemove);
                }
            }
        }

        public void EditorOnly_CleanupDecompressedTextures() => ImageTracker.EditorOnly_CleanupDecompressedTextures();

        public override void Init()
        {
            base.Init();

            unsafe
            {
                register = new BurstedMethod<BurstMethod>(DataStoreRunner.DoRegister);
                unregister = new BurstedMethod<BurstMethod>(DataStoreRunner.DoUnregister);
                preprocess = new BurstedMethod<BurstMethod>(DataStoreRunner.DoPreprocess);
            }

            Common.Init();
            DirtyState.Init();
            SurfaceData.Init();
            UIBlock2DData.Init();
            UIBlock3DData.Init();
            TextBlockData.Init();
            RootDataStore.Init();

            VisualModifierTracker.Init();
            ImageData.Init();

            ImageTracker = new ImageTracker(ref ImageData);

            InitJobStructs();

            runner = new DataStoreRunner()
            {
                Common = Common,
                SurfaceData = SurfaceData,
                UIBlock2DData = UIBlock2DData,
                UIBlock3DData = UIBlock3DData,
                TextBlockData = TextBlockData,
                RootData = RootDataStore,
                DirtyState = DirtyState,
                PreUpdateData = new RenderingPreUpdateData()
                {
                    DataStoreIDToDataStoreIndex = HierarchyDataStore.Instance.HierarchyLookup,
                    AllBatchGroupElements = HierarchyDataStore.Instance.BatchGroupTracker.BatchGroupElements,
                    BaseInfos = Common.BaseInfos,

                    DirtyState = DirtyState,
                },
            };
        }

        public override void Dispose()
        {
            base.Dispose();
            Common.Dispose();

            DirtyState.Dispose();

            SurfaceData.Dispose();
            UIBlock2DData.Dispose();
            UIBlock3DData.Dispose();
            TextBlockData.Dispose();
            ImageData.Dispose();
            RootDataStore.Dispose();

            VisualModifierTracker.Dispose();
            ImageTracker.Dispose();
        }
    }
}
