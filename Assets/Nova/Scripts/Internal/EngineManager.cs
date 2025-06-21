// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using System;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Nova.Internal
{
    internal class EngineManager : System<EngineManager>
    {
        /// <summary>
        /// The engines.
        /// NOTE: The order here matters, as the order will be order in which the updates run.
        /// </summary>
        private static EngineBase[] engines = null;

        /// <summary>
        /// Invoked before PreUpdate
        /// </summary>
        public static event Action EditorOnly_OnBeforeEngineUpdate = null;

        /// <summary>
        /// Invoked after CompleteUpdate but before PostUpdate
        /// </summary>
        public static event Action EditorOnly_OnAfterEngineUpdate = null;

        private static bool Initialized { get; set; } = false;


        private EngineUpdateInfo engineUpdateInfo;
        private bool isUpdating = false;

        public void UpdateElement(DataStoreID dataStoreID)
        {
            if (isUpdating)
            {
                return;
            }

            isUpdating = true;
            using (new Layouts.LayoutDataStore.UpdateScope(this))
            {
                Layouts.LayoutEngine.Instance.UpdateLayoutElement(dataStoreID, secondPass: false);
                Rendering.RenderEngine.Instance.ProcessTextBlocks(ref Layouts.LayoutEngine.Instance.EngineCache.AllProcessedElements);

                if (Layouts.LayoutDataStore.Instance.LayoutsNeedSecondPass)
                {
                    Layouts.LayoutEngine.Instance.UpdateLayoutElement(dataStoreID, secondPass: true);
                }
            }

            isUpdating = false;
        }

        private bool UpdateEngines => Hierarchy.HierarchyDataStore.Instance.Elements.Count > 0;

        /// <summary>
        /// Whether or not the engines have run yet. Will only be false on the
        /// first frame
        /// </summary>
        public bool HaveUpdated { get; private set; } = false;

        public static void ResetUpdateState()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.HaveUpdated = false;
        }

        protected override void Init()
        {
            try
            {
                if (Initialized)
                {
                    return;
                }

                engineUpdateInfo.Init();

                // Special casing the animation engine because it has its own
                // Update loop, so including it with the other engines is unnecessary
                Animations.AnimationEngine animationEngine = new Animations.AnimationEngine();
                animationEngine.Init();

                engines = new EngineBase[]
                {
                    new Hierarchy.HierarchyEngine(),
                    new Layouts.LayoutEngine(),
                    new Rendering.RenderEngine(),
                };

                for (int i = 0; i < engines.Length; ++i)
                {
                    engines[i].Init();
                }

                // Input Engine doesn't need a per-frame update but has 
                // the same lifetime as the other engines. Must be created
                // after the other engines are initialized
                Input.InputEngine.CreateInstance();

                PlayerLoopSystem currentSystem = PlayerLoop.GetCurrentPlayerLoop();

                if (!TryInsertEngineUpdate(ref currentSystem))
                {
                    Debug.LogError("Nova failed to insert engine update and will not work properly");
                    return;
                }
                PlayerLoop.SetPlayerLoop(currentSystem);

                Initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Nova initialization failed with {e}");
            }
        }

        /// <summary>
        /// In order to have our update run after all other updates, but before the cameras render, we
        /// need to find Unity's PostLateUpdate and insert ourselves right before that. This might not be in the
        /// top level if the user or another system has modified the player loop already
        /// </summary>
        private bool TryInsertEngineUpdate(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null)
            {
                return false;
            }

            // First check the top level
            for (int i = 0; i < system.subSystemList.Length; ++i)
            {
                if (system.subSystemList[i].type == typeof(UnityEngine.PlayerLoop.Initialization))
                {
                    PlayerLoopSystem[] preupdateSystems = system.subSystemList[i].subSystemList;
                    PlayerLoopSystem[] newPreUpdateSystems = new PlayerLoopSystem[preupdateSystems.Length + 1];
                    newPreUpdateSystems[0] = new PlayerLoopSystem()
                    {
                        type = typeof(NovaEngine.NovaNavigation),
                        updateDelegate = Input.InputEngine.PreUpdate
                    };

                    Array.Copy(preupdateSystems, sourceIndex: 0, newPreUpdateSystems, destinationIndex: 1, preupdateSystems.Length);

                    system.subSystemList[i].subSystemList = newPreUpdateSystems;

                    continue;
                }

                // We found the PostLateUpdate system
                if (system.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.PostLateUpdate))
                {
                    continue;
                }

                PlayerLoopSystem[] subSystemList = system.subSystemList[i].subSystemList;
                PlayerLoopSystem[] newLoops = new PlayerLoopSystem[subSystemList.Length + 2];

                // insert animation system first, we just want to run after user code and before other Unity systems (e.g. Canvas)
                newLoops[0] = new PlayerLoopSystem()
                {
                    type = typeof(NovaEngine.NovaAnimator),
                    updateDelegate = Animations.AnimationEngine.Instance.Update
                };

                for (int j = 0; j < subSystemList.Length; ++j)
                {
                    if (subSystemList[j].type != typeof(UnityEngine.PlayerLoop.PostLateUpdate.PlayerUpdateCanvases))
                    {
                        continue;
                    }

                    // Copy the elements before
                    Array.Copy(subSystemList, sourceIndex: 0, newLoops, destinationIndex: 1, j + 1);

                    // Insert the Nova update
                    newLoops[j + 2] = new PlayerLoopSystem()
                    {
                        type = typeof(NovaEngine),
                        updateDelegate = Update
                    };

                    if (j + 1 < subSystemList.Length)
                    {
                        Array.Copy(subSystemList, sourceIndex: j + 1, newLoops, destinationIndex: j + 3, subSystemList.Length - j - 1);
                    }

                    system.subSystemList[i].subSystemList = newLoops;

                    return true;
                }

                // Insert last if we didn't find the Canvas update
                newLoops[newLoops.Length - 1] = new PlayerLoopSystem()
                {
                    type = typeof(NovaEngine),
                    updateDelegate = Update
                };

                return true;
            }

            // Wasn't in the top level, so recurse down into each subsystem
            for (int i = 0; i < system.subSystemList.Length; ++i)
            {
                PlayerLoopSystem subSystem = system.subSystemList[i];
                if (!TryInsertEngineUpdate(ref subSystem))
                {
                    continue;
                }

                system.subSystemList[i] = subSystem;
                return true;
            }

            return false;
        }

        private bool TryRemoveUpdateLoop(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null)
            {
                return false;
            }

            for (int i = 0; i < system.subSystemList.Length; ++i)
            {
                if (system.subSystemList[i].type == typeof(UnityEngine.PlayerLoop.Initialization))
                {
                    PlayerLoopSystem[] preupdateSystems = system.subSystemList[i].subSystemList;
                    PlayerLoopSystem[] newPreUpdateSystems = new PlayerLoopSystem[preupdateSystems.Length - 1];

                    int index = 0;

                    for (int j = 0; j < preupdateSystems.Length; ++j)
                    {
                        PlayerLoopSystem subsytem = preupdateSystems[j];

                        bool isNavigationSystem = subsytem.type == typeof(NovaEngine.NovaNavigation);

                        // we expect this to be here, but we don't exactly know how many elements will be between them
                        if (!isNavigationSystem)
                        {
                            newPreUpdateSystems[index++] = preupdateSystems[j];
                        }
                    }

                    system.subSystemList[i].subSystemList = newPreUpdateSystems;

                    continue;
                }

                if (system.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.PostLateUpdate))
                {
                    continue;
                }

                PlayerLoopSystem[] subSystemList = system.subSystemList[i].subSystemList;
                PlayerLoopSystem[] newLoops = new PlayerLoopSystem[subSystemList.Length - 2];

                int destIndex = 0;

                for (int j = 0; j < subSystemList.Length; ++j)
                {
                    PlayerLoopSystem subsytem = subSystemList[j];

                    bool isEngine = subsytem.type == typeof(NovaEngine);
                    bool isAnimationSystem = subsytem.type == typeof(NovaEngine.NovaAnimator);

                    // we expect both these to be here, but we don't exactly know how many elements will be between them
                    if (!isEngine && !isAnimationSystem)
                    {
                        newLoops[destIndex++] = subSystemList[j];
                    }
                }

                system.subSystemList[i].subSystemList = newLoops;

                return true;
            }

            // Wasn't in the top level, so recurse down into each subsystem
            for (int i = 0; i < system.subSystemList.Length; ++i)
            {
                PlayerLoopSystem subSystem = system.subSystemList[i];
                if (!TryRemoveUpdateLoop(ref subSystem))
                {
                    continue;
                }
                system.subSystemList[i] = subSystem;
                return true;
            }

            return false;
        }

        private bool inFailureState = false;
        internal void Update()
        {
            if (inFailureState || !UpdateEngines)
            {
                return;
            }

            if (NovaApplication.IsEditor)
            {
                EditorOnly_OnBeforeEngineUpdate?.Invoke();
            }

            // This needs to happen before layout preupdate, but we do this manually (instead of 
            // in LateUpdate) to keep support with ECS, so people can update their camera in
            // PresentationSystemGroup
            if (Rendering.RenderingDataStore.Instance != null)
            {
                Rendering.RenderingDataStore.Instance.UpdateScreenSpaces();
            }

            isUpdating = true;
            HaveUpdated = true;

            JobHandle preUpdateHandle = default(JobHandle);

            {
                try
                {
                    for (int i = 0; i < engines.Length; ++i)
                    {
                        preUpdateHandle = engines[i].PreUpdate(preUpdateHandle);
                    }
                }
                catch (Exception e)
                {
                    inFailureState = true;
                    Debug.LogError($"NovaEngine CleanUp failed with {e}");
                }
                finally
                {
                    preUpdateHandle.Complete();
                }
            }

            engineUpdateInfo.Clear();

            {
                try
                {
                    for (int i = 0; i < engines.Length; ++i)
                    {
                        engines[i].UpdateFirstPass(ref engineUpdateInfo);
                    }
                }
                catch (Exception e)
                {
                    inFailureState = true;
                    Debug.LogError($"NovaEngine Update failed with {e}");
                }
            }

            Rendering.RenderEngine.Instance.EnsureTextMeshes();

            {
                try
                {
                    for (int i = 0; i < engines.Length; ++i)
                    {
                        engines[i].UpdateSecondPass(ref engineUpdateInfo);
                    }
                }
                catch (Exception e)
                {
                    inFailureState = true;
                    Debug.LogError($"NovaEngine Update failed with {e}");
                }
            }

            // Ensure this gets completed regardless of what was run
            engineUpdateInfo.EngineSequenceCompleteHandle.Complete();

            {
                try
                {
                    for (int i = 0; i < engines.Length; ++i)
                    {
                        engines[i].CompleteUpdate();
                    }
                }
                catch (Exception e)
                {
                    inFailureState = true;
                    Debug.LogError($"NovaEngine CompleteUpdate failed with {e}");
                }
            }

            isUpdating = false;

            if (NovaApplication.IsEditor)
            {
                EditorOnly_OnAfterEngineUpdate?.Invoke();
            }

            {
                try
                {
                    for (int i = 0; i < engines.Length; ++i)
                    {
                        engines[i].PostUpdate();
                    }
                }
                catch (Exception e)
                {
                    inFailureState = true;
                    Debug.LogError($"NovaEngine PostUpdate failed with {e}");
                }
            }
        }

        protected override void Dispose()
        {
            if (!Initialized)
            {
                return;
            }

            // Ensure any running update completes.
            engineUpdateInfo.EngineSequenceCompleteHandle.Complete();

            Animations.AnimationEngine.Instance.Dispose();

            for (int i = 0; i < engines.Length; ++i)
            {
                engines[i].Dispose();
            }

            // Input.InputEngine will get disposed automatically
            engineUpdateInfo.Dispose();

            engines = null;
            Initialized = false;
            HaveUpdated = false;
        }

        internal void EditorOnly_RemoveFromUpdateLoop()
        {
            PlayerLoopSystem currentSystem = PlayerLoop.GetCurrentPlayerLoop();
            TryRemoveUpdateLoop(ref currentSystem);
            PlayerLoop.SetPlayerLoop(currentSystem);
        }
    }
}

