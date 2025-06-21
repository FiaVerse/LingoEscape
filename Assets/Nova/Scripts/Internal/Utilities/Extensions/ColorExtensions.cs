// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class ColorExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Alpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 ToFloat4(ref this Color color)
        {
            return new float4(color.r, color.g, color.b, color.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Brightness(this Color color, float brightness)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            return Color.HSVToRGB(h, s, brightness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Saturation(this Color color, float saturation)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            return Color.HSVToRGB(h, saturation, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTransparent(this Color val)
        {
            return val.a < 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOpaque(this Color val)
        {
            return val.a == 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PassType ToPassType(this Color val)
        {
            return val.IsOpaque() ? PassType.Opaque : PassType.Transparent;
        }
    }
}
