// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Common;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal struct ZLayerCounts : IInitializable, IClearable
    {
        private NovaList<ZLayerCount> layerCounts;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IncrementCount(short layer)
        {
            if (layerCounts.TryGetIndexOf(layer, out int index))
            {
                return layerCounts.ElementAt(index).BlockCount++;
            }
            else
            {
                layerCounts.Add(new ZLayerCount()
                {
                    ZIndex = layer,
                    BlockCount = 1
                });
                return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRenderOrderOffset(short layer)
        {
            int offset = 0;
            for (int i = 0; i < layerCounts.Length; ++i)
            {
                ZLayerCount count = layerCounts[i];
                if (count.ZIndex == layer)
                {
                    return offset;
                }

                offset += count.BlockCount;
            }
            return offset;
        }

        public void SortLayers()
        {
            layerCounts.list.Sort();
        }

        public void Init()
        {
            layerCounts.Init();
        }

        public void Dispose()
        {
            layerCounts.Dispose();
        }

        public void Clear()
        {
            layerCounts.Clear();
        }

        private struct ZLayerCount : IEquatable<short>, IComparable<ZLayerCount>
        {
            public int BlockCount;
            public short ZIndex;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CompareTo(ZLayerCount other)
            {
                return ZIndex - other.ZIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(short other)
            {
                return ZIndex == other;
            }
        }
    }
}

