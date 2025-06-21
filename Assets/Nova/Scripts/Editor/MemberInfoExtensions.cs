// Copyright (c) Supernova Technologies LLC
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Nova.Editor.Utilities.Extensions
{
    internal static class MemberInfoExtensions 
    {
        public static bool HasCustomAttribute<AttributeType>(this MemberInfo memberInfo)
        {
            return memberInfo.GetCustomAttribute(typeof(AttributeType), false) != null;
        }
    }
}

