// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(MinMax))]
    internal class MinMaxDrawer : NovaPropertyDrawer<_MinMax>
    {
        protected override void OnGUI(Rect position, GUIContent label)
        {
            EditorGUI.PrefixLabel(position, label);

            position.ShiftAndResizeLabel();

            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = NovaGUI.SingleCharacterGUIWidth * 3;

            position.Split(out Rect minRect, out Rect maxRect);
            int oldIndex = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            DrawSingle(minRect, Labels.Min, wrapper.MinProp);
            DrawSingle(maxRect, Labels.Max, wrapper.MaxProp);
            EditorGUI.indentLevel = oldIndex;

            EditorGUIUtility.labelWidth = labelWidth;
        }

        private void DrawSingle(Rect rect, GUIContent label, SerializedProperty prop)
        {
            EditorGUI.BeginProperty(rect, label, prop);

            float currentValue = prop.floatValue;
            bool enabled = !float.IsInfinity(currentValue);

            rect.Split(NovaGUI.ToggleBoxSize, out Rect toggleRect, out Rect fieldRect);
            bool newEnabled = EditorGUI.ToggleLeft(toggleRect, GUIContent.none, enabled);
            rect.ShiftAndResize(rect.width);

            if (newEnabled != enabled)
            {
                if (newEnabled)
                {
                    prop.floatValue = 0f;
                }
                else
                {
                    prop.floatValue = float.NegativeInfinity;
                }
            }

            EditorGUI.BeginDisabledGroup(!newEnabled);
            EditorGUI.BeginChangeCheck();
            float newVal = EditorGUI.FloatField(fieldRect, label, prop.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = newVal;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.EndProperty();
        }
    }
}