// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(CrossLayout))]
    internal class CrossLayoutDrawer : NovaPropertyDrawer<_CrossLayout>
    {
        protected override float GetPropertyHeight(GUIContent label)
        {
            if (!wrapper.SerializedProperty.isExpanded)
            {
                return EditorGUI.GetPropertyHeight(wrapper.SerializedProperty, label, false);
            }

            return 9f * PropertyDrawerUtils.SingleLineHeight;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
           wrapper.SerializedProperty.isExpanded = EditorGUI.Foldout(position, wrapper.SerializedProperty.isExpanded, label, true);

            if (!wrapper.SerializedProperty.isExpanded)
            {
                EditorGUI.EndFoldoutHeaderGroup();
                return;
            }

            position.BumpLine();

            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUI.BeginChangeCheck();

                bool enabled = EditorGUI.Toggle(position, Labels.AutoLayout.Enabled, wrapper.AxisProp.boolValue);

                if (EditorGUI.EndChangeCheck())
                {
                    if (enabled)
                    {
                        wrapper.Axis = Axis.X;
                    }
                    else
                    {
                        wrapper.Axis = Axis.None;
                    }
                }

                position.BumpLine();

                EditorGUI.BeginDisabledGroup(!enabled);
                float tripleButtonWidth = (position.width - EditorGUIUtility.labelWidth) / 3f;
                EditorGUI.PrefixLabel(position, Labels.AutoLayout.Axis);
                Rect axisPosition = new Rect(position.x + NovaGUI.LabelWidth, position.y, position.width - NovaGUI.LabelWidth, position.height);
                EditorGUI.BeginChangeCheck();
                EditorGUI.BeginProperty(axisPosition, GUIContent.none, wrapper.AxisProp);
                int axisIndex = wrapper.Axis.Index();
                axisIndex = axisIndex >= 0 ? axisIndex : 0;
                int layoutAxis = NovaGUI.Toolbar(axisPosition, axisIndex, Labels.AxisToolbarLabels, tripleButtonWidth);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    wrapper.Axis = AxisIndex.GetAxis(layoutAxis);
                }
                EditorGUI.EndDisabledGroup();

                position.BumpLine();

                EditorGUI.PrefixLabel(position, Labels.AutoLayout.Alignment);

                Rect alignPosition = new Rect(position.x + NovaGUI.LabelWidth, position.y, position.width - NovaGUI.LabelWidth, position.height);
                EditorGUI.BeginChangeCheck();
                EditorGUI.BeginProperty(alignPosition, GUIContent.none, wrapper.alignmentProp);
                int newAlignment = NovaGUI.Toolbar(alignPosition, wrapper.alignment + 1, Labels.Alignment[axisIndex], tripleButtonWidth);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    wrapper.alignment = newAlignment - 1;
                }

                position.BumpLine();
                EditorGUI.PrefixLabel(position, Labels.AutoLayout.Order);

                float doubleButtonWidth = (position.width - EditorGUIUtility.labelWidth) / 2f;

                Rect orderPosition = new Rect(position.x + NovaGUI.LabelWidth, position.y, position.width - NovaGUI.LabelWidth, position.height);
                EditorGUI.BeginChangeCheck();
                EditorGUI.BeginProperty(orderPosition, GUIContent.none, wrapper.ReverseOrderProp);
                int order = NovaGUI.Toolbar(orderPosition, wrapper.ReverseOrder ? 1 : 0, Labels.Order[axisIndex], doubleButtonWidth);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    wrapper.ReverseOrder = order == 1;
                }

                position.BumpLine();

                EditorGUI.PropertyField(position, wrapper.AutoSpaceProp);
                position.BumpLine();
                EditorGUI.BeginDisabledGroup(wrapper.AutoSpace);
                EditorGUI.PropertyField(position, wrapper.SpacingProp);
                EditorGUI.EndDisabledGroup();
                position.BumpLine();
                EditorGUI.PropertyField(position, wrapper.SpacingMinMaxProp);
                position.BumpLine();
                EditorGUI.PropertyField(position, wrapper.ExpandToGridProp);
            }

            EditorGUI.EndFoldoutHeaderGroup();
        }
    }
}