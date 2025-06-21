// Copyright (c) Supernova Technologies LLC
using System;
using UnityEditor;

namespace Nova.Editor.Utilities
{
    internal struct MixedValueScope : IDisposable
    {
        private bool isValid;

        public void Dispose()
        {
            if (isValid)
            {
                EditorGUI.showMixedValue = false;
            }
        }

        public static MixedValueScope Create()
        {
            EditorGUI.showMixedValue = true;
            return new MixedValueScope()
            {
                isValid = true,
            };
        }
    }
}

