// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova.Internal
{
    internal enum ShadowDirection
    {
        Out = 0,
        In = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Shadow : System.IEquatable<Shadow>
    {
        public Color Color;
        public Length Width;
        public Length Blur;
        public Length2 Offset;
        public bool Enabled;
        public ShadowDirection Direction;

        public bool HasOuterShadow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Enabled && Direction == ShadowDirection.Out;
        }

        public bool HasInnerShadow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Enabled && Direction == ShadowDirection.In;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Shadow other)
        {
            return
                Color.Equals(other.Color) &&
                Width == other.Width &&
                Blur == other.Blur &&
                Offset.Equals(ref other.Offset) &&
                Enabled == other.Enabled &&
                Direction == other.Direction;
        }
    }
}