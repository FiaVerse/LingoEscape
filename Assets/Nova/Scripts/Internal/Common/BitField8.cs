// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Nova.Internal.Common
{
    internal struct BitField8
    {
        private byte bits;
        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return (bits & (1 << index)) != 0;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                bits = (byte) (value ? bits | (1 << index) : bits & ~(1 << index));
            }
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return bits == 0x00;
            }
        }

        public readonly bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return bits == 0xFF;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int CountBits()
        {
            return math.countbits((uint)bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitField8 operator &(BitField8 lhs, BitField8 rhs)
        {
            return new BitField8()
            {
                bits = (byte)(lhs.bits & rhs.bits)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator byte(BitField8 field)
        {
            return field.bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BitField8(byte field)
        {
            return new BitField8() { bits = field };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBit(int index)
        {
            return this[index] ? 1 : 0;
        }

        public static readonly BitField8 Empty = new BitField8() { bits = 0x00 };
        public static readonly BitField8 Full = new BitField8() { bits = 0xFF };

        public override string ToString()
        {
            return $"[{GetBit(0)}, {GetBit(1)}, {GetBit(2)}, {GetBit(3)}, {GetBit(4)}, {GetBit(5)}, {GetBit(6)}, {GetBit(7)}]";
        }
    }
}
