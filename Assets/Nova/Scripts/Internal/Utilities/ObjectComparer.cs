// Copyright (c) Supernova Technologies LLC
using System.Collections.Generic;

namespace Nova.Internal.Utilities
{
    internal struct ObjectComparer<T> : IEqualityComparer<T> where T : UnityEngine.Object
    {
        public static readonly ObjectComparer<T> Shared = default;

        public bool Equals(T x, T y)
        {
            return x == y;
        }

        public int GetHashCode(T obj)
        {
            return obj == null ? 0 : obj.GetHashCode();
        }
    }
}
