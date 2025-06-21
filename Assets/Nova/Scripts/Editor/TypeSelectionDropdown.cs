// Copyright (c) Supernova Technologies LLC
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    internal class TypeSelectionDropdown : AdvancedDropdown
    {
        private struct TypePath
        {
            public string Path;
            public Type Type;
        }

        private const string DefaultTitle = "Select Type";

        public event System.Action<Type> OnTypeSelected;

        private Dictionary<string, int> typeNames = new Dictionary<string, int>();
        private List<TypePath> sortedTypes = new List<TypePath>();

        private string dropdownTitle = null;

        public Vector2 MinSize { get => minimumSize; set => minimumSize = value; }

        public TypeSelectionDropdown(Type baseType, string title = DefaultTitle) : base(new AdvancedDropdownState())
        {
            dropdownTitle = title;

            // Get the types
            TypeCache.TypeCollection derivedTypes = TypeCache.GetTypesDerivedFrom(baseType);

            for (int i = 0; i < derivedTypes.Count; ++i)
            {
                Type derivedType = derivedTypes[i];

                if (derivedType.IsAbstract)
                {
                    continue;
                }

                string fullPath = GetFullPath(derivedType);

                if (typeNames.TryGetValue(fullPath, out int count))
                {
                    count++;
                }
                else
                {
                    count = 1;
                }

                typeNames[fullPath] = count;
                sortedTypes.Add(new TypePath() { Type = derivedType, Path = fullPath });
            }

            sortedTypes.Sort(new AlphabeticSorter());
        }

        private static string GetFullPath(Type type)
        {
            TypeMenuPathAttribute pathAttribute = type.GetCustomAttribute<TypeMenuPathAttribute>();
            TypeMenuNameAttribute nameAttribute = type.GetCustomAttribute<TypeMenuNameAttribute>();

            string name = nameAttribute != null && !string.IsNullOrWhiteSpace(nameAttribute.DisplayName) ? nameAttribute.DisplayName : ObjectNames.NicifyVariableName(type.Name);
            string path = pathAttribute != null && !string.IsNullOrWhiteSpace(pathAttribute.Path) ? pathAttribute.Path : string.Empty;

            return string.IsNullOrWhiteSpace(path) ? name : $"{path}/{name}";
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(dropdownTitle);

            int itemCount = 0;

            root.AddChild(new TypeSelectionDropdownItem(null, "None (Unassigned)")
            {
                id = itemCount++
            });

            for (int i = 0; i < sortedTypes.Count; ++i)
            {
                TypePath typePath = sortedTypes[i];

                Type type = typePath.Type;
                string fullPath = typePath.Path;
                string[] subPaths = fullPath.Split('\\', '/');
                string label = subPaths[subPaths.Length - 1];

                if (typeNames[fullPath] > 1)
                {
                    string assemblyName = type.Assembly.ToString().Split('(', ',')[0];
                    label = $"{label} [{type.Namespace}, Assembly: {assemblyName}]";
                }

                var item = new TypeSelectionDropdownItem(type, label)
                {
                    id = itemCount++,
                };

                GetParentForPath(root, subPaths, 0).AddChild(item);
            }

            return root;
        }

        private static AdvancedDropdownItem GetParentForPath(AdvancedDropdownItem parent, string[] subPaths, int subPathIndex)
        {
            if (subPaths == null || subPathIndex >= subPaths.Length - 1)
            {
                return parent;
            }

            AdvancedDropdownItem next = parent.children.Where(x => string.Compare(x.name, subPaths[subPathIndex], ignoreCase: true) == 0 && !(x is TypeSelectionDropdownItem)).FirstOrDefault();

            if (next == null)
            {
                next = new AdvancedDropdownItem(subPaths[subPathIndex]);
                parent.AddChild(next);
            }

            return GetParentForPath(next, subPaths, subPathIndex + 1);
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            base.ItemSelected(item);

            if (!item.enabled)
            {
                return;
            }

            if (item is TypeSelectionDropdownItem typePopupItem)
            {
                OnTypeSelected?.Invoke(typePopupItem.Type);
            }
        }

        private struct AlphabeticSorter : IComparer<TypePath>
        {
            public int Compare(TypePath x, TypePath y)
            {
                return x.Path.CompareTo(y.Path);
            }
        }

        private class TypeSelectionDropdownItem : AdvancedDropdownItem
        {
            public Type Type { get; }

            public TypeSelectionDropdownItem(Type type, string name) : base(name)
            {
                Type = type;
            }
        }
    }
}

