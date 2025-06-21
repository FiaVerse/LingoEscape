// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ShaderColor
    {
        private float4 val;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(ref Color col)
        {
            if (SystemSettings.ColorSpace == ColorSpace.Linear)
            {
                Color linearized = col.linear;
                Pack(ref linearized);
            }
            else
            {
                Pack(ref col);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(ref Color32 col, bool linearize)
        {
            if (linearize && SystemSettings.ColorSpace == ColorSpace.Linear)
            {
                Color linearized = ((Color)col).linear;
                Pack(ref linearized);
            }
            else
            {
                Color c = (Color)col;
                Pack(ref c);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pack(ref Color color)
        {
            val = math.saturate(color.ToFloat4());

            // We can't pack into a uint because of mali gpus :(
        }

        public static readonly ShaderColor Transparent = default;
    }
}

