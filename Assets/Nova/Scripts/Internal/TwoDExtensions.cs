// Copyright (c) Supernova Technologies LLC
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Extensions
{
    internal static partial class Util
    {
        public static bool Any(TwoD<bool> v)
        {
            return v.X || v.Y;
        }
    }

    internal static class TwoDExtensions
    {
        public static Unity.Mathematics.bool2 ToBool2(this TwoD<bool> twoD)
        {
            return new Unity.Mathematics.bool2(twoD.X, twoD.Y);
        }
    }
}
