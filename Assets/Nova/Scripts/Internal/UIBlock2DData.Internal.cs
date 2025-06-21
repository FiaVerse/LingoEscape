// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UIBlock2DData : IEquatable<UIBlock2DData>
    {
        public Color Color;
        public Length CornerRadius;
        public RadialFill RadialFill;
        public RadialGradient Gradient;
        public Border Border;
        public Shadow Shadow;
        public ImageData Image;
        public bool SoftenEdges;
        public bool FillEnabled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UIBlock2DData other)
        {
            return
                Color.Equals(other.Color) &&
                CornerRadius == other.CornerRadius &&
                RadialFill.Equals(other.RadialFill) &&
                Gradient.Equals(other.Gradient) &&
                Border.Equals(other.Border) &&
                Shadow.Equals(other.Shadow) &&
                Image.Equals(other.Image) &&
                SoftenEdges == other.SoftenEdges &&
                FillEnabled == other.FillEnabled;
        }
    }
}
