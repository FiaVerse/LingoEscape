// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct ImageDataStore : IInitializable
    {
        public struct RefCount : IEquatable<RefCount>
        {
            public int Static;
            public int Total;

            public bool Equals(RefCount other)
            {
                return Static == other.Static && Total == other.Total;
            }

            public override string ToString()
            {
                return $"Total: {Total}, Statie: {Static}";
            }
        }

        public NovaHashMap<ImageID, ImageDescriptor> ImageDescriptors;
        public NovaHashMap<TextureID, TextureDescriptor> TextureDescriptors;
        public NovaHashMap<TextureID, RefCount> RefCounts;

        public NovaHashMap<TextureID, TexturePackID> TextureIDToPack;
        public NovaHashMap<TexturePackID, int> PackCounts;
        public NovaHashMap<TextureID, TexturePackSlice> TexturePackSliceAssignments;
        public NativeList<TexturePackID> CurrentTexturePacks;
        public NovaHashMap<TexturePackID, TexturePackData> TexturePacks;

        public NovaHashMap<TextureDescriptor, TexturePackID> FormatToPackID;
        public NativeList<TexturePackData> TexturePackPool;

        private NativeReference<ValuePair<ImageID, TexturePackID>> nextID;
        private NovaHashMap<int, GraphicsFormatDescriptor> formatDescriptors;
        private NativeReference<bool> loggedSupportWarnings;
        private bool textureArraysSupported;
        private bool textureCopySupported;
        private bool fullResTextures;

        public bool PackedImagesSupported
        {
            get
            {
                if (!textureArraysSupported)
                {
                    if (!loggedSupportWarnings.Value && NovaSettings.Config.ShouldLog(LogFlags.PackedImageFailure))
                    {
                        Debug.LogWarning($"TextureArrays not supported on platform. Static images will not be able to be batched, falling back to dynamic. {Constants.LogDisableMessage}");
                        loggedSupportWarnings.Value = true;
                    }
                    return false;
                }

                if (!fullResTextures)
                {
                    if (!loggedSupportWarnings.Value && NovaSettings.Config.ShouldLog(LogFlags.PackedImageFailure))
                    {
                        Debug.LogWarning($"A bug with Texture2DArrays when the \"Texture Quality\" setting is not full resolution prevents static images from working properly. Falling back to dynamic. {Constants.LogDisableMessage}");
                        loggedSupportWarnings.Value = true;
                    }
                    return false;
                }

                if (!textureCopySupported)
                {
                    return false;
                }

                return true;
            }
        }
        /// <summary>
        /// Just a wrapper helper for the jobs to use
        /// </summary>
        public ImageDataProvider DataProvider;
        public TexturePackDataProvider PackDataProvider;

        public ImageID GetNextImageID() => nextID.Ref().Item1++;
        public TexturePackID GetNextPackID()
        {
            TexturePackID toRet = nextID.Ref().Item2++;
            CurrentTexturePacks.Add(toRet);
            return toRet;
        }

        public bool TryGetFormatDescriptor(GraphicsFormat format, out GraphicsFormatDescriptor descriptor)
        {
            descriptor = default;
            return formatDescriptors.TryGetValue((int)format, out descriptor);
        }

        public void SetFormatDescriptor(GraphicsFormat format, GraphicsFormatDescriptor descriptor)
        {
            formatDescriptors[(int)format] = descriptor;
        }

        public void Init()
        {
            ImageDescriptors.Init(Constants.SomeElementsInitialCapacity);
            TextureDescriptors.Init(Constants.SomeElementsInitialCapacity);
            formatDescriptors.Init(Constants.SomeElementsInitialCapacity);
            RefCounts.Init(Constants.SomeElementsInitialCapacity);

            TextureIDToPack.Init(Constants.SomeElementsInitialCapacity);
            PackCounts.Init(Constants.SomeElementsInitialCapacity);
            TexturePackSliceAssignments.Init(Constants.SomeElementsInitialCapacity);
            FormatToPackID.Init(Constants.SomeElementsInitialCapacity);

            TexturePacks.Init(Constants.SomeElementsInitialCapacity);
            TexturePackPool.Init(Constants.SomeElementsInitialCapacity);
            CurrentTexturePacks.Init(Constants.SomeElementsInitialCapacity);

            nextID.Init(new ValuePair<ImageID, TexturePackID>(0, 0));
            loggedSupportWarnings.Init(false);

            textureArraysSupported = SystemInfo.supports2DArrayTextures;
            textureCopySupported = (SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.DifferentTypes) != 0;
            fullResTextures = QualitySettingsUtils.GlobalTextureMipMapLimit == 0;

            DataProvider = new ImageDataProvider(ref ImageDescriptors, ref TextureDescriptors);
            PackDataProvider = new TexturePackDataProvider(ref TextureIDToPack, ref TexturePackSliceAssignments, ref PackCounts);
        }

        public void Dispose()
        {
            ImageDescriptors.Dispose();
            TextureDescriptors.Dispose();
            formatDescriptors.Dispose();
            RefCounts.Dispose();

            TextureIDToPack.Dispose();
            PackCounts.Dispose();
            TexturePackSliceAssignments.Dispose();
            FormatToPackID.Dispose();

            CurrentTexturePacks.Dispose();
            nextID.Dispose();
            loggedSupportWarnings.Dispose();

            using (var packs = TexturePacks.GetValueArray(Allocator.Temp))
            {
                for (int i = 0; i < packs.Length; ++i)
                {
                    packs[i].Dispose();
                }
            }

            TexturePacks.Clear();
            TexturePacks.Dispose();
            TexturePackPool.DisposeListAndElements();
        }
    }
}

