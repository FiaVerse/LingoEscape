// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities.Extensions;
using System;
using System.Collections.Generic;

namespace Nova.Editor.GUIs
{
    internal abstract class NovaEditor<T> : NovaEditor
    {
        protected List<T> targetComponents = null;

        protected virtual void OnEnable()
        {
            targetComponents = serializedObject.targetObjects.CastTo<T>();
        }

        protected virtual void UpdateSerializedObjects()
        {
            serializedObject.Update();
        }

#if !NOVA_DEBUG
        [System.Reflection.Obfuscation]
        protected override bool ShouldHideOpenButton() => true;
#endif

    }

    internal abstract class NovaEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";

        public void DrawDefaultInspectorWithoutScript(params string[] propertiesToExclude)
        {
            string[] excludes = new string[propertiesToExclude.Length + 1];
            Array.Copy(propertiesToExclude, excludes, propertiesToExclude.Length);

            excludes[excludes.Length - 1] = ScriptPropertyPath;

            DrawPropertiesExcluding(excludes);
        }

        public void DrawDefaultInspectorWithoutScript()
        {
            DrawPropertiesExcluding(ScriptPropertyPath);
        }

        public void DrawPropertiesExcluding(params string[] propertiesToExclude)
        {
            serializedObject.UpdateIfRequiredOrScript();

            UnityEditor.EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, propertiesToExclude);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}

