// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct TextBlockDataStore : IRenderingSubStore<TextBlockData, RenderIndex>
    {
        public NativeList<RenderIndex, TextBlockData> BlockData;
        public NativeList<RenderIndex, DataStoreIndex> DataStoreIndices;
        public NovaComputeBuffer<PerCharacterTextShaderData, PerVertTextShaderData> PerCharShaderData;

        public NativeList<RenderIndex, NovaList<ComputeBufferIndex>> ComputeBufferIndices;
        public NativeList<RenderIndex, TextMargin> Margins;
        public NativeList<ValuePair<DataStoreID, TextMargin>> DirtiedMargins;
        /// <summary>
        /// See comment on <see cref="ShrinkMask"/>
        /// </summary>
        public NativeList<ValuePair<DataStoreID, float2>> ForceSizeOverrideCall;
        /// <summary>
        /// This is a total hack, and I really hate that we have to do this, but
        /// I'm not going to try and be clever with TMP anymore since it's caused so many problems.
        /// Basically, if a TextBlock has a size of zero on an axis and then you shrink on that axis,
        /// the TextBlock size won't actually update until the next time the text mesh gets generated
        /// because we only call <see cref="Layouts.LayoutDataStore.UpdateShrinkSizeOverride(ILayoutBlock, float2)"/>
        /// whenever we reprocess the text mesh, but when we set the TMP margin to zero after you start shrinking
        /// it's *already* zero so it doesn't get dirtied. Anywho, this stupid array as a workaround to track
        /// when that specific edge case happens.
        /// </summary>
        public NativeList<RenderIndex, bool2> ShrinkMask;

        private NativeList<TextBlockMeshData> meshDataPool;
        private NativeList<TextBlockData> blockDataPool;
        private NativeList<NovaList<ComputeBufferIndex>> computeBufferIndexPool;

        private NativeDedupedList<DataStoreID> dirtiedElements;

        public void ClearDirtyState()
        {
            dirtiedElements.Clear();
            PerCharShaderData.ClearDirtyState();

        }

        public unsafe void DoPreUpdate(ref RenderingPreUpdateData preUpdateData)
        {
            for (int i = 0; i < dirtiedElements.Length; ++i)
            {
                if (!preUpdateData.DataStoreIDToDataStoreIndex.TryGetValue(dirtiedElements[i], out DataStoreIndex dataStoreIndex))
                {
                    continue;
                }

                preUpdateData.DirtyState.DirtyShaderData.Add(dataStoreIndex);
                DataStoreID batchRootID = preUpdateData.AllBatchGroupElements[dataStoreIndex].BatchRootID;
                preUpdateData.DirtyState.DirtyBatchRoots.Add(batchRootID);

                RenderIndex renderIndex = preUpdateData.BaseInfos[dataStoreIndex].RenderIndex;
                ref TextBlockData data = ref BlockData.ElementAt(renderIndex);
                ref NovaList<ComputeBufferIndex> indices = ref ComputeBufferIndices.ElementAt(renderIndex);

                int newQuadCount = data.QuadCount;
                if (newQuadCount == indices.Length)
                {
                    continue;
                }

                preUpdateData.DirtyState.DirtyBatchRoots.Add(preUpdateData.AllBatchGroupElements[dataStoreIndex].BatchRootID);

                int vertDiff = newQuadCount - indices.Length;
                if (vertDiff > 0)
                {
                    PerCharShaderData.GetFreeIndices(ref indices, vertDiff);
                }
                else if (vertDiff < 0)
                {
                    PerCharShaderData.FreeRange(indices, newQuadCount, -vertDiff);
                    indices.Length = newQuadCount;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TextBlockData Access(DataStoreID dataStoreID, RenderIndex renderIndex, int submeshCount)
        {
            ref TextBlockData toRet = ref BlockData.ElementAt(renderIndex);

            if (submeshCount != -1)
            {
                // Ensure correct submesh count
                int submeshCountDiff = toRet.MeshData.Length - submeshCount;
                if (submeshCountDiff > 0)
                {
                    for (int i = toRet.MeshData.Length - 1; i >= submeshCount; i--)
                    {
                        meshDataPool.ReturnToPool(ref toRet.MeshData.ElementAt(i));
                    }

                    toRet.MeshData.Length = submeshCount;
                }
                else if (submeshCountDiff < 0)
                {
                    for (int i = 0; i < -submeshCountDiff; ++i)
                    {
                        toRet.MeshData.Add(meshDataPool.GetFromPoolOrInit());
                    }
                }
            }

            dirtiedElements.Add(dataStoreID);
            return ref toRet;
        }

        public RenderIndex Add(DataStoreIndex dataStoreIndex)
        {
            TextBlockData data = blockDataPool.GetFromPoolOrInit();
            RenderIndex renderIndex = BlockData.Length;
            BlockData.Add(data);
            Margins.Add(TextMargin.Invalid);
            ShrinkMask.Add(default);
            DataStoreIndices.Add(dataStoreIndex);
            ComputeBufferIndices.Add(computeBufferIndexPool.GetFromPoolOrInit());
            return renderIndex;
        }

        public bool RemoveAtSwapBack(DataStoreID dataStoreID, RenderIndex index, out DataStoreIndex swappedForwardIndex)
        {
            ref TextBlockData data = ref BlockData.ElementAt(index);
            for (int i = 0; i < data.MeshData.Length; ++i)
            {
                ref TextBlockMeshData meshData = ref data.MeshData.ElementAt(i);
                meshDataPool.ReturnToPool(ref meshData);
            }

            blockDataPool.ReturnToPool(ref data);
            BlockData.RemoveAtSwapBack(index);
            Margins.RemoveAtSwapBack(index);
            ShrinkMask.RemoveAtSwapBack(index);

            NovaList<ComputeBufferIndex> indices = ComputeBufferIndices[index];
            PerCharShaderData.FreeRange(indices, 0, indices.Length);
            computeBufferIndexPool.ReturnToPool(ref indices);
            ComputeBufferIndices.RemoveAtSwapBack(index);
            DataStoreIndices.RemoveAtSwapBack(index);

            if (index == BlockData.Length)
            {
                // It was the last element, so no need to adjust the data store index
                swappedForwardIndex = -1;
                return false;
            }
            else
            {
                swappedForwardIndex = DataStoreIndices[index];
                return true;
            }
        }

        public void SetDataStoreIndex(RenderIndex renderIndex, DataStoreIndex newDataStoreIndex)
        {
            if (!IndexVerifier.ValidIndex(renderIndex, DataStoreIndices.Length))
            {
                Debug.LogError($"Tried to set global index from sublist index {renderIndex} when list was length {DataStoreIndices.Length}");
                return;
            }

            DataStoreIndices[renderIndex] = newDataStoreIndex;
        }

        public void Dispose()
        {
            BlockData.Dispose();
            Margins.Dispose();
            ShrinkMask.Dispose();
            DirtiedMargins.Dispose();
            ForceSizeOverrideCall.Dispose();
            PerCharShaderData.Dispose();
            ComputeBufferIndices.DisposeListAndElements();
            dirtiedElements.Dispose();
            DataStoreIndices.Dispose();
            meshDataPool.DisposeListAndElements();
            blockDataPool.DisposeListAndElements();
            computeBufferIndexPool.DisposeListAndElements();
        }

        public void Init()
        {
            BlockData.Init(Constants.SomeElementsInitialCapacity);
            Margins.Init(Constants.SomeElementsInitialCapacity);
            ShrinkMask.Init(Constants.SomeElementsInitialCapacity);
            DirtiedMargins.Init(Constants.SomeElementsInitialCapacity);
            ForceSizeOverrideCall.Init(Constants.SomeElementsInitialCapacity);
            PerCharShaderData.Init(Constants.AllElementsInitialCapacity);
            ComputeBufferIndices.Init(Constants.SomeElementsInitialCapacity);
            dirtiedElements.Init(Constants.SomeElementsInitialCapacity);
            DataStoreIndices.Init(Constants.SomeElementsInitialCapacity);
            meshDataPool.Init(Constants.SomeElementsInitialCapacity);
            blockDataPool.Init(Constants.SomeElementsInitialCapacity);
            computeBufferIndexPool.Init(Constants.SomeElementsInitialCapacity);
        }
    }
}

