// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct TexturePackData : IInitializable, IClearable
    {
        public struct Assignment : IEquatable<TextureID>
        {
            public TextureID TextureID;
            public TexturePackSlice Slice;

            public Assignment(TextureID textureID, TexturePackSlice slice)
            {
                TextureID = textureID;
                Slice = slice;
            }

            public bool Equals(TextureID other)
            {
                return TextureID == other;
            }
        }

        public TextureDescriptor TextureDescriptor;
        public GraphicsFormatDescriptor FormatDescriptor;

        public NovaList<TextureID> Textures;
        public NovaList<TexturePackSlice, TextureID> SliceOccupation;

        public NovaList<Assignment> NeedsCopied;
        private bool copyAll;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Textures.Length;
        }

        public bool IsDirty
        {
            get
            {
                if (Textures.Length < 2)
                {
                    return false;
                }

                if (copyAll)
                {
                    NeedsCopied.Clear();
                    for (int i = 0; i < SliceOccupation.Length; ++i)
                    {
                        NeedsCopied.Add(new Assignment(SliceOccupation[i], i));
                    }
                    return true;
                }
                else
                {
                    return NeedsCopied.Length > 0;
                }
            }
        }

        public void ClearDirtyState()
        {
            copyAll = false;
            NeedsCopied.Clear();
        }

        public void Clear()
        {
            Textures.Clear();
            SliceOccupation.Clear();
            NeedsCopied.Clear();
            copyAll = false;
        }

        public TexturePackSlice AddTexture(TextureID textureID)
        {
            Textures.Add(textureID);

            for (int i = 0; i < SliceOccupation.Length; i++)
            {
                if (SliceOccupation[i].IsValid)
                {
                    continue;
                }

                SliceOccupation[i] = textureID;
                NeedsCopied.Add(new Assignment(textureID, i));
                return i;
            }

            copyAll = true;
            SliceOccupation.Add(textureID);
            return SliceOccupation.Length - 1;
        }

        public void Remove(TextureID textureID)
        {
            if (!SliceOccupation.TryGetIndexOf(textureID, out int index))
            {
                Debug.LogError("Tried to remove untracked texture from TexturePack");
                return;
            }

            SliceOccupation[index] = TextureID.Invalid;

            if (Textures.TryGetIndexOf(textureID, out int texturesArrayIndex))
            {
                Textures.RemoveAtSwapBack(texturesArrayIndex);
            }

            if (NeedsCopied.TryGetIndexOf(textureID, out index))
            {
                NeedsCopied.RemoveAtSwapBack(index);
            }
        }

        public void Init()
        {
            Textures.Init();
            SliceOccupation.Init();
            NeedsCopied.Init();
            copyAll = false;
        }

        public void Dispose()
        {
            Textures.Dispose();
            SliceOccupation.Dispose();
            NeedsCopied.Dispose();
        }
    }
}

