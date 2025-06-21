// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(Scroller)), CanEditMultipleObjects]
    internal class ScrollerEditor : GestureRecognizerEditor<Scroller>
    {
        private List<MonoBehaviour> scrollbars = new List<MonoBehaviour>();
        private static GUIContent clickHeader = EditorGUIUtility.TrTextContent("Pointer Click");
        private static GUIContent dragHeader = EditorGUIUtility.TrTextContent("Pointer Drag");
        private static GUIContent vectorHeader = EditorGUIUtility.TrTextContent("Mouse Wheel or Joystick");
        private static GUIContent scrollbarHeader = EditorGUIUtility.TrTextContent("Scrollbar");

        SerializedProperty obstructDragsProp = null;
        SerializedProperty clickBehaviorProp = null;
        SerializedProperty overscrollEffectProp = null;
        SerializedProperty dragScrollingProp = null;
        SerializedProperty dragThresholdProp = null;
        SerializedProperty lowAccuracyDragThresholdProp = null;
        SerializedProperty vectorScrollingProp = null;
        SerializedProperty vectorScrollMultiplierProp = null;
        SerializedProperty scrollbarVisualProp = null;
        SerializedProperty dragScrollbarProp = null;

        private bool ShowHelperDialogues => !serializedObject.isEditingMultipleObjects;

        protected override void OnEnable()
        {
            base.OnEnable();

            obstructDragsProp = serializedObject.FindProperty(Names.Scroller.ObstructDrags);
            clickBehaviorProp = serializedObject.FindProperty(Names.Scroller.ClickBehavior);

            overscrollEffectProp = serializedObject.FindProperty(Names.Scroller.OverscrollEffect);

            dragScrollingProp = serializedObject.FindProperty(Names.Scroller.dragScrolling);
            dragThresholdProp = serializedObject.FindProperty(Names.GestureRecognizer.DragThreshold);
            lowAccuracyDragThresholdProp = serializedObject.FindProperty(Names.GestureRecognizer.LowAccuracyDragThreshold);

            vectorScrollingProp = serializedObject.FindProperty(Names.Scroller.vectorScrolling);
            vectorScrollMultiplierProp = serializedObject.FindProperty(Names.Scroller.VectorScrollMultiplier);

            dragScrollbarProp = serializedObject.FindProperty(Names.Scroller.draggableScrollbar);
            scrollbarVisualProp = serializedObject.FindProperty(Names.Scroller.scrollbarVisual);

            RefreshScrollbarList();
        }

        public override void OnInspectorGUI()
        {

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField(clickHeader, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(clickBehaviorProp, Labels.GestureRecognizer.ClickBehaviorLabel);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(dragHeader, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dragScrollingProp, Labels.Scroller.DragScrollingLabel);

            if (dragScrollingProp.boolValue)
            {
                NovaGUI.SliderField(Labels.GestureRecognizer.DragThresholdLabel, dragThresholdProp, 0, MaxDragThreshold);
                NovaGUI.SliderField(Labels.GestureRecognizer.LowAccuracyDragThresholdLabel, lowAccuracyDragThresholdProp, 0, MaxDragThreshold);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(vectorHeader, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(vectorScrollingProp, Labels.Scroller.VectorScrollingLabel);

            if (vectorScrollingProp.boolValue)
            {
                EditorGUILayout.PropertyField(vectorScrollMultiplierProp, Labels.Scroller.VectorScrollMultiplierLabel);
            }

            EditorGUILayout.Separator();
            DrawNavigationUI();

            ScrollbarField();

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.PropertyField(overscrollEffectProp, Labels.Scroller.OverscrollEffectLabel);
            EditorGUILayout.PropertyField(obstructDragsProp, Labels.GestureRecognizer.ObstructDragsLabel);

            if (EditorGUI.EndChangeCheck())
            {
                UpdateUnityObjects();
            }
        }

        protected override void UpdateUnityObjects()
        {
            base.UpdateUnityObjects();

            RefreshScrollbarList();
        }

        private void ScrollbarField()
        {
            bool missingInteractable = false;
            bool notDraggable = false;

            if (dragScrollbarProp.boolValue || dragScrollbarProp.hasMultipleDifferentValues)
            {
                for (int i = 0; i < targetComponents.Count; ++i)
                {
                    Scroller scroller = targetComponents[i];
                    MonoBehaviour scrollbar = scroller.ScrollbarVisual;

                    if (scrollbar == null || !scroller.DraggableScrollbar)
                    {
                        continue;
                    }

                    Interactable interactable = scrollbar.GetComponentInChildren<Interactable>();

                    if (scroller.DraggableScrollbar && interactable == null)
                    {
                        missingInteractable = true;
                        break;
                    }

                    if (scroller.UIBlock.AutoLayout.Axis.TryGetIndex(out int axis) && !interactable.Draggable[axis])
                    {
                        notDraggable = true;
                        break;
                    }
                }
            }

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(scrollbarHeader, EditorStyles.boldLabel);

            if (missingInteractable)
            {
                NovaGUI.WarningIcon("Scrollbar Visual is missing an Interactable component");
            }
            else if (notDraggable)
            {
                NovaGUI.WarningIcon("The Scrollbar Visual's Interactable component must be configured to be draggable along the scrolling axis.");
            }

            DrawDragScrollbarField();
            DrawScrollbarField();
        }

        private void DrawDragScrollbarField()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(dragScrollbarProp, Labels.Scroller.DragScrollbarLabel);

            if (!EditorGUI.EndChangeCheck() || !ShowHelperDialogues)
            {
                return;
            }

            ShowDraggableHelperDialogues();
        }

        private void DrawScrollbarField()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(scrollbarVisualProp, Labels.Scroller.ScrollbarVisualLabel);

            if (!(EditorGUI.EndChangeCheck() && ShowHelperDialogues && scrollbarVisualProp.objectReferenceValue is UIBlock scrollbarBlock))
            {
                return;
            }

            // Bug with unity, GetComponentInParent returns null if not active
            if (scrollbarBlock.transform.parent == null ||
                scrollbarBlock.transform.parent.GetComponent<UIBlock>() == null)
            {
                EditorUtility.DisplayDialog($"Scrollbar must have parent {nameof(UIBlock)}", $"The scrollbar visual is positioned and sized relative to the bounds of its parent {nameof(UIBlock)}.\n\nPlease ensure the scrollbar is a child of a {nameof(UIBlock)}.", "Okay");
                scrollbarVisualProp.objectReferenceValue = null;
            }
            else
            {
                ShowDraggableHelperDialogues();
            }
        }

        private void ShowDraggableHelperDialogues()
        {
            if (!(dragScrollbarProp.boolValue && scrollbarVisualProp.objectReferenceValue is UIBlock scrollbarBlock))
            {
                return;
            }

            Scroller targetScroller = target as Scroller;

            // Interactable check
            if (!scrollbarBlock.TryGetComponent(out Interactable interactable))
            {
                bool addInteractable = EditorUtility.DisplayDialog($"Scrollbar missing {nameof(Interactable)} Component", $"The scrollbar must have an {nameof(Interactable)} component in order to be draggable.\n\nWould you like to add an {nameof(Interactable)} component to the scrollbar?", "Yes", "No");

                if (addInteractable)
                {
                    interactable = Undo.AddComponent<Interactable>(scrollbarBlock.gameObject);

                    if (targetScroller.UIBlock.AutoLayout.Enabled && targetScroller.UIBlock.AutoLayout.Axis != Axis.None)
                    {
                        ThreeD<bool> interactableDraggable = new ThreeD<bool>();
                        interactableDraggable[targetScroller.UIBlock.AutoLayout.Axis.Index()] = true;
                        interactable.Draggable = interactableDraggable;
                    }
                }
            }

            if (interactable == null)
            {
                return;
            }

            if (!targetScroller.UIBlock.AutoLayout.Enabled || targetScroller.UIBlock.AutoLayout.Axis == Axis.None)
            {
                return;
            }

            Axis autolayoutAxis = targetScroller.UIBlock.AutoLayout.Axis;
            ThreeD<bool> draggable = interactable.Draggable;
            if (!draggable[autolayoutAxis.Index()])
            {
                bool makeDraggable = EditorUtility.DisplayDialog($"Scrollbar not marked draggable", $"The {nameof(Interactable)} on the scrollbar must be marked as draggable on the scrolling axis.\n\nWould you like to make the scrollbar's {nameof(Interactable)} draggable on {targetScroller.UIBlock.AutoLayout.Axis}?", "Yes", "No");

                if (makeDraggable)
                {
                    draggable[autolayoutAxis.Index()] = true;

                    Undo.RecordObject(interactable, "Mark scrollbar draggable");
                    interactable.Draggable = draggable;
                }
            }
        }

        private void RefreshScrollbarList()
        {
            scrollbars.Clear();

            for (int i = 0; i < targetComponents.Count; ++i)
            {
                if (targetComponents[i].ScrollbarVisual == null)
                {
                    continue;
                }

                scrollbars.Add(targetComponents[i].ScrollbarVisual);
            }
        }
    }
}
