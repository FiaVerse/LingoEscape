// Copyright (c) Supernova Technologies LLC
using System;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.Utilities
{
    internal static class ActiveEditorUtils
    {
        /// <summary>
        /// Given a base component type, T, returns true if the 
        /// editor window for that component is active and provides
        /// the derived type of the editor target as an out param.
        /// Returns false if there's no active editor for the base 
        /// component type.
        /// </summary>
        public static bool TryGetActiveEditorTargetType<T>(out Type targetType) where T : Component
        {
            UnityEditor.Editor[] editors = ActiveEditorTracker.sharedTracker.activeEditors;

            targetType = null;

            if (editors == null)
            {
                return false;
            }

            for (int i = 0; i < editors.Length; ++i)
            {
                UnityEditor.Editor editor = editors[i];

                if (editor == null || editor.target == null)
                {
                    continue;
                }

                Type editorTargetType = editor.target.GetType();

                if (typeof(T).IsAssignableFrom(editorTargetType))
                {
                    targetType = editorTargetType;
                    return true;
                }
            }

            return false;
        }
    }
}
