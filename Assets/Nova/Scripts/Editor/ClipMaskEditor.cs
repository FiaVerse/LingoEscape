// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using Nova.Internal.Utilities;
using UnityEditor;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(ClipMask))]
    [CanEditMultipleObjects]
    internal class ClipRectEditor : NovaEditor<ClipMask>
    {
        private _ClipMaskInfo wrapper = new _ClipMaskInfo();

        protected override void OnEnable()
        {
            base.OnEnable();
            wrapper.SerializedProperty = serializedObject.FindProperty(Names.ClipMask.info);
            Undo.undoRedoPerformed += RestoreUndoneRedoneProperties;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RestoreUndoneRedoneProperties;
        }

        private void RestoreUndoneRedoneProperties()
        {
            UpdateTargets();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            NovaGUI.ColorField(Labels.ClipMask.Tint, wrapper.ColorProp, true);
            NovaGUI.ToggleField(Labels.ClipMask.Clip, wrapper.ClipProp);
            SerializedProperty textureProp = serializedObject.FindProperty(Names.ClipMask.maskTexture);
            EditorGUILayout.ObjectField(textureProp, Labels.ClipMask.Mask);

            if (EditorGUI.EndChangeCheck())
            {
                UpdateTargets();
            }
        }

        private void UpdateTargets()
        {
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                targetComponents[i].RegisterOrUpdate();
            }
            EditModeUtils.QueueEditorUpdateNextFrame();
        }
    }
}
