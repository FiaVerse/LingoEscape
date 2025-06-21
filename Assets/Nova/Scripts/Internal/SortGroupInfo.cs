// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct SortGroupInfo
    {
        [SerializeField]
        public int SortingOrder;
        [SerializeField]
        public int RenderQueue;
        [SerializeField]
        public bool RenderOverOpaqueGeometry;

        internal static readonly SortGroupInfo Default = new SortGroupInfo()
        {
            SortingOrder = 0,
            RenderQueue = Constants.DefaultTransparentRenderQueue,
            RenderOverOpaqueGeometry = false,
        };
    }
}
