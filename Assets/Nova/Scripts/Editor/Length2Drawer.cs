// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(Length2))]
    internal class Length2Drawer : NovaPropertyDrawer<_Length2>
    {
        protected override void OnGUI(Rect position, GUIContent label)
        {
            EditorGUI.LabelField(position, label);

            position.ShiftAndResizeLabel();


            position.Split(out Rect xRect, out Rect yRect);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = NovaGUI.SingleCharacterGUIWidth;
            EditorGUI.PropertyField(xRect, wrapper.XProp);
            EditorGUI.PropertyField(yRect, wrapper.YProp);
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }
}