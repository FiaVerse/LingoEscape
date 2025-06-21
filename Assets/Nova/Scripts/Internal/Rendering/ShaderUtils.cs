// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal static class ShaderUtils
    {
        private const VisualType UIBlock2DMask = VisualType.UIBlock2D | VisualType.DropShadow;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIncluded(VisualType visualType, LightingModel lightingModel)
        {
            if (SystemSettings.UsingScriptableRenderPipeline)
            {
                // If using a scriptable render pipeline, only include unlit
                return lightingModel == LightingModel.Unlit;
            }


            LightingModelBuildFlag includedShadersMask;
            if ((visualType & UIBlock2DMask) != 0)
            {
                includedShadersMask = NovaSettings.Config.UIBlock2DLightingModels;
            }
            else if (visualType == VisualType.UIBlock3D)
            {
                includedShadersMask = NovaSettings.Config.UIBlock3DLightingModels;
            }
            else if ((visualType & VisualType.TEXT_MASK) != 0)
            {
                includedShadersMask = NovaSettings.Config.TextBlockLightingModels;
            }
            else
            {
                Debug.LogError($"Unknown VisualType of: {visualType.ToShaderName()}");
                return false;
            }

            return (lightingModel.ToBuildFlag() & includedShadersMask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LightingModelBuildFlag ToBuildFlag(this LightingModel lightingModel)
        {
            switch (lightingModel)
            {
                case LightingModel.Unlit:
                    return LightingModelBuildFlag.Unlit;
                case LightingModel.Lambert:
                    return LightingModelBuildFlag.Lambert;
                case LightingModel.BlinnPhong:
                    return LightingModelBuildFlag.BlinnPhong;
                case LightingModel.Standard:
                    return LightingModelBuildFlag.Standard;
                case LightingModel.StandardSpecular:
                    return LightingModelBuildFlag.StandardSpecular;
                default:
                    return LightingModelBuildFlag.None;
            }
        }
    }
}
