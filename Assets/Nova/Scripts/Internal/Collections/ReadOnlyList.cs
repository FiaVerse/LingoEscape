// Copyright (c) Supernova Technologies LLC
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Collections
{
    /// <summary>
    /// A ref struct wrapper around a list. The struct cannot be stored or written to.
    /// If we constrained T to struct types, then it would *really* be readonly...
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal readonly ref struct ReadOnlyList<T>
    {
        public static ReadOnlyList<T> Empty => default;
        private readonly IList<T> source;

        public readonly IList<T> Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => source;
        }

        public readonly int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => source == null ? 0 : source.Count;
        }

        public readonly T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => source[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int IndexOf(T value)
        {
            return source.IndexOf(value);
        }

        public ReadOnlyList(IList<T> source)
        {
            this.source = source;
        }

        public readonly void CopyTo(T[] dst, int dstIndex = 0)
        {
            source.CopyTo(dst, dstIndex);
        }

        public readonly void CopyTo(List<T> dst, int dstIndex = 0)
        {
            dst.InsertRange(dstIndex, source);
        }
    }
}
