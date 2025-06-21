// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    internal abstract class NovaPropertyDrawer<T> : PropertyDrawer where T : class, ISerializedPropertyWrapper, new()
    {
        protected T wrapper = new T();

        protected abstract void OnGUI(Rect position, GUIContent label);

        protected virtual float GetPropertyHeight(GUIContent label)
        {
            return -1f;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            wrapper = new T() { SerializedProperty = property };

            float val = GetPropertyHeight(label);
            if (val != -1f)
            {
                return val;
            }
            else
            {
                return base.GetPropertyHeight(property, label);
            }
        }

        public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            wrapper = new T() { SerializedProperty = property };

            EditorGUI.BeginChangeCheck();
            position.height = EditorGUIUtility.singleLineHeight;
            OnGUI(position, label);

            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
