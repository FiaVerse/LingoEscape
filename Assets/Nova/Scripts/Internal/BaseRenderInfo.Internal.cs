// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Internal
{
    /// <summary>
    /// NOTE: Be careful when modifying these. These are used as masks, <see cref="RenderNodeTypeExtensions"/>
    /// </summary>
    [Flags]
    internal enum BlockType : byte
    {
        Empty = 0,

        // 2D
        UIBlock2D = 1,
        Text = 2,

        // 3D
        UIBlock3D = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BaseRenderInfo
    {
        public int GameObjectLayer;
        public short ZIndex;
        public BlockType BlockType;
        public bool Visible;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BaseRenderInfo Default(BlockType renderNodeType) => new BaseRenderInfo()
        {
            BlockType = renderNodeType,
            Visible = true,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ref BaseRenderInfo other)
        {
            return
                GameObjectLayer == other.GameObjectLayer &&
                ZIndex == other.ZIndex &&
                BlockType == other.BlockType &&
                Visible == other.Visible;

        }
    }
}
