// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(ScreenSpace))]
    [CanEditMultipleObjects]
    internal class ScreenSpaceEditor : NovaEditor<ScreenSpace>
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            Undo.undoRedoPerformed += MarkDirty;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDirty;
        }

        public override void OnInspectorGUI()
        {
            // Target Camera
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.ObjectField(serializedObject.FindProperty(Names.ScreenSpace.targetCamera), typeof(Camera), Labels.ScreenSpace.TargetCamera);
            var fillModeProp = serializedObject.FindProperty(Names.ScreenSpace.fillMode);
            NovaGUI.EnumField(Labels.ScreenSpace.Mode, fillModeProp, targetComponents[0].Mode);

            var fillModeValue = (ScreenSpace.FillMode)fillModeProp.intValue;
            if (fillModeValue == ScreenSpace.FillMode.FixedWidth || fillModeValue == ScreenSpace.FillMode.FixedHeight)
            {
                NovaGUI.Vector2Field(Labels.ScreenSpace.ReferenceResolution, serializedObject.FindProperty(Names.ScreenSpace.referenceResolution));
            }

            NovaGUI.FloatFieldClamped(Labels.ScreenSpace.PlaneDistance, serializedObject.FindProperty(Names.ScreenSpace.planeDistance), 0f, float.MaxValue);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(Names.ScreenSpace.additionalCameras), Labels.ScreenSpace.AdditionalCameras);

            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
        }

        private void MarkDirty()
        {
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                targetComponents[i].RegisterOrUpdate();
            }
        }
    }
}

