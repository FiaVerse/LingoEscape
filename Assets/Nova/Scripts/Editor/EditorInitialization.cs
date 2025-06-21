// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;

namespace Nova.Editor.Utilities
{
    /// <summary>
    /// Handles initialization of Nova in editor
    /// </summary>
    internal static class EditorInitialization
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void Init()
        {
            Initialization.Init();
            SceneViewInput.CreateInstance();
            NavGraphDebugView.CreateInstance();
        }
    }
}

