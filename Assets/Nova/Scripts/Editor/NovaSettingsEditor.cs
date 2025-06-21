// Copyright (c) Supernova Technologies LLC
using Nova.Editor.GUIs;
using Nova.Editor.Serialization;
using UnityEditor;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor
{
    [CustomEditor(typeof(NovaSettings))]
    internal class NovaSettingsEditor : NovaEditor
    {
        private _SettingsConfig config = new _SettingsConfig();

        private void OnEnable()
        {
            config.SerializedProperty = serializedObject.FindProperty(Names.NovaSettings.settings);
            Undo.undoRedoPerformed += RestoreUndoneRedoneProperties;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RestoreUndoneRedoneProperties;
        }

        private void RestoreUndoneRedoneProperties()
        {
            MarkDirty(true);
            serializedObject.UpdateIfRequiredOrScript();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUI.BeginChangeCheck();
            NovaSettingsEditors.DrawGeneral(config);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(false);
            }

            EditorGUI.BeginChangeCheck();
            NovaSettingsEditors.DrawRendering(config);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(true);
            }

            EditorGUI.BeginChangeCheck();
            NovaSettingsEditors.DrawInput(config);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(false);
            }

            EditorGUI.BeginChangeCheck();
            NovaSettingsEditors.DrawEditor(serializedObject);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(false);
            }
        }

        private void MarkDirty(bool fireEvents)
        {
            serializedObject.ApplyModifiedProperties();
            if (fireEvents)
            {
                NovaSettings.Instance.MarkDirty(true, false);
            }
        }
    }
}
