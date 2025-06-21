// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
#pragma warning disable CS0660, CS0661  // Type defines operator == or operator != but does not override Object.Equals(object o)
    internal struct SpatialPartitionMask
#pragma warning restore CS0660, CS0661  // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        public const int OctantCount = 8;
        public const int OctantsPerFace = 4;

        private BitField8 mask;
        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return mask[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                mask[index] = value;
            }
        }

        public int Count => mask.CountBits();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAxis(int axisIndex, bool positiveHalf, bool value)
        {
            byte axis = GetAxisMask(axisIndex);
            axis = (byte)math.select(~axis, axis, positiveHalf);
            mask = (byte)math.select(mask & ~axis, mask | axis, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetAxisMask(int axisIndex)
        {
            int3 masks = math.select(Math.int3_Zero, AxisMasks, Math.AxisIndices == axisIndex);

            // vectorized -- only the one that matches will be non-zero
            return (byte)(masks.x | masks.y | masks.z);
        }

        private static readonly int3 AxisMasks = new int3(X, Y, Z);

        private const byte X = 0xF0; // 11110000
        private const byte Y = 0xCC; // 11001100
        private const byte Z = 0xAA; // 10101010

        private static readonly int3 BitMask = new int3(XBit, YBit, ZBit);

        private const byte XBit = 0x04;
        private const byte YBit = 0x02;
        private const byte ZBit = 0x01;

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return mask.IsEmpty;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpatialPartitionMask Overlap(SpatialPartitionMask mask)
        {
            return this & mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(SpatialPartitionMask mask)
        {
            return (this.mask & mask.mask) != BitField8.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpatialPartitionMask operator &(SpatialPartitionMask lhs, SpatialPartitionMask rhs)
        {
            return lhs.mask & rhs.mask;
        }

        public static readonly SpatialPartitionMask Empty = new SpatialPartitionMask() { mask = BitField8.Empty };
        public static readonly SpatialPartitionMask Full = new SpatialPartitionMask() { mask = BitField8.Full };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BitField8(SpatialPartitionMask mask)
        {
            return mask.mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SpatialPartitionMask(BitField8 mask)
        {
            return new SpatialPartitionMask() { mask = mask };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SpatialPartitionMask(byte mask)
        {
            return (BitField8)mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SpatialPartitionMask lhs, SpatialPartitionMask rhs)
        {
            return lhs.mask == rhs.mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SpatialPartitionMask lhs, SpatialPartitionMask rhs)
        {
            return lhs.mask != rhs.mask;
        }

        public static readonly float3[] Octants = new float3[] { new float3(-1, -1, -1),
                                                                 new float3(-1, -1, 1),
                                                                 new float3(-1, 1, -1),
                                                                 new float3(-1, 1, 1),
                                                                 new float3(1, -1, -1),
                                                                 new float3(1, -1, 1),
                                                                 new float3(1, 1, -1),
                                                                 new float3(1, 1, 1) };

        public static readonly float3x4 LeftOctants = new float3x4(new float3(-1, -1, -1),
                                                                   new float3(-1, -1, 1),
                                                                   new float3(-1, 1, 1),
                                                                   new float3(-1, 1, -1));

        public static readonly float3x4 RightOctants = new float3x4(new float3(1, -1, -1),
                                                                    new float3(1, -1, 1),
                                                                    new float3(1, 1, 1),
                                                                    new float3(1, 1, -1));

        public static readonly float3x4 BackOctants = new float3x4(new float3(-1, -1, 1),
                                                                   new float3(-1, 1, 1),
                                                                   new float3(1, 1, 1),
                                                                   new float3(1, -1, 1));

        public static readonly float3x4 FrontOctants = new float3x4(new float3(-1, -1, -1),
                                                                    new float3(-1, 1, -1),
                                                                    new float3(1, 1, -1),
                                                                    new float3(1, -1, -1));

        public static readonly float3x4 BottomOctants = new float3x4(new float3(-1, -1, -1),
                                                                     new float3(-1, -1, 1),
                                                                     new float3(1, -1, 1),
                                                                     new float3(1, -1, -1));

        public static readonly float3x4 TopOctants = new float3x4(new float3(-1, 1, -1),
                                                                  new float3(-1, 1, 1),
                                                                  new float3(1, 1, 1),
                                                                  new float3(1, 1, -1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetOctant(int index, int axis, float direction)
        {
            // X
            if (axis == 0)
            {
                if (direction < 0)
                {
                    return LeftOctants[index];
                }

                return RightOctants[index];
            }

            // Y
            if (axis == 1)
            {
                if (direction < 0)
                {
                    return BottomOctants[index];
                }

                return TopOctants[index];
            }

            // Z
            if (direction < 0)
            {
                return FrontOctants[index];
            }

            return BackOctants[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetOctant(int index)
        {
            return math.select(Math.float3_NegativeOne, Math.float3_One, (new int3(index) & BitMask) != Math.int3_Zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetOctantIndex(int3 octant)
        {
            return GetOctantIndex(octant > Math.int3_Zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetOctantIndex(bool3 octant)
        {
            int3 index = math.select(Math.int3_Zero, BitMask, octant);

            return (byte)(index.x | index.y | index.z);
        }
    }

    internal partial class LayoutCore
    {
        [BurstCompile]
        internal struct SpatialPartition : INovaJobParallelFor
        {
            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeList<SpatialPartitionMask> SpatialPartitions;

            [ReadOnly]
            public NativeList<float3> TotalContentSizes;
            [ReadOnly]
            public NativeList<float3> TotalContentOffsets;
            [ReadOnly]
            public NativeList<float3> LocalPositions;
            [ReadOnly]
            public NativeList<quaternion> LocalRotations;
            [ReadOnly]
            public NativeList<float3> LocalScales;
            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
            [ReadOnly]
            public NativeList<DataStoreIndex> DirtyElementIndices;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int dirtyIndex)
            {
                DataStoreIndex childIndex = DirtyElementIndices[dirtyIndex];
                HierarchyElement child = Hierarchy[childIndex];

                if (!HierarchyLookup.TryGetValue(child.ParentID, out DataStoreIndex parentIndex))
                {
                    SpatialPartitions[childIndex] = SpatialPartitionMask.Full;
                    return;
                }

                SpatialPartitionMask partitionMask = SpatialPartitionMask.Empty;

                float3 octantCenter = TotalContentOffsets[parentIndex];
                bool parent2D = TotalContentSizes[parentIndex].z == 0;

                float3 localScale = LocalScales[childIndex];
                float3 position = localScale * TotalContentOffsets[childIndex] + LocalPositions[childIndex];
                float3 extents = localScale * TotalContentSizes[childIndex] * Math.float3_Half;

                // Octants flip z bit every other index. If the parent is 2D,
                // we only need to check intersections with even corners
                int increment = parent2D ? 2 : 1;

                for (int cornerIndex = 0; cornerIndex < SpatialPartitionMask.OctantCount; cornerIndex += increment)
                {
                    float3 cornerPosition = position + (extents * SpatialPartitionMask.Octants[cornerIndex]);

                    int octantIndex = SpatialPartitionMask.GetOctantIndex(cornerPosition > octantCenter);
                    partitionMask[octantIndex] = true;
                }

                SpatialPartitions[childIndex] = partitionMask;
            }
        }
    }
}
