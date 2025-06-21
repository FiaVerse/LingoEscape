// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class TextBlockExtensions
    {
        /// <summary>
        /// Tries to log a helpful warning in situations where the text is non-empty 
        /// but the rendered mesh is empty
        /// </summary>
        /// <param name="textBlock"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogHelpfulWarnings(this TextBlock textBlock)
        {
            if (textBlock.TMP.overflowMode != TextOverflowModes.Ellipsis)
            {
                return;
            }

            bool2 shrinkMask = textBlock.RawTextShrinkMask;
            if (!math.any(shrinkMask))
            {
                return;
            }

            float2 marginSize = TextMargin.GetMarginSize(textBlock.CalculatedSize.XY.Value, textBlock.SizeMinMax.ToInternal().Max.xy, shrinkMask);
            if (!math.any(Math.ApproximatelyZero2(ref marginSize)))
            {
                return;
            }

            Debug.LogWarning("Ellipsis overflow mode and shrinking to text size is causing a TextBlock to not render. Provide a MaxSize in the TextBlock's layout on the shrinking axes, or disable shrinking to prevent this situation.");
        }
    }
}
