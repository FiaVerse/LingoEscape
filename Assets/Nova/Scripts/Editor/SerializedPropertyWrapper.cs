// Copyright (c) Supernova Technologies LLC
using UnityEditor;

namespace Nova.Editor.Serialization
{
    internal interface ISerializedPropertyWrapper
    {
        SerializedProperty SerializedProperty { get; set; }
    }
}

