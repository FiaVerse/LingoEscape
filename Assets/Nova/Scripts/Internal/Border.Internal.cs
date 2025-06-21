// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova.Internal
{
    internal enum BorderDirection
    {
        Out = 0,
        Center = 1,
        In = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Border : System.IEquatable<Border>
    {
        public Color Color;
        public Length Width;
        public bool Enabled;
        public BorderDirection Direction;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Border other)
        {
            return
                Color.Equals(other.Color) &&
                Width == other.Width &&
                Enabled == other.Enabled &&
                Direction == other.Direction;
        }
    }
}
