// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Internal
{
    [Flags]
    internal enum LightingModelBuildFlag : int
    {
        None = 0,
        Unlit = 1,
        Lambert = 2 * Unlit,
        BlinnPhong = 2 * Lambert,
        Standard = 2 * BlinnPhong,
        StandardSpecular = 2 * Standard,
    }

    internal enum PackedImageCopyMode : int
    {
        Blind = 0,
        Skip = 1,
    }

    [Flags]
    internal enum LogFlags : int
    {
        None = 0,
        PackedImageFailure = 1,
        UnsupportedTextShader = 2 * PackedImageFailure,
        LightingModelNotIncludedInBuild = 2 * UnsupportedTextShader,
        ListViewItemDestroyed = 2 * LightingModelNotIncludedInBuild,
        ListViewUntrackedItemsUnderRoot = 2 * ListViewItemDestroyed,
    }

    /// <summary>
    /// NOTE: This must be burst compatible
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SettingsConfig
    {
        #region General 
        public LogFlags LogFlags;
        #endregion

        #region Rendering
        public bool PackedImagesEnabled;
        public PackedImageCopyMode PackedImageCopyMode;
        public bool SuperSampleText;
        public float EdgeSoftenWidth;
        public int UIBlock3DCornerDivisions;
        public int UIBlock3DChamferDivisions;
        public LightingModelBuildFlag UIBlock2DLightingModels;
        public LightingModelBuildFlag TextBlockLightingModels;
        public LightingModelBuildFlag UIBlock3DLightingModels;
        #endregion

        #region Input
        public int ClickFrameDeltaThreshold;
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldLog(LogFlags flags) => (LogFlags & flags) != 0;
    }
}
