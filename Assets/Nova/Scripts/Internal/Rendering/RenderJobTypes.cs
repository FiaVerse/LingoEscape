// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct ComputeBufferIndices
    {
        [ReadOnly]
        public NativeList<DataStoreIndex, ComputeBufferIndex> TransformAndLighting;
        [ReadOnly]
        public NativeList<RenderIndex, ComputeBufferIndex> UIBlock2D;
        [ReadOnly]
        public NativeList<RenderIndex, ComputeBufferIndex> UIBlock3D;
        [ReadOnly]
        public NovaHashMap<RenderIndex, ComputeBufferIndex> Shadow;
        [ReadOnly]
        public NativeList<RenderIndex, NovaList<ComputeBufferIndex>> Text;
    }

    internal struct OverlapElements
    {
        [NativeDisableParallelForRestriction]
        public NativeList<DataStoreIndex, NovaList<VisualElementIndex>> OverlappingElements;
        [NativeDisableParallelForRestriction]
        public NativeList<ComputeBufferIndex, NovaList<VisualElementIndex>> ShadowOverlappingElements;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NovaList<VisualElementIndex> Get(ref VisualElement visualElement, ref ComputeBufferIndices indices)
        {
            switch (visualElement.Type)
            {
                case VisualType.UIBlock2D:
                case VisualType.UIBlock3D:
                case VisualType.TextBlock:
                case VisualType.TextSubmesh:
                    return ref OverlappingElements.ElementAt(visualElement.DataStoreIndex);
                case VisualType.DropShadow:
                {
                    ComputeBufferIndex computeBufferIndex = indices.Shadow[visualElement.RenderIndex];
                    return ref ShadowOverlappingElements.ElementAt(computeBufferIndex);
                }
                default:
                    Debug.LogError($"Failed to get overlap list for {visualElement.Type}");
                    return ref OverlappingElements.ElementAt(visualElement.DataStoreIndex);
            }
        }
    }

    internal struct RenderBounds
    {
        /// <summary>
        /// Only used for 2D and text blocks
        /// </summary>
        public float4x4 WorldSpaceCorners;
        public AABB CoplanarSpaceBounds;
    }

    internal struct AccentBounds
    {
        public RenderBounds Outer;
        public RenderBounds Inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps2D(ref AABB other)
        {
            if (!Outer.CoplanarSpaceBounds.Overlaps2D(ref other))
            {
                // Doesn't overlap
                return false;
            }

            return !Inner.CoplanarSpaceBounds.Encapsulates2D(ref other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps2D(ref AccentBounds other)
        {
            if (!Outer.CoplanarSpaceBounds.Overlaps2D(ref other.Outer.CoplanarSpaceBounds))
            {
                // Doesn't overlap
                return false;
            }

            return !(Inner.CoplanarSpaceBounds.Encapsulates2D(ref other.Inner.CoplanarSpaceBounds) ||
                other.Inner.CoplanarSpaceBounds.Encapsulates2D(ref Inner.CoplanarSpaceBounds));
        }
    }

    internal struct BlockBounds
    {
        [NativeDisableParallelForRestriction]
        public NativeList<DataStoreIndex, RenderBounds> Block;
        [NativeDisableParallelForRestriction]
        public NativeList<ComputeBufferIndex, AccentBounds> Shadow;
    }
}
