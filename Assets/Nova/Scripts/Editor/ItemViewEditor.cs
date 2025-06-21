// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(ItemView)), CanEditMultipleObjects]
    internal class ItemViewEditor : NovaEditor
    {
        private SerializedProperty visualsProp = null;
        private static readonly MethodInfo GetAssemblyMethod = typeof(MonoScript).GetMethod("GetAssemblyName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo GetNamespaceMethod = typeof(MonoScript).GetMethod("GetNamespace", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly GUIContent EditScriptLabel = EditorGUIUtility.TrTextContent("Edit Visuals Script", "Open the script where the selected \"Visuals\" type is defined.");

        private void OnEnable()
        {
            visualsProp = serializedObject.FindProperty(Names.ItemView.visuals);
        }

        public override void OnInspectorGUI()
        {
            bool hasMultipleTypes = SerializeReferenceUtil.HasMultipleManagedReferenceTypes(visualsProp);
            float visualsHeight = hasMultipleTypes ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(visualsProp, includeChildren: true);
            Rect position = EditorGUILayout.GetControlRect(GUILayout.Height(visualsHeight));

            SerializeReferenceUtil.PropertyField(position, visualsProp, Labels.ItemView.Visuals);

            serializedObject.ApplyModifiedProperties();

            if (hasMultipleTypes || GetAssemblyMethod == null)
            {
                return;
            }

            System.Type visualsType = SerializeReferenceUtil.GetSerializeReferenceType(visualsProp.managedReferenceFullTypename);

            if (visualsType == null)
            {
                return;
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(EditScriptLabel))
            {
                OpenFile(visualsType);
            }
        }

        private static async void OpenFile(System.Type type)
        {
            if (type == null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:script");

            string typeAssemblyName = type.Assembly.GetName().Name;

            bool hasNamespace = type.Namespace != null;
            string typeNamespace = hasNamespace ? type.Namespace : string.Empty;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null)
                {
                    continue;
                }

                string scriptAssembly = GetAssembly(script);

                if (string.IsNullOrWhiteSpace(scriptAssembly))
                {
                    continue;
                }

                if (hasNamespace && script.GetClass() != null)
                {
                    string scriptNamespace = GetNamespace(script);

                    if (typeNamespace != scriptNamespace)
                    {
                        continue;
                    }
                }

                string assemblyName = scriptAssembly.Replace(".dll", string.Empty);

                if (typeAssemblyName != assemblyName)
                {
                    continue;
                }

                string text = script.text;

                bool match = await Task.Run(() =>
                {
                    return Regex.IsMatch(text, $"class[ ]+{type.Name}[ ]*:");
                });

                if (match)
                {
                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }
        }

        private static string GetAssembly(MonoScript script)
        {
            return GetAssemblyMethod?.Invoke(script, null) as string;
        }

        private static string GetNamespace(MonoScript script)
        {
            return GetNamespaceMethod?.Invoke(script, null) as string;
        }
    }
}
