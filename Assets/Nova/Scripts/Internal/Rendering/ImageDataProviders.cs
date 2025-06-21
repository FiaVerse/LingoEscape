// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal struct ImageDataProvider
    {
        [ReadOnly]
        private NovaHashMap<ImageID, ImageDescriptor> imageDescriptors;
        [ReadOnly]
        private NovaHashMap<TextureID, TextureDescriptor> textureDescriptors;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetTextureID(ImageID imageID, out TextureID textureID)
        {
            if (!TryGetImageData(imageID, out ImageDescriptor imageDescriptor))
            {
                textureID = TextureID.Invalid;
                return false;
            }

            textureID = imageDescriptor.TextureID;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetImageData(ImageID imageID, out ImageDescriptor imageDescriptor) => imageDescriptors.TryGetValue(imageID, out imageDescriptor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetImageData(ImageID imageID, out ImageDescriptor imageDescriptor, out TextureDescriptor textureDescriptor)
        {
            if (!imageDescriptors.TryGetValue(imageID, out imageDescriptor))
            {
                textureDescriptor = default;
                return false;
            }

            return textureDescriptors.TryGetValue(imageDescriptor.TextureID, out textureDescriptor);
        }

        public ImageDataProvider(ref NovaHashMap<ImageID, ImageDescriptor> imageDescriptors, ref NovaHashMap<TextureID, TextureDescriptor> textureDescriptors)
        {
            this.imageDescriptors = imageDescriptors;
            this.textureDescriptors = textureDescriptors;
        }
    }

    internal struct TexturePackDataProvider
    {
        [ReadOnly]
        private NovaHashMap<TextureID, TexturePackID> textureIDToPack;
        [ReadOnly]
        private NovaHashMap<TextureID, TexturePackSlice> slices;
        [ReadOnly]
        private NovaHashMap<TexturePackID, int> packCounts;

        /// <summary>
        /// We always want to set the slice, even if the pack only has a count of 1 since this
        /// block may not get dirtied later when another image gets added to the pack
        /// </summary>
        /// <param name="textureID"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSlice(TextureID textureID, out TexturePackSlice index)
        {
            index = TexturePackSlice.Invalid;

            if (!NovaSettings.Config.PackedImagesEnabled)
            {
                return false;
            }

            return
                textureIDToPack.TryGetValue(textureID, out TexturePackID texturePackID) &&
                slices.TryGetValue(textureID, out index);
        }

        /// <summary>
        /// Always return the pack ID, even if the count is 1, use <see cref="ShouldUsePack(TexturePackID)"/>
        /// to determine if actually in use this frame
        /// </summary>
        /// <param name="textureID"></param>
        /// <param name="texturePackID"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPackID(TextureID textureID, out TexturePackID texturePackID)
        {
            texturePackID = TexturePackID.Invalid;
            if (!NovaSettings.Config.PackedImagesEnabled)
            {
                return false;
            }

            return textureIDToPack.TryGetValue(textureID, out texturePackID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldUsePack(TexturePackID texturePackID)
        {
            return packCounts.TryGetValue(texturePackID, out int count) && count > 1;
        }

        public TexturePackDataProvider(ref NovaHashMap<TextureID, TexturePackID> idToPack, ref NovaHashMap<TextureID, TexturePackSlice> indices,
            ref NovaHashMap<TexturePackID, int> counts)
        {
            textureIDToPack = idToPack;
            slices = indices;
            packCounts = counts;
        }
    }
}