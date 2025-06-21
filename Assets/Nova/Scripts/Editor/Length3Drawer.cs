// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(Length3))]
    internal class Length3Drawer : NovaPropertyDrawer<_Length3>
    {
        protected override void OnGUI(Rect position, GUIContent label)
        {
            EditorGUI.LabelField(position, label);

            position.ShiftAndResizeLabel();

            position.Split(out Rect x, out Rect y, out Rect z);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = NovaGUI.SingleCharacterGUIWidth;
            EditorGUI.PropertyField(x, wrapper.XProp);
            EditorGUI.PropertyField(y, wrapper.YProp);
            EditorGUI.PropertyField(z, wrapper.ZProp);
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }
}