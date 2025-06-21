// Copyright (c) Supernova Technologies LLC
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    [Obfuscation]
    [StructLayout(LayoutKind.Sequential)]
    internal struct CalculatedLayout
    {
        public Length3.Calculated Size;
        public Length3.Calculated Position;
        public LengthBounds.Calculated Padding;
        public LengthBounds.Calculated Margin;

        public Vector3 PaddedSize => Size.Value - Padding.Size;
    }
}
