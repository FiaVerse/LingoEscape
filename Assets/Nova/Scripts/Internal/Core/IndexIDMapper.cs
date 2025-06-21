// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)

namespace Nova.Internal.Core
{
    // NOTE: This purposefully is inconvenient to convert to an int.
     // These attributes are infuriating. That's why this is a string.
    [Serializable]
    internal struct DataStoreID : IEquatable<DataStoreID>
    {
        public static readonly DataStoreID Invalid = default;
        public const int Size = sizeof(ulong);
        private static ulong nextID = 1;

        [SerializeField]
        private ulong val;
        public readonly bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return val != default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(DataStoreID other)
        {
            return other == this;
        }

        public static DataStoreID Create()
        {
            return new DataStoreID(nextID++);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DataStoreID lhs, DataStoreID rhs)
        {
            return lhs.val == rhs.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(DataStoreID lhs, DataStoreID rhs)
        {
            return lhs.val != rhs.val;
        }

        private DataStoreID(ulong id)
        {
            val = id;
        }

        public override int GetHashCode()
        {
            return val.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("0x{0:X8}", val);
        }
    }

    /// <summary>
    /// An index into a batch group
    /// </summary>
    internal struct BatchGroupIndex : IEquatable<int>, IComparable<int>, IIndex<BatchGroupIndex>
    {
        public static readonly BatchGroupIndex Invalid = default;

        private int index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index - 1;
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return index != Invalid.index;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BatchGroupIndex(int index)
        {
            return new BatchGroupIndex()
            {
                index = index + 1
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(BatchGroupIndex bgIndex)
        {
            int index = bgIndex.index;
            return index - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator uint(BatchGroupIndex bgIndex)
        {
            return (uint)(bgIndex.index - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(BatchGroupIndex other)
        {
            return index.CompareTo(other.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(BatchGroupIndex other)
        {
            return index.Equals(other.index);
        }
    }

    /// <summary>
    /// An index into a data store
    /// </summary>
     // These attributes are infuriating. That's why this is a string.
    internal struct DataStoreIndex : IEquatable<int>, IEquatable<DataStoreIndex>, IComparable<int>, IIndex<DataStoreIndex>
    {
        public static readonly DataStoreIndex Invalid = default;

        private int index;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int invalid = Invalid.index;
                return index != invalid;
            }
        }

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index - 1;
        }

        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DataStoreIndex(int index)
        {
            return new DataStoreIndex()
            {
                index = index + 1
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(DataStoreIndex dsIndex)
        {
            int index = dsIndex.index;
            return index - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator uint(DataStoreIndex dsIndex)
        {
            int index = dsIndex.index;
            return (uint)(index - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        public bool Equals(DataStoreIndex other)
        {
            return this.index == other.index;
        }

        public int CompareTo(DataStoreIndex other)
        {
            return index.CompareTo(other.index);
        }
    }

    internal class IndexIDMapper : IInitializable
    {
        private List<DataStoreID> managedIndexToID = new List<DataStoreID>(Constants.AllElementsInitialCapacity);
        private Dictionary<DataStoreID, DataStoreIndex> managedIDToIndex = new Dictionary<DataStoreID, DataStoreIndex>();

        public void Init()
        {
            managedIDToIndex.Clear();
            managedIndexToID.Clear();
        }

        public bool Contains(DataStoreID id)
        {
            return managedIDToIndex.ContainsKey(id);
        }

        public void Add(DataStoreID id, DataStoreIndex index)
        {
            managedIDToIndex.Add(id, index);
            managedIndexToID.Add(id);
        }

        public void RemoveAtSwapBack(DataStoreIndex index)
        {
            DataStoreID idToRemove = ToID(index);
            DataStoreID idToMoveBack = ToID(managedIDToIndex.Count - 1);

            managedIDToIndex[idToMoveBack] = index;
            managedIDToIndex.Remove(idToRemove);
            managedIndexToID.RemoveAtSwapBack(index);
        }

        public bool TryGetIndex(DataStoreID id, out DataStoreIndex index)
        {
            return managedIDToIndex.TryGetValue(id, out index);
        }

        public DataStoreIndex ToIndexUnsafe(DataStoreID id)
        {
            return managedIDToIndex[id];
        }

        public DataStoreID ToID(DataStoreIndex index)
        {
            return managedIndexToID[index];
        }

        public void Dispose()
        {
            managedIDToIndex.Clear();
        }
    }

    /// <summary>
    /// Maps <see cref="DataStoreID"/> to a indices
    /// </summary>
    internal struct NativeIndexIDMapper : IDisposable
    {
        private NativeList<DataStoreID> IndexToID;
        private NovaHashMap<DataStoreID, DataStoreIndex> IDToIndex;

        public bool Contains(DataStoreID id)
        {
            return IDToIndex.ContainsKey(id);
        }

        public void Add(DataStoreID id, DataStoreIndex index)
        {
            IDToIndex.Add(id, index);
            IndexToID.Add(id);
        }

        public void RemoveAtSwapBack(DataStoreIndex index)
        {
            DataStoreID idToRemove = ToID(index);
            DataStoreID idToMoveBack = ToID(IndexToID.Length - 1);

            IDToIndex[idToMoveBack] = index;
            IndexToID.RemoveAtSwapBack(index);
            IDToIndex.Remove(idToRemove);
        }

        public bool TryGetIndex(DataStoreID id, out DataStoreIndex index)
        {
            if (!IDToIndex.TryGetValue(id, out index))
            {
                index = DataStoreIndex.Invalid;
                return false;
            }

            return index.IsValid;
        }

        public int ToIndexUnsafe(DataStoreID id)
        {
            return IDToIndex[id];
        }

        public DataStoreID ToID(DataStoreIndex index)
        {
            return IndexToID[index];
        }

        public void Dispose()
        {
            IndexToID.Dispose();
            IDToIndex.Dispose();
        }

        public static NativeIndexIDMapper Create()
        {
            return new NativeIndexIDMapper()
            {
                IndexToID = new NativeList<DataStoreID>(Constants.AllElementsInitialCapacity, Allocator.Persistent),
                IDToIndex = new NovaHashMap<DataStoreID, DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent)
            };
        }
    }
}

