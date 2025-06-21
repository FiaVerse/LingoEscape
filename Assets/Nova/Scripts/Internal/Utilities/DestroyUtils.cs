// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using UnityEngine;

namespace Nova.Internal.Utilities
{
    internal static class DestroyUtils
    {
        /// <summary>
        /// Horrible name, I know. It just destroys the component properly depending on the
        /// play/editor state
        /// </summary>
        /// <param name="obj"></param>
        public static void Destroy(Object obj)
        {
            if (NovaApplication.IsEditor)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(obj);
                }
                else
                {
                    Object.DestroyImmediate(obj);
                }
            }
            else
            {
                Object.Destroy(obj);
            }
        }

        public static void SafeDestroy(Object obj)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }
}

