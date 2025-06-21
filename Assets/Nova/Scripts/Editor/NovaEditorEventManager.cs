// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Editor.Utilities;
using Nova.Internal;
using Nova.Internal.Rendering;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    /// <summary>
    /// Handles the various editor events to ensure Nova responds properly
    /// </summary>
    internal static class NovaEditorEventManager
    {
        private static bool wasUndoRedo = false;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            ObjectChangeEvents.changesPublished -= HandleChanges;
            ObjectChangeEvents.changesPublished += HandleChanges;

            Undo.undoRedoPerformed -= HandleUndoRedo;
            Undo.undoRedoPerformed += HandleUndoRedo;

            EngineManager.EditorOnly_OnAfterEngineUpdate -= ClearUndoRedo;
            EngineManager.EditorOnly_OnAfterEngineUpdate += ClearUndoRedo;

            TMPro_EventManager.FONT_PROPERTY_EVENT.Add(HandleTMPFontSettingsChanged);

            SceneVisibilityManager.visibilityChanged += HandleVisibilityChanged;
        }

        private static void HandleVisibilityChanged()
        {
            VisibilityManager.ReprocessAll();
        }

        public static event System.Action PlayerSettingsChanged = null;

        private static void HandleTMPFontSettingsChanged(bool arg1, Object arg2)
        {
            TMP_FontAsset fontAsset = arg2 as TMP_FontAsset;
            if (fontAsset == null)
            {
                return;
            }

            UpdateTMPFont(fontAsset.material.GetInstanceID(), fontAsset.material);
        }

        private static void HandleUndoRedo()
        {
            wasUndoRedo = true;
        }

        private static void ClearUndoRedo()
        {
            wasUndoRedo = false;
        }

        private static void HandleChanges(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                ObjectChangeKind objectChangeKind = stream.GetEventType(i);


                switch (objectChangeKind)
                {
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        {
                            stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs data);
                            HandleEvent(ref data);
                            break;
                        }
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        {
                            stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs data);
                            HandleEvent(ref data);
                            break;
                        }
                    case ObjectChangeKind.ChangeGameObjectParent:
                        {
                            stream.GetChangeGameObjectParentEvent(i, out ChangeGameObjectParentEventArgs data);
                            HandleEvent(ref data);
                            break;
                        }
                }
            }

            wasUndoRedo = false;
        }

        private static void HandleEvent(ref ChangeAssetObjectPropertiesEventArgs data)
        {
            Object obj = EditorUtility.InstanceIDToObject(data.instanceId);

            switch (obj)
            {
                case Material material:
                    if (!material.shader.name.Contains("TextMeshPro"))
                    {
                        break;
                    }

                    UpdateTMPFont(data.instanceId, material);
                    break;

                case PlayerSettings playerSettings:
                    PlayerSettingsChanged?.Invoke();
                    break;
            }
        }

        private static void UpdateTMPFont(int instanceID, Material tmpMaterial)
        {
            if (!MaterialCache.HandleTMPFontPropertyChanged(instanceID, tmpMaterial))
            {
                return;
            }

            NovaApplication.QueueEditorPlayerLoop();
        }

        private static void HandleEvent(ref ChangeGameObjectOrComponentPropertiesEventArgs data)
        {
            Object obj = EditorUtility.InstanceIDToObject(data.instanceId);

            switch (obj)
            {
                case GameObject gameObject:
                    {
                        if (!LayoutTransformRecorder.IsTrackedTransform(gameObject.transform))
                        {
                            break;
                        }

                        if (gameObject.TryGetComponent(out UIBlock block))
                        {
                            // In order to handle when the user changes the game objects layer,
                            // we unfortunately have to mark the game object as dirty whenever it changes since these
                            // events don't give us any info about *what* actually changed
                            block.GameObjectLayer = block.GameObjectLayer;
                        }
                        break;
                    }
                case Transform transform:
                    {
                        if (!LayoutTransformRecorder.IsTrackedTransform(transform))
                        {
                            break;
                        }

                        if (transform.TryGetComponent(out UIBlock block))
                        {
                            if (wasUndoRedo)
                            {
                                // Handles undo/redo for OnTransformChildrenChanged events,
                                // since the transform change events on CoreBlock don't fire in that context.
                                block.EditorOnly_EnsureChildren();
                            }

                            if (block.gameObject.activeInHierarchy)
                            {
                                LayoutTransformRecorder.CopyFromTransform(block);
                            }
                        }

                        break;
                    }
            }
        }

        private static void HandleEvent(ref ChangeGameObjectParentEventArgs data)
        {
            Object obj = EditorUtility.InstanceIDToObject(data.instanceId);

            switch (obj)
            {
                case Transform transform:
                    {
                        if (!LayoutTransformRecorder.IsTrackedTransform(transform))
                        {
                            break;
                        }

                        if (wasUndoRedo && transform.TryGetComponent(out UIBlock block))
                        {
                            // Handles undo/redo for OnTransformParentChanged events,
                            // since the transform change events on CoreBlock don't fire in that context.
                            block.EditorOnly_EnsureParent();
                        }

                        break;
                    }
            }
        }
    }
}

