// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Animations;
using Nova.Internal.Rendering;

namespace Nova.Editor.Utilities
{
    internal static class PlayModeCleanup
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void TryCleanUpPlayMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged += (state) =>
            {
                // Need to dispose in both cases here because it seems like Exiting Play Mode
                // comes through mid-teardown, so some components haven't been disable/destroyed
                // yet, and if they triggered scheduling animations on disable/destroy, then we'll
                // have invalid animations in edit mode.
                bool shouldDispose = state == UnityEditor.PlayModeStateChange.EnteredEditMode || state == UnityEditor.PlayModeStateChange.ExitingPlayMode;

                if (shouldDispose && AnimationEngine.Instance != null)
                {
                    AnimationEngine.Instance.Dispose();
                }

                // Annoyingly, Unity sometimes clears the contents of textures when going between
                // play mode and edit mode. When you have the build target set to android
                // (meaning we need to track decompressed copies of the textures for texture packing)
                // and you have domain reloads disabled, we continue to track cleared textures thinking they
                // still have their old contents (as Unity doesn't give any indication that the texture contents
                // has been cleared). So we just clear our caches of decompressed texture copies.
                RenderingDataStore.Instance.EditorOnly_CleanupDecompressedTextures();
            };
        }
    }
}

