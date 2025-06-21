// Copyright (c) Supernova Technologies LLC
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    [UnityEditor.CustomEditor(typeof(ListView)), UnityEditor.CanEditMultipleObjects]
    internal class ListViewEditor : UnityEditor.Editor
    {
        protected virtual string[] ExcludedProperties { get; } = new string[] { "m_Script" };

        protected virtual void DrawBeforeProperties() { }
        protected virtual void DrawAfterProperties() { }

        public override void OnInspectorGUI()
        {
            AutoSpaceCheck();
            CrossLayoutCheck();

            serializedObject.UpdateIfRequiredOrScript();

            UnityEditor.EditorGUI.BeginChangeCheck();
            DrawBeforeProperties();

            DrawPropertiesExcluding(serializedObject, ExcludedProperties);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void AutoSpaceCheck()
        {
            if (!(target is ListView listView && listView.TryGetComponent(out UIBlock uiBlock)))
            {
                return;
            }

            if (!uiBlock.AutoLayout.AutoSpace)
            {
                return;
            }

            string nameOfType = target.GetType().Name;
            bool disableAutoSpace = EditorUtility.DisplayDialog($"\"Auto\" spacing cannot be used with {nameOfType}", "Would you like to disable auto spacing on this UIBlock?", "Yes", "No");

            if (!disableAutoSpace)
            {
                return;
            }

            uiBlock.AutoLayout.AutoSpace = false;
        }

        private void CrossLayoutCheck()
        {
            if (!(target is ListView listView && listView.TryGetComponent(out UIBlock uiBlock)))
            {
                return;
            }
            

            if (!uiBlock.AutoLayout.Cross.Enabled)
            {
                return;
            }

            string nameOfType = target.GetType().Name;
            bool disableCrossAxis = EditorUtility.DisplayDialog($"\"Cross Axis\" cannot be used with {nameOfType}", "Would you like to disable the Auto Layout's cross axis on this UIBlock?", "Yes", "No");

            if (!disableCrossAxis)
            {
                return;
            }

            uiBlock.AutoLayout.Cross.Axis = Axis.None;
        }
    }
}
