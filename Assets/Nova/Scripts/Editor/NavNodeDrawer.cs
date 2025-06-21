// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(NavNode))]
    internal class NavNodeDrawer : NovaPropertyDrawer<_NavNode>
    {
        protected override float GetPropertyHeight(GUIContent label)
        {
            if (!wrapper.SerializedProperty.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            int numSpaces = 5;
            float height = PropertyDrawerUtils.SingleLineHeight;

            height += NavLinkDrawer.GetPropertyHeight(wrapper.Up);
            height += Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Left), NavLinkDrawer.GetPropertyHeight(wrapper.Right));
            height += NavLinkDrawer.GetPropertyHeight(wrapper.Down);

            if (NovaEditorPrefs.DisplayNavigationZAxis)
            {
                numSpaces++;
                height += Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Back), NavLinkDrawer.GetPropertyHeight(wrapper.Forward));
            }

            return height + (numSpaces * NovaGUI.MinSpaceBetweenFields);
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, wrapper.SerializedProperty);

            wrapper.SerializedProperty.isExpanded = EditorGUI.Foldout(position, wrapper.SerializedProperty.isExpanded, label);

            if (!wrapper.SerializedProperty.isExpanded)
            {
                return;
            }

            position.BumpLine();

            Rect box = position;
            box.height = GetPropertyHeight(label) - (PropertyDrawerUtils.SingleLineHeight + NovaGUI.MinSpaceBetweenFields);

            EditorGUI.HelpBox(box, string.Empty, MessageType.None);

            position.y += NovaGUI.MinSpaceBetweenFields;
            position.xMin += NovaGUI.MinSpaceBetweenFields;
            position.xMax -= NovaGUI.MinSpaceBetweenFields;

            Draw3DToggle(position);

            position.height = NavLinkDrawer.GetPropertyHeight(wrapper.Up);

            Rect center = position.Center(position.width * 0.5f);
            EditorGUI.PropertyField(center, wrapper.UpProp);

            position.Bump(position.height + NovaGUI.MinSpaceBetweenFields);
            position.height = Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Left), NavLinkDrawer.GetPropertyHeight(wrapper.Right));

            position.Split(out Rect left, out Rect right);
            EditorGUI.PropertyField(left, wrapper.LeftProp);
            EditorGUI.PropertyField(right, wrapper.RightProp);

            position.Bump(position.height + NovaGUI.MinSpaceBetweenFields);
            position.height = NavLinkDrawer.GetPropertyHeight(wrapper.Down);

            center = position.Center(position.width * 0.5f);
            EditorGUI.PropertyField(center, wrapper.DownProp);

            if (NovaEditorPrefs.DisplayNavigationZAxis)
            {
                position.Bump(position.height + NovaGUI.MinSpaceBetweenFields);
                position.height = Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Back), NavLinkDrawer.GetPropertyHeight(wrapper.Forward));

                position.Split(out left, out right);
                EditorGUI.PropertyField(left, wrapper.BackProp);
                EditorGUI.PropertyField(right, wrapper.ForwardProp);
            }


            EditorGUI.EndFoldoutHeaderGroup();

            EditorGUI.EndProperty();
        }

        private void Draw3DToggle(Rect position)
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 2 * NovaGUI.SingleCharacterGUIWidth;

            float toggleWidth = 2 * NovaGUI.SingleCharacterGUIWidth;
            Rect zToggle = position.TopRight(toggleWidth, PropertyDrawerUtils.SingleLineHeight);
            NovaEditorPrefs.DisplayNavigationZAxis = GUI.Toggle(zToggle, NovaEditorPrefs.DisplayNavigationZAxis, Labels.NavNode.ThreeDToggle, NovaGUI.Styles.ToolbarButtonMid);
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }
}
