// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Internal.Hierarchy
{
    /// <summary>
    /// The info tracked and updated in-line per hierarchy element
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    
    internal struct HierarchyElement : IDisposable
    {
        public DataStoreID ID;
        public DataStoreID ParentID;

        public NovaList<DataStoreIndex> Children;

        public int ChildCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Children.Length;
            }
        }

        public void Dispose()
        {
            Children.Dispose();
        }

        public static HierarchyElement Create(DataStoreID id)
        {
            return new HierarchyElement()
            {
                ID = id,
                ParentID = DataStoreID.Invalid,
            };
        }
    }
}
