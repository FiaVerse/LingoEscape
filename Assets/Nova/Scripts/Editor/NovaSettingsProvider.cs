// Copyright (c) Supernova Technologies LLC
using Nova.Editor.GUIs;
using Nova.Editor.Serialization;
using System.Collections.Generic;
using UnityEditor;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor
{
    internal class NovaSettingsProvider : SettingsProvider
    {
        private _SettingsConfig config = new _SettingsConfig();
        private SerializedObject serializedObject = null;

        public override void OnGUI(string searchContext)
        {
            if (serializedObject == null || config.SerializedProperty == null)
            {
                serializedObject = new SerializedObject(NovaSettings.Instance);
                config.SerializedProperty = serializedObject.FindProperty(Names.NovaSettings.settings);
            }

            serializedObject.UpdateIfRequiredOrScript();

            Foldout.InProjectSettings = true;
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

            Foldout.InProjectSettings = false;
        }

        private void MarkDirty(bool fireEvents)
        {
            serializedObject.ApplyModifiedProperties();
            if (fireEvents)
            {
                NovaSettings.Instance.MarkDirty(true, false);
            }
        }

        public NovaSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }

        [SettingsProvider]
        private static SettingsProvider CreateProjectSettingsProvider()
        {
            NovaSettingsProvider provider = new NovaSettingsProvider("Project/Nova", SettingsScope.Project, SettingsProvider.GetSearchKeywordsFromGUIContentProperties<Labels.Settings>());
            return provider;
        }
    }
}

