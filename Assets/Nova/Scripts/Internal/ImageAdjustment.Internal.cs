// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Nova.Internal
{
    internal enum ImageScaleMode
    {
        Manual = 0,
        Fit = 1,
        Envelope = 2,
        Sliced = 3,
        Tiled = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageAdjustment : System.IEquatable<ImageAdjustment>
    {
        public float2 CenterUV;
        public float2 UVScale;
        public float PixelsPerUnitMultiplier;
        public ImageScaleMode ScaleMode;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ImageAdjustment other)
        {
            return
                CenterUV.Equals(other.CenterUV) &&
                UVScale.Equals(other.UVScale) &&
                ScaleMode == other.ScaleMode &&
                PixelsPerUnitMultiplier == other.PixelsPerUnitMultiplier;
        }
    }
}