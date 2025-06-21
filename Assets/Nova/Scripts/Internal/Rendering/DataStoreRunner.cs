// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct DataStoreRunner
    {
        public CommonData Common;
        public UIBlock2DDataStore UIBlock2DData;
        public TextBlockDataStore TextBlockData;
        public UIBlock3DDataStore UIBlock3DData;
        public SurfaceDataStore SurfaceData;
        public RenderRootDataStore RootData;
        public RenderingDirtyState DirtyState;

        public NovaHashMap<DataStoreID, NovaList<DataStoreID>> ContainedSortGroups;

        #region Element Operations
        public bool IsHierarchyRoot;
        public DataStoreIndex DataStoreIndex;
        public DataStoreID DataStoreID;
        public BaseRenderInfo BaseInfo;
        public BlockDataUnion BlockData;
        public Surface Surface;

        private void Register()
        {
            RenderElement<BaseRenderInfo> element = new RenderElement<BaseRenderInfo>(ref BaseInfo);
            switch (BaseInfo.BlockType)
            {
                case BlockType.UIBlock2D:
                    element.RenderIndex = UIBlock2DData.Add(DataStoreIndex, ref BlockData.Block2D);
                    break;
                case BlockType.UIBlock3D:
                    element.RenderIndex = UIBlock3DData.Add(DataStoreIndex, ref BlockData.Block3D);
                    break;
                case BlockType.Text:
                    element.RenderIndex = TextBlockData.Add(DataStoreIndex);
                    break;
            }

            SurfaceData.Add(DataStoreIndex, ref Surface);

            // Common
            Common.BaseInfos.Add(element);
            Common.OverlappingElements.Add(Common.OverlappingElementsPool.GetFromPoolOrInit());
            Common.OrderInZLayer.Add(default);
            Common.CoplanarSetIDs.Add(default);
            Common.RotationSetIDs.Add(default);
            Common.BlockRenderBounds.Add(default);
            Common.VisualModifierIDs.Add(VisualModifierID.Invalid);
            Common.TransformIndices.Add(Common.TransformAndLightingData.GetFreeIndex());

            DirtyState.AddedElements.Add(DataStoreID);

            if (IsHierarchyRoot && !RootData.Roots.ContainsKey(DataStoreID))
            {
                RootData.AddHierarchyRoot(DataStoreID);
            }
        }

        private void Unregister()
        {
            RenderElement<BaseRenderInfo> baseInfoToRemove = Common.BaseInfos[DataStoreIndex];

            // Remove from sub store
            switch (baseInfoToRemove.Val.BlockType)
            {
                case BlockType.UIBlock2D:
                    RemoveAtSwapBackSubArray<UIBlock2DData, UIBlock2DDataStore>(ref baseInfoToRemove, ref UIBlock2DData);
                    break;
                case BlockType.Text:
                    RemoveAtSwapBackSubArray<TextBlockData, TextBlockDataStore>(ref baseInfoToRemove, ref TextBlockData);
                    break;
                case BlockType.UIBlock3D:
                    RemoveAtSwapBackSubArray<UIBlock3DData, UIBlock3DDataStore>(ref baseInfoToRemove, ref UIBlock3DData);
                    break;
            }

            // Common
            SurfaceData.RemoveAtSwapBack(DataStoreIndex);

            if (DataStoreIndex != Common.BaseInfos.Length - 1)
            {
                // If we are not the last element, we need to swap
                RenderElement<BaseRenderInfo> lastElement = Common.BaseInfos[Common.BaseInfos.Length - 1];
                switch (lastElement.Val.BlockType)
                {
                    case BlockType.UIBlock2D:
                        UIBlock2DData.SetDataStoreIndex(lastElement.RenderIndex, DataStoreIndex);
                        break;
                    case BlockType.UIBlock3D:
                        UIBlock3DData.SetDataStoreIndex(lastElement.RenderIndex, DataStoreIndex);
                        break;
                    case BlockType.Text:
                        TextBlockData.SetDataStoreIndex(lastElement.RenderIndex, DataStoreIndex);
                        break;
                }
            }

            Common.BaseInfos.RemoveAtSwapBack(DataStoreIndex);
            Common.VisualModifierIDs.RemoveAtSwapBack(DataStoreIndex);
            Common.OrderInZLayer.RemoveAtSwapBack(DataStoreIndex);
            Common.CoplanarSetIDs.RemoveAtSwapBack(DataStoreIndex);
            Common.RotationSetIDs.RemoveAtSwapBack(DataStoreIndex);
            Common.OverlappingElementsPool.ReturnToPool(ref Common.OverlappingElements.ElementAt(DataStoreIndex));
            Common.OverlappingElements.RemoveAtSwapBack(DataStoreIndex);
            Common.BlockRenderBounds.RemoveAtSwapBack(DataStoreIndex);
            Common.TransformAndLightingData.FreeIndex(Common.TransformIndices[DataStoreIndex]);
            Common.TransformIndices.RemoveAtSwapBack(DataStoreIndex);
        }

        private void RemoveAtSwapBackSubArray<T, TStore>(ref RenderElement<BaseRenderInfo> baseInfoToRemove, ref TStore subStore)
            where TStore : struct, IRenderingSubStore<T, RenderIndex>
        {
            if (subStore.RemoveAtSwapBack(DataStoreID, baseInfoToRemove.RenderIndex, out DataStoreIndex swappedIndex))
            {
                // Change the swapped elements' render index
                Common.BaseInfos.ElementAt(swappedIndex).RenderIndex = baseInfoToRemove.RenderIndex;
            }
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoRegister(void* data)
        {
            UnsafeUtility.AsRef<DataStoreRunner>(data).Register();
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoUnregister(void* data)
        {
            UnsafeUtility.AsRef<DataStoreRunner>(data).Unregister();
        }
        #endregion

        #region PreUpdate
        public RenderingPreUpdateData PreUpdateData;

        private void PreProcess()
        {
            ProcessNewlyAddedElements();
            ProcessModifiedBaseInfos();
            ProcessDirtyVisualModifiers();

            UIBlock2DData.DoPreUpdate(ref PreUpdateData);
            UIBlock3DData.DoPreUpdate(ref PreUpdateData);
            TextBlockData.DoPreUpdate(ref PreUpdateData);
            SurfaceData.DoPreUpdate(ref PreUpdateData);
        }

        private void ProcessDirtyVisualModifiers()
        {
            for (int i = 0; i < PreUpdateData.DirtyState.DirtyVisualModifiers.Length; ++i)
            {
                DataStoreID dataStoreID = PreUpdateData.DirtyState.DirtyVisualModifiers[i];
                if (!PreUpdateData.DataStoreIDToDataStoreIndex.TryGetValue(dataStoreID, out DataStoreIndex dataStoreIndex))
                {
                    continue;
                }

                DataStoreID batchRootID = PreUpdateData.AllBatchGroupElements[dataStoreIndex].BatchRootID;
                UpdateRootAndDescendents(batchRootID);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateRootAndDescendents(DataStoreID rootID)
        {
            PreUpdateData.DirtyState.DirtyBatchRoots.Add(rootID);

            if (!ContainedSortGroups.TryGetValue(rootID, out NovaList<DataStoreID> childRoots))
            {
                return;
            }

            for (int i = 0; i < childRoots.Length; ++i)
            {
                UpdateRootAndDescendents(childRoots[i]);
            }
        }

        private void ProcessNewlyAddedElements()
        {
            for (int i = 0; i < PreUpdateData.DirtyState.AddedElements.Length; ++i)
            {
                if (!PreUpdateData.DataStoreIDToDataStoreIndex.TryGetValue(PreUpdateData.DirtyState.AddedElements[i], out DataStoreIndex dataStoreIndex))
                {
                    continue;
                }

                // Don't need to add to dirty batch roots, as the hierarchy data store will handle that
                PreUpdateData.DirtyState.DirtyShaderData.Add(dataStoreIndex);
            }
        }

        private void ProcessModifiedBaseInfos()
        {
            for (int i = 0; i < PreUpdateData.DirtyState.DirtyBaseInfos.Length; ++i)
            {
                if (!PreUpdateData.DataStoreIDToDataStoreIndex.TryGetValue(PreUpdateData.DirtyState.DirtyBaseInfos[i], out DataStoreIndex dataStoreIndex))
                {
                    continue;
                }

                DataStoreID batchRootID = PreUpdateData.AllBatchGroupElements[dataStoreIndex].BatchRootID;
                PreUpdateData.DirtyState.DirtyShaderData.Add(dataStoreIndex);
                PreUpdateData.DirtyState.DirtyBatchRoots.Add(batchRootID);
            }
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoPreprocess(void* data)
        {
            UnsafeUtility.AsRef<DataStoreRunner>(data).PreProcess();
        }
        #endregion
    }
}

