// Copyright (c) Supernova Technologies LLC
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Editor.Utilities.Extensions
{
    internal static class ObjectArrayExtensions
    {
        public static List<T> CastTo<T>(this Object[] objects)
        {
            List<T> list = new List<T>();
            for (int i = 0; i < objects.Length; ++i)
            {
                if (objects[i] is T t)
                {
                    list.Add(t);
                }
            }
            return list;
        }
    }
}

