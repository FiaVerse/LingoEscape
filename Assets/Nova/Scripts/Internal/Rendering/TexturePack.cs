// Copyright (c) Supernova Technologies LLC
//#define VERBOSE
using Nova.Compat;
using Nova.Internal.Common;
using Nova.Internal.Utilities;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// Handles the managed side of a texture pack
    /// </summary>
    internal class TexturePack : IDisposable, IClearable
    {
        private Texture2DArray textureArray = null;
        private bool recreatedTextureArray = false;

        // These should only be accessed when NovaApplication.IsEditor == true
        private List<Texture2D> compressedCopyTexturePool = new List<Texture2D>();
        private Dictionary<TextureID, Texture2D> decompressed = new Dictionary<TextureID, Texture2D>();


        /// <summary>
        /// This list of batch roots that are using this texture set. We need this, as
        /// if the underlying Texture2DArray gets recreated, we need to notify all of the batch groups,
        /// even if they are not dirty
        /// </summary>
        private List<ITexturePackSubscriber> subscribers = new List<ITexturePackSubscriber>(Constants.FewElementsInitialCapacity);

        public TexturePackID ID;

        public void Clear()
        {

            subscribers.Clear();
            recreatedTextureArray = false;

            DestroyUtils.SafeDestroy(textureArray);
            textureArray = null;

            if (NovaApplication.IsEditor)
            {
                for (int i = 0; i < compressedCopyTexturePool.Count; ++i)
                {
                    DestroyUtils.SafeDestroy(compressedCopyTexturePool[i]);
                }
                compressedCopyTexturePool.Clear();

                foreach (var texture in decompressed.Values)
                {
                    DestroyUtils.SafeDestroy(texture);
                }
            }
        }

        public void NotifySubscribers()
        {
            if (!recreatedTextureArray || textureArray == null)
            {
                return;
            }

            for (int i = 0; i < subscribers.Count; ++i)
            {
                if (recreatedTextureArray)
                {
                    subscribers[i].HandleTextureArrayRecreated(textureArray);
                }
            }
        }

        public void AddSubscriber(ITexturePackSubscriber subscriber)
        {
            subscribers.Add(subscriber);
            if (textureArray != null)
            {
                subscriber.HandleTextureArrayRecreated(textureArray);
            }
        }

        public void RemoveSubscriber(ITexturePackSubscriber subscriber)
        {
            int index = subscribers.IndexOf(subscriber);
            if (index == -1)
            {
                return;
            }

            subscribers.RemoveAtSwapBack(index);
        }

        public void ClearDirtyState()
        {
            recreatedTextureArray = false;
        }

        public void UpdateTextureArray(ref TexturePackData data)
        {
            if (data.Textures.Length < 2)
            {
                DestroyUtils.SafeDestroy(textureArray);
                return;
            }

            if (!NovaSettings.Config.PackedImagesEnabled)
            {
                return;
            }

            EnsureTextureArray(ref data);

            if (NovaApplication.IsEditor && !data.FormatDescriptor.IsSupportedStatic)
            {
                EditorOnly_CopyDecompressedTextures(ref data);
                return;
            }

            for (int i = 0; i < data.NeedsCopied.Length; ++i)
            {
                TexturePackData.Assignment toCopy = data.NeedsCopied[i];

                if (toCopy.Slice >= textureArray.depth)
                {
                    Debug.LogError($"Tried to copy {toCopy.Slice} into size {textureArray.depth} texture");
                    continue;
                }

                if (!RenderingDataStore.Instance.ImageTracker.TryGet(toCopy.TextureID, out Texture texture))
                {
                    Debug.LogError($"TexturePack tried to copy texture which wasn't tracked");
                    continue;
                }

                CopyToArray(ref data, texture, toCopy.Slice);
            }
        }

        private void CopyToArray(ref TexturePackData data, Texture texture, int index)
        {
            if (!data.TextureDescriptor.HasMips || data.FormatDescriptor.BlockSize == 0)
            {
                // No mips or not a block based format, 
                Graphics.CopyTexture(texture, 0, textureArray, index);
                return;
            }

            // Manually copy each mip, skipping over ones that don't meet the block size requirement.
            // Nvidia opengl driver crashes if you just try to copy it
            int2 blockSize2 = data.FormatDescriptor.BlockSize;
            for (int mip = 0; mip < texture.mipmapCount; ++mip)
            {
                switch (NovaSettings.Config.PackedImageCopyMode)
                {
                    case PackedImageCopyMode.Blind:
                    {
                        Graphics.CopyTexture(texture,
                            srcElement: 0,
                            srcMip: mip,
                            textureArray,
                            dstElement: index,
                            dstMip: mip);
                        break;
                    }
                    case PackedImageCopyMode.Skip:
                    {
                        int2 mipDimensions = math.max(1, data.TextureDescriptor.Dimensions >> mip);
                        if (math.any((mipDimensions % blockSize2) != int2.zero))
                        {
                            continue;
                        }
                        Graphics.CopyTexture(texture,
                            srcElement: 0,
                            srcMip: mip,
                            textureArray,
                            dstElement: index,
                            dstMip: mip);
                        break;
                    }
                }
            }
        }


        private bool EnsureTextureArray(ref TexturePackData data)
        {
            if (textureArray != null && data.Count <= textureArray.depth)
            {
                return false;
            }

            if (textureArray != null)
            {
                recreatedTextureArray = true;
                DestroyUtils.Destroy(textureArray);
            }

            TextureFormat texFormat;

            if (NovaApplication.IsEditor)
            {
                texFormat = data.FormatDescriptor.IsSupportedStatic ? data.FormatDescriptor.TextureFormat : TextureFormat.RGBA32;
            }
            else
            {
                texFormat = data.FormatDescriptor.TextureFormat;
            }

            textureArray = new Texture2DArray(data.TextureDescriptor.Dimensions.x, data.TextureDescriptor.Dimensions.y, data.Textures.Length, texFormat, data.TextureDescriptor.HasMips, data.FormatDescriptor.IsLinear);
            textureArray.wrapMode = TextureWrapMode.Clamp;
            textureArray.hideFlags = HideFlags.DontSave;

            if (!NovaApplication.IsEditor || data.FormatDescriptor.IsSupportedStatic)
            {
                // Make unreadable on cpu
                textureArray.Apply(false, true);
            }
            return true;
        }

        private Texture2D EditorOnly_GetDecompressedTexture(ref TexturePackData data)
        {
            int bestMatchIndex = -1;
            float bestMatchAreaFilled = 0f;
            for (int i = 0; i < compressedCopyTexturePool.Count; ++i)
            {
                Texture2D tex = compressedCopyTexturePool[i];
                if (tex == null)
                {
                    compressedCopyTexturePool.RemoveAtSwapBack(i--);
                    continue;
                }

                bool matches = tex.width >= data.TextureDescriptor.Dimensions.x && tex.height >= data.TextureDescriptor.Dimensions.y && (tex.mipmapCount > 1) == data.TextureDescriptor.HasMips;
                if (!matches)
                {
                    continue;
                }

                float areaFilled = (float)(data.TextureDescriptor.Dimensions.x * data.TextureDescriptor.Dimensions.y) / (float)(tex.width * tex.height);
                if (areaFilled < bestMatchAreaFilled)
                {
                    continue;
                }

                bestMatchIndex = i;
                bestMatchAreaFilled = areaFilled;
            }

            if (bestMatchIndex != -1)
            {
                Texture2D toRet = compressedCopyTexturePool[bestMatchIndex];
                compressedCopyTexturePool.RemoveAtSwapBack(bestMatchIndex);
                return toRet;
            }
            else
            {
                return new Texture2D(data.TextureDescriptor.Dimensions.x, data.TextureDescriptor.Dimensions.y, TextureFormat.RGBA32, data.TextureDescriptor.HasMips, data.FormatDescriptor.IsLinear);
            }
        }

        private unsafe Texture2D EditorOnly_Decompress(ref TexturePackData data, TextureID textureID, Texture2D texture2D)
        {

            RenderTexture renderTex = RenderTexture.GetTemporary(
                texture2D.width,
                texture2D.height,
                0,
                RenderTextureFormat.ARGB32,
                data.FormatDescriptor.IsLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);

            if (data.TextureDescriptor.HasMips && !renderTex.useMipMap)
            {
                if (renderTex.IsCreated())
                {
                    renderTex.Release();
                }
                renderTex.useMipMap = true;
            }

            // Auto generate mips doesn't work...so disable it and then generate them manually
            renderTex.autoGenerateMips = false;
            Texture2D decompressedCopy = EditorOnly_GetDecompressedTexture(ref data);

            Graphics.Blit(texture2D, renderTex);
            if (data.TextureDescriptor.HasMips)
            {
                renderTex.GenerateMips();
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            // Try to do the copy gpu side if it's supported
            if ((SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.RTToTexture) != 0)
            {
                Graphics.CopyTexture(renderTex, decompressedCopy);
            }
            else
            {
                decompressedCopy.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0, recalculateMipMaps: data.TextureDescriptor.HasMips);
            }

            if (previous == renderTex)
            {
                // For some reason, sometimes the temporary render texture *was* the active one
                RenderTexture.active = null;
            }
            else
            {
                RenderTexture.active = previous;
            }
            RenderTexture.ReleaseTemporary(renderTex);

            decompressed.Add(textureID, decompressedCopy);
            return decompressedCopy;
        }

        public void EditorOnly_ClearDecompressedTextures()
        {
            decompressed.Clear();
        }

        private void EditorOnly_CopyDecompressedTextures(ref TexturePackData data)
        {
            for (int i = 0; i < data.NeedsCopied.Length; ++i)
            {
                TexturePackData.Assignment toCopy = data.NeedsCopied[i];

                if (toCopy.Slice >= textureArray.depth)
                {
                    Debug.LogError($"TexturePack {ID} tried to copy {toCopy.Slice} into size {textureArray.depth} texture");
                    continue;
                }

                if (!decompressed.TryGetValue(toCopy.TextureID, out Texture2D decompressedCopy) ||
                    decompressedCopy == null)
                {
                    decompressed.Remove(toCopy.TextureID);

                    if (!RenderingDataStore.Instance.ImageTracker.TryGet(toCopy.TextureID, out Texture texture))
                    {
                        Debug.LogError($"TexturePack {ID} not tracking texture");
                        continue;
                    }
                    decompressedCopy = EditorOnly_Decompress(ref data, toCopy.TextureID, texture as Texture2D);
                }
                Graphics.CopyTexture(decompressedCopy, 0, textureArray, toCopy.Slice);
            }

            if ((SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.RTToTexture) == 0)
            {
                textureArray.Apply();
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
