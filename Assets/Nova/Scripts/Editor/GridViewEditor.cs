// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(GridView)), CanEditMultipleObjects]
    internal class GridViewEditor : ListViewEditor
    {
        protected override string[] ExcludedProperties { get; } = new string[] { "m_Script", Names.GridView.crossAxis, Names.GridView.crossAxisItemCount };

        private SerializedProperty crossAxisProp = null;
        private SerializedProperty crossAxisItemCountProp = null;

        private void OnEnable()
        {
            crossAxisProp = serializedObject.FindProperty(Names.GridView.crossAxis);
            crossAxisItemCountProp = serializedObject.FindProperty(Names.GridView.crossAxisItemCount);
        }

        protected override void DrawBeforeProperties()
        {
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);

            if (serializedObject.targetObjects.Length == 1)
            {
                EditorGUI.BeginDisabledGroup(true);
                GridView grid = target as GridView;
                EditorGUILayout.EnumPopup(new GUIContent("Primary Axis", "The Primary Axis is the scrollable axis and is implicitly assigned via the attached UIBlock's AutoLayout.Axis property"), grid.UIBlock.AutoLayout.Axis);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.PropertyField(crossAxisProp);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(crossAxisItemCountProp);
            if (EditorGUI.EndChangeCheck() && Application.isPlaying)
            {
                crossAxisItemCountProp.serializedObject.ApplyModifiedProperties();

                foreach (Object obj in targets)
                {
                    GridView grid = obj as GridView;
                    if (!Application.IsPlaying(grid) || !grid.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    grid.RebalanceGrid();
                }
            }

            EditorGUILayout.Space();
        }
    }
}
