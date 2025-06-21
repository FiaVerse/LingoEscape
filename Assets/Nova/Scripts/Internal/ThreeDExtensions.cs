// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Extensions
{
    internal static partial class Util
    {
        public static bool All(ThreeD<bool> v)
        {
            return v.X && v.Y && v.Z;
        }

        public static bool Any(ThreeD<bool> v)
        {
            return v.X || v.Y || v.Z;
        }

        public static bool None(ThreeD<bool> v)
        {
            return !(v.X || v.Y || v.Z);
        }

        public static ThreeD<bool> Or(ThreeD<bool> a, ThreeD<bool> b)
        {
            return new ThreeD<bool>(a.X || b.X, a.Y || b.Y, a.Z || b.Z);
        }

        public static Vector3 Mask(ThreeD<bool> v)
        {
            return new Vector3(v.X ? 1 : 0, v.Y ? 1 : 0, v.Z ? 1 : 0);
        }
    }

    internal static class ThreeDExtensions
    {
        public static bool Any<T>(this ThreeD<T> threeD, ThreeD<T> other) where T : unmanaged
        {
            return Util.Any(threeD == other);
        }

        public static bool All<T>(this ThreeD<T> threeD, ThreeD<T> other) where T : unmanaged
        {
            return Util.All(threeD == other);
        }
    }
}
