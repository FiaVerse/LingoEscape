// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    internal static class BlockDataValidation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Validate(ref this UIBlock2DData data)
        {
            data.CornerRadius.ClampPositive100();
            data.Gradient.Radius.ClampPositive();
            data.Border.Width.ClampPositive();
            data.Shadow.Blur.ClampPositive();
            data.RadialFill.Rotation = math.clamp(data.RadialFill.Rotation, -360f, 360f);
            data.RadialFill.FillAngle = math.clamp(data.RadialFill.FillAngle, -360f, 360f);
            data.Image.Adjustment.PixelsPerUnitMultiplier = math.max(data.Image.Adjustment.PixelsPerUnitMultiplier, .01f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Validate(ref this UIBlock3DData data)
        {
            data.CornerRadius.ClampPositive100();
            data.EdgeRadius.ClampPositive100();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void Validate(ref this Surface data)
        {
            data.param1 = math.saturate(data.param1);
            data.param2 = math.saturate(data.param2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampPositive100(ref this Length length)
        {
            if (length.Type == LengthType.Value)
            {
                length.Raw = math.max(length.Raw, 0);
            }
            else
            {
                length.Raw = math.clamp(length.Raw, 0, 1f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampPositive(ref this Length length)
        {
            length.Raw = math.max(length.Raw, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampPositive(ref this Length2 length)
        {
            length.Raw = math.max(length.Raw, 0);
        }
    }
}

