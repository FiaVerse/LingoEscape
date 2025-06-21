// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct AccentDataStore<T> : IInitializable where T : unmanaged
    {
        public NovaComputeBuffer<T, T> ComputeBuffer;
        public NovaHashMap<RenderIndex, ComputeBufferIndex> Indices;
        public NativeList<ComputeBufferIndex, NovaList<VisualElementIndex>> OverlappingElements;
        public NativeList<ComputeBufferIndex, AccentBounds> Bounds;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearDirtyState()
        {
            ComputeBuffer.ClearDirtyState();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(RenderIndex renderIndex)
        {
            ComputeBufferIndex computeBufferIndex = ComputeBuffer.GetFreeIndex();
            Indices.Add(renderIndex, computeBufferIndex);

            if (computeBufferIndex >= OverlappingElements.Length)
            {
                OverlappingElements.AddEmpty();
                Bounds.Add(default);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(RenderIndex renderIndex, RenderIndex swappedForward, out ComputeBufferIndex computeBufferIndex)
        {
            if (!Indices.TryGetValue(renderIndex, out computeBufferIndex))
            {
                TrySetIndex(swappedForward, renderIndex);
                return false;
            }

            ComputeBuffer.FreeIndex(computeBufferIndex);

            if (!TrySetIndex(swappedForward, renderIndex))
            {
                Indices.Remove(renderIndex);
            }

            return true;
        }

        private bool TrySetIndex(RenderIndex toSet, RenderIndex newIndex)
        {
            if (toSet.IsValid && Indices.TryGetValue(toSet, out ComputeBufferIndex swappedIndex))
            {
                Indices[newIndex] = swappedIndex;
                Indices.Remove(toSet);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Dispose()
        {
            ComputeBuffer.Dispose();
            Indices.Dispose();
            OverlappingElements.DisposeListAndElements();
            Bounds.Dispose();
        }

        public void Init()
        {
            ComputeBuffer.Init(Constants.SomeElementsInitialCapacity);
            Indices.Init(Constants.SomeElementsInitialCapacity);
            OverlappingElements.Init(Constants.SomeElementsInitialCapacity);
            Bounds.Init(Constants.SomeElementsInitialCapacity);
        }
        public static implicit operator ShaderBuffer(AccentDataStore<T> ds) => ds.ComputeBuffer;
    }

    internal unsafe struct UIBlock2DDataStore : IRenderingSubStore<UIBlock2DData, RenderIndex>
    {
        public NativeList<RenderIndex, UIBlock2DData> BlockData;

        public NativeList<RenderIndex, SubQuadData> SubQuadData;
        public NovaComputeBuffer<UIBlock2DShaderData, UIBlock2DShaderData> ShaderData;
        public NativeList<RenderIndex, ComputeBufferIndex> ComputeBufferIndices;

        public AccentDataStore<PerInstanceDropShadowShaderData> Shadow;
        public NovaComputeBuffer<AllQuadsDropShadowShaderData, PerQuadDropShadowShaderData> ShadowQuadShaderData;

        private NativeList<RenderIndex, DataStoreIndex> dataStoreIndices;
        private NativeList<RenderIndex, AccessIndex> accessIndices;

        private NativeList<AccessWrapper<UIBlock2DData>> accessedBlocks;
        private NativeList<SubQuadData> subQuadDataPool;

        public void ClearDirtyState()
        {
            accessedBlocks.Clear();

            ShaderData.ClearDirtyState();
            Shadow.ClearDirtyState();
            ShadowQuadShaderData.ClearDirtyState();
        }

        public void DoPreUpdate(ref RenderingPreUpdateData preUpdateData)
        {
            for (int i = 0; i < accessedBlocks.Length; ++i)
            {
                ref AccessWrapper<UIBlock2DData> accessed = ref accessedBlocks.ElementAt(i);

                using GCHandleCleanup cleanup = new GCHandleCleanup(accessed.GCHandle);

                if (!accessed.DataStoreID.IsValid ||
                    !preUpdateData.DataStoreIDToDataStoreIndex.TryGetValue(accessed.DataStoreID, out DataStoreIndex dataStoreIndex))
                {
                    continue;
                }

                RenderIndex renderIndex = preUpdateData.BaseInfos[dataStoreIndex].RenderIndex;
                accessIndices[renderIndex] = AccessIndex.Invalid;
                if (!BlockData.TryGetPointerAt(renderIndex, out UIBlock2DData* currentDataPtr))
                {
                    continue;
                }

                accessed.Data.Validate();

                if (accessed.Data.Equals(*currentDataPtr))
                {
                    continue;
                }

                if (accessed.Data.Shadow.HasOuterShadow != currentDataPtr->Shadow.HasOuterShadow)
                {
                    if (accessed.Data.Shadow.HasOuterShadow)
                    {
                        AddShadow(renderIndex);
                    }
                    else
                    {
                        RemoveShadow(renderIndex, RenderIndex.Invalid);
                    }
                }

                MemoryUtils.MemCpy(currentDataPtr, accessed.Ptr);
                preUpdateData.DirtyState.DirtyShaderData.Add(dataStoreIndex);
                DataStoreID batchRootID = preUpdateData.AllBatchGroupElements[dataStoreIndex].BatchRootID;
                preUpdateData.DirtyState.DirtyBatchRoots.Add(batchRootID);
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

        public RenderIndex Add(DataStoreIndex dataStoreIndex, ref UIBlock2DData data)
        {
            RenderIndex renderIndex = BlockData.Length;
            BlockData.Add(data);
            SubQuadData.Add(subQuadDataPool.GetFromPoolOrInit());
            ComputeBufferIndices.Add(ShaderData.GetFreeIndex());
            accessIndices.Add(AccessIndex.Invalid);
            dataStoreIndices.Add(dataStoreIndex);

            if (data.Shadow.HasOuterShadow)
            {
                AddShadow(renderIndex);
            }

            return renderIndex;
        }

        public ref UIBlock2DData GetUpToDateInfo(RenderIndex renderIndex)
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
        public ref UIBlock2DData Access(IUIBlock2D block, RenderIndex renderIndex)
        {
            ref AccessIndex accessIndex = ref accessIndices.ElementAt(renderIndex);
            if (accessIndex >= 0)
            {
                return ref accessedBlocks.ElementAt(accessIndex).Data;
            }

            accessIndex = accessedBlocks.Length;

            AccessWrapper<UIBlock2DData> access = new AccessWrapper<UIBlock2DData>(block, ref block.RenderData);
            accessedBlocks.Add(access);
            return ref access.Data;
        }

        public bool RemoveAtSwapBack(DataStoreID dataStoreID, RenderIndex renderIndex, out DataStoreIndex swappedForwardIndex)
        {
            BlockData.RemoveAtSwapBack(renderIndex);
            subQuadDataPool.ReturnToPool(ref SubQuadData.ElementAt(renderIndex));
            SubQuadData.RemoveAtSwapBack(renderIndex);
            ShaderData.FreeIndex(ComputeBufferIndices[renderIndex]);
            ComputeBufferIndices.RemoveAtSwapBack(renderIndex);
            dataStoreIndices.RemoveAtSwapBack(renderIndex);

            AccessIndex accessIndex = accessIndices[renderIndex];
            accessIndices.RemoveAtSwapBack(renderIndex);
            if (accessIndex >= 0)
            {
                accessedBlocks.ElementAt(accessIndex).DataStoreID = DataStoreID.Invalid;
            }

            RemoveShadow(renderIndex, BlockData.Length);

            if (renderIndex == BlockData.Length)
            {
                // It was the last element, so no need to adjust the data store index
                swappedForwardIndex = -1;
                return false;
            }
            else
            {
                swappedForwardIndex = dataStoreIndices[renderIndex];
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddShadow(RenderIndex renderIndex)
        {
            Shadow.Add(renderIndex);
            ShadowQuadShaderData.GetFreeIndex();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveShadow(RenderIndex renderIndex, RenderIndex swappedForward)
        {
            if (Shadow.Remove(renderIndex, swappedForward, out ComputeBufferIndex computeBufferIndex))
            {
                ShadowQuadShaderData.FreeIndex(computeBufferIndex);
            }
        }

        public void Dispose()
        {
            BlockData.Dispose();

            SubQuadData.DisposeListAndElements();
            ShaderData.Dispose();
            Shadow.Dispose();
            ShadowQuadShaderData.Dispose();

            ComputeBufferIndices.Dispose();

            dataStoreIndices.Dispose();
            accessIndices.Dispose();

            accessedBlocks.Dispose();
            subQuadDataPool.DisposeListAndElements();
        }

        public void Init()
        {
            BlockData.Init(Constants.AllElementsInitialCapacity);

            SubQuadData.Init(Constants.AllElementsInitialCapacity);

            ShaderData.Init(Constants.AllElementsInitialCapacity);
            Shadow.Init();
            ShadowQuadShaderData.Init(Constants.SomeElementsInitialCapacity);

            ComputeBufferIndices.Init(Constants.AllElementsInitialCapacity);

            dataStoreIndices.Init(Constants.AllElementsInitialCapacity);
            accessIndices.Init(Constants.AllElementsInitialCapacity);

            accessedBlocks.Init(Constants.AllElementsInitialCapacity);
            subQuadDataPool.Init(Constants.SomeElementsInitialCapacity);
        }
    }
}

