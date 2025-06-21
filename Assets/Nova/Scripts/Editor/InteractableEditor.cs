// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using Nova.Extensions;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(Interactable)), CanEditMultipleObjects]
    internal class InteractableEditor : GestureRecognizerEditor<Interactable>
    {
        private static GUIContent dragHeader = EditorGUIUtility.TrTextContent("Drag");
        private static GUIContent clickHeader = EditorGUIUtility.TrTextContent("Click");

        SerializedProperty gestureSpaceProp = null;
        SerializedProperty obstructDragsProp = null;
        SerializedProperty clickBehaviorProp = null;
        SerializedProperty draggableProp = null;
        SerializedProperty dragThresholdProp = null;
        SerializedProperty lowAccuracyDragThresholdProp = null;

        protected override void OnEnable()
        {
            base.OnEnable();

            gestureSpaceProp = serializedObject.FindProperty(Names.Interactable.GestureSpace);

            obstructDragsProp = serializedObject.FindProperty(Names.Scroller.ObstructDrags);

            clickBehaviorProp = serializedObject.FindProperty(Names.Interactable.ClickBehavior);

            draggableProp = serializedObject.FindProperty(Names.Interactable.draggable);
            dragThresholdProp = serializedObject.FindProperty(Names.GestureRecognizer.DragThreshold);
            lowAccuracyDragThresholdProp = serializedObject.FindProperty(Names.GestureRecognizer.LowAccuracyDragThreshold);
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField(clickHeader, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(clickBehaviorProp, Labels.GestureRecognizer.ClickBehaviorLabel);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(dragHeader, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            NovaGUI.Toggle3DField(draggableProp, Labels.Interactable.DraggableLabel);
            if (EditorGUI.EndChangeCheck() && Application.isPlaying)
            {
                UpdateUnityObjects();
            }

            if (Util.Any(targetComponents[0].Draggable))
            {
                NovaGUI.SliderField(Labels.GestureRecognizer.DragThresholdLabel, dragThresholdProp, 0, MaxDragThreshold);
                NovaGUI.SliderField(Labels.GestureRecognizer.LowAccuracyDragThresholdLabel, lowAccuracyDragThresholdProp, 0, MaxDragThreshold);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.PropertyField(gestureSpaceProp, Labels.Interactable.GestureSpaceLabel);
            EditorGUILayout.PropertyField(obstructDragsProp, Labels.GestureRecognizer.ObstructDragsLabel);

            EditorGUILayout.Separator();
            DrawNavigationUI();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
