// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal struct SurfaceDataStore
    {
        public NativeList<DataStoreIndex, Surface> Data;

        private NativeList<DataStoreIndex, AccessIndex> accessIndices;
        private NativeList<AccessWrapper<Surface>> accessedBlocks;

        public ref Surface GetUpToDateInfo(DataStoreIndex dataStoreIndex)
        {
            ref AccessIndex accessIndex = ref accessIndices.ElementAt(dataStoreIndex);
            if (accessIndex >= 0)
            {
                return ref accessedBlocks.ElementAt(accessIndex).Data;
            }
            else
            {
                return ref Data.ElementAt(dataStoreIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Surface Access(IRenderBlock block)
        {
            ref AccessIndex accessIndex = ref accessIndices.ElementAt(block.Index);
            if (accessIndex >= 0)
            {
                return ref accessedBlocks.ElementAt(accessIndex).Data;
            }

            accessIndex = accessedBlocks.Length;

            AccessWrapper<Surface> access = new AccessWrapper<Surface>(block, ref block.Surface);
            accessedBlocks.Add(access);
            return ref access.Data;
        }

        public unsafe void DoPreUpdate(ref RenderingPreUpdateData preUpdateData)
        {
            for (int i = 0; i < accessedBlocks.Length; ++i)
            {
                ref AccessWrapper<Surface> accessed = ref accessedBlocks.ElementAt(i);
                using GCHandleCleanup cleanup = new GCHandleCleanup(accessed.GCHandle);

                if (!accessed.DataStoreID.IsValid ||
                    !preUpdateData.DataStoreIDToDataStoreIndex.TryGetValue(accessed.DataStoreID, out DataStoreIndex dataStoreIndex))
                {
                    continue;
                }

                accessIndices[dataStoreIndex] = AccessIndex.Invalid;
                if (!Data.TryGetPointerAt(dataStoreIndex, out Surface* currentDataPtr))
                {
                    continue;
                }

                accessed.Data.Validate();

                if (accessed.Data.Equals(*currentDataPtr))
                {
                    continue;
                }

                if (accessed.Data.LightingModel != currentDataPtr->LightingModel ||
                    accessed.Data.ReceiveShadows != currentDataPtr->ReceiveShadows ||
                    accessed.Data.ShadowCastingMode != currentDataPtr->ShadowCastingMode)
                {
                    // Lighting model changed, need to dirty the whole batch
                    DataStoreID batchRootID = preUpdateData.AllBatchGroupElements[dataStoreIndex].BatchRootID;
                    preUpdateData.DirtyState.DirtyBatchRoots.Add(batchRootID);
                }

                MemoryUtils.MemCpy(currentDataPtr, accessed.Ptr);
                preUpdateData.DirtyState.DirtyShaderData.Add(dataStoreIndex);
            }
        }


        public DataStoreIndex Add(DataStoreIndex dataStoreIndex, ref Surface data)
        {
            Data.Add(data);
            accessIndices.Add(AccessIndex.Invalid);
            return dataStoreIndex;
        }

        public void ClearDirtyState()
        {
            accessedBlocks.Clear();
        }

        public void Dispose()
        {
            Data.Dispose();

            accessIndices.Dispose();
            accessedBlocks.Dispose();
        }

        public void Init()
        {
            Data.Init(Constants.AllElementsInitialCapacity);
            accessIndices.Init(Constants.AllElementsInitialCapacity);
            accessedBlocks.Init(Constants.SomeElementsInitialCapacity);
        }

        public void RemoveAtSwapBack(DataStoreIndex index)
        {
            Data.RemoveAtSwapBack(index);
            AccessIndex accessIndex = accessIndices[index];
            accessIndices.RemoveAtSwapBack(index);
            if (accessIndex >= 0)
            {
                accessedBlocks.ElementAt(accessIndex).DataStoreID = DataStoreID.Invalid;
            }
        }
    }
}