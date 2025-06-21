// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RadialGradient : IEquatable<RadialGradient>
    {
        public Color Color;
        public Length2 Center;
        public Length2 Radius;
        /// <summary>
        /// In degrees
        /// </summary>
        public float Rotation;
        public bool Enabled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RadialGradient other)
        {
            return
                Color.Equals(other.Color) &&
                Center.Equals(ref other.Center) &&
                Radius.Equals(ref other.Radius) &&
                Rotation == other.Rotation &&
                Enabled == other.Enabled;
        }
    }
}
