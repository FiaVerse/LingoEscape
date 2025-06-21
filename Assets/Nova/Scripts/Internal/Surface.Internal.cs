// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nova.Internal
{

    internal enum LightingModel
    {
        Unlit = 0,
        Lambert = 1,
        BlinnPhong = 2,
        Standard = 3,
        StandardSpecular = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Surface : System.IEquatable<Surface>
    {
        public Color SpecularColor;
        /// <summary>
        /// BlinnPhong: Specular
        /// Standard: Smoothness
        /// StandardSpecular: Smoothness
        /// </summary>
        public float param1;
        /// <summary>
        /// BlinnPhong: Gloss
        /// Standard: Metallic
        /// </summary>
        public float param2;
        private LightingModel lightingModel;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;

        public LightingModel LightingModel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SystemSettings.UsingScriptableRenderPipeline ? LightingModel.Unlit : lightingModel;
        }

        public float Specular
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => param1;
        }

        public float Smoothness
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => param1;
        }

        public float Gloss
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => param2;
        }

        public float Metallic
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => param2;
        }

        public bool IsLit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => LightingModel != LightingModel.Unlit;
        }

        public bool HasShaderData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsLit && LightingModel != LightingModel.Lambert;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Surface other)
        {
            return
                SpecularColor.Equals(other.SpecularColor) &&
                param1 == other.param1 &&
                param2 == other.param2 &&
                LightingModel == other.LightingModel &&
                ShadowCastingMode == other.ShadowCastingMode &&
                ReceiveShadows == other.ReceiveShadows;
        }
    }
}
