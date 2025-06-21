// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ClipMaskInfo : IEquatable<ClipMaskInfo>
    {
        public Color Color;
        public bool Clip;
        public bool HasMask;

        public bool IsClipMask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Clip && HasMask;
        }

        internal static readonly ClipMaskInfo Default = new ClipMaskInfo()
        {
            Color = Color.white,
            Clip = true,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ClipMaskInfo other)
        {
            return Color.Equals(other.Color) && Clip == other.Clip;
        }
    }
}

