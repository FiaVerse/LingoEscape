// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal partial struct SubQuadShaderDataJob : INovaJobParallelFor
    {
        private interface IBoundsConverter
        {
            float CornerRadius { get; }
            float MaxCoverageRemainder { get; }

            RotationSpaceBounds MaxCoverageBounds { get; }
            ref RotationSpaceBounds Bounds { get; }

            /// <summary>
            /// Index => R, T, L, B
            /// </summary>
            /// <param name="cornerIndex"></param>
            /// <returns></returns>
            RotationSpaceBounds GetEdgeBounds(int edgeIndex);


            /// <summary>
            /// Index => BL, TL, TR, BR
            /// </summary>
            /// <param name="cornerIndex"></param>
            /// <returns></returns>
            RotationSpaceBounds GetCornerBounds(int cornerIndex);
        }

        private struct BodyOnly : IBoundsConverter
        {
            private QuadBoundsDescriptor descriptor;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BodyOnly(ref QuadBoundsDescriptor quadBoundsDescriptor)
            {

                descriptor = quadBoundsDescriptor;
            }

            public float CornerRadius
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => descriptor.CornerRadius;
            }

            public float MaxCoverageRemainder
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Math.OneMinusSin45 * descriptor.CornerRadius;
            }

            public RotationSpaceBounds MaxCoverageBounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float halfPoint = MaxCoverageRemainder;
                    return new RotationSpaceBounds()
                    {
                        BL = descriptor.Bounds.BL + halfPoint,
                        TR = descriptor.Bounds.TR - halfPoint
                    };
                }
            }

            public unsafe ref RotationSpaceBounds Bounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    fixed (BodyOnly* ptr = &this)
                    {
                        return ref ptr->descriptor.Bounds;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetCornerBounds(int cornerIndex)
            {
                return SubQuadShaderDataJob.GetCornerBounds(cornerIndex, ref descriptor.Bounds, descriptor.CornerRadius);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetEdgeBounds(int edgeIndex)
            {
                return SubQuadShaderDataJob.GetEdgeBounds(edgeIndex, ref descriptor.Bounds, descriptor.CornerRadius);
            }
        }

        private struct BodyAndBorder : IBoundsConverter
        {
            private QuadBoundsDescriptor descriptor;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BodyAndBorder(ref QuadBoundsDescriptor quadBoundsDescriptor)
            {
                descriptor = quadBoundsDescriptor;
            }

            public float CornerRadius
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => descriptor.Border.OuterRadius;
            }

            public float MaxCoverageRemainder
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Math.OneMinusSin45 * descriptor.Border.OuterRadius;
            }

            public RotationSpaceBounds MaxCoverageBounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float halfPoint = MaxCoverageRemainder;
                    return new RotationSpaceBounds()
                    {
                        BL = descriptor.Border.Bounds.BL + halfPoint,
                        TR = descriptor.Border.Bounds.TR - halfPoint
                    };
                }
            }

            public unsafe ref RotationSpaceBounds Bounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    fixed (BodyAndBorder* ptr = &this)
                    {
                        return ref ptr->descriptor.Border.Bounds;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetCornerBounds(int cornerIndex)
            {
                return SubQuadShaderDataJob.GetCornerBounds(cornerIndex, ref descriptor.Border.Bounds, descriptor.Border.OuterRadius);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetEdgeBounds(int edgeIndex)
            {
                return SubQuadShaderDataJob.GetEdgeBounds(edgeIndex, ref descriptor.Border.Bounds, descriptor.Border.OuterRadius);
            }
        }

        private struct BorderOnly : IBoundsConverter
        {
            private QuadBoundsDescriptor descriptor;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BorderOnly(ref QuadBoundsDescriptor quadBoundsDescriptor)
            {
                descriptor = quadBoundsDescriptor;
            }

            public float CornerRadius
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => descriptor.Border.OuterRadius;
            }

            public float MaxCoverageRemainder
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Math.OneMinusSin45 * descriptor.Border.OuterRadius;
            }

            public RotationSpaceBounds MaxCoverageBounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float halfPoint = MaxCoverageRemainder;
                    return new RotationSpaceBounds()
                    {
                        BL = descriptor.Border.Bounds.BL + halfPoint,
                        TR = descriptor.Border.Bounds.TR - halfPoint
                    };
                }
            }

            public unsafe ref RotationSpaceBounds Bounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    fixed (BorderOnly* ptr = &this)
                    {
                        return ref ptr->descriptor.Border.Bounds;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetCornerBounds(int cornerIndex)
            {
                return SubQuadShaderDataJob.GetCornerBounds(cornerIndex, ref descriptor.Border.Bounds, descriptor.Border.OuterRadius);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetEdgeBounds(int edgeIndex)
            {
                RotationSpaceBounds cornerCenters = new RotationSpaceBounds()
                {
                    BL = descriptor.Border.Bounds.BL + descriptor.Border.OuterRadius,
                    TR = descriptor.Border.Bounds.TR - descriptor.Border.OuterRadius
                };

                switch (edgeIndex)
                {
                    case 0:
                        // Right
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(descriptor.Border.Bounds.TR.x - descriptor.Border.BorderWidth, cornerCenters.BL.y),
                            TR = new float2(descriptor.Border.Bounds.TR.x, cornerCenters.TR.y)
                        };
                    case 1:
                        // Top
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(cornerCenters.BL.x, descriptor.Border.Bounds.TR.y - descriptor.Border.BorderWidth),
                            TR = new float2(cornerCenters.TR.x, descriptor.Border.Bounds.TR.y)
                        };
                    case 2:
                        // Left
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(descriptor.Border.Bounds.BL.x, cornerCenters.BL.y),
                            TR = new float2(descriptor.Border.Bounds.BL.x + descriptor.Border.BorderWidth, cornerCenters.TR.y)
                        };
                    case 3:
                        // Bottom
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(cornerCenters.BL.x, descriptor.Border.Bounds.BL.y),
                            TR = new float2(cornerCenters.TR.x, descriptor.Border.Bounds.BL.y + descriptor.Border.BorderWidth)
                        };
                    default:
                        Debug.LogError("Invalid edge index");
                        return default;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RotationSpaceBounds GetEdgeBounds(int edgeIndex, ref RotationSpaceBounds bounds, float radius)
        {
            if (Math.ApproximatelyZero(radius))
            {
                // If there is no corner rounding, then we return one "edge" which is really just the entire
                // bounds
                return bounds;
            }

            switch (edgeIndex)
            {
                case 0:
                    // Right
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.TR.x - radius, bounds.BL.y + radius),
                        TR = new float2(bounds.TR.x, bounds.TR.y - radius),
                    };
                case 1:
                    // Top
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x + radius, bounds.TR.y - radius),
                        TR = new float2(bounds.TR.x - radius, bounds.TR.y),
                    };
                case 2:
                    // Left
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x, bounds.BL.y + radius),
                        TR = new float2(bounds.BL.x + radius, bounds.TR.y - radius),
                    };
                case 3:
                    // Bottom
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x + radius, bounds.BL.y),
                        TR = new float2(bounds.TR.x - radius, bounds.BL.y + radius),
                    };
                default:
                    Debug.LogError("Invalid edge index");
                    return default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RotationSpaceBounds GetCornerBounds(int cornerIndex, ref RotationSpaceBounds bounds, float radius)
        {
            switch (cornerIndex)
            {
                case 0:
                    // BL
                    return new RotationSpaceBounds()
                    {
                        BL = bounds.BL,
                        TR = bounds.BL + radius
                    };
                case 1:
                    // TL
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x, bounds.TR.y - radius),
                        TR = new float2(bounds.BL.x + radius, bounds.TR.y),
                    };
                case 2:
                    return new RotationSpaceBounds()
                    {
                        BL = bounds.TR - radius,
                        TR = bounds.TR
                    };
                case 3:
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.TR.x - radius, bounds.BL.y),
                        TR = new float2(bounds.TR.x, bounds.BL.y + radius),
                    };
                default:
                    Debug.LogError("Invalid corner index");
                    return default;
            }
        }
    }
}
