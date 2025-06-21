// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Lighting models (as a mask) to include in builds.
    /// </summary>
    /// <seealso cref="NovaSettings.UIBlock2DLightingModels"/>
    /// <seealso cref="NovaSettings.UIBlock3DLightingModels"/>
    /// <seealso cref="NovaSettings.TextBlockLightingModels"/>
    [Flags]
    public enum LightingModelBuildFlag : int
    {
        /// <summary>
        /// Don't include any shaders.
        /// </summary>
        None = Internal.LightingModelBuildFlag.None,
        /// <summary>
        /// Include <see cref="LightingModel.Unlit"/> shaders.
        /// </summary>
        Unlit = Internal.LightingModelBuildFlag.Unlit,
        /// <summary>
        /// Include shaders for the <see cref="LightingModel.Lambert"/> lighting model.
        /// </summary>
        Lambert = Internal.LightingModelBuildFlag.Lambert,
        /// <summary>
        /// Include shaders for the <see cref="LightingModel.BlinnPhong"/> lighting model.
        /// </summary>
        BlinnPhong = Internal.LightingModelBuildFlag.BlinnPhong,
        /// <summary>
        /// Include shaders for the <see cref="LightingModel.Standard"/> lighting model.
        /// </summary>
        Standard = Internal.LightingModelBuildFlag.Standard,
        /// <summary>
        /// Include shaders for the <see cref="LightingModel.StandardSpecular"/> lighting model.
        /// </summary>
        StandardSpecular = Internal.LightingModelBuildFlag.StandardSpecular,
    }

    /// <summary>
    /// Certain, older versions of the Nvidia OpenGL driver for certain graphics cards crash
    /// when trying to copy a texture to a texture array when the texture is compressed with a 
    /// block-based format and has a mip-map with dimensions which are not a multiple of the block size.
    /// This enum configures how to copy such textures.
    /// </summary>
    public enum PackedImageCopyMode : int
    {
        /// <summary>
        /// Copy the texture without checking if all mip levels are multiples of block size.
        /// </summary>
        Blind = Internal.PackedImageCopyMode.Blind,
        /// <summary>
        /// Skip copying mip-levels when the dimensions are not a multiple of block size. You should only use this
        /// if you encounter issues.
        /// </summary>
        Skip = Internal.PackedImageCopyMode.Skip,
    }

    /// <summary>
    /// Warnings which can be disabled or enabled.
    /// </summary>
    /// <seealso cref="NovaSettings.LogFlags"/>
    [Flags]
    public enum LogFlags : int
    {
        /// <summary>
        /// Don't log any warnings.
        /// </summary>
        None = Internal.LogFlags.None,
        /// <summary>
        /// Log whenever an issue is encountered that prevents <see cref="ImagePackMode.Packed"/> images from working 
        /// (which will causes all images to fallback to <see cref="ImagePackMode.Unpacked"/>).
        /// </summary>
        PackedImageFailure = Internal.LogFlags.PackedImageFailure,
        /// <summary>
        /// Log when an unsupported TMP shader is used (which will cause the <see cref="TextBlock"/> to fallback to 
        /// <c>Mobile/Distance Field</c>).
        /// </summary>
        UnsupportedTextShader = Internal.LogFlags.UnsupportedTextShader,
        /// <summary>
        /// Log when a <see cref="UIBlock"/> uses a <see cref="LightingModel"/> which has not been marked 
        /// to be included in builds via <see cref="NovaSettings.UIBlock2DLightingModels"/>, 
        /// <see cref="NovaSettings.UIBlock3DLightingModels"/>, or <see cref="NovaSettings.TextBlockLightingModels"/> 
        /// (depending on <see cref="UIBlock"/> type).
        /// </summary>
        LightingModelNotIncludedInBuild = Internal.LogFlags.LightingModelNotIncludedInBuild,
        /// <summary>
        /// Log when a child of a <see cref="ListView"/> is destroyed out from under it.
        /// </summary>
        ListViewItemDestroyed = Internal.LogFlags.ListViewItemDestroyed,
        /// <summary>
        /// Log when <see cref="ListView"/> has children under it that it is not tracking.
        /// </summary>
        ListViewUntrackedItemsUnderRoot = Internal.LogFlags.ListViewUntrackedItemsUnderRoot,
    }

    /// <summary>
    /// NOTE: This must be burst compatible
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    internal struct SettingsConfig
    {
        #region General 
        [SerializeField]
        public LogFlags LogFlags;
        #endregion

        #region Rendering
        [SerializeField]
        public bool PackedImagesEnabled;
        [SerializeField]
        public PackedImageCopyMode PackedImageCopyMode;
        [SerializeField]
        public bool SuperSampleText;
        [SerializeField]
        public float EdgeSoftenWidth;
        [SerializeField]
        public int UIBlock3DCornerDivisions;
        [SerializeField]
        public int UIBlock3DEdgeDivisions;
        [SerializeField]
        public LightingModelBuildFlag UIBlock2DLightingModels;
        [SerializeField]
        public LightingModelBuildFlag TextBlockLightingModels;
        [SerializeField]
        public LightingModelBuildFlag UIBlock3DLightingModels;
        #endregion

        #region Input
        [SerializeField]
        public int ClickFrameDeltaThreshold;
        #endregion


        public static readonly SettingsConfig Default = new SettingsConfig()
        {
            LogFlags = (LogFlags)(-1),
            PackedImagesEnabled = true,
            PackedImageCopyMode = PackedImageCopyMode.Blind,
            SuperSampleText = false,
            EdgeSoftenWidth = 1f,
            UIBlock3DCornerDivisions = 8,
            UIBlock3DEdgeDivisions = 8,
            UIBlock2DLightingModels = LightingModelBuildFlag.Unlit,
            TextBlockLightingModels = LightingModelBuildFlag.Unlit,
            UIBlock3DLightingModels = LightingModelBuildFlag.Lambert,
            ClickFrameDeltaThreshold = 1,
        };
    }
}
