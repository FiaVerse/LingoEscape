// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal unsafe struct UIBlock3DDataStore : IRenderingSubStore<UIBlock3DData, RenderIndex>
    {
        public NativeList<RenderIndex, UIBlock3DData> BlockData;

        public NovaComputeBuffer<UIBlock3DShaderData, UIBlock3DShaderData> ShaderData;

        public NativeList<RenderIndex, ComputeBufferIndex> ComputeBufferIndices;
        private NativeList<RenderIndex, DataStoreIndex> dataStoreIndices;
        private NativeList<RenderIndex, AccessIndex> accessIndices;

        private NativeList<AccessWrapper<UIBlock3DData>> accessedBlocks;

        public ref UIBlock3DData GetUpToDateInfo(RenderIndex renderIndex)
        {
            ref AccessIndex accessIndex = ref accessIndices.ElementAt(renderIndex);
            if (accessIndex >= 0)
            {
                return ref accessedBlocks.ElementAt(accessIndex).Data;
            }
            else
            {
                return ref BlockData.ElementAt(renderIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UIBlock3DData Access(IUIBlock3D block, RenderIndex renderIndex)
        {
            ref AccessIndex accessIndex = ref accessIndices.ElementAt(renderIndex);
            if (accessIndex >= 0)
            {
                return ref accessedBlocks.ElementAt(accessIndex).Data;
            }

            accessIndex = accessedBlocks.Length;

            AccessWrapper<UIBlock3DData> access = new AccessWrapper<UIBlock3DData>(block, ref block.RenderData);
            accessedBlocks.Add(access);
            return ref access.Data;
        }

        public RenderIndex Add(DataStoreIndex dataStoreIndex, ref UIBlock3DData data)
        {
            RenderIndex renderIndex = BlockData.Length;
            BlockData.Add(data);
            ComputeBufferIndices.Add(ShaderData.GetFreeIndex());
            accessIndices.Add(AccessIndex.Invalid);
            dataStoreIndices.Add(dataStoreIndex);
            return renderIndex;
        }

        public void ClearDirtyState()
        {
            accessedBlocks.Clear();
            ShaderData.ClearDirtyState();
        }

        public void DoPreUpdate(ref RenderingPreUpdateData preUpdateData)
        {
            for (int i = 0; i < accessedBlocks.Length; ++i)
            {
                ref AccessWrapper<UIBlock3DData> accessed = ref accessedBlocks.ElementAt(i);
                using GCHandleCleanup cleanup = new GCHandleCleanup(accessed.GCHandle);

                if (!accessed.DataStoreID.IsValid ||
                    !preUpdateData.DataStoreIDToDataStoreIndex.TryGetValue(accessed.DataStoreID, out DataStoreIndex dataStoreIndex))
                {
                    continue;
                }

                RenderIndex renderIndex = preUpdateData.BaseInfos[dataStoreIndex].RenderIndex;
                accessIndices[renderIndex] = AccessIndex.Invalid;
                if (!BlockData.TryGetPointerAt(renderIndex, out UIBlock3DData* currentDataPtr))
                {
                    continue;
                }

                accessed.Data.Validate();

                if (accessed.Data.Equals(*currentDataPtr))
                {
                    continue;
                }

                MemoryUtils.MemCpy(currentDataPtr, accessed.Ptr);
                preUpdateData.DirtyState.DirtyShaderData.Add(dataStoreIndex);
                DataStoreID batchRootID = preUpdateData.AllBatchGroupElements[dataStoreIndex].BatchRootID;
                preUpdateData.DirtyState.DirtyBatchRoots.Add(batchRootID);
            }
        }

        public bool RemoveAtSwapBack(DataStoreID dataStoreID, RenderIndex index, out DataStoreIndex swappedForwardIndex)
        {
            BlockData.RemoveAtSwapBack(index);
            ShaderData.FreeIndex(ComputeBufferIndices[index]);
            ComputeBufferIndices.RemoveAtSwapBack(index);
            dataStoreIndices.RemoveAtSwapBack(index);

            AccessIndex accessIndex = accessIndices[index];
            accessIndices.RemoveAtSwapBack(index);
            if (accessIndex >= 0)
            {
                accessedBlocks.ElementAt(accessIndex).DataStoreID = DataStoreID.Invalid;
            }

            if (index == BlockData.Length)
            {
                // It was the last element, so no need to adjust the data store index
                swappedForwardIndex = -1;
                return false;
            }
            else
            {
                swappedForwardIndex = dataStoreIndices[index];
                return true;
            }
        }

        public void SetDataStoreIndex(RenderIndex renderIndex, DataStoreIndex newDataStoreIndex)
        {
            if (!IndexVerifier.ValidIndex(renderIndex, dataStoreIndices.Length))
            {
                Debug.LogError($"Tried to set global index from sublist index {renderIndex} when list was length {dataStoreIndices.Length}");
                return;
            }

            dataStoreIndices[renderIndex] = newDataStoreIndex;
        }

        public void Dispose()
        {
            ShaderData.Dispose();

            BlockData.Dispose();
            ComputeBufferIndices.Dispose();
            dataStoreIndices.Dispose();
            accessIndices.Dispose();
            accessedBlocks.Dispose();
        }

        public void Init()
        {
            ShaderData.Init(Constants.SomeElementsInitialCapacity);

            BlockData.Init(Constants.SomeElementsInitialCapacity);
            ComputeBufferIndices.Init(Constants.SomeElementsInitialCapacity);
            dataStoreIndices.Init(Constants.SomeElementsInitialCapacity);
            accessIndices.Init(Constants.SomeElementsInitialCapacity);
            accessedBlocks.Init(Constants.SomeElementsInitialCapacity);
        }
    }
}
