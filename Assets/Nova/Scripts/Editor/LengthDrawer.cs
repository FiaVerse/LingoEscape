// Copyright (c) Supernova Technologies LLC
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(Length))]
    internal class LengthDrawer : NovaPropertyDrawer<_Length>
    {
        protected override void OnGUI(Rect position, GUIContent label)
        {
            GUIContent propertyLabel = EditorGUI.BeginProperty(position, label, wrapper.SerializedProperty);
            Rect floatField = position;
            floatField.width = Mathf.Max(NovaGUI.MinFloatFieldWidth, floatField.width - NovaGUI.ToggleToolbarFieldWidth) - NovaGUI.MinSpaceBetweenFields;

            EditorGUI.BeginChangeCheck();
            float raw = wrapper.Raw;
            float fieldValue = float.IsNaN(raw) ? 0 : wrapper.Type == LengthType.Value ? raw : raw * 100;
            fieldValue = EditorGUI.FloatField(floatField, propertyLabel, fieldValue);
            raw = fieldValue / (wrapper.Type == LengthType.Value ? 1 : 100);
            bool rawValueChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            Rect lengthTypeField = floatField;
            lengthTypeField.width = NovaGUI.ToggleToolbarFieldWidth;
            lengthTypeField.x += floatField.width + NovaGUI.MinSpaceBetweenFields;
            LengthType newType = NovaGUI.LengthTypeField(lengthTypeField, wrapper.Type);
            bool typeChanged = EditorGUI.EndChangeCheck();
            EditorGUI.EndProperty();

            if (typeChanged)
            {
                wrapper.Type = newType;
            }

            if (rawValueChanged)
            {
                wrapper.Raw = raw;
            }
        }
    }
}

