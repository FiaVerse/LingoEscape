// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal static partial class CameraSorting
    {
        /// <summary>
        /// The final bounds and config for a draw call
        /// </summary>
        internal struct ProcessedDrawCall
        {
            /// <summary>
            /// The bounds after adjustments have been made to guarantee correct render order
            /// </summary>
            public Bounds AdjustedBounds;
            public bool ViewingFromBehind;
            public bool SkipRendering;

            public static readonly ProcessedDrawCall DontRender = new ProcessedDrawCall()
            {
                SkipRendering = true,
            };
        }

        /// <summary>
        /// The distances used to sort coplanar sets and determine render order.
        /// NOTE: The distance calculation dependes on <see cref="CameraConfig"/> and 
        /// whether or not it's orthographic.
        /// </summary>
        internal struct CoplanarSetDistances
        {
            public float3 CenterPoint;
            public float MinDistance;
            public float MaxDistance;
            public float CenterDistance;

            public bool IsValid
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => MinDistance >= 0;
            }

            public static readonly CoplanarSetDistances Invalid = new CoplanarSetDistances()
            {
                MinDistance = -1,
                MaxDistance = -1,
            };
        }

        /// <summary>
        /// The center and offset direction of a coplanar set after adjusting
        /// for render order dependencies
        /// </summary>
        internal struct ProcessedCoplanarSet
        {
            /// <summary>
            /// The center of bounds after adjusting for render order dependencies
            /// </summary>
            public float3 AdjustedWorldSpaceCenter;
            /// <summary>
            /// The direction in world space to use for offsetting for multiple draw calls in the coplanar set
            /// </summary>
            public float3 DrawCallOffsetDirection;
        }

        /// <summary>
        /// The config for a coplanar set
        /// </summary>
        internal struct CoplanarSetLocation
        {
            public CoplanarSetDistances Distances;
            /// <summary>
            /// The render bounds after being adjusted to render on top of
            /// other coplanar sets
            /// </summary>
            public ProcessedCoplanarSet ProcessedBounds;
            public Quadrilateral3D CameraSpaceBounds;
            /// <summary>
            /// The AABB of the coplanar set in NDC space, which can be used to prune
            /// if two coplanar sets overlap
            /// </summary>
            public AABB NDCBounds;
            public float3 WorldSpaceSize;
            public bool ViewingFromBehind;
        }
    }
}
