// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using Nova.Internal.Rendering;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [Obfuscation]
    internal enum SurfacePreset
    {
        Unlit,
        Matte,
        Plastic,
        Glossy,
        Rubber,
        PolishedMetal,
        BrushedMetal,
    }

    [CustomPropertyDrawer(typeof(Surface))]
    internal class SurfaceDrawer : NovaPropertyDrawer<_Surface>
    {
        private const float MetallicPresetThreshold = 0.8f;
        private const float GlossyPresetThreshold = 0.5f;

        protected override float GetPropertyHeight(GUIContent label)
        {
            if (!foldout)
            {
                return EditorGUI.GetPropertyHeight(wrapper.SerializedProperty, label, false);
            }

            switch (wrapper.LightingModel)
            {
                case LightingModel.Unlit:
                    return 2f * PropertyDrawerUtils.SingleLineHeight;
                case LightingModel.Lambert:
                    return 4f * PropertyDrawerUtils.SingleLineHeight;
                case LightingModel.BlinnPhong:
                case LightingModel.Standard:
                case LightingModel.StandardSpecular:
                default:
                    return 6f * PropertyDrawerUtils.SingleLineHeight;
            }
        }

        private bool foldout = true;
        protected override void OnGUI(Rect position, GUIContent label)
        {
            Rect labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth;
            foldout = EditorGUI.Foldout(labelRect, foldout, label, true);

            EditorGUI.BeginChangeCheck();

            Rect presetRect = position;
            presetRect.width -= EditorGUIUtility.labelWidth;
            presetRect.x += EditorGUIUtility.labelWidth;

            bool showMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = wrapper.SerializedProperty.hasMultipleDifferentValues;

            SurfacePreset preset = (SurfacePreset)EditorGUI.EnumPopup(presetRect, GetApproximatePreset(wrapper));

            EditorGUI.showMixedValue = showMixed;

            if (EditorGUI.EndChangeCheck())
            {
                SetPreset(preset, wrapper);
            }

            if (!foldout)
            {
                EditorGUI.EndFoldoutHeaderGroup();
                return;
            }

            position.BumpLine();

            EditorGUI.indentLevel++;

            EditorGUI.PropertyField(position, wrapper.LightingModelProp);

            switch (wrapper.LightingModel)
            {
                case LightingModel.BlinnPhong:
                    position.BumpLine();
                    EditorGUI.PropertyField(position, wrapper.param1Prop, Labels.Surface.Specular);
                    position.BumpLine();
                    EditorGUI.PropertyField(position, wrapper.param2Prop, Labels.Surface.Gloss);
                    break;
                case LightingModel.Standard:
                    position.BumpLine();
                    EditorGUI.PropertyField(position, wrapper.param1Prop, Labels.Surface.Smoothness);
                    position.BumpLine();
                    EditorGUI.PropertyField(position, wrapper.param2Prop, Labels.Surface.Metallic);
                    break;
                case LightingModel.StandardSpecular:
                    position.BumpLine();
                    EditorGUI.PropertyField(position, wrapper.specularColorProp, Labels.Surface.SpecularColor);
                    position.BumpLine();
                    EditorGUI.PropertyField(position, wrapper.param1Prop, Labels.Surface.Smoothness);
                    break;
            }

            if (wrapper.LightingModel != LightingModel.Unlit)
            {
                position.BumpLine();
                EditorGUI.PropertyField(position, wrapper.ShadowCastingModeProp);
                position.BumpLine();
                EditorGUI.PropertyField(position, wrapper.ReceiveShadowsProp);
            }

            EditorGUI.indentLevel--;

            EditorGUI.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// Retrieve the approximate <see cref="SurfacePreset"/> based on the given surface configuration.
        /// It's "approximate" because there's a default value for each preset, but the given configuration doesn't need to match the default 100%.
        /// </summary>
        /// <param name="surface"></param>
        /// <returns></returns>
        public static SurfacePreset GetApproximatePreset(_Surface surface)
        {
            LightingModel lightingModel = surface.LightingModel;
            float param1 = surface.param1;
            float param2 = surface.param2;
            Color specularColor = surface.specularColor;

            switch (lightingModel)
            {
                case LightingModel.Lambert:
                    return SurfacePreset.Matte;
                case LightingModel.BlinnPhong:
                    return SurfacePreset.Plastic;
                case LightingModel.Standard:
                    Standard standard = new Standard(param1, param2);

                    if (standard.Metallic > MetallicPresetThreshold)
                    {
                        return standard.Smoothness > GlossyPresetThreshold ? SurfacePreset.PolishedMetal : SurfacePreset.BrushedMetal;
                    }

                    return standard.Smoothness > GlossyPresetThreshold ? SurfacePreset.Glossy : SurfacePreset.Rubber;

                case LightingModel.StandardSpecular:
                    StandardSpecular standardSpecular = new StandardSpecular(specularColor, param1);

                    Color.RGBToHSV(specularColor, out float h, out float s, out float metallic);

                    if (metallic > MetallicPresetThreshold)
                    {
                        return standardSpecular.Smoothness > GlossyPresetThreshold ? SurfacePreset.PolishedMetal : SurfacePreset.BrushedMetal;
                    }

                    return standardSpecular.Smoothness > GlossyPresetThreshold ? SurfacePreset.Glossy : SurfacePreset.Rubber;
            }

            return SurfacePreset.Unlit;
        }

        public static void SetLightingModel(_Surface surface, LightingModel lightingModel)
        {
            VisualType visualType = VisualType.Invalid;
            switch (surface.SerializedProperty.serializedObject.targetObject)
            {
                case UIBlock2D uIBlock2D:
                    visualType = VisualType.UIBlock2D;
                    break;
                case TextBlock textBlock:
                    visualType = VisualType.TextBlock;
                    break;
                case UIBlock3D uIBlock3D:
                    visualType = VisualType.UIBlock3D;
                    break;
            }

            Internal.LightingModel internalLightingModel = (Internal.LightingModel)lightingModel;
            if (visualType != VisualType.Invalid && !ShaderUtils.IsIncluded(visualType, internalLightingModel))
            {
                bool addModel = EditorUtility.DisplayDialog("Lighting Model Not Included", $"The {internalLightingModel.ToName()} lighting model is not marked to be included in builds for {visualType.ToShaderName()}s. Would you like to include it?", "Yes", "No");

                if (addModel)
                {
                    switch (visualType)
                    {
                        case VisualType.UIBlock2D:
                            NovaSettings.UIBlock2DLightingModels |= (LightingModelBuildFlag)internalLightingModel.ToBuildFlag();
                            break;
                        case VisualType.UIBlock3D:
                            NovaSettings.UIBlock3DLightingModels |= (LightingModelBuildFlag)internalLightingModel.ToBuildFlag();
                            break;
                        case VisualType.TextBlock:
                            NovaSettings.TextBlockLightingModels |= (LightingModelBuildFlag)internalLightingModel.ToBuildFlag();
                            break;
                    }
                }
            }

            surface.LightingModel = lightingModel;
        }

        public static void SetPreset(SurfacePreset preset, _Surface surface)
        {
            switch (preset)
            {
                case SurfacePreset.Unlit:
                    SetLightingModel(surface, LightingModel.Unlit);
                    break;
                case SurfacePreset.Matte:
                    SetLightingModel(surface, LightingModel.Lambert);
                    break;
                case SurfacePreset.Plastic:
                    Surface plastic = Surface.Plastic(shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                    SetLightingModel(surface, plastic.LightingModel);


                    BlinnPhong blinnPhong = plastic.BlinnPhong;
                    surface.param1 = blinnPhong.Specular;
                    surface.param2 = blinnPhong.Gloss;
                    break;
                case SurfacePreset.Glossy:
                    if (surface.LightingModel == LightingModel.StandardSpecular)
                    {
                        Surface glossy = Surface.Glossy(surface.specularColor, shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, glossy.LightingModel);

                        StandardSpecular standardSpecular = glossy.StandardSpecular;
                        surface.param1 = standardSpecular.Smoothness;

                        Color.RGBToHSV(standardSpecular.SpecularColor, out float h, out float s, out float metallic);
                        surface.param2 = metallic;
                        surface.specularColor = standardSpecular.SpecularColor;
                    }
                    else
                    {
                        Surface glossy = Surface.Glossy(shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, glossy.LightingModel);

                        Standard standard = glossy.Standard;
                        surface.param1 = standard.Smoothness;
                        surface.param2 = standard.Metallic;
                    }
                    break;
                case SurfacePreset.Rubber:
                    if (surface.LightingModel == LightingModel.StandardSpecular)
                    {
                        Surface rubber = Surface.Rubber(surface.specularColor, shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, rubber.LightingModel);

                        StandardSpecular standardSpecular = rubber.StandardSpecular;
                        surface.param1 = standardSpecular.Smoothness;

                        Color.RGBToHSV(standardSpecular.SpecularColor, out float h, out float s, out float metallic);
                        surface.param2 = metallic;
                        surface.specularColor = standardSpecular.SpecularColor;
                    }
                    else
                    {
                        Surface rubber = Surface.Rubber(shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, rubber.LightingModel);

                        Standard standard = rubber.Standard;
                        surface.param1 = standard.Smoothness;
                        surface.param2 = standard.Metallic;
                    }
                    break;
                case SurfacePreset.PolishedMetal:
                    if (surface.LightingModel == LightingModel.StandardSpecular)
                    {
                        Surface polishedMetal = Surface.PolishedMetal(surface.specularColor, shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, polishedMetal.LightingModel);

                        StandardSpecular standardSpecular = polishedMetal.StandardSpecular;
                        surface.param1 = standardSpecular.Smoothness;

                        Color.RGBToHSV(standardSpecular.SpecularColor, out float h, out float s, out float metallic);
                        surface.param2 = metallic;
                        surface.specularColor = standardSpecular.SpecularColor;
                    }
                    else
                    {
                        Surface polishedMetal = Surface.PolishedMetal(shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, polishedMetal.LightingModel);

                        Standard standard = polishedMetal.Standard;
                        surface.param1 = standard.Smoothness;
                        surface.param2 = standard.Metallic;
                    }
                    break;
                case SurfacePreset.BrushedMetal:
                    if (surface.LightingModel == LightingModel.StandardSpecular)
                    {
                        Surface brushedMetal = Surface.BrushedMetal(surface.specularColor, shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, brushedMetal.LightingModel);

                        StandardSpecular standardSpecular = brushedMetal.StandardSpecular;
                        surface.param1 = standardSpecular.Smoothness;

                        Color.RGBToHSV(standardSpecular.SpecularColor, out float h, out float s, out float metallic);
                        surface.param2 = metallic;
                        surface.specularColor = standardSpecular.SpecularColor;
                    }
                    else
                    {
                        Surface brushedMetal = Surface.BrushedMetal(shadowCasting: surface.ShadowCastingMode, receiveShadows: surface.ReceiveShadows);
                        SetLightingModel(surface, brushedMetal.LightingModel);

                        Standard standard = brushedMetal.Standard;
                        surface.param1 = standard.Smoothness;
                        surface.param2 = standard.Metallic;
                    }
                    break;
            }
        }
    }
}