// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nova.Internal.Rendering
{
    internal static class MaterialCache
    {
        #region Keywords
        private const string ClipRectKeyword = "NOVA_CLIP_RECT";
        private const string ClipMaskKeyword = "NOVA_CLIP_MASK";
        private const string DynamicImageKeyword = "NOVA_DYNAMIC_IMAGE";
        private const string StaticImageKeyword = "NOVA_STATIC_IMAGE";
        private const string InnerShadowKeyword = "NOVA_INNER_SHADOW";
        private const string SuperSampleKeyword = "NOVA_SUPER_SAMPLE";
        private const string OuterBorderKeyword = "NOVA_OUTER_BORDER";
        private const string InnerBorderKeyword = "NOVA_INNER_BORDER";
        private const string CenterBorderKeyword = "NOVA_CENTER_BORDER";
        private const string RadialFillKeyword = "NOVA_RADIAL_FILL";
        private const string FallbackRenderingKeyword = "NOVA_FALLBACK_RENDERING";
        #endregion

        private static List<Material> materials = new List<Material>(Constants.SomeElementsInitialCapacity);
        private static List<Shader> shaders = new List<Shader>(Constants.SomeElementsInitialCapacity);

        // We need to cache this because sometimes they get destroyed by unity
        private static List<ValuePair<ShaderCacheIndex, MaterialDescriptor>> materialDescriptors = new List<ValuePair<ShaderCacheIndex, MaterialDescriptor>>(Constants.SomeElementsInitialCapacity);
        private static List<ShaderDescriptor> shaderDescriptors = new List<ShaderDescriptor>(Constants.SomeElementsInitialCapacity);

        public static NovaHashMap<MaterialDescriptor, MaterialCacheIndex> CachedMaterials;
        public static NovaHashMap<ShaderDescriptor, ShaderCacheIndex> CachedShaders;

        public static NativeList<ValuePair<ShaderCacheIndex, MaterialDescriptor>> MaterialsToAdd;
        public static NativeList<ShaderDescriptor> ShadersToAdd;

        public static Material Get(MaterialCacheIndex materialCacheIndex)
        {
            Material mat = materials[materialCacheIndex];
            if (mat == null)
            {
                ValuePair<ShaderCacheIndex, MaterialDescriptor> vals = materialDescriptors[materialCacheIndex];
                mat = CreateMaterial(vals.Item1, ref vals.Item2);
                materials[materialCacheIndex] = mat;
            }
            return mat;
        }

        private static Shader Get(ShaderCacheIndex shaderCacheIndex)
        {
            Shader shader = shaders[shaderCacheIndex];
            if (shader == null)
            {
                ShaderDescriptor descriptor = shaderDescriptors[shaderCacheIndex];
                shader = CreateShader(ref descriptor);
                shaders[shaderCacheIndex] = shader;
            }
            return shader;
        }

        public static MaterialCacheIndex NextMaterialIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => materials.Count;
        }

        public static ShaderCacheIndex NextShaderIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => shaders.Count;
        }

        public static void EnsureMaterials()
        {
            AddMissingShaders();

            if (MaterialsToAdd.Length == 0)
            {
                return;
            }

            for (int i = 0; i < MaterialsToAdd.Length; ++i)
            {
                ref ValuePair<ShaderCacheIndex, MaterialDescriptor> vals = ref MaterialsToAdd.ElementAt(i);
                Material material = CreateMaterial(vals.Item1, ref vals.Item2);
                CachedMaterials.Add(vals.Item2, materials.Count);
                materials.Add(material);
                materialDescriptors.Add(vals);
            }

            MaterialsToAdd.Clear();
        }

        private static Material CreateMaterial(ShaderCacheIndex shaderCacheIndex, ref MaterialDescriptor descriptor)
        {
            Material material = null;
            if (!descriptor.ShaderDescriptor.IsText)
            {
                // Non text
                material = new Material(Get(shaderCacheIndex));
                material.hideFlags = HideFlags.DontSaveInEditor;
            }
            else if (RenderingDataStore.Instance.TextMaterials.TryGetValue(descriptor.TextMaterialID, out Material originalFontMaterial))
            {
                // Text and we have the copy
                EnsureSupportedTMPShader(originalFontMaterial, descriptor.TextMaterialID);
                material = new Material(originalFontMaterial);
                material.hideFlags = HideFlags.DontSaveInEditor;
                material.shader = Get(shaderCacheIndex);
            }
            else
            {
                // We failed to get the original font material to copy the properties from, so we'll
                // just create it from scratch. It wont have the right properties, but it's hopefully better than
                // nothing
                Debug.LogError("Failed to get font material");
                material = new Material(Get(shaderCacheIndex));
                material.hideFlags = HideFlags.DontSaveInEditor;
            }

            SetMaterialProperties(material, ref descriptor);
            return material;
        }

        private enum ZTestCompare : int
        {
            Disabled = 0,
            Never = 1,
            Less = 2,
            Equal = 3,
            LessEqual = 4,
            Greater = 5,
            NotEqual = 6,
            GreaterEqual = 7,
            Always = 8
        }

        private static void SetMaterialProperties(Material material, ref MaterialDescriptor descriptor)
        {
            if (descriptor.ShaderDescriptor.PassType == PassType.Opaque)
            {
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetInt(ShaderPropertyIDs.ZWrite, 1);
                material.SetInt(ShaderPropertyIDs.SrcBlend, (int)BlendMode.One);
                material.SetInt(ShaderPropertyIDs.DestBlend, (int)(BlendMode.Zero));

                material.SetInt(ShaderPropertyIDs.AdditiveLightingSrcBlend, (int)BlendMode.One);
                material.SetInt(ShaderPropertyIDs.AdditiveLightingDstBlend, (int)BlendMode.One);
            }
            else
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt(ShaderPropertyIDs.ZWrite, 0);
                BlendMode srcBlend = descriptor.ShaderDescriptor.PremulColors ? BlendMode.One : BlendMode.SrcAlpha;
                material.SetInt(ShaderPropertyIDs.SrcBlend, (int)srcBlend);
                material.SetInt(ShaderPropertyIDs.DestBlend, (int)(BlendMode.OneMinusSrcAlpha));

                material.SetInt(ShaderPropertyIDs.AdditiveLightingSrcBlend, (int)srcBlend);
                material.SetInt(ShaderPropertyIDs.AdditiveLightingDstBlend, (int)BlendMode.One);
            }

            material.SetInt(ShaderPropertyIDs.ZTest, (int)(descriptor.DisableZTest ? ZTestCompare.Disabled : ZTestCompare.LessEqual));

            if (descriptor.ShaderDescriptor.VisualType == VisualType.UIBlock3D && !descriptor.IsClip)
            {
                // Only cull backside for 3D blocks that aren't being clipped
                material.SetInt(ShaderPropertyIDs.CullMode, (int)CullMode.Back);
            }
            else
            {
                material.SetInt(ShaderPropertyIDs.CullMode, (int)CullMode.Off);
            }

            material.renderQueue = descriptor.RenderQueue;

            if (SystemSettings.UseFallbackRendering)
            {
                material.EnableKeyword(FallbackRenderingKeyword);
            }
            else
            {
                material.DisableKeyword(FallbackRenderingKeyword);
            }

            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.ClipRect, ClipRectKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.ClipMask, ClipMaskKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.DynamicImage, DynamicImageKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.StaticImage, StaticImageKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.InnerShadow, InnerShadowKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.SuperSample, SuperSampleKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.OuterBorder, OuterBorderKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.InnerBorder, InnerBorderKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.CenterBorder, CenterBorderKeyword);
            SetKeyword(material, descriptor.MaterialModifiers, MaterialModifier.RadialFill, RadialFillKeyword);
            material.enableInstancing = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetKeyword(Material material, MaterialModifier modifiers, MaterialModifier mask, string keyword)
        {
            if ((modifiers & mask) != 0)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static void AddMissingShaders()
        {
            if (ShadersToAdd.Length == 0)
            {
                return;
            }

            for (int i = 0; i < ShadersToAdd.Length; ++i)
            {
                ref ShaderDescriptor descriptor = ref ShadersToAdd.ElementAt(i);

#pragma warning disable CS0162 // Unreachable code detected
                if (NovaApplication.ConstIsEditor)
                {
                    if (NovaSettings.Config.ShouldLog(LogFlags.LightingModelNotIncludedInBuild) &&
                        !ShaderUtils.IsIncluded(descriptor.VisualType, descriptor.LightingModel))
                    {
                        Debug.LogWarning($"{descriptor.LightingModel.ToName()} lighting model being used on {descriptor.VisualType.ToBlockType().ToName()}, but it is not marked to be included in builds. If you wish to use this lighting model in builds, you can specify to include it in NovaSettings. {Constants.LogDisableMessage}");
                    }
                }
                else if (!ShaderUtils.IsIncluded(descriptor.VisualType, descriptor.LightingModel))
                {
                    // Shader wasn't included in build
                    Debug.LogError($"{descriptor.LightingModel.ToName()} lighting model being used on {descriptor.VisualType.ToBlockType().ToName()}, but it was not marked to be included in builds. You can specify to include it in NovaSettings.");
                }
#pragma warning restore CS0162 // Unreachable code detected

                Shader shader = CreateShader(ref descriptor);
                if (shader == null)
                {
                    continue;
                }

                CachedShaders.Add(descriptor, shaders.Count);
                shaders.Add(shader);
                shaderDescriptors.Add(descriptor);
            }

            ShadersToAdd.Clear();
        }

        private static Shader CreateShader(ref ShaderDescriptor descriptor)
        {
            string shaderName = descriptor.GetShaderName();
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"Could not find shader {shaderName}. Please ensure the project has been imported correctly");
            }
            return shader;
        }

        public static bool HandleTMPFontPropertyChanged(int instanceID, Material tmpMaterial)
        {
            bool isUsingFontMaterial = false;
            TextMaterialID materialID = TextMaterialID.Invalid;
            for (int i = 0; i < materials.Count; i++)
            {
                ValuePair<ShaderCacheIndex, MaterialDescriptor> vals = materialDescriptors[i];
                if (vals.Item2.TextMaterialID != instanceID)
                {
                    continue;
                }

                isUsingFontMaterial = true;
                Material material = materials[i];
                material.CopyPropertiesFromMaterial(tmpMaterial);
                materialID = vals.Item2.TextMaterialID;
                SetMaterialProperties(material, ref vals.Item2);
            }

            if (!isUsingFontMaterial)
            {
                return false;
            }

            // Only log if nova is actually using the material
            EnsureSupportedTMPShader(tmpMaterial, materialID);
            return true;
        }

        private static void EnsureSupportedTMPShader(Material material, TextMaterialID textMaterialID)
        {
            if (!NovaSettings.Config.ShouldLog(LogFlags.UnsupportedTextShader) ||
                IsSupportedTMPShader(material.shader))
            {
                return;
            }

            // If we got to here, it is not a supported shader
            DataStoreIndex dataStoreIndex = GetBlockUsingUnsupportedShader(textMaterialID);
            GameObject gameObject = null;

            if (dataStoreIndex.IsValid)
            {
                DataStoreID dataStoreID = HierarchyDataStore.Instance.IDToIndexMap.ToID(dataStoreIndex);
                gameObject = RenderingDataStore.Instance.Elements[dataStoreID].Transform.gameObject;
            }

            Debug.LogWarning($"Unsupported text shader [{material.shader.name}] being used, Nova only supports [{Constants.TMPSupportedShaderName}]. {Constants.LogDisableMessage}", gameObject);
        }

        private static Shader supportedTMPShader = null;

        public static Shader GetSupportedTMPShder()
        {
            if (supportedTMPShader != null)
            {
                return supportedTMPShader;
            }

            supportedTMPShader = Shader.Find(Constants.TMPSupportedShaderName);
            return supportedTMPShader;
        }

        public static bool IsSupportedTMPShader(Shader shader)
        {
            if (supportedTMPShader == null)
            {
                if (shader.name == Constants.TMPSupportedShaderName)
                {
                    supportedTMPShader = shader;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return supportedTMPShader == shader;
            }
        }

        private static DataStoreIndex GetBlockUsingUnsupportedShader(TextMaterialID textMaterialID)
        {
            NativeList<RenderIndex, TextBlockData> textBlockData = RenderingDataStore.Instance.TextBlockData.BlockData;
            for (int i = 0; i < textBlockData.Length; ++i)
            {
                ref TextBlockData blockData = ref textBlockData.ElementAt(i); ;
                for (int j = 0; j < blockData.MeshData.Length; ++j)
                {
                    if (!blockData.MeshData.ElementAt(j).MaterialID.Equals(textMaterialID))
                    {
                        continue;
                    }

                    // It matches, so return it
                    return RenderingDataStore.Instance.TextBlockData.DataStoreIndices[i];
                }
            }

            return DataStoreIndex.Invalid;
        }

        public static void Init()
        {
            CachedMaterials.Init(Constants.SomeElementsInitialCapacity);
            CachedShaders.Init(Constants.SomeElementsInitialCapacity);
            MaterialsToAdd.Init(Constants.SomeElementsInitialCapacity);
            ShadersToAdd.Init(Constants.SomeElementsInitialCapacity);
        }

        public static void Dispose()
        {
            CachedMaterials.Dispose();
            CachedShaders.Dispose();
            MaterialsToAdd.Dispose();
            ShadersToAdd.Dispose();

            for (int i = 0; i < materials.Count; ++i)
            {
                DestroyUtils.Destroy(materials[i]);
            }

            materials.Clear();
            shaders.Clear();
            materialDescriptors.Clear();
            shaderDescriptors.Clear();
        }
    }
}

