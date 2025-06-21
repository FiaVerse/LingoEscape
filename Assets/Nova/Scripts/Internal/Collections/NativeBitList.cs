// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Collections
{
    internal struct NativeBitList : IDisposable
    {
        public struct ReadOnly
        {
            [ReadOnly]
            private NativeList<BitField32> list;
            [ReadOnly]
            private NativeList<int> lengthPtr;

            public int Length
            {
                get => lengthPtr[0];
                set
                {
                    GetIndexAndOffset(value, out int listLength, out int remainder);

                    list.Length = math.select(listLength, listLength + 1, remainder != 0);
                    lengthPtr[0] = list.Length;
                }
            }

            public bool this[int index]
            {
                get
                {
                    if (index < 0)
                    {
                        Debug.LogError($"Value {index} must be positive.");
                    }
                    if ((uint)index >= (uint)lengthPtr[0])
                    {
                        Debug.LogError($"Value {index} is out of range in NativeBitList of '{lengthPtr[0]}' Length.");
                    }

                    GetIndexAndOffset(index, out int listIndex, out int bitIndex);

                    BitField32 bits = list[listIndex];
                    return bits.IsSet(bitIndex);
                }
            }

            public ReadOnly(NativeBitList bitList)
            {
                list = bitList.list;
                lengthPtr = bitList.lengthPtr;
            }
        }

        private NativeList<BitField32> list;

        private NativeList<int> lengthPtr;

        public ReadOnly AsReadOnly() => new ReadOnly(this);

        public int Length
        {
            get => lengthPtr[0];
            set
            {
                GetIndexAndOffset(value, out int listLength, out int remainder);

                list.Length = math.select(listLength, listLength + 1, remainder != 0);
                lengthPtr[0] = list.Length;
            }
        }

        public bool this[int index]
        {
            get
            {
                if (index < 0)
                {
                    Debug.LogError($"Value {index} must be positive.");
                }
                if ((uint)index >= (uint)lengthPtr[0])
                {
                    Debug.LogError($"Value {index} is out of range in NativeBitList of '{lengthPtr[0]}' Length.");
                }

                GetIndexAndOffset(index, out int listIndex, out int bitIndex);

                BitField32 bits = list[listIndex];
                return bits.IsSet(bitIndex);
            }
            set
            {
                if (index < 0)
                {
                    Debug.LogError($"Value {index} must be positive.");
                }
                if ((uint)index >= (uint)lengthPtr[0])
                {
                    Debug.LogError($"Value {index} is out of range in NativeBitList of '{lengthPtr}' Length.");
                }

                GetIndexAndOffset(index, out int listIndex, out int bitIndex);

                BitField32 bits = list[listIndex];
                bits.SetBits(bitIndex, value);
                list[listIndex] = bits;
            }
        }

        public NativeBitList(int initialCapacity, Allocator allocator)
        {
            list = new NativeList<BitField32>(GetSizeForList(initialCapacity), allocator);
            lengthPtr = new NativeList<int>(1, allocator);
            lengthPtr.Add(0);
        }

        public void Add(bool isSet)
        {
            int length = lengthPtr[0];
            GetIndexAndOffset(length, out int listIndex, out int bitIndex);

            if (listIndex >= list.Length)
            {
                list.Add(new BitField32());
            }

            BitField32 bits = list[listIndex];
            bits.SetBits(bitIndex, isSet);
            list[listIndex] = bits;

            length++;

            lengthPtr[0] = length;
        }

        public void RemoveAtSwapBack(int index)
        {
            int length = lengthPtr[0];
            this[index] = this[length - 1];
            length--;

            lengthPtr[0] = length;

        }

        public void SetAll(bool isSet)
        {
            unsafe
            {
                UnsafeUtility.MemSet(list.GetRawPtr(), isSet ? (byte)0xFF : (byte)0x00, list.Length * sizeof(uint));
            }
        }

        private static int GetSizeForList(int desiredSize)
        {
            GetIndexAndOffset(desiredSize, out int size, out int remainder);
            return math.select(size + 1, size, remainder == 0);
        }

        private static void GetIndexAndOffset(int inputIndex, out int outputIndex, out int outputOffset)
        {
            outputIndex = inputIndex / 32;
            outputOffset = inputIndex % 32;
        }

        public void Dispose()
        {
            list.Dispose();
            lengthPtr.Dispose();
        }
    }
}
