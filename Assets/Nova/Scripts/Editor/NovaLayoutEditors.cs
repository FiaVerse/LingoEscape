// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Editor.Serialization;
using Nova.Editor.Tools;
using Nova.Editor.Utilities;
using Nova.Extensions;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    internal static class NovaLayoutEditors
    {
        private static bool ScreenSpaceControlsSize(UIBlock uiBlock)
        {
            if (uiBlock.TryGetComponent(out ScreenSpace screenSpace) &&
                screenSpace.enabled &&
                screenSpace.Mode != ScreenSpace.FillMode.Manual)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool ScreenSpaceControlsPosition(UIBlock uiBlock)
        {
            if (uiBlock.TryGetComponent(out ScreenSpace screenSpace) &&
                screenSpace.enabled)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void DrawSizeUI(_Layout layout, UIBlock uiBlock, SerializedProperty previewSize)
        {
            using (Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Size"))
            {
                if (!foldout)
                {
                    return;
                }

                using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                {
                    bool screenSpaceControlsSize = ScreenSpaceControlsSize(uiBlock);

                    EditorGUI.BeginDisabledGroup(screenSpaceControlsSize);

                    NovaGUI.AspectRatioField(layout, uiBlock, NovaGUI.ShowZAxisValues(uiBlock) ? Labels.LockAspectToolbarLabels3D : Labels.LockAspectToolbarLabels2D);

                    if (uiBlock.Parent != null && !(uiBlock.Parent is TextBlock))
                    {
                        ConflictingPropertyMessage(uiBlock.Parent.AutoSize, uiBlock.AutoSize, uiBlock.Size.Type);
                    }

                    ThreeD<bool> aspectLocked = layout.AspectRatioAxis == new ThreeD<Axis>(Axis.X, Axis.Y, Axis.Z);

                    _AutoSize3 autoSize = new _AutoSize3() { SerializedProperty = layout.AutoSizeProp };
                    EditorGUI.BeginChangeCheck();
                    AutoSizeField(autoSize, aspectLocked, NovaGUI.ShowZAxisValues(uiBlock));
                    if (EditorGUI.EndChangeCheck())
                    {
                        ThreeD<AutoSize> auto = new ThreeD<AutoSize>((AutoSize)autoSize.X, (AutoSize)autoSize.Y, (AutoSize)autoSize.Z);


                        ApplyAutosizeTypeChanges(layout.Size.X, auto.X);
                        ApplyAutosizeTypeChanges(layout.Size.Y, auto.Y);
                        ApplyAutosizeTypeChanges(layout.Size.Z, auto.Z);
                    }

                    ThreeD<bool> autosizeDisables = Util.Or(uiBlock.AutoSize == AutoSize.Expand, uiBlock.AutoSize == AutoSize.Shrink);

                    ThreeD<bool> sizeDisabled = new ThreeD<bool>(autosizeDisables.X || Util.Any(aspectLocked.YZ),
                                                                 autosizeDisables.Y || Util.Any(aspectLocked.XZ),
                                                                 autosizeDisables.Z || Util.Any(aspectLocked.XY));

                    EditorGUI.BeginChangeCheck();
                    ThreeD<LengthType> prevTypes = new ThreeD<LengthType>(layout.Size.X.Type, layout.Size.Y.Type, layout.Size.Z.Type);

                    bool show = NovaGUI.Length3Field(Labels.Size.Label, layout.Size, layout.SizeMinMax, uiBlock.CalculatedSize, sizeDisabled, NovaGUI.ShowZAxisValues(uiBlock), NovaEditorPrefs.DisplayMinMaxSize);

                    EditorGUI.EndDisabledGroup();

                    if (EditorGUI.EndChangeCheck())
                    {
                        NovaEditorPrefs.DisplayMinMaxSize = show;

                        if (layout.SerializedProperty.serializedObject.targetObjects.Length == 1)
                        {
                            if (uiBlock.Parent == null && SceneViewUtils.IsInCurrentPrefabStage(uiBlock.gameObject))
                            {
                                ThreeD<LengthType> newTypes = new ThreeD<LengthType>(layout.Size.X.Type, layout.Size.Y.Type, layout.Size.Z.Type);

                                Vector3 previousSize = uiBlock.CalculatedSize.Percent;

                                ThreeD<bool> changed = newTypes != prevTypes;

                                Vector3 previewSizeScalar = new Vector3(previousSize.x == 0 ? 0 : changed.X ? 1 : layout.Size.X.Raw / previousSize.x,
                                                                        previousSize.y == 0 ? 0 : changed.Y ? 1 : layout.Size.Y.Raw / previousSize.y,
                                                                        previousSize.z == 0 ? 0 : changed.Z ? 1 : layout.Size.Z.Raw / previousSize.z);

                                Vector3 currentPreviewSize = previewSize.vector3Value;

                                Vector3 preview = new Vector3(newTypes.X == LengthType.Value && prevTypes.X == LengthType.Value ? layout.Size.X.Raw : previewSizeScalar.x * currentPreviewSize.x,
                                                              newTypes.Y == LengthType.Value && prevTypes.Y == LengthType.Value ? layout.Size.Y.Raw : previewSizeScalar.y * currentPreviewSize.y,
                                                              newTypes.Z == LengthType.Value && prevTypes.Z == LengthType.Value ? layout.Size.Z.Raw : previewSizeScalar.z * currentPreviewSize.z);

                                previewSize.vector3Value = preview;
                            }
                        }
                    }

                    NovaGUI.ToggleField(Labels.Size.RotateSize, layout.RotateSizeProp);
                }
            }
        }

        private static void ApplyAutosizeTypeChanges(_Length length, AutoSize autoSize)
        {
            if (autoSize == AutoSize.None)
            {
                return;
            }

            LengthType type = autoSize == AutoSize.Expand ? LengthType.Percent : LengthType.Value;

            length.Type = type;
            length.SerializedProperty.serializedObject.ApplyModifiedProperties();

            Object[] targets = length.SerializedProperty.serializedObject.targetObjects;

            for (int i = 0; i < targets.Length; ++i)
            {
                float rawLength = NovaGUI.CalculatedLengthFromPropertyPath(targets[i] as UIBlock, length.SerializedProperty.propertyPath, type);
                NovaGUI.SetRawLength(targets[i], length.RawProp.propertyPath, rawLength);
            }
        }

        public static void DrawPositionUI(_Layout layout, UIBlock uiBlock)
        {
            using (Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Position"))
            {
                if (!foldout)
                {
                    return;
                }

                EditorGUI.BeginDisabledGroup(ScreenSpaceControlsPosition(uiBlock));

                // Go through the hierarchy here. Parent might be virtual
                IUIBlock parentBlock = uiBlock.GetParentBlock();
                bool parentAutoLayoutEnabled = parentBlock != null && parentBlock.SerializedAutoLayout.Enabled;

                int parentLayoutAxis = parentAutoLayoutEnabled ? parentBlock.SerializedAutoLayout.Axis.Index() : -1;
                int parentCrossAxis = parentAutoLayoutEnabled && parentBlock.SerializedAutoLayout.Cross.Enabled ? parentBlock.SerializedAutoLayout.Cross.Axis.Index() : -1;

                ThreeD<bool> disabled = new ThreeD<bool>(parentLayoutAxis == Axis.X.Index() || parentCrossAxis == Axis.X.Index(),
                                                         parentLayoutAxis == Axis.Y.Index() || parentCrossAxis == Axis.Y.Index(),
                                                         parentLayoutAxis == Axis.Z.Index() || parentCrossAxis == Axis.Z.Index());

                AlignmentField(layout.Alignment, disabled, NovaGUI.ShowZAxisValues(uiBlock));

                EditorGUI.BeginChangeCheck();
                bool show = NovaGUI.Length3Field(Labels.Position.Label, layout.Position, layout.PositionMinMax, uiBlock.CalculatedPosition, disabled, NovaGUI.ShowZAxisValues(uiBlock), NovaEditorPrefs.DisplayMinMaxPosition);
                if (EditorGUI.EndChangeCheck())
                {
                    NovaEditorPrefs.DisplayMinMaxPosition = show;

                    if (!UnityEditor.EditorTools.ToolManager.IsActiveTool(UIBlockTool.Instance))
                    {
                        UnityEditor.EditorTools.ToolManager.SetActiveTool<UIBlockTool>();
                    }
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        public static void AlignmentField(_Alignment alignment, ThreeD<bool> disabled, bool zField)
        {
            float labelWidth = NovaGUI.LabelWidth;

            NovaGUI.Layout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(false);
            NovaGUI.PrefixLabel(Labels.Position.Alignment, alignment.SerializedProperty);
            EditorGUI.EndDisabledGroup();

            NovaGUI.Layout.GetXYZFieldRects(zField, out Rect x, out Rect y, out Rect z);

            NovaGUI.LabelWidth = NovaGUI.SingleCharacterGUIWidth;
            EditorGUI.BeginDisabledGroup(disabled.X);
            AlignmentField(x, Labels.X, alignment.XProp, Labels.Alignment[0]);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(disabled.Y);
            AlignmentField(y, Labels.Y, alignment.YProp, Labels.Alignment[1]);
            EditorGUI.EndDisabledGroup();

            if (zField)
            {
                EditorGUI.BeginDisabledGroup(disabled.Z);
                AlignmentField(z, Labels.Z, alignment.ZProp, Labels.Alignment[2]);
                EditorGUI.EndDisabledGroup();
            }

            NovaGUI.Layout.EndHorizontal();

            NovaGUI.LabelWidth = labelWidth;
        }

        public static (int, int) AlignmentField(GUIContent label, int xAlignment, int yAlignment, GUIContent[][] axisIcons)
        {
            float labelWidth = NovaGUI.LabelWidth;

            NovaGUI.Layout.BeginHorizontal();

            NovaGUI.PrefixLabel(label);

            NovaGUI.Layout.GetXYZFieldRects(false, out Rect x, out Rect y, out Rect z);

            NovaGUI.LabelWidth = NovaGUI.SingleCharacterGUIWidth;

            xAlignment = AlignmentField(x, Labels.X, axisIcons[0], xAlignment);
            yAlignment = AlignmentField(y, Labels.Y, axisIcons[1], yAlignment);

            NovaGUI.Layout.EndHorizontal();

            NovaGUI.LabelWidth = labelWidth;

            return (xAlignment, yAlignment);
        }

        public static void AlignmentField(Rect position, GUIContent label, SerializedProperty property, GUIContent[] axisIcons)
        {
            GUIContent propertyLabel = EditorGUI.BeginProperty(position, label, property);
            if (label != null)
            {
                EditorGUI.PrefixLabel(position, propertyLabel);
                float labeWidth = EditorStyles.label.CalcSize(propertyLabel).x + NovaGUI.MinSpaceBetweenFields;
                position.x += labeWidth;
                position.width -= labeWidth;
            }

            int aligmentValue = property.hasMultipleDifferentValues ? -1000 : property.intValue + 1;

            EditorGUI.BeginChangeCheck();
            int selected = NovaGUI.Toolbar(position, aligmentValue, axisIcons) - 1;
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = selected;
            }

            EditorGUI.EndProperty();
        }

        public static int AlignmentField(Rect position, GUIContent label, GUIContent[] axisIcons, int alignment)
        {
            if (label != null)
            {
                EditorGUI.PrefixLabel(position, label);
                float labeWidth = EditorStyles.label.CalcSize(label).x + NovaGUI.MinSpaceBetweenFields;
                position.x += labeWidth;
                position.width -= labeWidth;
            }

            int selected = NovaGUI.Toolbar(position, alignment + 1, axisIcons) - 1;

            return selected;
        }

        public static void AutoSizeField(_AutoSize3 autosize, ThreeD<bool> aspectLocked, bool zField)
        {
            float labelWidth = NovaGUI.LabelWidth;

            NovaGUI.Layout.BeginHorizontal();

            NovaGUI.PrefixLabel(Labels.Size.AutoSize, autosize.SerializedProperty);

            NovaGUI.Layout.GetXYZFieldRects(zField, out Rect x, out Rect y, out Rect z);

            bool lockAspectX = aspectLocked.X;
            bool lockAspectY = aspectLocked.Y;
            bool lockAspectZ = aspectLocked.Z;

            NovaGUI.LabelWidth = NovaGUI.SingleCharacterGUIWidth;
            EditorGUI.BeginDisabledGroup(!lockAspectX && (lockAspectY || lockAspectZ));
            AutoSizeField(x, Axis.X.Index(), Labels.X, autosize.XProp);
            EditorGUI.EndDisabledGroup();
            NovaGUI.LabelWidth = NovaGUI.SingleCharacterGUIWidth;
            EditorGUI.BeginDisabledGroup(!lockAspectY && (lockAspectX || lockAspectZ));
            AutoSizeField(y, Axis.Y.Index(), Labels.Y, autosize.YProp);
            EditorGUI.EndDisabledGroup();

            if (zField)
            {
                EditorGUI.BeginDisabledGroup(!lockAspectZ && (lockAspectX || lockAspectY));
                AutoSizeField(z, Axis.Z.Index(), Labels.Z, autosize.ZProp);
                EditorGUI.EndDisabledGroup();
            }

            NovaGUI.Layout.EndHorizontal();


            NovaGUI.LabelWidth = labelWidth;
        }

        public static void AutoSizeField(Rect position, int axis, GUIContent label, SerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            GUIContent propertyLabel = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PrefixLabel(position, label);

            float labeWidth = EditorStyles.label.CalcSize(propertyLabel).x + NovaGUI.MinSpaceBetweenFields;
            position.x += labeWidth;
            position.width -= labeWidth;

            int autoSizeValue = property.hasMultipleDifferentValues ? -1000 : property.intValue - 1;

            int autosize = NovaGUI.Toolbar(position, autoSizeValue, Labels.AutoSize[axis], position.width * 0.5f, toggleToDeselect: true) + 1;
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = autosize;
            }
        }

        public static void DrawPaddingMarginUI(_Layout layout, UIBlock uiBlock)
        {
            using (Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Padding & Margin"))
            {
                if (!foldout)
                {
                    return;
                }

                using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                {
                    EditorGUI.BeginChangeCheck();
                    (bool showPaddingSides, bool showPaddingRange) = NovaGUI.LengthBoundsField(Labels.PaddingAndMargin.Padding, layout.Padding, layout.PaddingMinMax, uiBlock.CalculatedPadding, NovaGUI.ShowZAxisValues(uiBlock), NovaEditorPrefs.DisplaySidesPadding, NovaEditorPrefs.DisplayMinMaxPadding);
                    if (EditorGUI.EndChangeCheck())
                    {
                        NovaEditorPrefs.DisplaySidesPadding = showPaddingSides;
                        NovaEditorPrefs.DisplayMinMaxPadding = showPaddingRange;

                        if (!UnityEditor.EditorTools.ToolManager.IsActiveTool(Tools.UITool.Instance))
                        {
                            UnityEditor.EditorTools.ToolManager.SetActiveTool<SpacingTool>();
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    (bool showMarginSides, bool showMarginRange) = NovaGUI.LengthBoundsField(Labels.PaddingAndMargin.Margin, layout.Margin, layout.MarginMinMax, uiBlock.CalculatedMargin, NovaGUI.ShowZAxisValues(uiBlock), NovaEditorPrefs.DisplaySidesMargin, NovaEditorPrefs.DisplayMinMaxMargin);
                    if (EditorGUI.EndChangeCheck())
                    {
                        NovaEditorPrefs.DisplaySidesMargin = showMarginSides;
                        NovaEditorPrefs.DisplayMinMaxMargin = showMarginRange;

                        if (!UnityEditor.EditorTools.ToolManager.IsActiveTool(Tools.UITool.Instance))
                        {
                            UnityEditor.EditorTools.ToolManager.SetActiveTool<SpacingTool>();
                        }
                    }
                }
            }
        }

        public static void DrawAutoLayoutUI(_AutoLayout autoLayout, UIBlock uiBlock)
        {
            EditorGUI.BeginChangeCheck();

            bool wasEnabled = autoLayout.AxisProp.hasMultipleDifferentValues ? false : autoLayout.AxisProp.boolValue;
            using Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Auto Layout", autoLayout.AxisProp);

            if (EditorGUI.EndChangeCheck() && wasEnabled != autoLayout.AxisProp.boolValue)
            {
                if (wasEnabled)
                {
                    ApplyAutoLayoutChildPositions(autoLayout.AxisProp.serializedObject);
                }
                else
                {
                    switch (autoLayout.Cross.Axis)
                    {
                        case Axis.X:
                            autoLayout.AxisProp.intValue = (int)Axis.Y;
                            break;
                        case Axis.Y:
                            autoLayout.AxisProp.intValue = (int)Axis.X;
                            break;
                        case Axis.Z:
                            autoLayout.AxisProp.intValue = (int)Axis.Y;
                            break;
                    }

                    ApplyKnownAutoLayoutPropsToChildren(autoLayout.AxisProp, -1, autoLayout.alignment);
                }
            }

            if (foldout)
            {
                EditorGUI.BeginChangeCheck();

                (bool showAutoRange, bool showCrossRange) = AutoLayoutField(uiBlock, autoLayout, NovaEditorPrefs.DisplayMinMaxAutoLayout, NovaEditorPrefs.DisplayMinMaxCrossLayout);

                if (EditorGUI.EndChangeCheck())
                {
                    NovaEditorPrefs.DisplayMinMaxAutoLayout = showAutoRange;
                    NovaEditorPrefs.DisplayMinMaxCrossLayout = showCrossRange;
                }
            }
        }

        public static (bool, bool) AutoLayoutField(UIBlock uiBlock, _AutoLayout autoLayout, bool expandedLength, bool expandedCrossLength)
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                _CrossLayout crossLayout = autoLayout.Cross;
                bool crossAxisEnabled = crossLayout.Axis != Axis.None;

                GUIContent primaryAxisLabel = Labels.AutoLayout.PrimaryAxis;
                Rect labelRect = NovaGUI.Layout.GetControlRect();
                labelRect = labelRect.Center(EditorStyles.boldLabel.CalcSize(primaryAxisLabel).x);
                EditorGUI.LabelField(labelRect, primaryAxisLabel, EditorStyles.boldLabel);

                AutoLayoutToolbar(autoLayout.AxisProp, autoLayout.alignmentProp, autoLayout.ReverseOrderProp, out bool axisChanged);
                expandedLength = AutoLayoutSpacing(uiBlock, autoLayout.Spacing, autoLayout.SpacingMinMax, autoLayout.AutoSpaceProp, uiBlock.CalculatedSpacing, expandedLength);
                NovaGUI.FloatField(Labels.AutoLayout.Offset, autoLayout.OffsetProp);

                if (axisChanged && crossAxisEnabled)
                {
                    int previousCrossAxis = (int)crossLayout.Axis;
                    crossLayout.Axis = OppositeAxis(autoLayout.Axis);
                    ApplyKnownAutoLayoutPropsToChildren(crossLayout.AxisProp, previousCrossAxis, crossLayout.alignment);
                }

                EditorGUILayout.Space();
                expandedCrossLength = CrossLayoutField(uiBlock, crossLayout, autoLayout.Axis, expandedCrossLength);
            }

            return (expandedLength, expandedCrossLength);
        }

        private static bool CrossLayoutField(UIBlock uiBlock, _CrossLayout crossLayout, Axis primaryAxis, bool expandedCrossLength)
        {
            bool hasListView = uiBlock.TryGetComponent(out ListView _);
            EditorGUI.BeginDisabledGroup(hasListView);

            Rect labelRect = NovaGUI.Layout.GetControlRect();
            GUIContent crossAxisLabel = hasListView ? Labels.AutoLayout.CrossAxisDisabled : Labels.AutoLayout.CrossAxis;
            crossAxisLabel = EditorGUI.BeginProperty(labelRect, crossAxisLabel, crossLayout.SerializedProperty);
            
            labelRect = labelRect.Center(EditorStyles.boldLabel.CalcSize(crossAxisLabel).x);
            EditorGUI.LabelField(labelRect, crossAxisLabel, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool wasEnabled = crossLayout.AxisProp.hasMultipleDifferentValues ? false : crossLayout.AxisProp.boolValue;

            Rect controlRect = labelRect;
            controlRect.x = controlRect.xMax + NovaGUI.MinSpaceBetweenFields;
            controlRect.width = NovaGUI.ToggleBoxSize;

            GUIContent toggleLabel = EditorGUI.BeginProperty(controlRect, GUIContent.none, crossLayout.AxisProp);
            bool isEnabled = EditorGUI.Toggle(controlRect, toggleLabel, wasEnabled);
            EditorGUI.EndProperty();

            if (EditorGUI.EndChangeCheck())
            {
                if (wasEnabled)
                {
                    ApplyAutoLayoutChildPositions(crossLayout.AxisProp.serializedObject);
                    crossLayout.Axis = Axis.None;
                }
                else
                {
                    crossLayout.Axis = OppositeAxis(primaryAxis);
                    ApplyKnownAutoLayoutPropsToChildren(crossLayout.AxisProp, -1, crossLayout.alignment);
                }
            }

            if (isEnabled)
            {
                EditorGUILayout.Space(1);

                AutoLayoutToolbar(crossLayout.AxisProp, crossLayout.alignmentProp, crossLayout.ReverseOrderProp, out _, primaryAxis);
                expandedCrossLength = AutoLayoutSpacing(uiBlock, crossLayout.Spacing, crossLayout.SpacingMinMax, crossLayout.AutoSpaceProp, uiBlock.CalculatedCrossSpacing, expandedCrossLength);

                Rect expandToGridPosition = NovaGUI.Layout.GetControlRect();
                EditorGUI.BeginChangeCheck();
                GUIContent expandToGridLabel = EditorGUI.BeginProperty(expandToGridPosition, Labels.AutoLayout.ExpandToGrid, crossLayout.ExpandToGridProp);
                bool expandToGrid = EditorGUI.Toggle(expandToGridPosition, expandToGridLabel, crossLayout.ExpandToGrid);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    crossLayout.ExpandToGrid = expandToGrid;
                }
            }

            EditorGUI.EndProperty();
            EditorGUI.EndDisabledGroup();

            return expandedCrossLength;
        }

        private static Axis OppositeAxis(Axis axis)
        {
            switch (axis)
            {
                case Axis.Y:
                    return Axis.X;
                case Axis.X:
                case Axis.Z:
                    return Axis.Y;
                default:
                    return Axis.None;
            }
        }

        private static void AutoLayoutToolbar(SerializedProperty axisProp, SerializedProperty alignmentProp, SerializedProperty orderProp, out bool axisChanged, Axis disabledAxis = Axis.None)
        {
            float toolbarHeight = EditorGUIUtility.singleLineHeight + (3 * NovaGUI.MinSpaceBetweenFields);

            axisChanged = false;
            float fieldWidth = NovaGUI.FieldWidth;
            float toolbarButtonWidth = fieldWidth / 8f;

            NovaGUI.Layout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            Rect axisPosition = NovaGUI.Layout.GetControlRect(hasLabel: false, height: toolbarHeight, GUILayout.Width(3 * toolbarButtonWidth));
            Rect alignPosition = NovaGUI.Layout.GetControlRect(hasLabel: false, height: toolbarHeight, GUILayout.Width(3 * toolbarButtonWidth));
            Rect orderPosition = NovaGUI.Layout.GetControlRect(hasLabel: false, height: toolbarHeight, GUILayout.Width(2 * toolbarButtonWidth));

            EditorGUI.BeginChangeCheck();

            Rect axisPropertyRect = axisPosition;
            axisPropertyRect.xMin = 0;
            EditorGUI.BeginProperty(axisPropertyRect, GUIContent.none, axisProp);
            int axisIndex = axisProp.intValue - 1;
            axisIndex = axisIndex >= 0 ? axisIndex : 0;
            int layoutAxis = NovaGUI.Toolbar(axisPosition, axisIndex, Labels.AxisToolbarLabels, toolbarButtonWidth, disabledIndex: disabledAxis.Index());
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                axisProp.intValue = (int)AxisIndex.GetAxis(layoutAxis);
                ApplyKnownAutoLayoutPropsToChildren(axisProp, axisIndex, alignmentProp.intValue);

                axisChanged = true;
            }

            Rect alignPropertyRect = alignPosition;
            alignPropertyRect.y -= NovaGUI.MinSpaceBetweenFields;
            alignPropertyRect.height += 2 * NovaGUI.MinSpaceBetweenFields;
            EditorGUI.BeginProperty(alignPropertyRect, GUIContent.none, alignmentProp);
            AlignmentField(alignPosition, null, alignmentProp, Labels.Alignment[axisIndex]);
            EditorGUI.EndProperty();

            Rect orderPropertyRect = orderPosition;
            orderPropertyRect.xMax = NovaGUI.ViewWidth;
            EditorGUI.BeginProperty(orderPropertyRect, GUIContent.none, orderProp);
            EditorGUI.BeginChangeCheck();
            int order = NovaGUI.Toolbar(orderPosition, orderProp.boolValue ? 1 : 0, Labels.Order[axisIndex], toolbarButtonWidth);
            EditorGUI.EndProperty();

            if (EditorGUI.EndChangeCheck())
            {
                orderProp.boolValue = order == 1;
            }

            GUILayout.FlexibleSpace();

            NovaGUI.Layout.EndHorizontal();
        }

        private static bool AutoLayoutSpacing(MonoBehaviour target, _Length spacing, _MinMax spacingMinMax, SerializedProperty autospaceProp, Length.Calculated calc, bool expandedLength)
        {
            float labelWidth = NovaGUI.LabelWidth;

            bool lengthDisabled = autospaceProp.boolValue;

            NovaGUI.Layout.BeginHorizontal();

            expandedLength = NovaGUI.PrefixFoldout(expandedLength);

            NovaGUI.Space(-NovaGUI.Layout.FoldoutArrowIndentSpace);

            float autospaceLabelWidth = EditorStyles.label.CalcSize(Labels.AutoLayout.AutoSpace).x;
            float autospaceFieldWidth = autospaceLabelWidth + NovaGUI.MinSpaceBetweenFields + NovaGUI.ToggleBoxSize;
            float lengthLabelWidth = NovaGUI.PrefixLabelWidth + autospaceLabelWidth - NovaGUI.MinSpaceBetweenFields;

            EditorGUI.BeginDisabledGroup(lengthDisabled);
            NovaGUI.LabelWidth = lengthLabelWidth;
            Rect lengthPosition = NovaGUI.Layout.GetControlRect();
            lengthPosition.width -= NovaGUI.ToggleBoxSize;

            // total hack, but using another labelfield is shifting the text around. 
            NovaGUI.LengthField(lengthPosition, Labels.AutoLayout.Spacing, spacing, calc, spacingMinMax.Min, spacingMinMax.Max);

            EditorGUI.EndDisabledGroup();

            if (lengthDisabled && Event.current.type == EventType.Repaint)
            {
                GUIStyle labelStyle = spacing.SerializedProperty.prefabOverride ? EditorStyles.boldLabel : EditorStyles.label;
                labelStyle.Draw(lengthPosition, Labels.AutoLayout.Spacing, false, false, false, false);
            }

            NovaGUI.LabelWidth = autospaceFieldWidth - NovaGUI.ToggleBoxSize;
            Rect autospacePosition = NovaGUI.Layout.GetControlRect(GUILayout.Width(autospaceFieldWidth));

            bool hasListView = target.TryGetComponent<ListView>(out _);

            EditorGUI.BeginDisabledGroup(hasListView);

            EditorGUI.BeginChangeCheck();
            GUIContent autospaceLabel = EditorGUI.BeginProperty(autospacePosition, hasListView ? Labels.AutoLayout.AutoSpaceDisabled : Labels.AutoLayout.AutoSpace, autospaceProp);
            bool autoFill = EditorGUI.Toggle(autospacePosition, autospaceLabel, autospaceProp.boolValue);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                autospaceProp.boolValue = autoFill;
            }

            EditorGUI.EndDisabledGroup();

            NovaGUI.LabelWidth = labelWidth;

            NovaGUI.Layout.EndHorizontal();

            if (expandedLength)
            {
                EditorGUILayout.Space(1);
                NovaGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                NovaGUI.Layout.BeginHorizontal(NovaGUI.Styles.InnerContent);
                NovaGUI.LabelWidth = NovaGUI.PrefixLabelWidth + autospaceLabelWidth + NovaGUI.ToggleBoxSize - NovaGUI.MinSpaceBetweenFields;
                NovaGUI.LengthRangeField(GUIContent.none, spacing, spacingMinMax, calc, spacing.Type == LengthType.Value);
                NovaGUI.LabelWidth = labelWidth;
                NovaGUI.Layout.EndHorizontal();
            }

            return expandedLength;
        }


        /// <summary>
        /// When a parent's autolayout is disabled, we want to ensure their auto-layout-calculated position is 
        /// serialized as expected, so if anything triggers a copy-to-datastore, we don't see the object jump
        /// to a stale-but-serialized position.
        /// </summary>
        /// <param name="serializedObject">The serialized parent object</param>
        private static void ApplyAutoLayoutChildPositions(SerializedObject serializedObject)
        {
            UIBlock[] targets = serializedObject.targetObjects.Cast<UIBlock>().ToArray();

            for (int i = 0; i < targets.Length; ++i)
            {
                ReadOnlyList<ICoreBlock> children = targets[i].ChildBlocks;

                for (int j = 0; j < children.Count; ++j)
                {
                    if (!(children[j] is UIBlock child))
                    {
                        continue;
                    }

                    SerializedObject serializedChild = new SerializedObject(child);
                    _Length3 pos = new _Length3() { SerializedProperty = serializedChild.FindProperty(Names.UIBlock.layout).FindPropertyRelative(Names.Layout.Position) };

                    Length3 position = child.Position;

                    pos.X.Raw = position.X.Raw;
                    pos.X.Type = position.X.Type;
                    pos.Y.Raw = position.Y.Raw;
                    pos.Y.Type = position.Y.Type;
                    pos.Z.Raw = position.Z.Raw;
                    pos.Z.Type = position.Z.Type;

                    serializedChild.ApplyModifiedProperties();
                }
            }
        }

        /// <summary>
        /// When adjusting AutoLayout.Axis on a parent, this will apply/serialize certain values to the children
        /// so that the correct values persist after a domain reload. Otherwise the children may move unexpectedly.
        /// </summary>
        private static void ApplyKnownAutoLayoutPropsToChildren(SerializedProperty axisProperty, int previousAxisIndex, int newAlignment)
        {
            UIBlock[] targets = axisProperty.serializedObject.targetObjects.Cast<UIBlock>().ToArray();

            for (int i = 0; i < targets.Length; ++i)
            {
                ReadOnlyList<ICoreBlock> children = targets[i].ChildBlocks;
                if (children.Count == 0)
                {
                    continue;
                }

                ApplyKnownAutoLayoutPropsToChildren<UIBlock>(children, axisProperty, previousAxisIndex, newAlignment);
                ApplyKnownAutoLayoutPropsToChildren<UIBlock2D>(children, axisProperty, previousAxisIndex, newAlignment);
                ApplyKnownAutoLayoutPropsToChildren<UIBlock3D>(children, axisProperty, previousAxisIndex, newAlignment);
                ApplyKnownAutoLayoutPropsToChildren<TextBlock>(children, axisProperty, previousAxisIndex, newAlignment);
            }
        }

        private static void ApplyKnownAutoLayoutPropsToChildren<T>(ReadOnlyList<ICoreBlock> children, SerializedProperty axisProperty, int previousAxisIndex, int newAlignment) where T : UIBlock
        {
            T[] childObjects = children.Source.Where((x) => typeof(T).IsAssignableFrom(x.GetType()) && x.GetType().IsAssignableFrom(typeof(T))).Cast<T>().ToArray();

            if (childObjects == null || childObjects.Length == 0)
            {
                return;
            }

            SerializedObject serializedChildren = new SerializedObject(childObjects);
            _Length3 pos = new _Length3() { SerializedProperty = serializedChildren.FindProperty(Names.UIBlock.layout).FindPropertyRelative(Names.Layout.Position) };
            _Alignment alignment = new _Alignment() { SerializedProperty = serializedChildren.FindProperty(Names.UIBlock.layout).FindPropertyRelative(Names.Layout.Alignment) };

            Internal.Axis axis = (Internal.Axis)axisProperty.intValue;

            switch (previousAxisIndex)
            {
                case 0: // X
                    pos.X.Raw = 0;
                    break;
                case 1: // Y
                    pos.Y.Raw = 0;
                    break;
                case 2: // Z
                    pos.Z.Raw = 0;
                    break;
                default:
                    pos.X.Raw = 0;
                    pos.Y.Raw = 0;
                    pos.Z.Raw = 0;
                    break;
            }

            switch (axis)
            {
                case Internal.Axis.X: // X
                    alignment.XProp.intValue = newAlignment;
                    break;
                case Internal.Axis.Y: // Y
                    alignment.YProp.intValue = newAlignment;
                    break;
                case Internal.Axis.Z: // Z
                    alignment.ZProp.intValue = newAlignment;
                    break;
                default:
                    break;
            }

            serializedChildren.ApplyModifiedProperties();
        }

        /// <summary>
        /// Some of my best work
        /// </summary>>
        private static void ConflictingPropertyMessage(ThreeD<AutoSize> parent, ThreeD<AutoSize> child, ThreeD<LengthType> childRelative)
        {
            bool lockAspectX = false;
            bool lockAspectY = false;
            bool lockAspectZ = false;

            bool conflictingX = parent.X == AutoSize.Shrink && (childRelative.X == LengthType.Percent || lockAspectX);
            bool conflictingY = parent.Y == AutoSize.Shrink && (childRelative.Y == LengthType.Percent || lockAspectY);
            bool conflictingZ = parent.Z == AutoSize.Shrink && (childRelative.Z == LengthType.Percent || lockAspectZ);

            if (!conflictingX && !conflictingY && !conflictingZ)
            {
                return;
            }

            bool multiple = (conflictingX && conflictingY) || (conflictingX && conflictingZ) || (conflictingY && conflictingZ);
            string axes = multiple ? "axes" : "axis";
            string are = multiple ? "are" : "is";

            string orExpand = (conflictingX && child.X == AutoSize.Expand) || (conflictingY && child.Y == AutoSize.Expand) || (conflictingZ && child.Z == AutoSize.Expand) ? " (or Expand)" : string.Empty;

            string conflicts = $"Percent{orExpand}";
            string list = conflictingX ? conflictingY ? conflictingZ ? "X, Y, and Z" : "X and Y" : conflictingZ ? "X and Z" : "X" : conflictingY ? conflictingZ ? "Y and Z" : "Y" : "Z";
            string plural = multiple ? "s" : string.Empty;
            string message = $"The {list} {axes} {are} set to {conflicts}, while its parent is set to Shrink in the same direction{plural}. These properties conflict and could lead to undesired behavior.";

            NovaGUI.WarningIcon(message);
        }
    }
}

