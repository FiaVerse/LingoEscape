// Copyright (c) Supernova Technologies LLC
//#define USE_FALLBACK
using Nova.Compat;
using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nova.Internal.Rendering
{
    internal class SystemSettings
    {
        public static ColorSpace ColorSpace
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Settings.Data.ColorSpace;
        }

        public static bool UsingScriptableRenderPipeline
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Settings.Data.UsingScriptableRenderPipeline;
        }

        public static bool UseFallbackRendering
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Settings.Data.UseFallbackRendering;
        }

        public static bool NewTMP
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Settings.Data.NewTMP;
        }

        public struct SettingsData
        {
            public ColorSpace ColorSpace;
            public bool UsingScriptableRenderPipeline;
            public bool UseFallbackRendering;
            /// <summary>
            /// Hate that we have this here, but we need a burst/job safe place to have this value
            /// </summary>
            public bool NewTMP;
        }

        public static readonly SharedStatic<SettingsData> Settings = SharedStatic<SettingsData>.GetOrCreate<SystemSettings, SettingsContext>();

        public static void Init()
        {
            ref SettingsData settingsData = ref Settings.Data;
            settingsData.ColorSpace = QualitySettings.activeColorSpace;
            settingsData.UsingScriptableRenderPipeline = GraphicsSettings.defaultRenderPipeline != null;
            settingsData.UseFallbackRendering = SystemInfo.maxComputeBufferInputsVertex < 4;
            settingsData.NewTMP = UnityVersionUtils.NewTMP;
        }

        private class SettingsContext { }
    }
}

