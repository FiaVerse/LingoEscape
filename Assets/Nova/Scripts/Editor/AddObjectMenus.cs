// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Nova.Editor
{
    internal static class AddObjectMenus
    {
        [MenuItem("GameObject/Nova/UIBlock 2D", false, 8)]
        private static void AddUIBlock2D(MenuCommand menuCommand)
        {
            Add<UIBlock2D, Tools.UIBlockTool>(menuCommand, "UIBlock2D").CopyToDataStore();
        }

        [MenuItem("GameObject/Nova/TextBlock", false, 9)]
        private static void AddTextBlock(MenuCommand menuCommand)
        {
            Add<TextBlock, Tools.UIBlockTool>(menuCommand, "TextBlock").CopyToDataStore();
        }

        [MenuItem("GameObject/Nova/UIBlock 3D", false, 10)]
        private static void AddUIBlock3D(MenuCommand menuCommand)
        {
            Add<UIBlock3D, Tools.UIBlockTool>(menuCommand, "UIBlock3D").CopyToDataStore();
        }

        [MenuItem("GameObject/Nova/UIBlock", false, 11)]
        private static void AddUIBlock(MenuCommand menuCommand)
        {
            Add<UIBlock, Tools.UIBlockTool>(menuCommand, "UIBlock").CopyToDataStore();
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Nova/Button", false, 112)]
#else
        [MenuItem("GameObject/Nova/Controls/Button", false, 12)]
#endif
        private static void AddButton(MenuCommand menuCommand)
        {
            if (NovaSettings.Instance.ButtonPrefab == null)
            {
                Debug.LogError("Button prefab source unassigned. A prefab can be assigned under Project Settings > Nova.");
                return;
            }

            InstantiatePrefab(NovaSettings.Instance.ButtonPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Nova/Toggle", false, 113)]
#else
        [MenuItem("GameObject/Nova/Controls/Toggle", false, 13)]
#endif
        private static void AddToggle(MenuCommand menuCommand)
        {
            if (NovaSettings.Instance.TogglePrefab == null)
            {
                Debug.LogError("Toggle prefab source unassigned. A prefab can be assigned under Project Settings > Nova.");
                return;
            }

            InstantiatePrefab(NovaSettings.Instance.TogglePrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Nova/Slider", false, 114)]
#else
        [MenuItem("GameObject/Nova/Controls/Slider", false, 14)]
#endif
        private static void AddSlider(MenuCommand menuCommand)
        {
            if (NovaSettings.Instance.SliderPrefab == null)
            {
                Debug.LogError("Slider prefab source unassigned. A prefab can be assigned under Project Settings > Nova.");
                return;
            }

            InstantiatePrefab(NovaSettings.Instance.SliderPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Nova/Dropdown", false, 115)]
#else
        [MenuItem("GameObject/Nova/Controls/Dropdown", false, 15)]
#endif
        private static void AddDropdown(MenuCommand menuCommand)
        {
            if (NovaSettings.Instance.DropdownPrefab == null)
            {
                Debug.LogError("Dropdown prefab source unassigned. A prefab can be assigned under Project Settings > Nova.");
                return;
            }

            InstantiatePrefab(NovaSettings.Instance.DropdownPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Nova/Text Field", false, 116)]
#else
        [MenuItem("GameObject/Nova/Controls/Text Field", false, 16)]
#endif
        private static void AddTextField(MenuCommand menuCommand)
        {
            if (NovaSettings.Instance.TextFieldPrefab == null)
            {
                Debug.LogError("Text Field prefab source unassigned. A prefab can be assigned under Project Settings > Nova.");
                return;
            }

            InstantiatePrefab(NovaSettings.Instance.TextFieldPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Nova/Scroll View", false, 117)]
#else
        [MenuItem("GameObject/Nova/Controls/Scroll View", false, 17)]
#endif
        private static void AddScrollView(MenuCommand menuCommand)
        {
            if (NovaSettings.Instance.ScrollViewPrefab == null)
            {
                Debug.LogError("Scroll View prefab source unassigned. A prefab can be assigned under Project Settings > Nova.");
                return;
            }

            InstantiatePrefab(NovaSettings.Instance.ScrollViewPrefab, menuCommand);
        }

#if UNITY_2021_1_OR_NEWER
        [MenuItem("GameObject/Nova/UI Root", false, 118)]
#else
        [MenuItem("GameObject/Nova/Controls/UI Root", false, 18)]
#endif
        private static void AddUIRoot(MenuCommand menuCommand)
        {
            if (NovaSettings.Instance.UIRootPrefab == null)
            {
                Debug.LogError("UI Root prefab source unassigned. A prefab can be assigned under Project Settings > Nova.");
                return;
            }

            InstantiatePrefab(NovaSettings.Instance.UIRootPrefab, menuCommand);
        }

        private static void InstantiatePrefab(UIBlock uiBlock, MenuCommand menuCommand)
        {
            // Just clone in play mode, since that's more consistent with
            // Unity behavior of dragging a prefab into the scene while playing
            bool createPrefab = !Application.IsPlaying(UnityEditor.SceneManagement.StageUtility.GetCurrentStage());

            GameObject go =  createPrefab ? (PrefabUtility.InstantiatePrefab(uiBlock) as UIBlock).gameObject : Object.Instantiate(uiBlock).gameObject;

            if (menuCommand.context is GameObject parent)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }
            else
            {
                UnityEditor.SceneManagement.StageUtility.PlaceGameObjectInCurrentStage(go);
            }

            if (go != null)
            {
                go.transform.SetAsLastSibling();
            }

            Selection.activeObject = go;
        }

        private static T Add<T, TTool>(MenuCommand menuCommand, string name = "GameObject") where T : Component where TTool : UnityEditor.EditorTools.EditorTool
        {
            // Create a custom game object
            GameObject go = new GameObject(name);

            if (menuCommand.context is GameObject parent)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }
            else
            {
                UnityEditor.SceneManagement.StageUtility.PlaceGameObjectInCurrentStage(go);
            }

            T addedComponent = go.AddComponent<T>();

            Preset[] presets = Preset.GetDefaultPresetsForObject(addedComponent);

            if (presets != null && presets.Length > 0 && presets[0] != null)
            {
                presets[0].ApplyTo(addedComponent);
            }

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeGameObject = go;

            EditorApplication.delayCall += () =>
            {
                if (!ActiveEditorUtils.TryGetActiveEditorTargetType<T>(out _))
                {
                    // editor window not active, which can happen if the inspector
                    // is locked to another object or if the single-frame delay
                    // is insufficient (thanks Unity), so the tool can't be set active.
                    return;
                }

                UnityEditor.EditorTools.ToolManager.SetActiveTool<TTool>();
            };

            return addedComponent;
        }
    }
}

