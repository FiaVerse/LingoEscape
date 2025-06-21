// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(ImageAdjustment))]
    internal class ImageAdjustmentDrawer : NovaPropertyDrawer<_ImageAdjustment>
    {
        protected override float GetPropertyHeight(GUIContent label)
        {
            if (foldout)
            {
                if (wrapper.scaleMode == ImageScaleMode.Manual)
                {
                    return 4f * PropertyDrawerUtils.SingleLineHeight;
                }
                else
                {
                    return 2f * PropertyDrawerUtils.SingleLineHeight;
                }
            }
            else
            {
                return EditorGUI.GetPropertyHeight(wrapper.SerializedProperty, label, false);
            }
        }

        private bool foldout = true;
        protected override void OnGUI(Rect position, GUIContent label)
        {
            foldout = EditorGUI.Foldout(position, foldout, label, true);

            if (!foldout)
            {
                EditorGUI.EndFoldoutHeaderGroup();
                return;
            }

            position.BumpLine();

            EditorGUI.indentLevel++;

            EditorGUI.PropertyField(position, wrapper.scaleModeProp);

            if (wrapper.scaleMode == ImageScaleMode.Manual)
            {
                position.BumpLine();
                EditorGUI.PropertyField(position, wrapper.CenterUVProp);
                position.BumpLine();
                EditorGUI.PropertyField(position, wrapper.UVScaleProp);
            }

            EditorGUI.indentLevel--;

            EditorGUI.EndFoldoutHeaderGroup();
        }
    }
}