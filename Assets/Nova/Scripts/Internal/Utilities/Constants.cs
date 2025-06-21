// Copyright (c) Supernova Technologies LLC
using UnityEngine.Rendering;

namespace Nova.Internal.Utilities
{
    internal static class Constants
    {
        public const string ProjectName = "Nova";
        public const string PackageName = "com.nova.nova";

        public const int PhysicsAllLayers = -1;

        public const int AllElementsInitialCapacity = 128;
        public const int SomeElementsInitialCapacity = 32;
        public const int FewElementsInitialCapacity = 4;

        public const float SingleLineEditorPadding = 2f;

        public const int DefaultTransparentRenderQueue = (int)RenderQueue.Transparent;
        public const int DefaultOpaqueRenderQueue = (int)RenderQueue.Geometry;
        public const int MaxRenderQueue = 5000;
        public const int MaxVisualModifiers = 16;

        public const int QuadsPerAccent = 8;
        public const float DistanceComparisonThreshold = .98f;
        public const float DrawCallDistanceAdjustmentRatio = .999f;
        public const float InverseDrawCallDistanceAdjustmentRatio = 1 - DrawCallDistanceAdjustmentRatio;

        public const int LightingModelMax = (int)LightingModel.StandardSpecular + 1;

        public const int MaxTextureDimension = 16384;

        public const string TMPSupportedShaderName = "TextMeshPro/Mobile/Distance Field";

        public const string LambertLightingKeyword = "NOVA_LAMBERT_LIGHTING";
        public const string BlinnPhongLightingKeyword = "NOVA_BLINNPHONG_LIGHTING";
        public const string StandardLightingKeyword = "NOVA_STANDARD_LIGHTING";
        public const string StanardSpecularLightingKeyword = "NOVA_STANDARD_SPECULAR_LIGHTING";

        public const string LogDisableMessage = "(This warning can be disabled via Nova settings)";
    }
}

