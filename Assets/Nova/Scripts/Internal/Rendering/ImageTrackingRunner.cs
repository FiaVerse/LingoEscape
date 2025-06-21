// Copyright (c) Supernova Technologies LLC
//#define VERBOSE
using AOT;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct ImageTrackingRunner : IDisposable
    {
        public struct Output
        {
            /// <summary>
            /// Texture to untrack
            /// </summary>
            public TextureID Untrack;

            public static readonly Output Default = new Output()
            {
                Untrack = TextureID.Invalid,
            };
        }

        private struct RegistrationDelta : IEquatable<TextureID>
        {
            public TextureID TextureID;
            public int Delta;

            public bool Equals(TextureID other) => TextureID.Equals(other);

            public override string ToString()
            {
                return Delta.ToString();
            }
        }

        public NativeReference<ImageID> ImageID;
        public ImageDescriptor ImageDescriptor;

        public ImageDataStore DataStore;

        public bool ApplicationIsEditor;
        public NativeReference<Output> RunOutput;
        public NativeList<TexturePackID> PacksToRemove;
        public NativeList<TexturePackID> DirtyPacks;
        private NativeList<RegistrationDelta> registrationDeltas;

        private void Track()
        {
            RunOutput.Value = Output.Default;

            ref ImageID imageID = ref ImageID.Ref();

            if (!ImageDescriptor.TextureID.IsValid)
            {
                // No new texture, just make sure the current one is untracked
                EnsureNotTracked(ref imageID);
                return;
            }

            if (!DataStore.RefCounts.TryGetValue(ImageDescriptor.TextureID, out ImageDataStore.RefCount counts))
            {
                counts = default;
            }

            bool bumpCounts = true;
            if (TryGetDescriptor(ref imageID, out ImageDescriptor previousDescriptor))
            {
                // Compare with previous registration
                if (previousDescriptor.Equals(ImageDescriptor))
                {
                    // Duplicate registration, nothing to do
                    return;
                }

                // Different, unregister the current one
                // First bump the count, so we don't remove the texture if it is the same
                counts.Total += 1;
                bumpCounts = false;
                DataStore.RefCounts[ImageDescriptor.TextureID] = counts;
                EnsureNotTracked(ref imageID);
            }

            imageID = DataStore.GetNextImageID();
            DataStore.ImageDescriptors.Add(imageID, ImageDescriptor);

            if (bumpCounts)
            {
                counts.Total += 1;
                DataStore.RefCounts[ImageDescriptor.TextureID] = counts;
            }

            if (IsStatic(ref ImageDescriptor))
            {
                AddRegistrationDelta(ImageDescriptor.TextureID, 1);
            }
        }

        private void AddRegistrationDelta(TextureID textureID, int delta)
        {
            if (registrationDeltas.TryGetIndexOf(textureID, out int index))
            {
                ref RegistrationDelta registrationDelta = ref registrationDeltas.ElementAt(index);
                registrationDelta.Delta += delta;


                if (registrationDelta.Delta == 0)
                {
                    registrationDeltas.RemoveAtSwapBack(index);
                }
            }
            else
            {
                registrationDeltas.Add(new RegistrationDelta()
                {
                    TextureID = textureID,
                    Delta = delta
                });
            }
        }

        private void CreatePack(ref TextureDescriptor textureDescriptor, out TexturePackID packID, out TexturePackData packData)
        {
            packID = DataStore.GetNextPackID();
            packData = DataStore.TexturePackPool.GetFromPoolOrInit();
            DataStore.TryGetFormatDescriptor(textureDescriptor.Format, out packData.FormatDescriptor);
            packData.TextureDescriptor = textureDescriptor;
            DataStore.PackCounts[packID] = 0;
            DataStore.FormatToPackID.Add(textureDescriptor, packID);
        }

        private void Untrack()
        {
            RunOutput.Value = Output.Default;
            ref ImageID imageID = ref ImageID.Ref();
            EnsureNotTracked(ref imageID);
        }

        private void EnsureNotTracked(ref ImageID imageID)
        {
            if (!TryGetDescriptor(ref imageID, out ImageDescriptor imageDescriptor))
            {
                return;
            }

            DataStore.ImageDescriptors.Remove(imageID);
            imageID = Internal.ImageID.Invalid;

            if (!DataStore.RefCounts.TryGetValue(imageDescriptor.TextureID, out ImageDataStore.RefCount count))
            {
                Debug.LogError("Failed to get ref counts for texture");
                return;
            }

            count.Total -= 1;
            if (IsStatic(ref imageDescriptor))
            {
                AddRegistrationDelta(imageDescriptor.TextureID, -1);
            }

            DataStore.RefCounts[imageDescriptor.TextureID] = count;

            if (count.Total > 0)
            {
                // Other blocks are still referencing it
                return;
            }

            RunOutput.Ref().Untrack = imageDescriptor.TextureID;
        }

        private bool IsStatic(ref ImageDescriptor imageDescriptor)
        {
            if (imageDescriptor.Mode == ImagePackMode.Unpacked || !NovaSettings.Config.PackedImagesEnabled)
            {
                return false;
            }

            if (!DataStore.PackedImagesSupported)
            {
                return false;
            }

            if (!DataStore.TextureDescriptors[imageDescriptor.TextureID].IsTexture2D)
            {
                return false;
            }

            if (ApplicationIsEditor)
            {
                // In editor we decompress them
                return true;
            }

            if (!DataStore.TextureDescriptors.TryGetValue(imageDescriptor.TextureID, out TextureDescriptor textureDescriptor) ||
                !DataStore.TryGetFormatDescriptor(textureDescriptor.Format, out GraphicsFormatDescriptor graphicsFormatDescriptor))
            {
                return false;
            }

            if (!graphicsFormatDescriptor.IsSupportedStatic && NovaSettings.Config.ShouldLog(LogFlags.PackedImageFailure))
            {
                Debug.LogWarning($"Platform does not support texture format. {Constants.LogDisableMessage}");
            }
            return graphicsFormatDescriptor.IsSupportedStatic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetDescriptor(ref ImageID imageID, out ImageDescriptor imageDescriptor)
        {
            if (!imageID.IsValid)
            {
                imageDescriptor = default;
                return false;
            }

            bool toRet = DataStore.ImageDescriptors.TryGetValue(imageID, out imageDescriptor);
            if (!toRet)
            {
                Debug.LogError("Failed to get image descriptor for image");
            }
            return toRet;
        }

        private void RemovePack(TexturePackID texturePackID, ref TexturePackData pack)
        {
            PacksToRemove.Add(texturePackID);
            DataStore.FormatToPackID.Remove(pack.TextureDescriptor);

            for (int j = 0; j < pack.Textures.Length; ++j)
            {
                TextureID toRemove = pack.Textures[j];
                DataStore.TexturePackSliceAssignments.Remove(toRemove);

                DataStore.TextureIDToPack.Remove(toRemove);
            }

            DataStore.PackCounts.Remove(texturePackID);
            DataStore.TexturePacks.Remove(texturePackID);
            DataStore.TexturePackPool.ReturnToPool(ref pack);
        }

        private void HandleTextureChanges()
        {
            // Handle removed
            for (int i = 0; i < registrationDeltas.Length; ++i)
            {
                ref RegistrationDelta delta = ref registrationDeltas.ElementAt(i);
                if (delta.Delta >= 0)
                {
                    continue;
                }

                bool removeFromPack = false;
                if (DataStore.RefCounts.TryGetValue(delta.TextureID, out ImageDataStore.RefCount counts))
                {
                    counts.Static += delta.Delta;
                    DataStore.RefCounts[delta.TextureID] = counts;
                    removeFromPack = counts.Static == 0;
                }
                else
                {
                    removeFromPack = DataStore.TextureIDToPack.ContainsKey(delta.TextureID);
                }

                if (!removeFromPack)
                {
                    continue;
                }

                if (!DataStore.TextureIDToPack.TryGetAndRemove(delta.TextureID, out TexturePackID texturePackID))
                {
                    Debug.LogError("Failed to get TexturePack ID");
                    return;
                }


                if (!DataStore.TexturePacks.TryGetValue(texturePackID, out TexturePackData pack))
                {
                    Debug.LogError("Failed to get TexturePack");
                    return;
                }

                DataStore.TexturePackSliceAssignments.Remove(delta.TextureID);
                pack.Remove(delta.TextureID);
                DataStore.PackCounts[texturePackID] = pack.Count;
                DataStore.TexturePacks[texturePackID] = pack;
            }

            // Handle adds
            for (int i = 0; i < registrationDeltas.Length; ++i)
            {
                ref RegistrationDelta delta = ref registrationDeltas.ElementAt(i);
                if (delta.Delta <= 0)
                {
                    continue;
                }

                if (!DataStore.RefCounts.TryGetValue(delta.TextureID, out ImageDataStore.RefCount counts))
                {
                    Debug.LogError("Missing ref count for texture");
                    continue;
                }

                int previousStaticCount = counts.Static;
                counts.Static += delta.Delta;
                DataStore.RefCounts[delta.TextureID] = counts;
                if (previousStaticCount > 0)
                {
                    // Already added
                    continue;
                }

                AddToTexturePack(delta.TextureID);
            }

            registrationDeltas.Clear();
        }

        private void AddToTexturePack(TextureID textureID)
        {
            TextureDescriptor textureDescriptor = DataStore.TextureDescriptors[textureID];

            TexturePackData packData = default;
            if (DataStore.FormatToPackID.TryGetValue(textureDescriptor, out TexturePackID packID))
            {
                if (!DataStore.TexturePacks.TryGetValue(packID, out packData))
                {
                    Debug.LogError("Failed to get TexturePack for pack ID");
                    return;
                }
            }
            else
            {
                // Need to create a new pack
                CreatePack(ref textureDescriptor, out packID, out packData);
            }

            TexturePackSlice slice = packData.AddTexture(textureID);
            DataStore.TexturePackSliceAssignments.Add(textureID, slice);


            DataStore.TextureIDToPack.Add(textureID, packID);
            DataStore.PackCounts[packID] = packData.Count;
            DataStore.TexturePacks[packID] = packData;
        }

        private void PackUpdate()
        {
            PacksToRemove.Clear();
            DirtyPacks.Clear();

            HandleTextureChanges();

            for (int i = 0; i < DataStore.CurrentTexturePacks.Length; ++i)
            {
                TexturePackID texturePackID = DataStore.CurrentTexturePacks[i];
                if (!DataStore.TexturePacks.TryGetValue(texturePackID, out TexturePackData pack))
                {
                    Debug.LogError("PackID not found");
                    continue;
                }

                if (pack.Count < 1)
                {
                    RemovePack(texturePackID, ref pack);
                    DataStore.CurrentTexturePacks.RemoveAtSwapBack(i--);
                    continue;
                }

                if (pack.IsDirty)
                {
                    DirtyPacks.Add(texturePackID);
                }

                DataStore.TexturePacks[texturePackID] = pack;
            }
        }

        private void DisablePacks()
        {
            registrationDeltas.Clear();
            PacksToRemove.Clear();
            DirtyPacks.Clear();

            while (DataStore.CurrentTexturePacks.Length != 0)
            {
                TexturePackID texturePackID = DataStore.CurrentTexturePacks[0];
                if (!DataStore.TexturePacks.TryGetValue(texturePackID, out TexturePackData pack))
                {
                    Debug.LogError("PackID not found");
                    continue;
                }

                RemovePack(texturePackID, ref pack);
                DataStore.CurrentTexturePacks.RemoveAtSwapBack(0);
            }

            using var imageIDs = DataStore.ImageDescriptors.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < imageIDs.Length; ++i)
            {
                ImageID imageID = imageIDs[i];
                ImageDescriptor imageDescriptor = DataStore.ImageDescriptors[imageID];
                if (!DataStore.RefCounts.TryGetValue(imageDescriptor.TextureID, out ImageDataStore.RefCount counts))
                {
                    Debug.LogError("Failed to get ref counts for texture");
                    continue;
                }

                counts.Static -= 1;
                DataStore.RefCounts[imageDescriptor.TextureID] = counts;
            }
        }

        private void EnablePacks()
        {
            registrationDeltas.Clear();
            PacksToRemove.Clear();
            DirtyPacks.Clear();

            using var imageIDs = DataStore.ImageDescriptors.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < imageIDs.Length; ++i)
            {
                ImageID imageID = imageIDs[i];
                ImageDescriptor imageDescriptor = DataStore.ImageDescriptors[imageID];

                if (!IsStatic(ref imageDescriptor))
                {
                    continue;
                }

                if (!DataStore.RefCounts.TryGetValue(imageDescriptor.TextureID, out ImageDataStore.RefCount counts))
                {
                    Debug.LogError("Failed to get ref counts for texture");
                    continue;
                }

                counts.Static += 1;
                DataStore.RefCounts[imageDescriptor.TextureID] = counts;

                if (counts.Static > 1)
                {
                    continue;
                }

                AddToTexturePack(imageDescriptor.TextureID);
            }

            PackUpdate();
        }

        public ImageTrackingRunner(ref ImageDataStore imageDataStore, bool isEditorApplication)
        {
            ApplicationIsEditor = isEditorApplication;
            DataStore = imageDataStore;
            ImageID = default;
            RunOutput = default;
            ImageDescriptor = default;
            PacksToRemove = default;
            DirtyPacks = default;
            registrationDeltas = default;

            ImageID.Init();
            RunOutput.Init();
            PacksToRemove.Init();
            DirtyPacks.Init();
            registrationDeltas.Init();
        }

        public void Dispose()
        {
            ImageID.Dispose();
            RunOutput.Dispose();
            PacksToRemove.Dispose();
            DirtyPacks.Dispose();
            registrationDeltas.Dispose();
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoTrack(void* data)
        {
            UnsafeUtility.AsRef<ImageTrackingRunner>(data).Track();
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoUntrack(void* data)
        {
            UnsafeUtility.AsRef<ImageTrackingRunner>(data).Untrack();
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoPackUpdate(void* data)
        {
            UnsafeUtility.AsRef<ImageTrackingRunner>(data).PackUpdate();
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoDisablePacks(void* data)
        {
            UnsafeUtility.AsRef<ImageTrackingRunner>(data).DisablePacks();
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void DoEnablePacks(void* data)
        {
            UnsafeUtility.AsRef<ImageTrackingRunner>(data).EnablePacks();
        }
    }
}

