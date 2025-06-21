// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using Nova.Editor.Utilities;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    internal abstract class GestureRecognizerEditor<T> : NovaEditor<T> where T : GestureRecognizer
    {
        private static GUIContent navigationHeader = EditorGUIUtility.TrTextContent("Navigate");

        private List<T> recognizers = new List<T>();

        public const float MaxDragThreshold = 89;

        private SerializedProperty navigationProp = null;
        private SerializedProperty navigableProp = null;
        private SerializedProperty onSelectProp = null;
        private SerializedProperty autoSelectProp = null;

        protected override void OnEnable()
        {
            base.OnEnable();

            navigableProp = serializedObject.FindProperty(Names.GestureRecognizer.navigable);
            navigationProp = serializedObject.FindProperty(Names.GestureRecognizer.Navigation);
            onSelectProp = serializedObject.FindProperty(Names.GestureRecognizer.onSelect);
            autoSelectProp = serializedObject.FindProperty(Names.GestureRecognizer.autoSelect);

            if (Application.IsPlaying(target))
            {
                Undo.undoRedoPerformed += UpdateUnityObjects;
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= UpdateUnityObjects;
        }

        protected virtual void UpdateUnityObjects()
        {
            DisableActiveRecognizers();
            serializedObject.ApplyModifiedProperties();
            EnableDisabledRecognizers();
        }

        protected void DrawNavigationUI()
        {
            EditorGUILayout.LabelField(navigationHeader, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(navigableProp, Labels.GestureRecognizer.NavigableLabel);
            if (EditorGUI.EndChangeCheck() && Application.isPlaying)
            {
                UpdateUnityObjects();
            }

            bool notNavigable = !navigableProp.boolValue;

            EditorGUI.BeginDisabledGroup(notNavigable);
            DrawNavGraphToggle();
            EditorGUI.EndDisabledGroup();

            if (notNavigable)
            {
                return;
            }

            EditorGUILayout.PropertyField(autoSelectProp, Labels.GestureRecognizer.AutoSelectLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(onSelectProp, Labels.GestureRecognizer.OnSelectLabel);
            if (EditorGUI.EndChangeCheck() && Application.isPlaying)
            {
                // Update unity objects while playing if onselect prop changed
                UpdateUnityObjects();
            }

            EditorGUILayout.PropertyField(navigationProp, Labels.GestureRecognizer.NavigationLabel);
        }

        private void DrawNavGraphToggle()
        {
            EditorGUI.BeginChangeCheck();

            Rect toolBar = GUILayoutUtility.GetLastRect();

            toolBar = toolBar.TopRight(2 * NovaGUI.SingleCharacterGUIWidth, PropertyDrawerUtils.SingleLineHeight);
            Rect visibilityToggle = toolBar;
            visibilityToggle.x -= visibilityToggle.width;

            bool graphEnabled = NovaEditorPrefs.DisplayNavigationDebugView;
            GUIContent navGraphLabel = Labels.GestureRecognizer.GetNavGraphLabel(graphEnabled);
            NovaEditorPrefs.DisplayNavigationDebugView = GUI.Toggle(visibilityToggle, graphEnabled, navGraphLabel, NovaGUI.Styles.ToolbarButtonLeft);

            EditorGUI.BeginDisabledGroup(!NovaEditorPrefs.DisplayNavigationDebugView);
            bool filtered = NovaEditorPrefs.FilterNavDebugViewToSelection;
            GUIContent filterLabel = Labels.GestureRecognizer.GetNavGraphFilterLabel(filtered);
            NovaEditorPrefs.FilterNavDebugViewToSelection = GUI.Toggle(toolBar, filtered, filterLabel, NovaGUI.Styles.ToolbarButtonRight);
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// A bit of a hack part 1: disable and cache the active components before applying modified properties.
        /// This will let us properly unregister any existing event registrations
        /// </summary>
        private void DisableActiveRecognizers()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            recognizers.Clear();

            for (int i = 0; i < targetComponents.Count; ++i)
            {
                T recognizer = targetComponents[i];
                if (!recognizer.isActiveAndEnabled)
                {
                    continue;
                }

                // record state in case of undo, so component doesn't get stuck disabled
                Undo.RecordObject(recognizer, "Inspector");

                recognizer.enabled = false;
                recognizers.Add(recognizer);
            }
        }

        /// <summary>
        /// A bit of a hack part 2: enable the cached components after applying modified properties.
        /// This will let us properly register for any update event registrations.
        /// </summary>
        private void EnableDisabledRecognizers()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            for (int i = 0; i < recognizers.Count; ++i)
            {
                T recognizer = recognizers[i];
                recognizer.enabled = true;
            }
        }
    }
}
