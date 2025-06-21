// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UIBlock3DData : System.IEquatable<UIBlock3DData>
    {
        public Color Color;
        public Length CornerRadius;
        public Length EdgeRadius;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UIBlock3DData other)
        {
            return
                Color.Equals(other.Color) &&
                CornerRadius == other.CornerRadius &&
                EdgeRadius == other.EdgeRadius;
        }
    }
}