// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal static class ShaderPropertyIDs
    {
        // Material properties
        public static int ZWrite;
        public static int ZTest;
        public static int SrcBlend;
        public static int DestBlend;
        public static int AdditiveLightingSrcBlend;
        public static int AdditiveLightingDstBlend;
        public static int CullMode;

        // Shared
        public static int WorldFromLocalTransform;
        public static int LocalFromWorldTransform;
        public static int TransformsAndLighting;
        public static int FirstIndex;
        public static int LastIndex;
        public static int ViewingFromBehind;
        public static int ShaderData;
        public static int DataIndices;

        // Clip Mask
        public static int VisualModifierCount;
        public static int VisualModifersFromRoot;
        public static int ClipRectInfos;
        public static int ClipMaskIndex;
        public static int GlobalColorModifiers;
        public static int ClipMaskTexture;

        // UIBlock2D
        public static int SubQuadVerts;
        public static int EdgeSoftenWidth;
        public static int DynamicTexture;
        public static int StaticTexture;

        // Drop shadow
        public static int PerBlockData;

        /// <summary>
        /// We can't init these statically because it complains about calling Shader.PropertyToID
        /// </summary>
        public static void Init()
        {
            ZWrite = Shader.PropertyToID("_ZWrite");
            ZTest = Shader.PropertyToID("_ZTest");
            SrcBlend = Shader.PropertyToID("_SrcBlend");
            DestBlend = Shader.PropertyToID("_DstBlend");
            AdditiveLightingSrcBlend = Shader.PropertyToID("_AdditiveLightingSrcBlend");
            AdditiveLightingDstBlend = Shader.PropertyToID("_AdditiveLightingDstBlend");
            CullMode = Shader.PropertyToID("_CullMode");

            // Shared
            WorldFromLocalTransform = Shader.PropertyToID("_NovaWorldFromLocal");
            LocalFromWorldTransform = Shader.PropertyToID("_NovaLocalFromWorld");
            TransformsAndLighting = Shader.PropertyToID("_NovaTransformsAndLighting");
            FirstIndex = Shader.PropertyToID("_NovaFirstIndex");
            LastIndex = Shader.PropertyToID("_NovaLastIndex");
            ViewingFromBehind = Shader.PropertyToID("_NovaViewingFromBehind");
            ShaderData = Shader.PropertyToID("_NovaData");
            DataIndices = Shader.PropertyToID("_NovaDataIndices");

            // Clip Mask
            VisualModifierCount = Shader.PropertyToID("_NovaVisualModifierCount");
            VisualModifersFromRoot = Shader.PropertyToID("_NovaVisualModifiersFromRoot");
            ClipRectInfos = Shader.PropertyToID("_NovaClipRectInfos");
            ClipMaskIndex = Shader.PropertyToID("_NovaClipMaskIndex");
            GlobalColorModifiers = Shader.PropertyToID("_NovaGlobalColorModifiers");
            ClipMaskTexture = Shader.PropertyToID("_ClipMaskTex");

            // UIBlock2D
            SubQuadVerts = Shader.PropertyToID("_NovaSubQuadVerts");
            EdgeSoftenWidth = Shader.PropertyToID("_NovaEdgeSoftenWidth");

            // Image/Color
            DynamicTexture = Shader.PropertyToID("_NovaDynamicTexture");
            StaticTexture = Shader.PropertyToID("_NovaTextureArray");

            // Drop Shadow
            PerBlockData = Shader.PropertyToID("_NovaPerBlockData");
        }
    }
}
