// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Rendering
{
    internal struct CoplanarSet
    {
        /// <summary>
        /// The root of the CoplanarSet, which should be used to access the matrices
        /// </summary>
        public DataStoreID RootID;
        public AABB CoplanarSpaceRenderBounds;
        public int TransparentDrawCallCount;

        public CoplanarSet(DataStoreID root)
        {
            RootID = root;
            CoplanarSpaceRenderBounds = default;
            TransparentDrawCallCount = 0;
        }
    }

    
    internal struct CoplanarSetIdentifier : IEquatable<CoplanarSetIdentifier>
    {
        public DataStoreID BatchRootID;
        public CoplanarSetID CoplanarSetID;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CoplanarSetIdentifier other)
        {
            return BatchRootID == other.BatchRootID && CoplanarSetID == other.CoplanarSetID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + BatchRootID.GetHashCode();
            hash = (hash * 7) + CoplanarSetID.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return $"{BatchRootID}: {CoplanarSetID}";
        }
    }

    internal static class CoplanarSetExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetSet<T>(this ref NovaHashMap<DataStoreID, NovaList<CoplanarSetID, T>> map, ref CoplanarSetIdentifier set) where T : unmanaged
        {
            return ref map[set.BatchRootID].ElementAt(set.CoplanarSetID);
        }
    }
}