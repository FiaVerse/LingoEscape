// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Specifies how the Nova Engine should store and attempt to batch a given texture.
    /// </summary>
    /// <seealso cref="UIBlock2D.ImagePackMode"/>
    /// <seealso cref="NovaSettings.PackedImagesEnabled"/>
    public enum ImagePackMode
    {
        /// <summary>
        /// Store the texture by itself. Useful if the underlying texture's content changes regularly (such as for a video player) or
        /// if <see cref="Packed"/> causes visual artifacts. An unpacked texture will be rendered in its own draw call.
        /// </summary>
        Unpacked = Internal.ImagePackMode.Unpacked,
        /// <summary>
        /// Store the texture with other compatible textures (compatible being determined by texture format, mips, dimensions, etc.).<br/>
        /// Packed textures can be batched, potentially leading to <i>many</i> fewer draw calls than if all textures were <see cref="Unpacked"/>.
        /// </summary>
        Packed = Internal.ImagePackMode.Packed,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageID : IEquatable<ImageID>
    {
        private int val;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => val != -1;
        }

        public static readonly ImageID Invalid = new ImageID()
        {
            val = -1,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ImageID(Internal.ImageID imageID) => new ImageID()
        {
            val = imageID,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Internal.ImageID(ImageID imageID) => imageID.val;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ImageID other) => val == other.val;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageData
    {
        [SerializeField]
        public ImageAdjustment Adjustment;
        [SerializeField]
        public ImagePackMode Mode;
        [NonSerialized]
        public ImageID ImageID;

        internal static readonly ImageData Default = new ImageData()
        {
            Adjustment = ImageAdjustment.Default,
            ImageID = ImageID.Invalid,
            Mode = ImagePackMode.Packed,
        };
    }
}