// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Nova.Internal.Rendering
{
    internal struct SpriteBorder : IEquatable<SpriteBorder>
    {
        // L, B, R, T
        public float4 vals;

        public float Left
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => vals.x;
        }

        public float Bottom
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => vals.y;
        }

        public float Right
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => vals.z;
        }

        public float Top
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => vals.w;
        }

        public float2 BL
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => vals.xy;
        }

        public float2 TL
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => vals.xw;
        }

        public float2 TR
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => vals.zw;
        }

        public float2 TotalSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new float2(Left + Right, Top + Bottom);
        }

        public float TotalHeight
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Top + Bottom;
        }

        public float TotalWidth
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Left + Right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SpriteBorder other)
        {
            return vals.Equals(other.vals);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SpriteBorder(float4 vals) => new SpriteBorder()
        {
            vals = vals
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SpriteBorder(Vector4 vals) => new SpriteBorder()
        {
            vals = vals
        };
    }

    /// <summary>
    /// Describes an image, which is a Nova concept. E.g., a Sprite is an image, and multiple
    /// sprites might map back to the same texture even though they are different "Images"
    /// </summary>
    internal struct ImageDescriptor : IEquatable<ImageDescriptor>
    {
        public Rect Rect;
        public TextureID TextureID;
        public ImagePackMode Mode;
        /// <summary>
        /// I hate that we need this, but in order to handle when a sprite
        /// being edited in the sprite editor, we need to track this
        /// </summary>
        public int SpriteID;
        public SpriteBorder Border;

        /// <summary>
        /// Width / height
        /// </summary>
        public float AspectRatio
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Rect.width / (float)Rect.height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImageDescriptor(ImagePackMode imagePackMode)
        {
            Mode = imagePackMode;
            TextureID = TextureID.Invalid;
            SpriteID = 0;
            Rect = default;
            Border = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ImageDescriptor other)
        {
            return
                Rect.Equals(other.Rect) &&
                TextureID.Equals(other.TextureID) &&
                Mode == other.Mode &&
                SpriteID == other.SpriteID &&
                Border.Equals(other.Border);
        }

        public static ImageDescriptor Invalid = new ImageDescriptor()
        {
            TextureID = TextureID.Invalid,
            SpriteID = 0,
        };
    }

    internal struct TextureDescriptor : IEquatable<TextureDescriptor>
    {
        public int2 Dimensions;
        public int MipCount;
        public GraphicsFormat Format;
        public bool IsTexture2D;
        public bool HasAlphaChannel;

        public bool HasMips
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MipCount > 1;
        }

        public TextureDescriptor(Texture texture)
        {
            Dimensions = new int2(texture.width, texture.height);
            Format = texture.graphicsFormat;
            MipCount = texture.mipmapCount;
            IsTexture2D = texture is Texture2D;
            HasAlphaChannel = GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TextureDescriptor other)
        {
            return
                Dimensions.Equals(other.Dimensions) &&
                MipCount == other.MipCount &&
                Format == other.Format &&
                IsTexture2D == other.IsTexture2D &&
                HasAlphaChannel == other.HasAlphaChannel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Dimensions.GetHashCode();
            hash = (hash * 7) + MipCount.GetHashCode();
            hash = (hash * 7) + ((int)Format).GetHashCode();
            hash = (hash * 7) + IsTexture2D.GetHashCode();
            return hash;
        }
    }

    internal struct GraphicsFormatDescriptor
    {
        public int BlockSize;
        public bool IsSupportedStatic;
        public bool IsLinear;
        public TextureFormat TextureFormat;

        public bool IsCompressed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BlockSize != 0;
        }
    }
}