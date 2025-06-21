// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Internal.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ShaderIndex
    {
        /// <summary>
        /// We have to store the index as a float, because on Mali GPUs passing a uint doesn't work
        /// </summary>
        private float index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ShaderIndex(uint index) => new ShaderIndex()
        {
            index = index
        };
    }
}
