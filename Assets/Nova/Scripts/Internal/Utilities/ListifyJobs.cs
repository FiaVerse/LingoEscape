// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Nova.Internal.Utilities
{
    internal static partial class EngineUtils
    {
        [BurstCompile]
        internal struct Listify<KeyType,ValType> : INovaJob
            where KeyType : unmanaged, IEquatable<KeyType>
            where ValType : unmanaged
        {
            [ReadOnly]
            public NovaHashMap<KeyType, ValType> Map;
            [ReadOnly]
            public NativeList<KeyType> KeysToListify;

            [WriteOnly]
            public NativeList<ValType> Listified;

            public void Execute()
            {
                Listified.Clear();

                for (int i = 0; i < KeysToListify.Length; ++i)
                {
                    if (!Map.TryGetValue(KeysToListify[i], out ValType val))
                    {
                        Debug.LogError($"Key {KeysToListify[i]} was not in map");
                        continue;
                    }

                    Listified.Add(val);
                }
            }
        }
    }
}

