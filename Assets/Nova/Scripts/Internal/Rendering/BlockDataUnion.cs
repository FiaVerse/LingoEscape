// Copyright (c) Supernova Technologies LLC
using System.Runtime.InteropServices;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// A union of the block data
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct BlockDataUnion
    {
        [FieldOffset(0)]
        public UIBlock2DData Block2D;
        [FieldOffset(0)]
        public UIBlock3DData Block3D;
    }
}

