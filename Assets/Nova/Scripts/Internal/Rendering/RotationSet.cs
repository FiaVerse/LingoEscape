// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    internal struct BoundarySummary : IInitializable, IClearable
    {
        public NovaList<QuadBoundsDescriptor> Descriptors;
        public NovaList<ValuePair<QuadBoundsDescriptor, int>> InProgress;

        public NovaList<float3> Scratch;

        public void ResizeQuadDescriptors(int length)
        {
            Descriptors.Length = length;
        }

        public void Clear()
        {
            Descriptors.Clear();
            InProgress.Clear();
            Scratch.Clear();
        }

        public void Dispose()
        {
            Descriptors.Dispose();
            InProgress.Dispose();
            Scratch.Dispose();
        }

        public void Init()
        {
            Descriptors.Init();
            InProgress.Init();
            Scratch.Init();
        }
    }

    /// <summary>
    /// A rotation set is a group of content which is coplanar as well as identity rotations
    /// </summary>
    internal struct RotationSet : IInitializable, IClearable
    {
        public float4x4 WorldFromSet;
        public float4x4 SetFromWorld;
        public DataStoreID VisualModifierID;
        public NovaList<VisualElementIndex> QuadProviders;
        public BoundarySummary BoundarySummary;

        public void ResizeDescriptorArray()
        {
            BoundarySummary.ResizeQuadDescriptors(QuadProviders.Length);
        }

        public void Clear()
        {
            QuadProviders.Clear();
            BoundarySummary.Clear();
        }

        public void Dispose()
        {
            QuadProviders.Dispose();
            BoundarySummary.Dispose();
        }

        public void Init()
        {
            QuadProviders.Init();
            BoundarySummary.Init();
        }
    }

    /// <summary>
    /// Summary of the rotation sets in a batch group
    /// </summary>
    internal struct RotationSetSummary : IInitializable, IClearable
    {
        public NovaList<RotationSetID, RotationSet> Sets;
        private NovaList<RotationSet> rotationSetPool;

        public int SetCount
        {
            get => Sets.Length;
        }

        public int QuadProviderCount
        {
            get
            {
                int toRet = 0;
                for (int i = 0; i < Sets.Length; ++i)
                {
                    toRet += Sets.ElementAt(i).QuadProviders.Length;
                }
                return toRet;
            }
        }

        public void ResizeDescriptorArray()
        {
            for (int i = 0; i < Sets.Length; ++i)
            {
                Sets.ElementAt(i).ResizeDescriptorArray();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterQuadProvider(RotationSetID rotationSetID, VisualElementIndex visualElementIndex)
        {
            Sets.ElementAt(rotationSetID).QuadProviders.Add(visualElementIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RotationSetID CreateSet(ref float4x4 localFromWorld, ref float4x4 worldFromLocal)
        {
            RotationSetID toRet = Sets.Length;

            if (!rotationSetPool.TryPopBack(out RotationSet rotationSet))
            {
                rotationSet.Init();
            }

            rotationSet.SetFromWorld = localFromWorld;
            rotationSet.WorldFromSet = worldFromLocal;
            Sets.Add(rotationSet);
            return toRet;
        }

        public void Clear()
        {
            Sets.ReturnAllToPool(ref rotationSetPool);
        }

        public void Dispose()
        {
            Sets.DisposeListAndElements();
            rotationSetPool.DisposeListAndElements();
        }

        public void Init()
        {
            Sets.Init();
            rotationSetPool.Init();
        }
    }

    internal static class RotationSetSummaryUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetQuadProvider(this ref NativeList<DataStoreID> dirtyBatches, ref NovaHashMap<DataStoreID, RotationSetSummary> rotationSets, ref int index, out RotationSet rotationSet, out DataStoreID batchRootID)
        {
            for (int i = 0; i < dirtyBatches.Length; ++i)
            {
                batchRootID = dirtyBatches[i];
                RotationSetSummary rotationSetSummary = rotationSets[batchRootID];

                if (index >= rotationSetSummary.QuadProviderCount)
                {
                    index -= rotationSetSummary.QuadProviderCount;
                    continue;
                }

                for (int j = 0; j < rotationSetSummary.SetCount; ++j)
                {
                    rotationSet = rotationSetSummary.Sets.ElementAt(j);
                    if (index >= rotationSet.QuadProviders.Length)
                    {
                        index -= rotationSet.QuadProviders.Length;
                        continue;
                    }

                    return true;
                }
            }

            rotationSet = default;
            batchRootID = default;
            return false;
        }
    }
}


