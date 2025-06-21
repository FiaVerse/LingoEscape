// Copyright (c) Supernova Technologies LLC
using System;
using System.Collections.Generic;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value from the dictionary, otherwise creates a new instance and adds it.
        /// Returns true if it already existed in the dictionary, and false if the object is new
        /// </summary>
        /// <typeparam name="KeyType"></typeparam>
        /// <typeparam name="ValType"></typeparam>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public static bool GetOrAdd<KeyType, ValType>(this Dictionary<KeyType, ValType> dict, KeyType key, out ValType val) where ValType : class, new()
        {
            if (dict.TryGetValue(key, out val))
            {
                return true;
            }
            else
            {
                val = new ValType();
                dict.Add(key, val);
                return false;
            }
        }

        public static void Deconstruct<T1, T2>(this KeyValuePair<T1, T2> tuple, out T1 key, out T2 value)
        {
            key = tuple.Key;
            value = tuple.Value;
        }

        public static void DisposeValues<K, V>(this Dictionary<K, V> dict) where V : IDisposable
        {
            foreach (var val in dict.Values)
            {
                val.Dispose();
            }
        }
    }
}

