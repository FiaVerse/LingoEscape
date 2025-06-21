// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System;
using System.Runtime.InteropServices;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SortGroupInfo : IEquatable<SortGroupInfo>
    {
        public int SortingOrder;
        public int RenderQueue;
        public bool RenderOverOpaqueGeometry;

        public bool Equals(SortGroupInfo other)
        {
            return 
                SortingOrder == other.SortingOrder &&
                RenderOverOpaqueGeometry == other.RenderOverOpaqueGeometry && 
                RenderQueue == other.RenderQueue;
        }

        internal static readonly SortGroupInfo Default = new SortGroupInfo()
        {
            SortingOrder = 0,
            RenderQueue = Constants.DefaultTransparentRenderQueue,
            RenderOverOpaqueGeometry = false,
        };
    }
}
