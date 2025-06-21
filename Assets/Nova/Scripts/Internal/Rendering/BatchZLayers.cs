// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Common;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal struct ZLayerAssignment
    {
        public ZLayerIndex Index;
        public short Layer;
    }

    internal struct BatchZLayers : IInitializable, IClearable
    {
        private NovaList<ZLayer> pool;
        private NovaList<ZLayer> layers;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            for (int i = 0; i < layers.Length; ++i)
            {
                ZLayer layer = layers[i];
                layer.Clear();
                pool.Add(layer);
            }
            layers.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRenderOrder(ZLayerAssignment zLayerAssignment)
        {
            int count = 0;
            for (int i = 0; i < layers.Length; ++i)
            {
                ref ZLayer layer = ref layers.ElementAt(i);
                if (layer.Layer < zLayerAssignment.Layer)
                {
                    count += layer.Length;
                    continue;
                }
                count += zLayerAssignment.Index;
                break;
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddElement(VisualElementIndex visualElementIndex, short zLayer)
        {
            if (layers.TryGetIndexOf(zLayer, out int index))
            {
                ref ZLayer layer = ref layers.ElementAt(index);
                layer.Elements.Add(visualElementIndex);
            }
            else
            {
                if (!pool.TryPopBack(out ZLayer layer))
                {
                    layer.Init();
                }
                layer.Layer = zLayer;
                layer.Elements.Add(visualElementIndex);
                layers.Add(layer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetReverseIterator(int index, out ReverseIterator iterator)
        {
            for (int i = 0; i < layers.Length; ++i)
            {
                ref ZLayer layer = ref layers.ElementAt(i);
                if (index >= layer.Length)
                {
                    index -= layer.Length;
                    continue;
                }

                iterator = new ReverseIterator()
                {
                    CurrentLayerIndex = i,
                    ElementIndex = index,
                };
                return true;
            }

            iterator = ReverseIterator.Invalid;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(out VisualElementIndex visualElementIndex, ref ReverseIterator iterator)
        {
            if (iterator.CurrentLayerIndex < 0)
            {
                visualElementIndex = VisualElementIndex.Invalid;
                return false;
            }

            ref ZLayer layer = ref layers.ElementAt(iterator.CurrentLayerIndex);
            if (iterator.ElementIndex < 0)
            {
                // Go to next layer
                iterator.CurrentLayerIndex -= 1;
                if (iterator.CurrentLayerIndex < 0)
                {
                    visualElementIndex = VisualElementIndex.Invalid;
                    return false;
                }

                iterator.ElementIndex = layers.ElementAt(iterator.CurrentLayerIndex).Length - 1;
                return TryGet(out visualElementIndex, ref iterator);
            }

            visualElementIndex = layer.Elements[iterator.ElementIndex--];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetVal(int index, out VisualElementIndex visualElementIndex, out Iterator iterator)
        {
            for (int i = 0; i < layers.Length; ++i)
            {
                ref ZLayer layer = ref layers.ElementAt(i);
                if (index >= layer.Length)
                {
                    index -= layer.Length;
                    continue;
                }

                visualElementIndex = layer.Elements[index];

                iterator = new Iterator()
                {
                    CurrentLayerIndex = i,
                    ElementIndex = index + 1,
                };
                return true;
            }

            iterator = Iterator.Invalid;
            visualElementIndex = VisualElementIndex.Invalid;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetNext(out VisualElementIndex visualElementIndex, ref Iterator iterator)
        {
            if (iterator.CurrentLayerIndex >= layers.Length)
            {
                visualElementIndex = VisualElementIndex.Invalid;
                return false;
            }

            ref ZLayer layer = ref layers.ElementAt(iterator.CurrentLayerIndex);
            if (iterator.ElementIndex >= layer.Length)
            {
                // Go to next layer
                iterator.ElementIndex = 0;
                iterator.CurrentLayerIndex += 1;
                return TryGetNext(out visualElementIndex, ref iterator);
            }

            visualElementIndex = layer.Elements[iterator.ElementIndex++];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SortLayers()
        {
            layers.list.Sort();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            pool.DisposeListAndElements();
            layers.DisposeListAndElements();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            pool.Init();
            layers.Init(1);
        }

        public struct ZLayer : IInitializable, IEquatable<short>, IComparable<ZLayer>
        {
            public NovaList<ZLayerIndex, VisualElementIndex> Elements;
            public short Layer;

            public int Length
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Elements.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear()
            {
                Elements.Clear();
                Layer = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CompareTo(ZLayer other)
            {
                return Layer.CompareTo(other.Layer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Elements.Dispose();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(short other)
            {
                return Layer == other;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Init()
            {
                Elements.Init();
            }
        }

        public struct ReverseIterator
        {
            public ZLayerIndex ElementIndex;
            public int CurrentLayerIndex;

            public static readonly ReverseIterator Invalid = new ReverseIterator()
            {
                ElementIndex = -1,
                CurrentLayerIndex = -1,
            };
        }

        public struct Iterator
        {
            public ZLayerIndex ElementIndex;
            public int CurrentLayerIndex;

            public static readonly Iterator Invalid = new Iterator()
            {
                ElementIndex = -1,
                CurrentLayerIndex = -1,
            };
        }
    }
}

