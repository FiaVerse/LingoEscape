// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using System;

namespace Nova.Internal.Utilities
{
    internal static class EditModeUtils
    {
        /// <summary>
        /// Can this get stuck as true...?
        /// </summary>
        [NonSerialized]
        private static bool queued;

        /// <summary>
        /// Will queue a player loop update for the *following* edit mode frame
        /// </summary>
        public static void QueueEditorUpdateNextFrame()
        {
            if (!NovaApplication.IsEditor)
            {
                return;
            }

            if (queued || NovaApplication.IsPlaying)
            {
                return;
            }

            queued = true;
            NovaApplication.EditorDelayCall += () =>
            {
                NovaApplication.QueueEditorPlayerLoop();
                queued = false;
            };

        }
    }
}
