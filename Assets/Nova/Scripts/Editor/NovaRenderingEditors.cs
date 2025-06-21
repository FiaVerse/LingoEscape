// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using Nova.Editor.Tools;
using Nova.Editor.Utilities;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Nova.Editor.GUIs.NovaGUI;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    internal static class NovaRenderingEditors
    {
        private const string Visuals = "Visuals";
        private const string Body = "Body";

        public static void DrawBodyVisualsUI(float minHalfSize, _UIBlock2DData uiNode2DData, _Surface surface, _BaseRenderInfo baseInfo, ref ImageSelectionType imageMode, ref UIBlock2DData.Calculated calc)
        {
            using (Foldout bodyFoldout = NovaGUI.EditorPrefFoldoutHeader(Body, uiNode2DData.FillEnabledProp))
            {
                if (bodyFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

                    DrawUIBlock2DUI(uiNode2DData, ref calc);
                    DrawImageUI(uiNode2DData, ref imageMode);
                }
            }

            using (Foldout visualsFoldout = NovaGUI.EditorPrefFoldoutHeader(Visuals))
            {
                if (visualsFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

                    NovaGUI.ToggleField(Labels.Rendering.Visible, baseInfo.VisibleProp);
                    NovaGUI.LengthField(NovaGUI.Layout.GetControlRect(), Labels.UIBlock2D.CornerRadius, uiNode2DData.CornerRadius, calc.CornerRadius, min: 0, max: minHalfSize);
                    DrawRadialFillUI(uiNode2DData.RadialFill, calc.RadialFill);
                    DrawBaseInfoUI(baseInfo);
                    NovaGUI.ToggleField(Labels.UIBlock2D.SoftenEdges, uiNode2DData.SoftenEdgesProp);
                    DrawSurfaceUI(surface, false);
                }
            }
        }

        /// <summary>
        /// Text
        /// </summary>
        /// <param name="baseInfo"></param>
        /// <param name="surface"></param>
        public static void DrawBodyVisualsUI(_BaseRenderInfo baseInfo, _Surface surface, TMPProperties tmpProps)
        {
            using (Foldout bodyFoldout = NovaGUI.EditorPrefFoldoutHeader(Body))
            {
                if (bodyFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                    TextBlock textBlock = baseInfo.SerializedProperty.serializedObject.targetObject as TextBlock;
                    TMPFields(tmpProps);
                }
            }

            using (Foldout visualsFoldout = NovaGUI.EditorPrefFoldoutHeader(Visuals))
            {
                if (visualsFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                    NovaGUI.ToggleField(Labels.Rendering.Visible, baseInfo.VisibleProp);
                    DrawBaseInfoUI(baseInfo);
                    DrawSurfaceUI(surface, false);
                }
            }
        }

        public static void DrawBodyVisualsUI(Vector3 size, _UIBlock3DData uiNode3DData, _Surface surface, _BaseRenderInfo baseInfo, ref UIBlock3DData.Calculated calc)
        {
            using (Foldout bodyFoldout = NovaGUI.EditorPrefFoldoutHeader(Body))
            {
                if (bodyFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                    ColorField(Labels.UIBlock3D.Color, uiNode3DData.ColorProp);
                }
            }

            using (Foldout visualsFoldout = NovaGUI.EditorPrefFoldoutHeader(Visuals))
            {
                if (visualsFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

                    NovaGUI.ToggleField(Labels.Rendering.Visible, baseInfo.VisibleProp);
                    float minXY = Mathf.Min(size.x, size.y);
                    NovaGUI.LengthField(NovaGUI.Layout.GetControlRect(), Labels.UIBlock3D.CornerRadius, uiNode3DData.CornerRadius, calc.CornerRadius, min: 0, max: 0.5f * minXY);
                    NovaGUI.LengthField(NovaGUI.Layout.GetControlRect(), Labels.UIBlock3D.EdgeRadius, uiNode3DData.EdgeRadius, calc.EdgeRadius, min: 0, max: 0.5f * Mathf.Min(minXY, size.z));
                    DrawSurfaceUI(surface, true);
                }
            }
        }

        public static void DrawRadialFillUI(_RadialFill radialFill, RadialFill.Calculated calc)
        {
            NovaGUI.Layout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            bool expand = NovaGUI.PrefixFoldout(NovaEditorPrefs.DisplayExpandedRadialFill);
            if (EditorGUI.EndChangeCheck())
            {
                NovaEditorPrefs.DisplayExpandedRadialFill = expand;
            }

            Rect toggleRect = NovaGUI.Layout.GetControlRect();
            toggleRect.x -= Foldout.ArrowIconSize + NovaGUI.MinSpaceBetweenFields;
            NovaGUI.ToggleField(toggleRect, Labels.RadialFill.Enabled, radialFill.EnabledProp);

            NovaGUI.Layout.EndHorizontal();

            if (!expand)
            {
                return;
            }


            NovaGUI.Layout.BeginHorizontal(NovaGUI.Styles.InnerContent);
            NovaGUI.Space(4f / 3f);
            NovaGUI.Layout.BeginVertical();

            EditorGUI.BeginDisabledGroup(!radialFill.Enabled);

            NovaGUI.Length2Field(Labels.RadialFill.Center, radialFill.Center, calc.Center, MinMax2.Unclamped.Min, MinMax2.Unclamped.Max);
            NovaGUI.SliderField(Labels.RadialFill.Rotation, radialFill.RotationProp, min: -360f, max: 360f);
            NovaGUI.SliderField(Labels.RadialFill.FillAngle, radialFill.FillAngleProp, min: -360f, max: 360f);

            EditorGUI.EndDisabledGroup();

            NovaGUI.Layout.EndVertical();
            NovaGUI.Layout.EndHorizontal();
        }

        public static void DrawBorderUI(_Border borderData, Border.Calculated calc)
        {
            using (Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Border", borderData.EnabledProp))
            {
                using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                {
                    if (foldout)
                    {
                        ColorField(Labels.Border.Color, borderData.ColorProp);

                        // Width
                        NovaGUI.LengthField(NovaGUI.Layout.GetControlRect(), Labels.Border.Width, borderData.Width, calc.Width, min: 0);

                        // Direction
                        Rect directionPosition = NovaGUI.Layout.GetControlRect();
                        GUIContent label = EditorGUI.BeginProperty(directionPosition, Labels.Border.Direction, borderData.DirectionProp);
                        EditorGUI.BeginChangeCheck();
                        BorderDirection strokeDirection = (BorderDirection)EditorGUI.EnumPopup(directionPosition, label, borderData.Direction);
                        if (EditorGUI.EndChangeCheck())
                        {
                            borderData.Direction = strokeDirection;
                        }
                        EditorGUI.EndProperty();
                    }
                }
            }
        }

        public static void DrawShadowUI(_UIBlock2DData renderData, Shadow.Calculated calc)
        {
            using (Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Shadow", renderData.Shadow.EnabledProp))
            {
                if (!foldout)
                {
                    return;
                }

                using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);


                _Shadow shadowData = renderData.Shadow;
                Rect fieldRect = NovaGUI.Layout.GetControlRect();
                EditorGUI.BeginChangeCheck();
                GUIContent label = EditorGUI.BeginProperty(fieldRect, Labels.Shadow.Direction, shadowData.DirectionProp);
                ShadowDirection newDirection = (ShadowDirection)EditorGUI.EnumPopup(fieldRect, label, shadowData.Direction);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    shadowData.Direction = newDirection;
                }

                NovaGUI.ColorField(Labels.Shadow.Color, shadowData.ColorProp);
                NovaGUI.LengthField(NovaGUI.Layout.GetControlRect(), Labels.Shadow.Width, shadowData.Width, calc.Width);
                NovaGUI.LengthField(NovaGUI.Layout.GetControlRect(), Labels.Shadow.Blur, shadowData.Blur, calc.Blur, min: 0);
                NovaGUI.Length2Field(Labels.Shadow.Offset, shadowData.Offset, calc.Offset, MinMax2.Unclamped.Min, MinMax2.Unclamped.Max);
            }
        }

        public static void DrawBaseInfoUI(_BaseRenderInfo baseInfo)
        {
            using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

            Rect baseInfoField = NovaGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            GUIContent propertyLabel = EditorGUI.BeginProperty(baseInfoField, Labels.Rendering.ZIndex, baseInfo.ZIndexProp);
            short newRenderLayer = (short)EditorGUI.IntField(baseInfoField, propertyLabel, baseInfo.ZIndexProp.intValue);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                baseInfo.ZIndexProp.intValue = newRenderLayer;
            }
        }

        public static void DrawUIBlock2DUI(_UIBlock2DData data, ref UIBlock2DData.Calculated calc)
        {
            EditorGUI.BeginChangeCheck();
            NovaGUI.Layout.BeginVertical();
            NovaGUI.Layout.BeginHorizontal();
            Rect labelRect = NovaGUI.Layout.GetControlRect(GUILayout.Width(Foldout.ArrowIconSize));
            bool expandedColor = Foldout.FoldoutToggle(labelRect, NovaEditorPrefs.DisplayExpandedColor);
            if (EditorGUI.EndChangeCheck())
            {
                NovaEditorPrefs.DisplayExpandedColor = expandedColor;
            }

            NovaGUI.Space(-NovaGUI.Layout.FoldoutArrowIndentSpace);
            NovaGUI.ColorField(Labels.UIBlock2D.Color, data.ColorProp);
            NovaGUI.Layout.EndHorizontal();

            EditorGUILayout.Space(1);

            if (expandedColor)
            {
                GradientField(data.Gradient, calc.Gradient);
            }

            NovaGUI.Layout.EndVertical();
        }

        public static void DrawImageUI(_UIBlock2DData uiNode2DData, ref ImageSelectionType imageMode)
        {
            SerializedObject serializedObject = uiNode2DData.SerializedProperty.serializedObject;
            SerializedProperty textureProp = serializedObject.FindProperty(Names.UIBlock2D.texture);
            SerializedProperty spriteProp = serializedObject.FindProperty(Names.UIBlock2D.sprite);

            bool expandedImage = NovaEditorPrefs.DisplayExpandedImage;

            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                EditorGUI.BeginChangeCheck();
                NovaGUI.Layout.BeginVertical();
                NovaGUI.Layout.BeginHorizontal();
                Rect imageFieldRect = NovaGUI.Layout.GetControlRect();
                expandedImage = Foldout.FoldoutToggle(imageFieldRect, expandedImage);
                if (EditorGUI.EndChangeCheck())
                {
                    NovaEditorPrefs.DisplayExpandedImage = expandedImage;
                }

                imageFieldRect.xMax -= NovaGUI.ToggleToolbarFieldWidth + NovaGUI.MinSpaceBetweenFields;

                // The texture/sprite
                switch (imageMode)
                {
                    case ImageSelectionType.Texture:
                        {
                            _ImageAdjustment adjustment = uiNode2DData.Image.Adjustment;
                            if (adjustment.scaleMode == ImageScaleMode.Sliced && !adjustment.scaleModeProp.hasMultipleDifferentValues)
                            {
                                Rect warningRect = imageFieldRect;
                                warningRect.xMin += EditorGUIUtility.labelWidth;
                                NovaGUI.WarningIcon(warningRect, Labels.Image.SlicedWarningTooltip);
                            }

                            EditorGUI.BeginChangeCheck();
                            GUIContent label = EditorGUI.BeginProperty(imageFieldRect, Labels.Image.Label, textureProp);
                            Texture newVal = EditorGUI.ObjectField(imageFieldRect, label, textureProp.objectReferenceValue, typeof(Texture), false) as Texture;
                            EditorGUI.EndProperty();
                            bool changed = EditorGUI.EndChangeCheck();
                            if (!changed)
                            {
                                break;
                            }

                            if (newVal == null)
                            {
                                textureProp.objectReferenceValue = null;
                            }
                            else if (newVal is Texture2D || newVal is RenderTexture)
                            {
                                textureProp.objectReferenceValue = newVal;
                            }
                            else
                            {
                                Debug.LogError("Unsupported texture type. Texture must be a Texture2D or RenderTexture");
                                textureProp.objectReferenceValue = null;
                            }

                            break;
                        }
                    case ImageSelectionType.Sprite:
                        {
                            EditorGUI.BeginChangeCheck();
                            GUIContent label = EditorGUI.BeginProperty(imageFieldRect, Labels.Image.Label, spriteProp);
                            Sprite newVal = EditorGUI.ObjectField(imageFieldRect, label, spriteProp.objectReferenceValue, typeof(Sprite), false) as Sprite;
                            EditorGUI.EndProperty();
                            bool changed = EditorGUI.EndChangeCheck();
                            if (!changed)
                            {
                                break;
                            }

                            spriteProp.objectReferenceValue = newVal;

                            if (!(newVal is Sprite newSprite))
                            {
                                break;
                            }
                            break;
                        }
                }

                // Sprite vs Texture selector
                Rect toolbarRect = imageFieldRect;
                toolbarRect.x = toolbarRect.xMax + NovaGUI.MinSpaceBetweenFields;
                toolbarRect.width = NovaGUI.ToggleToolbarFieldWidth;
                EditorGUI.BeginChangeCheck();
                Rect toolbarPropertyRect = toolbarRect;
                toolbarPropertyRect.width += NovaGUI.SingleCharacterGUIWidth;
                ImageSelectionType newImageMode = NovaGUI.Toolbar(toolbarRect, imageMode, Labels.Image.TypeLabels);
                bool imageTypeChanged = EditorGUI.EndChangeCheck() && newImageMode != imageMode;
                NovaGUI.Layout.EndHorizontal();
                NovaGUI.Layout.EndVertical();

                if (imageTypeChanged)
                {
                    textureProp.objectReferenceValue = null;
                    spriteProp.objectReferenceValue = null;
                    uiNode2DData.Image.Adjustment.UVScale = Vector2.one;
                    uiNode2DData.Image.Adjustment.CenterUV = Vector2.zero;
                    imageMode = newImageMode;
                }

                if (expandedImage)
                {
                    EditorGUILayout.Space(1);
                    NovaGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                    NovaGUI.Layout.BeginHorizontal(NovaGUI.Styles.InnerContent);
                    NovaGUI.Space(1.5f);
                    NovaGUI.Layout.BeginVertical();

                    // Scale Mode
                    Rect scaleModeRect = NovaGUI.Layout.GetControlRect();
                    EditorGUI.BeginChangeCheck();
                    GUIContent scaleModeLabel = EditorGUI.BeginProperty(scaleModeRect, Labels.Image.ImageScaleMode, uiNode2DData.Image.Adjustment.scaleModeProp);
                    ImageScaleMode newScaleMode = (ImageScaleMode)EditorGUI.EnumPopup(scaleModeRect, scaleModeLabel, uiNode2DData.Image.Adjustment.scaleMode);
                    EditorGUI.EndProperty();
                    if (EditorGUI.EndChangeCheck())
                    {
                        uiNode2DData.Image.Adjustment.UVScale = Vector2.one;
                        uiNode2DData.Image.Adjustment.CenterUV = Vector2.zero;
                        uiNode2DData.Image.Adjustment.scaleMode = newScaleMode;
                    }

                    if (newScaleMode == ImageScaleMode.Manual)
                    {
                        NovaGUI.Vector2Field(Labels.Image.ImageCenter, uiNode2DData.Image.Adjustment.CenterUVProp);
                        NovaGUI.Vector2Field(Labels.Image.ImageScale, uiNode2DData.Image.Adjustment.UVScaleProp);
                    }
                    else if (newScaleMode == ImageScaleMode.Sliced || newScaleMode == ImageScaleMode.Tiled)
                    {
                        NovaGUI.FloatFieldClamped(Labels.Image.PixelsPerUnit, uiNode2DData.Image.Adjustment.PixelsPerUnitMultiplierProp, .01f, float.MaxValue);
                    }

                    if (NovaSettings.PackedImagesEnabled)
                    {
                        Rect renderModeField = NovaGUI.Layout.GetControlRect();
                        EditorGUI.BeginChangeCheck();
                        GUIContent renderModeLabel = EditorGUI.BeginProperty(renderModeField, Labels.Image.ImageMode, uiNode2DData.Image.ModeProp);
                        ImagePackMode startRenderingMode = uiNode2DData.Image.Mode;
                        ImagePackMode newRenderMode = (ImagePackMode)EditorGUI.EnumPopup(renderModeField, renderModeLabel, startRenderingMode);
                        EditorGUI.EndProperty();
                        if (EditorGUI.EndChangeCheck() && startRenderingMode != newRenderMode)
                        {
                            uiNode2DData.Image.Mode = newRenderMode;
                        }
                    }


                    NovaGUI.Layout.EndVertical();
                    NovaGUI.Layout.EndHorizontal();
                }


            }
        }

        private static void DrawDisabledSurfaceUI()
        {
            NovaGUI.WarningIcon(Labels.Surface.DisabledSurfaceSRPWarning);

            EditorGUI.BeginDisabledGroup(true);

            Rect presetRect = NovaGUI.Layout.GetControlRect();
            EditorGUI.EnumPopup(presetRect, Labels.Surface.SurfaceEffect, SurfacePreset.Unlit);

            EditorGUI.EndDisabledGroup();
        }

        private static void DrawSurfaceUI(_Surface surface, bool canReceiveShadows)
        {
            if (SystemSettings.UsingScriptableRenderPipeline)
            {
                DrawDisabledSurfaceUI();
                return;
            }

            NovaGUI.Layout.BeginHorizontal();

            bool expandedSurface = false;

            if (surface.LightingModel != LightingModel.Unlit)
            {
                EditorGUI.BeginChangeCheck();
                Rect labelRect = NovaGUI.Layout.GetControlRect(GUILayout.Width(Foldout.ArrowIconSize));
                expandedSurface = Foldout.FoldoutToggle(labelRect, NovaEditorPrefs.DisplayExpandedSurface);
                if (EditorGUI.EndChangeCheck())
                {
                    NovaEditorPrefs.DisplayExpandedSurface = expandedSurface;
                }

                NovaGUI.Space(-1f);
            }

            SurfacePreset preset = SurfaceDrawer.GetApproximatePreset(surface);

            Rect presetRect = NovaGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            bool showMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = surface.SerializedProperty.hasMultipleDifferentValues;
            preset = (SurfacePreset)EditorGUI.EnumPopup(presetRect, Labels.Surface.SurfaceEffect, preset);
            EditorGUI.showMixedValue = showMixed;
            if (EditorGUI.EndChangeCheck())
            {
                SurfaceDrawer.SetPreset(preset, surface);
            }

            NovaGUI.Layout.EndHorizontal();

            if (!expandedSurface || preset == SurfacePreset.Unlit || surface.LightingModelProp.hasMultipleDifferentValues)
            {
                return;
            }

            NovaGUI.Layout.BeginHorizontal(Styles.InnerContent);
            NovaGUI.Space(1.5f);
            NovaGUI.Layout.BeginVertical();

            Rect fieldRect = NovaGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            GUIContent lightingModelLabel = EditorGUI.BeginProperty(fieldRect, Labels.Surface.LightingModel, surface.LightingModelProp);
            LightingModel newLightingModel = (LightingModel)EditorGUI.EnumPopup(fieldRect, lightingModelLabel, surface.LightingModel);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                SurfaceDrawer.SetLightingModel(surface, newLightingModel);
            }

            Rect shadowCastRect = NovaGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            GUIContent shadowCastingLabel = EditorGUI.BeginProperty(shadowCastRect, Labels.Surface.ShadowCasting, surface.ShadowCastingModeProp);
            ShadowCastingMode newShadowCasting = (ShadowCastingMode)EditorGUI.EnumPopup(shadowCastRect, shadowCastingLabel, surface.ShadowCastingMode);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                surface.ShadowCastingMode = newShadowCasting;
            }

            if (canReceiveShadows)
            {
                Rect receiveShadowsRect = NovaGUI.Layout.GetControlRect();
                EditorGUI.BeginChangeCheck();
                GUIContent receiveShadowsLabel = EditorGUI.BeginProperty(receiveShadowsRect, Labels.Surface.ReceiveShadows, surface.ReceiveShadowsProp);
                bool recvShadows = EditorGUI.Toggle(receiveShadowsRect, receiveShadowsLabel, surface.ReceiveShadows);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    surface.ReceiveShadows = recvShadows;
                }
            }


            switch (newLightingModel)
            {
                case LightingModel.Lambert:
                    // Do nothing
                    break;
                case LightingModel.BlinnPhong:
                    NovaGUI.SliderField(Labels.Surface.Specular, surface.param1Prop, 0, 1);
                    NovaGUI.SliderField(Labels.Surface.Gloss, surface.param2Prop, 0, 1);
                    break;
                case LightingModel.Standard:
                    NovaGUI.SliderField(Labels.Surface.Metallic, surface.param2Prop, 0, 1);
                    NovaGUI.SliderField(Labels.Surface.Smoothness, surface.param1Prop, 0, 1);
                    break;
                case LightingModel.StandardSpecular:
                    NovaGUI.ColorField(Labels.Surface.SpecularColor, surface.specularColorProp, false);
                    NovaGUI.SliderField(Labels.Surface.Smoothness, surface.param1Prop, 0, 1);
                    break;
                default:
                    break;
            }

            NovaGUI.Layout.EndVertical();
            NovaGUI.Layout.EndHorizontal();
        }

        private static void GradientField(_RadialGradient gradientData, RadialGradient.Calculated calc)
        {
            NovaGUI.Layout.BeginHorizontal(NovaGUI.Styles.InnerContent);
            NovaGUI.Layout.BeginVertical();
            NovaGUI.Layout.BeginHorizontal();

            NovaGUI.Space(4f / 3f);

            EditorGUI.BeginChangeCheck();
            bool expandGradient = NovaGUI.PrefixFoldout(NovaEditorPrefs.DisplayExpandedGradient);
            if (EditorGUI.EndChangeCheck())
            {
                NovaEditorPrefs.DisplayExpandedGradient = expandGradient;
            }

            EditorGUI.BeginChangeCheck();

            Rect controlRect = NovaGUI.Layout.GetControlRect();
            Rect toggleRect = controlRect;
            toggleRect.width = NovaGUI.LabelWidth;
            toggleRect.width += NovaGUI.ToggleBoxSize;
            toggleRect.x -= NovaGUI.IndentSize + NovaGUI.MinSpaceBetweenFields;

            Rect colorFieldRect = controlRect;
            colorFieldRect.xMin = toggleRect.xMax;

            GUIContent gradientLabel = EditorGUI.BeginProperty(toggleRect, Labels.Gradient.Label, gradientData.EnabledProp);
            gradientData.Enabled = EditorGUI.Toggle(toggleRect, gradientLabel, gradientData.Enabled);
            EditorGUI.EndProperty();

            EditorGUI.BeginDisabledGroup(!gradientData.Enabled);

            NovaGUI.ColorField(colorFieldRect, Labels.Gradient.Color, gradientData.ColorProp);
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck() && gradientData.Enabled)
            {
                // Need to apply modifications because in 2022 will throw an error
                // if you try to set the tool when IsAvailable return false
                gradientData.ApplyModifications();
                UnityEditor.EditorTools.ToolManager.SetActiveTool<GradientTool>();
            }

            NovaGUI.Layout.EndHorizontal();

            if (expandGradient)
            {
                NovaGUI.Space(2 / NovaGUI.IndentSize);
                NovaGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                NovaGUI.Layout.BeginHorizontal(NovaGUI.Styles.InnerContent);
                NovaGUI.Space(2.5f);
                NovaGUI.Layout.BeginVertical();
                EditorGUI.BeginDisabledGroup(!gradientData.Enabled);

                NovaGUI.Length2Field(Labels.Gradient.Center, gradientData.Center, calc.Center, MinMax2.Unclamped.Min, MinMax2.Unclamped.Max);
                NovaGUI.Length2Field(Labels.Gradient.Radius, gradientData.Radius, calc.Radius, MinMax2.Positive.Min, MinMax2.Positive.Max);
                NovaGUI.FloatField(Labels.Gradient.Rotation, gradientData.RotationProp);

                EditorGUI.EndDisabledGroup();
                NovaGUI.Layout.EndVertical();
                NovaGUI.Layout.EndHorizontal();
            }

            NovaGUI.Layout.EndVertical();
            NovaGUI.Layout.EndHorizontal();
        }

        private static void TMPFields(TMPProperties tmpProps)
        {
            NovaGUI.Layout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool expandTextProperties = NovaGUI.PrefixFoldout(NovaEditorPrefs.DisplayExpandedText);
            NovaGUI.Space(-NovaGUI.Layout.FoldoutArrowIndentSpace);
            if (EditorGUI.EndChangeCheck())
            {
                NovaEditorPrefs.DisplayExpandedText = expandTextProperties;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginChangeCheck();
            Rect textField = NovaGUI.Layout.GetControlRect();

            string text = null;
            using (var scope = tmpProps.TextDiffer ? MixedValueScope.Create() : default)
            {
                text = EditorGUI.TextField(textField, Labels.TMP.Text, tmpProps.Text);
            }

            if (EditorGUI.EndChangeCheck())
            {
                tmpProps.Text = text;
            }

            EditorGUILayout.LabelField(Labels.TMP.Info, GUILayout.Width(NovaGUI.ToggleBoxSize));
            NovaGUI.Layout.EndHorizontal();
            if (expandTextProperties)
            {
                NovaGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                NovaGUI.Layout.BeginHorizontal(NovaGUI.Styles.InnerContent);
                NovaGUI.Space(1.5f);
                NovaGUI.Layout.BeginVertical();

                int previousX = tmpProps.HorizontalAlignmentDiffer ? -1000 : AlignmentFromTMPAlignment(tmpProps.HorizontalAlignment);
                int previousY = tmpProps.VerticalAlignmentDiffer ? -1000 : AlignmentFromTMPAlignment(tmpProps.VerticalAlignment);
                EditorGUI.BeginChangeCheck();
                (int xAlignment, int yAlignment) = NovaLayoutEditors.AlignmentField(Labels.TMP.Alignment, previousX, previousY, Labels.TMPAlignment);
                if (EditorGUI.EndChangeCheck())
                {
                    // since we aren't 1:1 with TMP's alignment options, only write when the specific axis field is modified
                    if (xAlignment != previousX)
                    {
                        tmpProps.HorizontalAlignment = AlignmentToTMPHorizontal(xAlignment);
                    }

                    if (yAlignment != previousY)
                    {
                        tmpProps.VerticalAlignment = AlignmentToTMPVertical(yAlignment);
                    }
                }

                EditorGUI.BeginChangeCheck();
                Rect colorField = NovaGUI.Layout.GetControlRect();
                Color color = NovaGUI.ColorField(colorField, Labels.TMP.Color, tmpProps.Color, tmpProps.ColorDiffer);
                if (EditorGUI.EndChangeCheck())
                {
                    tmpProps.Color = color;
                }

                EditorGUI.BeginChangeCheck();
                Rect fontField = NovaGUI.Layout.GetControlRect();
                TMPro.TMP_FontAsset font = null;
                using (var scope = tmpProps.FontDiffer ? MixedValueScope.Create() : default)
                {
                    font = EditorGUI.ObjectField(fontField, Labels.TMP.Font, tmpProps.Font, typeof(TMPro.TMP_FontAsset), allowSceneObjects: false) as TMPro.TMP_FontAsset;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    DoUnsupportedTMPShaderDialogue(font);
                    tmpProps.Font = font;
                }

                EditorGUI.BeginChangeCheck();
                Rect floatField = NovaGUI.Layout.GetControlRect();
                float fontSize = 0f;
                using (var scope = tmpProps.FontSizeDiffer ? MixedValueScope.Create() : default)
                {
                    fontSize = EditorGUI.FloatField(floatField, Labels.TMP.FontSize, tmpProps.FontSize);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    tmpProps.FontSize = fontSize;
                }

                NovaGUI.Layout.EndVertical();
                NovaGUI.Layout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditModeUtils.QueueEditorUpdateNextFrame();
            }
        }

        private static void DoUnsupportedTMPShaderDialogue(TMPro.TMP_FontAsset font)
        {
            if (MaterialCache.IsSupportedTMPShader(font.material.shader))
            {
                // It's supported
                return;
            }


            bool changeShader = EditorUtility.DisplayDialog("Unsupported TMP Shader", $"The provided font is using an unsupported TMP shader: [{font.material.shader.name}]. Nova currently only supports the [{Constants.TMPSupportedShaderName}] shader.\n\nWould you like to change the font over to use the supported shader?", "Yes", "No");

            if (!changeShader)
            {
                return;
            }

            Shader supportedShader = MaterialCache.GetSupportedTMPShder();
            if (supportedShader == null)
            {
                Debug.LogWarning($"Failed to change the shader to {Constants.TMPSupportedShaderName} on the font {font.name}. Please change it manually.", font);
                return;
            }

            font.material.shader = supportedShader;
        }

        private static int AlignmentFromTMPAlignment(TMPro.HorizontalAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TMPro.HorizontalAlignmentOptions.Left:
                    return -1;
                case TMPro.HorizontalAlignmentOptions.Right:
                    return 1;
            }

            return 0;
        }

        private static TMPro.HorizontalAlignmentOptions AlignmentToTMPHorizontal(int alignment)
        {
            switch (alignment)
            {
                case -1:
                    return TMPro.HorizontalAlignmentOptions.Left;
                case 1:
                    return TMPro.HorizontalAlignmentOptions.Right;
            }

            return TMPro.HorizontalAlignmentOptions.Center;
        }

        private static int AlignmentFromTMPAlignment(TMPro.VerticalAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TMPro.VerticalAlignmentOptions.Bottom:
                    return -1;
                case TMPro.VerticalAlignmentOptions.Top:
                    return 1;
            }

            return 0;
        }

        private static TMPro.VerticalAlignmentOptions AlignmentToTMPVertical(int alignment)
        {
            switch (alignment)
            {
                case -1:
                    return TMPro.VerticalAlignmentOptions.Bottom;
                case 1:
                    return TMPro.VerticalAlignmentOptions.Top;
            }

            return TMPro.VerticalAlignmentOptions.Middle;
        }
    }
}

