// Copyright (c) Supernova Technologies LLC
//#define VERBOSE
using Nova.Compat;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Nova.Internal.Rendering
{
    internal class ImageTracker : IDisposable
    {
        /// <summary>
        /// A wrapper for a texture to minimize dictionary accesses
        /// </summary>
        private class TrackedTexture
        {
            public Texture Texture;
        }


        private Dictionary<TextureID, TrackedTexture> trackedTextures = new Dictionary<TextureID, TrackedTexture>(Constants.SomeElementsInitialCapacity);
        private Dictionary<TexturePackID, TexturePack> texturePacks = new Dictionary<TexturePackID, TexturePack>(Constants.SomeElementsInitialCapacity);
        private List<TexturePack> texturePackPool = new List<TexturePack>(Constants.SomeElementsInitialCapacity);
        private bool packedImagesSupported = false;
        private bool initializedPackedImageSetting = false;

        private ImageDataStore dataStore = default;


        #region Runners
        [FixedAddressValueType]
        private static ImageTrackingRunner runner;
        private static BurstedMethod<BurstMethod> track;
        private static BurstedMethod<BurstMethod> untrack;
        private static BurstedMethod<BurstMethod> packUpdate;
        private static BurstedMethod<BurstMethod> disablePacks;
        private static BurstedMethod<BurstMethod> enablePacks;
        #endregion

        #region Track/Untrack
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTracked(TextureID textureID)
        {
            return trackedTextures.TryGetValue(textureID, out TrackedTexture tracked) && tracked.Texture != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(TextureID textureID, out Texture texture)
        {
            if (trackedTextures.TryGetValue(textureID, out TrackedTexture tracked))
            {
                texture = tracked.Texture;
                return texture != null;
            }
            else
            {
                texture = null;
                return false;
            }
        }

        public void Track(Sprite sprite, ImagePackMode packMode, ref ImageID imageID)
        {
            ImageDescriptor imageDescriptor = new ImageDescriptor(packMode);

            Texture texture = null;
            if (sprite != null && sprite.texture != null)
            {
                texture = sprite.texture;
                imageDescriptor.TextureID = texture.GetInstanceID();
                imageDescriptor.SpriteID = sprite.GetInstanceID();

                Rect rect = sprite.textureRect;

                // In some situations, unity silently adds padding to a sprite,
                // so correct for that
                Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                rect.xMin -= padding.x;
                rect.yMin -= padding.y;
                rect.width += padding.z;
                rect.height += padding.w;

                imageDescriptor.Rect = rect;
                imageDescriptor.Border = sprite.border;
            }
            Track(texture, ref imageDescriptor, ref imageID);
        }

        public void Track(Texture texture, ImagePackMode packMode, ref ImageID imageID)
        {
            ImageDescriptor imageDescriptor = new ImageDescriptor(packMode);

            if (texture != null)
            {
                imageDescriptor.TextureID = texture.GetInstanceID();
                imageDescriptor.Rect = new Rect(Vector2.zero, new Vector2(texture.width, texture.height));
            }

            Track(texture, ref imageDescriptor, ref imageID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Track(Texture texture, ref ImageDescriptor imageDescriptor, ref ImageID imageID)
        {
            if (imageDescriptor.TextureID.IsValid)
            {
                // Ensure the data for the new texture exists in the data store
                TrackTexture(imageDescriptor.TextureID, texture);
            }

            runner.ImageID.Value = imageID;
            runner.ImageDescriptor = imageDescriptor;

            unsafe
            {
                track.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
            }

            HandleRunnerOutput(ref imageID);
        }

        public void Untrack(ref ImageID imageID)
        {
            runner.ImageID.Value = imageID;

            unsafe
            {
                untrack.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
            }

            HandleRunnerOutput(ref imageID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleRunnerOutput(ref ImageID imageID)
        {
            imageID = runner.ImageID.Value;

            ref ImageTrackingRunner.Output output = ref runner.RunOutput.Ref();

            if (output.Untrack.IsValid && trackedTextures.TryGetValue(output.Untrack, out TrackedTexture tracked))
            {
                tracked.Texture = null;
            }
        }

        private void TrackTexture(TextureID textureID, Texture texture)
        {
            if (trackedTextures.TryGetValue(textureID, out TrackedTexture tracked))
            {
                tracked.Texture = texture;
                return;
            }


            trackedTextures.Add(textureID, new TrackedTexture()
            {
                Texture = texture,
            });

            if (!dataStore.TextureDescriptors.ContainsKey(textureID))
            {
                TextureDescriptor textureDescriptor = new TextureDescriptor(texture);
                dataStore.TextureDescriptors.Add(textureID, textureDescriptor);
            }

            EnsureGraphicsFormatDescriptor(texture);
        }

        private void EnsureGraphicsFormatDescriptor(Texture texture)
        {
            if (dataStore.TryGetFormatDescriptor(texture.graphicsFormat, out GraphicsFormatDescriptor formatDescriptor))
            {
                return;
            }

            formatDescriptor.IsLinear = !GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);

            if (texture is Texture2D tex2D)
            {   
                formatDescriptor.IsSupportedStatic = SystemInfo.SupportsTextureFormat(tex2D.format) && !GraphicsFormatUtility.IsCrunchFormat(tex2D.format);
                formatDescriptor.TextureFormat = tex2D.format;
            }
            else
            {
                formatDescriptor.IsSupportedStatic = false;
            }

            if (GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat) &&
                !GraphicsFormatUtility.IsPVRTCFormat(texture.graphicsFormat))
            {
                formatDescriptor.BlockSize = (int)math.max(GraphicsFormatUtility.GetBlockHeight(texture.graphicsFormat), GraphicsFormatUtility.GetBlockWidth(texture.graphicsFormat));
            }
            else
            {
                formatDescriptor.BlockSize = 0;
            }

            dataStore.SetFormatDescriptor(texture.graphicsFormat, formatDescriptor);
        }

        /// <summary>
        /// The first frame after the build target changes, the textures are actually in the old build
        /// targets format...so we need to change them
        /// </summary>
        public void EditorOnly_HandleBuildTargetChanged()
        {
            if (trackedTextures.Count == 0)
            {
                return;
            }


            foreach (var tracked in trackedTextures)
            {
                if (tracked.Value.Texture == null)
                {
                    continue;
                }

                dataStore.TextureDescriptors[tracked.Key] = new TextureDescriptor(tracked.Value.Texture);
                EnsureGraphicsFormatDescriptor(tracked.Value.Texture);
            }
        }

        public void EditorOnly_CleanupDecompressedTextures()
        {
            foreach (var pack in texturePacks.Values)
            {
                if (pack == null)
                {
                    continue;
                }
                pack.EditorOnly_ClearDecompressedTextures();
            }
        }

        /// <summary>
        /// Returns true if any of the sprites were tracked and changed
        /// </summary>
        public bool EditorOnly_TryUpdateSprites(Texture baseTexture, UnityEngine.Object[] sprites)
        {
            var textureID = baseTexture.GetInstanceID();
            if (!IsTracked(textureID))
            {
                // If texture is not tracked, it means we're not using any of the sprites
                return false;
            }

            using var keys = dataStore.ImageDescriptors.GetKeyArray(Allocator.Temp);

            bool toRet = false;
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                var imageDescriptor = dataStore.ImageDescriptors[key];
                if (imageDescriptor.TextureID != textureID || imageDescriptor.SpriteID == 0)
                {
                    // Different texture
                    continue;
                }

                for (int j = 0; j < sprites.Length; j++)
                {
                    if (!(sprites[j] is Sprite sprite))
                    {
                        continue;
                    }

                    if (sprite.GetInstanceID() != imageDescriptor.SpriteID)
                    {
                        // Different sprite
                        continue;
                    }

                    Rect newRect = sprite.rect;
                    if (newRect.Equals(imageDescriptor.Rect))
                    {
                        // Rect didn't change
                        continue;
                    }

                    // The rect changed
                    imageDescriptor.Rect = newRect;
                    dataStore.ImageDescriptors[key] = imageDescriptor;
                    toRet = true;
                }
            }

            return toRet;
        }

        /// <summary>
        /// Returns true if it is a tracked texture and the description changed
        /// </summary>
        public bool EditorOnly_TryUpdateTextureDescriptor(Texture texture)
        {
            TextureID id = texture.GetInstanceID();

            if (!dataStore.TextureDescriptors.TryGetValue(id, out TextureDescriptor currentDescriptor))
            {
                // Not tracking this texture, nothing to do
                return false;
            }

            TextureDescriptor newDescriptor = new TextureDescriptor(texture);
            if (newDescriptor.Equals(currentDescriptor))
            {
                // The descriptors match
                return false;
            }

            dataStore.TextureDescriptors[id] = newDescriptor;
            EnsureGraphicsFormatDescriptor(texture);

            if (texture is Texture2D && !currentDescriptor.Dimensions.Equals(newDescriptor.Dimensions))
            {
                // If it's 2D texture and the dimensions have changed (e.g., via the POT settings)
                // we need to also update the ImageDescriptors
                using var keys = dataStore.ImageDescriptors.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++)
                {
                    var key = keys[i];
                    var imageDescriptor = dataStore.ImageDescriptors[key];
                    if (imageDescriptor.TextureID != id)
                    {
                        // Different texture
                        continue;
                    }

                    // Update the rect
                    imageDescriptor.Rect = new Rect(Vector2.zero, new Vector2(texture.width, texture.height));
                    dataStore.ImageDescriptors[key] = imageDescriptor;
                }
            }

            return true;
        }
        #endregion

        #region Subscriptions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Get(TexturePackID packID, ITexturePackSubscriber subscriber)
        {
            if (TryGetPack(packID, out TexturePack texturePack, true))
            {
                texturePack.AddSubscriber(subscriber);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unsubscribe(TexturePackID packID, ITexturePackSubscriber subscriber)
        {
            if (TryGetPack(packID, out TexturePack texturePack, false))
            {
                texturePack.RemoveSubscriber(subscriber);
            }
        }

        private bool TryGetPack(TexturePackID packID, out TexturePack texturePack, bool log)
        {
            if (!texturePacks.TryGetValue(packID, out texturePack))
            {
                if (log)
                {
                    Debug.LogError($"Unknown TexturePack {packID}");
                }
                return false;
            }

            return true;
        }

        public void NotifySubscribers()
        {
            for (int i = 0; i < runner.DirtyPacks.Length; ++i)
            {
                TexturePackID texturePackID = runner.DirtyPacks[i];
                if (!texturePacks.TryGetValue(texturePackID, out TexturePack texturePack))
                {
                    continue;
                }
                texturePack.NotifySubscribers();
            }
        }
        #endregion

        public void ClearDirtyState()
        {
            for (int i = 0; i < runner.DirtyPacks.Length; ++i)
            {
                TexturePackID texturePackID = runner.DirtyPacks[i];
                if (texturePacks.TryGetValue(texturePackID, out TexturePack pack))
                {
                    pack.ClearDirtyState();
                }
            }

            runner.DirtyPacks.Clear();
            runner.PacksToRemove.Clear();
        }

        public void PreUpdate()
        {
            unsafe
            {
                packUpdate.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
            }
        }

        public void UpdatePacks()
        {
            // Handle destroyed
            for (int i = 0; i < runner.PacksToRemove.Length; ++i)
            {
                TexturePackID texturePackID = runner.PacksToRemove[i];
                if (!texturePacks.TryGetValue(texturePackID, out TexturePack texturePack))
                {
                    continue;
                }

                texturePack.Clear();
                texturePackPool.Add(texturePack);
                texturePacks.Remove(texturePackID);
            }

            // Update dirty
            for (int i = 0; i < runner.DirtyPacks.Length; ++i)
            {
                TexturePackID texturePackID = runner.DirtyPacks[i];
                if (!dataStore.TexturePacks.TryGetValue(texturePackID, out TexturePackData texturePackData))
                {
                    // It may have been marked dirty and destroyed in the same frame
                    continue;
                }

                if (!texturePacks.TryGetValue(texturePackID, out TexturePack texturePack))
                {
                    if (!texturePackPool.TryPopBack(out texturePack))
                    {
                        texturePack = new TexturePack();
                    }

                    texturePack.ID = texturePackID;
                    texturePacks[texturePackID] = texturePack;
                }

                texturePack.UpdateTextureArray(ref texturePackData);
                texturePackData.ClearDirtyState();
                dataStore.TexturePacks[texturePackID] = texturePackData;
            }
        }

        private void HandleSettingsChanged()
        {
            if (!initializedPackedImageSetting)
            {
                // First time the event is firing
                initializedPackedImageSetting = true;
                packedImagesSupported = NovaSettings.Config.PackedImagesEnabled;
                return;
            }

            if (NovaSettings.Config.PackedImagesEnabled == packedImagesSupported)
            {
                // Didn't change
                return;
            }

            packedImagesSupported = NovaSettings.PackedImagesEnabled;

            if (packedImagesSupported)
            {
                unsafe
                {
                    enablePacks.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
                }
            }
            else
            {
                unsafe
                {
                    disablePacks.Method.Invoke(UnsafeUtility.AddressOf(ref runner));
                }
            }

            UpdatePacks();
        }

        public ImageTracker(ref ImageDataStore imageDataStore)
        {
            unsafe
            {
                track = new BurstedMethod<BurstMethod>(ImageTrackingRunner.DoTrack);
                untrack = new BurstedMethod<BurstMethod>(ImageTrackingRunner.DoUntrack);
                packUpdate = new BurstedMethod<BurstMethod>(ImageTrackingRunner.DoPackUpdate);
                disablePacks = new BurstedMethod<BurstMethod>(ImageTrackingRunner.DoDisablePacks);
                enablePacks = new BurstedMethod<BurstMethod>(ImageTrackingRunner.DoEnablePacks);
            }

            this.dataStore = imageDataStore;
            runner = new ImageTrackingRunner(ref imageDataStore, NovaApplication.IsEditor);

            NovaSettings.OnRenderSettingsChanged += HandleSettingsChanged;

            if (NovaSettings.Initialized)
            {
                initializedPackedImageSetting = true;
                packedImagesSupported = NovaSettings.Config.PackedImagesEnabled;
            }
        }

        public void Dispose()
        {
            runner.Dispose();

            NovaSettings.OnRenderSettingsChanged -= HandleSettingsChanged;

            foreach (var pack in texturePacks.Values)
            {
                pack.Dispose();
            }
            texturePacks.Clear();

            texturePackPool.DisposeElementsAndClear();
        }
    }
}

