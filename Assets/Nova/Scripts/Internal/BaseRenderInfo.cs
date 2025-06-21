// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    [Flags]
    internal enum BlockType : byte
    {
        Empty = Internal.BlockType.Empty,

        // 2D
        UIBlock2D = Internal.BlockType.UIBlock2D,
        Text = Internal.BlockType.Text,

        // 3D
        UIBlock3D = Internal.BlockType.UIBlock3D,
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct BaseRenderInfo
    {
        [NonSerialized]
        public int GameObjectLayer;
        [SerializeField]
        public short ZIndex;
        [SerializeField]
        [HideInInspector]
        public BlockType BlockType;
        [SerializeField]
        public bool Visible;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BaseRenderInfo Default(BlockType blockType) => new BaseRenderInfo()
        {
            BlockType = blockType,
            Visible = true,
        };
    }
}
