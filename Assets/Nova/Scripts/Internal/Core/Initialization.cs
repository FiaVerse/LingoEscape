// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Utilities;
using UnityEngine;

namespace Nova.Internal.Core
{
    /// <summary>
    /// Handles initializing the data stores and engines
    /// </summary>
    internal static class Initialization
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Init()
        {
            // Init the utils for core/uncore
            PrefabStageUtilsImpl.Init();
            UnityVersionUtilsImpl.Init();
            QualitySettingsUtilsImpl.Init();

            NovaSettingsSystem.CreateInstance();
            DataStoreSystem.CreateInstance();
            EngineManager.CreateInstance();

            // CreateInstance() won't create a new instance
            // if one already exists, but we need to at
            // least reset a flag on the engine manager
            // whenever we enter play mode
            EngineManager.ResetUpdateState();

            Nova.Interaction.Init();
            Navigation.Init();
        }
    }
}
