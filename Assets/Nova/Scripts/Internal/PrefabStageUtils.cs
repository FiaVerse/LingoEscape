// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Compat
{
    internal static class PrefabStageUtils
    {
        public abstract class Impl
        {
            public abstract bool IsInPrefabStage { get; }

            public abstract bool TryGetCurrentStageRoot(out GameObject root);
        }

        public static Impl Instance = null;

        public static bool IsInPrefabStage => Instance != null ? Instance.IsInPrefabStage : false;

        public static bool TryGetCurrentStageRoot(out GameObject root)
        {
            if (Instance != null)
            {
                return Instance.TryGetCurrentStageRoot(out root);
            }
            else
            {
                root = null;
                return false;
            }
        }
    }
}
