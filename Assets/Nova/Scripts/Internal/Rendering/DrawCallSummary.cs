// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Common;
using System;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Rendering
{
    internal struct DrawCall : IEquatable<DrawCallDescriptorID>, IEquatable<DrawCallSummary.DrawCallBatchingInfo>
    {
        public AABB CoplanarSpaceRenderBounds;
        public DrawCallDescriptorID DescriptorID;
        public DrawCallID ID;
        public CoplanarSetID CoplanarSetID;
        /// <summary>
        /// If this is a 2D draw call, this will be the order of the draw call
        /// in the coplanar set. We need this so that we can offset the transparent draw calls
        /// a certain amount towards the camera to ensure that the draw calls are rendered
        /// correctly by Unity
        /// </summary>
        public int TransparentDrawCallOrderInCoplanarSet;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DrawCallDescriptorID other)
        {
            return DescriptorID == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DrawCallSummary.DrawCallBatchingInfo other)
        {
            return DescriptorID == other.DescriptorID && CoplanarSetID == other.CoplanarID;
        }
    }

    internal struct ShaderIndexBounds
    {
        public int InstanceStart;
        public int InstanceCount;
    }

    /// <summary>
    /// The draw call summary for a single batch group
    /// </summary>
    internal struct DrawCallSummary : IInitializable, IClearable
    {
        public NovaList<DrawCallDescriptorID, DrawCallDescriptor> DrawCallDescriptors;
        public NovaList<DrawCallID, DrawCall> DrawCalls;
        public NovaList<DrawCallID, NovaList<VisualElementIndex>> NonIndexedElements;
        private NovaList<DrawCallID, NovaList<DrawCallIndex, ShaderIndex>> drawCallIndices;
        public NovaList<ShaderIndexBounds> IndexBounds;
        private NovaList<NovaList<DrawCallIndex, ShaderIndex>> indexListPool;
        private NovaList<NovaList<VisualElementIndex>> nonIndexedPool;
        public int TotalIndices;

        public int DrawCallCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => DrawCalls.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateIndexBounds()
        {
            // Update the shader index bounds
            int runningIndexCount = 0;
            for (int i = 0; i < drawCallIndices.Length; ++i)
            {
                if (DrawCallDescriptors[DrawCalls[i].DescriptorID].DrawCallType == VisualType.UIBlock2D)
                {
                    IndexBounds.Add(default);
                    continue;
                }

                IndexBounds.Add(new ShaderIndexBounds()
                {
                    InstanceStart = runningIndexCount,
                    InstanceCount = drawCallIndices[i].Length
                });
                runningIndexCount += drawCallIndices[i].Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DrawCallDescriptorID EnsureDescriptor(ref DrawCallDescriptor descriptor)
        {
            if (DrawCallDescriptors.TryGetIndexOf(descriptor, out int index))
            {
                return index;
            }
            else
            {
                DrawCallDescriptors.Add(descriptor);
                return DrawCallDescriptors.Length - 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIndexBuffer(ref ShaderBuffer<ShaderIndex> shaderBuffer)
        {
            ShaderBufferUtils.EnsureSizeAndCreated(ref shaderBuffer, TotalIndices);
            int offset = 0;

            bool applyChanges = false;
            for (int i = 0; i < drawCallIndices.Length; ++i)
            {
                NovaList<DrawCallIndex, ShaderIndex> indices = drawCallIndices[i];
                if (indices.Length == 0)
                {
                    continue;
                }

                using (var indicesAsArray = indices.UnderlyingList.AsArray())
                {
                    var arr = indicesAsArray.Array;
                    shaderBuffer.SetData(ref arr, 0, offset, apply: false);
                    applyChanges = true;
                }
                offset += indices.Length;
            }

            if (applyChanges)
            {
                shaderBuffer.ApplyChanges();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMatchingDrawCall(DrawCallDescriptorID drawCallDescriptorID, CoplanarSetID coplanarSetID, DrawCallID minID, out DrawCallID matchingID)
        {
            DrawCallBatchingInfo batchingInfo = new DrawCallBatchingInfo()
            {
                CoplanarID = coplanarSetID,
                DescriptorID = drawCallDescriptorID,
            };

            bool toRet = DrawCalls.TryGetIndexOf(batchingInfo, out int index, minID);
            matchingID = index;
            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DrawCallID AddDrawCall(DrawCallDescriptorID descriptorID, CoplanarSetID coplanarSetID)
        {
            DrawCallID toRet = DrawCalls.Length;
            DrawCalls.Add(new DrawCall()
            {
                ID = toRet,
                DescriptorID = descriptorID,
                CoplanarSetID = coplanarSetID
            });

            if (!indexListPool.TryPopBack(out NovaList<DrawCallIndex, ShaderIndex> freeList))
            {
                freeList.Init();
            }
            drawCallIndices.Add(freeList);

            if (DrawCallDescriptors.ElementAt(descriptorID).DrawCallType == VisualType.UIBlock2D)
            {
                if (!nonIndexedPool.TryPopBack(out NovaList<VisualElementIndex> ordered))
                {
                    ordered.Init();
                }
                NonIndexedElements.Add(ordered);
            }
            else
            {
                NonIndexedElements.Add(default);
            }
            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddElementWithIndex(ComputeBufferIndex shaderIndex, ref DrawCallID drawCallID, ref AABB renderBounds)
        {
            TotalIndices += 1;
            drawCallIndices.ElementAt(drawCallID).Add((uint)shaderIndex);
            DrawCalls.ElementAt(drawCallID).CoplanarSpaceRenderBounds.Encapsulate(ref renderBounds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddElementNoIndex(ref DrawCallID drawCallID, ref AABB renderBounds, ref VisualElementIndex visualElementIndex)
        {
            DrawCalls.ElementAt(drawCallID).CoplanarSpaceRenderBounds.Encapsulate(ref renderBounds);
            NonIndexedElements.ElementAt(drawCallID).Add(visualElementIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ShaderIndexBounds GetIndexBounds(DrawCallID id)
        {
            return IndexBounds[id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            TotalIndices = 0;

            for (int i = 0; i < DrawCalls.Length; ++i)
            {
                ref DrawCallDescriptor descriptor = ref DrawCallDescriptors.ElementAt(DrawCalls.ElementAt(i).DescriptorID);
                if (descriptor.DrawCallType != VisualType.UIBlock2D)
                {
                    continue;
                }

                ref NovaList<VisualElementIndex> nonIndexed = ref NonIndexedElements.ElementAt(i);
                nonIndexed.Clear();
                nonIndexedPool.Add(nonIndexed);
            }
            NonIndexedElements.Clear();

            DrawCalls.Clear();
            drawCallIndices.ReturnAllToPool(ref indexListPool);
            NonIndexedElements.Clear();
            IndexBounds.Clear();
            DrawCallDescriptors.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            DrawCallDescriptors.Dispose();
            DrawCalls.Dispose();
            drawCallIndices.DisposeListAndElements();
            NonIndexedElements.DisposeListAndElements();
            indexListPool.DisposeListAndElements();
            IndexBounds.Dispose();
            nonIndexedPool.DisposeListAndElements();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            DrawCallDescriptors.Init();
            DrawCalls.Init();
            drawCallIndices.Init();
            NonIndexedElements.Init();
            IndexBounds.Init();
            indexListPool.Init();
            nonIndexedPool.Init();
        }

        public struct DrawCallBatchingInfo
        {
            public CoplanarSetID CoplanarID;
            public DrawCallDescriptorID DescriptorID;
        }
    }
}

