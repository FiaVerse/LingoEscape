// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct ComputeBufferReadOnlyAccess<V>
        where V : unmanaged
    {
        [ReadOnly]
        private NativeList<ComputeBufferIndex, V> list;

        public V this[ComputeBufferIndex index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list[index];
        }

        public ComputeBufferReadOnlyAccess(ref NativeList<ComputeBufferIndex, V> list)
        {
            this.list = list;
        }
    }

    internal struct ComputeBufferAccess<V>
        where V : unmanaged
    {
        [NativeDisableParallelForRestriction]
        private NativeList<ComputeBufferIndex, V> list;
        [NativeDisableParallelForRestriction]
        private NativeReference<int> dirtyState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureDirty()
        {
            if (dirtyState.Value == 0)
            {
                Interlocked.Exchange(ref dirtyState.Ref(), 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref V ElementAt(ComputeBufferIndex index)
        {
            EnsureDirty();
            return ref list.ElementAt(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryGetPointerAt(ComputeBufferIndex index, out V* ptr)
        {
            EnsureDirty();
            return list.TryGetPointerAt(index, out ptr);
        }

        public ComputeBufferAccess(ref NativeList<ComputeBufferIndex, V> list, ref NativeReference<int> dirtyState)
        {
            this.list = list;
            this.dirtyState = dirtyState;
        }
    }

    internal struct NovaComputeBuffer<TCPU,TGPU> : ICapacityInitializable
        where TCPU : unmanaged
        where TGPU : unmanaged
    {
        private NativeList<ComputeBufferIndex, TCPU> nativeList;
        public NativeList<ComputeBufferIndex> FreeIndices;
        private NativeReference<int> dirtyState;
        private int computeBufferIndex;

        private ShaderBuffer<TGPU> ShaderBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => shaderBuffers[computeBufferIndex] as ShaderBuffer<TGPU>;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearDirtyState()
        {
            dirtyState.Value = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureDirty()
        {
            dirtyState.Value = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TCPU ElementAt(ComputeBufferIndex index)
        {
            EnsureDirty();
            return ref nativeList.ElementAt(index);
        }

        /// <summary>
        /// Returns true if recreated the compute buffer
        /// </summary>
        /// <returns></returns>
        public unsafe bool UpdateComputeBuffer(bool force = false)
        {
            if (nativeList.Length == 0 || (dirtyState.Value == 0 && !force))
            {
                return false;
            }

            bool toRet = false;
            ShaderBuffer<TGPU> shaderBuffer = ShaderBuffer;

            NativeArray<TCPU> cpuList = nativeList.UnderlyingList.AsArray();
            NativeArray<TGPU> gpuList = cpuList.Reinterpret<TGPU>(sizeof(TCPU));
            if (shaderBuffer == null || shaderBuffer.Count < gpuList.Length)
            {
                if (shaderBuffer != null)
                {
                    toRet = true;
                    shaderBuffer.Dispose();
                }
                shaderBuffer = new ShaderBuffer<TGPU>(Mathf.Max(gpuList.Length, Constants.AllElementsInitialCapacity));
                shaderBuffers[computeBufferIndex] = shaderBuffer;
            }

            shaderBuffer.SetData(ref gpuList);
            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComputeBufferIndex GetFreeIndex()
        {
            EnsureDirty();
            if (!FreeIndices.TryPopBack(out ComputeBufferIndex computeBufferIndex))
            {
                computeBufferIndex = nativeList.Length;
                nativeList.Add(default);
            }
            return computeBufferIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void GetFreeIndices(ref NovaList<ComputeBufferIndex> dest, int count)
        {
            EnsureDirty();
            if (FreeIndices.Length > 0)
            {
                // First try to get from the free indices array
                int startIndex = math.max(0, FreeIndices.Length - count);
                int elementsToCopy = FreeIndices.Length - startIndex;
                dest.AddRange(FreeIndices.GetRawPtr() + startIndex, elementsToCopy);
                count -= elementsToCopy;
                FreeIndices.Length -= elementsToCopy;
            }

            // Add remaining
            for (int i = 0; i < count; i++)
            {
                dest.Add(GetFreeIndex());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FreeIndex(ComputeBufferIndex computeBufferIndex)
        {
            FreeIndices.Add(computeBufferIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void FreeRange(NovaList<ComputeBufferIndex> indices, int startIndex, int count)
        {
            FreeIndices.AddRange(indices.Ptr + startIndex, count);
        }

        public ComputeBufferAccess<TCPU> GetAccess()
        {
            return new ComputeBufferAccess<TCPU>(ref nativeList, ref dirtyState);
        }

        public ComputeBufferReadOnlyAccess<TCPU> GetReadonlyAccess()
        {
            return new ComputeBufferReadOnlyAccess<TCPU>(ref nativeList);
        }

        public void Dispose()
        {
            nativeList.Dispose();
            FreeIndices.Dispose();
            dirtyState.Dispose();
            FreeComputeBuffer(computeBufferIndex);
        }

        public unsafe void Init(int capacity = 0)
        {
            computeBufferIndex = GetComputeBufferIndex();
            nativeList.Init(capacity);
            FreeIndices.Init(16);
            dirtyState.Init();

        }

        public static implicit operator ShaderBuffer(NovaComputeBuffer<TCPU, TGPU> nova) => nova.ShaderBuffer;

        // In order to make NovaComputeBuffer job safe, we need to not actually store the compute buffer in
        // the struct instance
        #region Static
        private static List<int> freeComputeBufferIndices = new List<int>(Constants.SomeElementsInitialCapacity);
        private static List<ShaderBuffer> shaderBuffers = new List<ShaderBuffer>(Constants.SomeElementsInitialCapacity);

        private static void FreeComputeBuffer(int index)
        {
            ShaderBuffer buff = shaderBuffers[index];
            if (buff != null)
            {
                buff.Dispose();
            }
            freeComputeBufferIndices.Add(index);
        }

        private static int GetComputeBufferIndex()
        {
            if (!freeComputeBufferIndices.TryPopBack(out int index))
            {
                index = shaderBuffers.Count;
                shaderBuffers.Add(null);
            }
            return index;
        }

        public static void FreeAllComputeBuffers()
        {
            shaderBuffers.DisposeElementsAndClear();
            shaderBuffers.Clear();
        }
        #endregion
    }
}

