// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    internal static class SerializeReferenceUtil
    {
        private static readonly GUIContent MixedValue = EditorGUIUtility.TrTextContent("\u2014", "Mixed Values");

        // Consistent with Unity's Add Component menu size
        private static readonly Vector2 MinDropdownSize = new Vector2(230, 315);

        private static SerializedProperty property = null;
        private static TypeSelectionDropdown dropdown = null;

        public static void PropertyField(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            SerializeReferenceUtil.property = property;

            bool showMixed = EditorGUI.showMixedValue;
            bool hasMultipleReferenceTypes = HasMultipleManagedReferenceTypes(property);

            if (hasMultipleReferenceTypes)
            {
                // In this scenario, the label won't be drawn
                // by the property field, so we do it explicitly
                EditorGUI.PrefixLabel(position, label);
            }

            EditorGUI.showMixedValue = hasMultipleReferenceTypes;
            SerializeReferenceMenu(position, label);
            EditorGUI.showMixedValue = showMixed;

            if (!hasMultipleReferenceTypes)
            {
                EditorGUI.PropertyField(position, property, includeChildren: true);
            }

            EditorGUI.EndProperty();
        }

        private static void SerializeReferenceMenu(Rect position, GUIContent label)
        {
            Rect typeSelectionField = position;

            float labelWidth = Mathf.Max(EditorStyles.boldLabel.CalcSize(label).x, NovaGUI.LabelWidth);

            typeSelectionField.x += labelWidth + NovaGUI.MinSpaceBetweenFields;
            typeSelectionField.width = position.width - labelWidth - NovaGUI.MinSpaceBetweenFields;
            typeSelectionField.height = EditorGUIUtility.singleLineHeight;

            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                Type type = GetSerializeReferenceType(property.managedReferenceFullTypename); ;


                bool assigned = type != null;
                string className = assigned ? type.Name : "None (Unassigned)";
                string assemblyName = assigned ? type.Assembly.FullName : "Null";

                string labelText = ObjectNames.NicifyVariableName(className.Substring(className.LastIndexOf(".") + 1));

                if (type != null)
                {
                    TypeMenuNameAttribute typeName = type.GetCustomAttribute<TypeMenuNameAttribute>();

                    if (typeName != null && !string.IsNullOrWhiteSpace(typeName.DisplayName))
                    {
                        labelText = typeName.DisplayName;
                    }
                }

                GUIContent typeLabel = EditorGUI.showMixedValue ? MixedValue : new GUIContent(labelText, className + " (" + assemblyName + ")");

                bool showContextMenu = GUI.Button(typeSelectionField, typeLabel, EditorStyles.popup);

                if (!showContextMenu)
                {
                    return;
                }

                if (dropdown == null)
                {
                    Type serializeReferenceType = GetSerializeReferenceType(property.managedReferenceFieldTypename);
                    if (serializeReferenceType == null)
                    {
                        Debug.LogError($"SerializeReference type, [{property.managedReferenceFieldTypename}], not found.");
                        return;
                    }

                    dropdown = new TypeSelectionDropdown(serializeReferenceType, "Visuals");
                    dropdown.OnTypeSelected += HandleTypeSelected;
                }

                dropdown.MinSize = MinDropdownSize;
                dropdown.Show(typeSelectionField);
            }
        }

        private static void HandleTypeSelected(Type selectedType)
        {
            if (selectedType == null)
            {
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            UnityEngine.Object[] targetObjects = property.serializedObject.targetObjects;

            if (targetObjects == null || targetObjects.Length == 0)
            {
                targetObjects = new UnityEngine.Object[1] { property.serializedObject.targetObject };
            }

            foreach (UnityEngine.Object target in targetObjects)
            {
                SerializedObject serializedObject = new SerializedObject(target);
                SerializedProperty prop = serializedObject.FindProperty(property.propertyPath);

                if (GetSerializeReferenceType(prop.managedReferenceFullTypename) != selectedType)
                {
                    prop.managedReferenceValue = Activator.CreateInstance(selectedType);
                    prop.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        public static Type GetSerializeReferenceType(string stringType)
        {
            (string assemblyName, string className) = GetSplitNamesFromTypename(stringType);

            return Type.GetType($"{className}, {assemblyName}");
        }

        public static (string AssemblyName, string ClassName) GetSplitNamesFromTypename(string typename)
        {
            if (string.IsNullOrEmpty(typename))
            {
                return ("", "");
            }

            string[] typeSplitString = typename.Split(char.Parse(" "));
            string typeClassName = typeSplitString[1];
            string typeAssemblyName = typeSplitString[0];
            return (typeAssemblyName, typeClassName);
        }

        public static bool HasMultipleManagedReferenceTypes(SerializedProperty property)
        {
            UnityEngine.Object[] targetObjects = property.serializedObject.targetObjects;

            if (targetObjects == null || targetObjects.Length < 2)
            {
                return false;
            }

            string managedReferenceType = property.managedReferenceFullTypename;

            foreach (UnityEngine.Object target in targetObjects)
            {
                SerializedObject serializedObject = new SerializedObject(target);
                SerializedProperty prop = serializedObject.FindProperty(property.propertyPath);

                if (managedReferenceType != prop.managedReferenceFullTypename)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
