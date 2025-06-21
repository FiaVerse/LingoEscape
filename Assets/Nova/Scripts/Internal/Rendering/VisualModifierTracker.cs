// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct VisualModifierRenderData
    {
        public Matrix4x4 ModifierFromRoot;
        /// <summary>
        /// xy -> nHalfSize
        /// z -> nFactor
        /// w -> nRadius
        /// </summary>
        public Vector4 ClipRectInfo;
        public Color ColorModifier;
        public bool IsMask;
    }

    internal struct NestedVisualModiferData<T> where T : unmanaged
    {
        public T V0;
        public T V1;
        public T V2;
        public T V3;
        public T V4;
        public T V5;
        public T V6;
        public T V7;
        public T V8;
        public T V9;
        public T V10;
        public T V11;
        public T V12;
        public T V13;
        public T V14;
        public T V15;

        public unsafe ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (NestedVisualModiferData<T>* array = &this) { return ref ((T*)array)[index]; }
            }
        }
    }

    internal struct VisualModifierShaderData
    {
        public int Count;
        public int ClipMaskIndex;
        public NestedVisualModiferData<VisualModifierID> ModifierIDs;
        public NestedVisualModiferData<Matrix4x4> ModifiersFromRoot;
        public NestedVisualModiferData<Vector4> ClipRectInfos;
        public NestedVisualModiferData<Color> ColorModifiers;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
            ClipMaskIndex = -1;
        }

        /// <summary>
        /// Sets the info of the next visual modifier
        /// </summary>
        /// <param name="renderData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(VisualModifierID id, ref VisualModifierRenderData renderData)
        {
            if (Count == Constants.MaxVisualModifiers)
            {
                return;
            }

            Set(Count++, id, ref renderData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, VisualModifierID id, ref VisualModifierRenderData renderData)
        {
            ModifierIDs[index] = id;
            ModifiersFromRoot[index] = renderData.ModifierFromRoot;
            ClipRectInfos[index] = renderData.ClipRectInfo;
            ColorModifiers[index] = renderData.ColorModifier;

            if (ClipMaskIndex == -1 && renderData.IsMask)
            {
                // This one is a mask, and we haven't set a mask yet
                ClipMaskIndex = index;
            }
        }

        public static readonly VisualModifierShaderData Default = new VisualModifierShaderData()
        {
            ClipMaskIndex = -1
        };
    }

    internal class ManagedVisualModifierShaderData
    {
        public int Count;
        public int ClipMaskIndex;
        public Matrix4x4[] VisualModifiersFromRoot = new Matrix4x4[Constants.MaxVisualModifiers];
        public Vector4[] ClipRectInfos = new Vector4[Constants.MaxVisualModifiers];
        public Vector4[] VisualModifierColors = new Vector4[Constants.MaxVisualModifiers];
        public VisualModifierID[] ModifierIDs = new VisualModifierID[Constants.MaxVisualModifiers];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyFrom(VisualModifierShaderData data)
        {
            Count = data.Count;
            ClipMaskIndex = data.ClipMaskIndex;

            if (Count == 0)
            {
                return;
            }

            fixed (Matrix4x4* dest = VisualModifiersFromRoot)
            {
                MemoryUtils.MemCpy(dest, (Matrix4x4*)&data.ModifiersFromRoot, Count);
            }

            fixed (Vector4* dest = ClipRectInfos)
            {
                MemoryUtils.MemCpy(dest, (Vector4*)&data.ClipRectInfos, Count);
            }

            fixed (Vector4* dest = VisualModifierColors)
            {
                MemoryUtils.MemCpy(dest, (Vector4*)&data.ColorModifiers, Count);
            }

            fixed (VisualModifierID* dest = ModifierIDs)
            {
                MemoryUtils.MemCpy(dest, (VisualModifierID*)&data.ModifierIDs, Count);
            }
        }
    }

    internal class VisualModifierTracker : IInitializable
    {
        public NovaHashMap<DataStoreID, VisualModifierID> BlockToModifierID;
        public NativeList<VisualModifierID, ClipMaskInfo> Data;
        public NativeList<VisualModifierID, DataStoreID> ModifierToBlockID;
        public NativeList<VisualModifierID, VisualModifierRenderData> RenderData;
        public NativeList<VisualModifierID, VisualModifierShaderData> ShaderData;
        public List<ManagedVisualModifierShaderData> ManagedShaderData = new List<ManagedVisualModifierShaderData>();
        public NovaHashMap<VisualModifierID, AABB> ClipBounds;
        /// <summary>
        /// If the modifier is nested, this will contain it's parent modifier
        /// </summary>
        public NativeList<VisualModifierID, VisualModifierID> ParentModifier;
        public List<Texture> ClipMaskTextures = new List<Texture>();

        private RenderingDataStore DataStore => RenderingDataStore.Instance;
        private Stack<VisualModifierID> freeIDs = new Stack<VisualModifierID>();

        public void AddOrUpdate(DataStoreID dataStoreID, ClipMaskInfo newInfo, Texture texture)
        {
            texture = newInfo.Clip ? texture : null;

            if (BlockToModifierID.TryGetValue(dataStoreID, out VisualModifierID visualModifierID))
            {
                ClipMaskInfo currentInfo = Data[visualModifierID];

                if (newInfo.Equals(currentInfo) && texture == ClipMaskTextures[visualModifierID])
                {
                    // Info didn't change
                    return;
                }

                // Update the info for the already existing modifier
                Data[visualModifierID] = newInfo;
                ClipMaskTextures[visualModifierID] = texture;
                DataStore.DirtyState.DirtyVisualModifiers.Add(dataStoreID);
            }
            else
            {
                // New modifier
                if (freeIDs.Count > 0)
                {
                    visualModifierID = freeIDs.Pop();
                    Data[visualModifierID] = newInfo;
                    ModifierToBlockID[visualModifierID] = dataStoreID;
                    ClipMaskTextures[visualModifierID] = texture;
                    ParentModifier[visualModifierID] = VisualModifierID.Invalid;
                }
                else
                {
                    visualModifierID = Data.Length;
                    Data.Add(newInfo);
                    RenderData.Add(default);
                    ManagedShaderData.Add(new ManagedVisualModifierShaderData());
                    ShaderData.Add(default);
                    ModifierToBlockID.Add(dataStoreID);
                    ClipMaskTextures.Add(texture);
                    ParentModifier.Add(VisualModifierID.Invalid);
                }

                BlockToModifierID.Add(dataStoreID, visualModifierID);
                DataStore.DirtyState.DirtyVisualModifiers.Add(dataStoreID);
            }

            if (newInfo.Clip)
            {
                ClipBounds[visualModifierID] = default;
            }
            else
            {
                ClipBounds.Remove(visualModifierID);
            }
        }

        public void Remove(DataStoreID dataStoreID)
        {
            if (!BlockToModifierID.TryGetValue(dataStoreID, out VisualModifierID visualModifierID))
            {
                return;
            }

            freeIDs.Push(visualModifierID);
            ClipMaskTextures[visualModifierID] = null;
            BlockToModifierID.Remove(dataStoreID);
            ClipBounds.Remove(visualModifierID);
            DataStore.DirtyState.DirtyVisualModifiers.Add(dataStoreID);
        }

        public void Dispose()
        {
            BlockToModifierID.Dispose();
            Data.Dispose();
            ClipBounds.Dispose();
            RenderData.Dispose();
            ShaderData.Dispose();
            ModifierToBlockID.Dispose();
            ParentModifier.Dispose();
        }

        public void Init()
        {
            BlockToModifierID.Init(Constants.FewElementsInitialCapacity);
            Data.Init(Constants.FewElementsInitialCapacity);
            ClipBounds.Init(Constants.FewElementsInitialCapacity);
            RenderData.Init(Constants.FewElementsInitialCapacity);
            ShaderData.Init(Constants.FewElementsInitialCapacity);
            ModifierToBlockID.Init(Constants.FewElementsInitialCapacity);
            ParentModifier.Init(Constants.FewElementsInitialCapacity);
        }
    }
}

