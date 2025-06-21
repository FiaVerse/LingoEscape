// Copyright (c) Supernova Technologies LLC
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Rendering
{
    [System.Flags]
    internal enum QuadDescriptorFlags
    {
        BodyOccludes = 1,
        BodyRenders = 2 * BodyOccludes,
        HasBorder = 2 * BodyRenders,
        BorderOccludes = 2 * HasBorder,
    }

    /// <summary>
    /// Descriptor of the bounds of a quad in a rotation set
    /// </summary>
    
    internal struct QuadBoundsDescriptor : System.IComparable<QuadBoundsDescriptor>
    {
        public struct BorderData
        {
            public RotationSpaceBounds Bounds;
            public float OuterRadius;
            public float BorderWidth;
        }

        public RotationSpaceBounds Bounds;
        public float CornerRadius;
        public RenderIndex RenderIndex;
        public VisualElementIndex VisualElementIndex;

        public BorderData Border;
        public short ZLayer;
        public QuadDescriptorFlags Flags;

        public unsafe ref RotationSpaceBounds MaxRenderBounds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (QuadBoundsDescriptor* ptr = &this)
                {
                    return ref HasBorder ? ref ptr->Border.Bounds : ref ptr->Bounds;
                }
            }
        }

        public unsafe ref RotationSpaceBounds MaxOcclusionBounds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (QuadBoundsDescriptor* ptr = &this)
                {
                    return ref BorderOccludes ? ref ptr->Border.Bounds : ref ptr->Bounds;
                }
            }
        }

        public bool HasBorder
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & QuadDescriptorFlags.HasBorder) != 0;
        }

        public bool HasAnyOcclusion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & (QuadDescriptorFlags.BodyOccludes | QuadDescriptorFlags.BorderOccludes)) != 0;
        }

        public bool BodyRenders
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & QuadDescriptorFlags.BodyRenders) != 0;
        }

        public bool BodyOccludes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & QuadDescriptorFlags.BodyOccludes) != 0;
        }

        public bool BorderOccludes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & QuadDescriptorFlags.BorderOccludes) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RendersUnder(ref QuadBoundsDescriptor other)
        {
            return ZLayer != other.ZLayer ? ZLayer < other.ZLayer : VisualElementIndex < other.VisualElementIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(QuadBoundsDescriptor other)
        {
            return Bounds.CompareTo(ref other.Bounds);
        }
    }
}


