// Copyright (c) Supernova Technologies LLC
using System;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class ArrayExtensions
    {
        public static void Memset<T>(this T[] array, T valueToWrite)
        {
            int length = array.Length;

            if (length == 0)
            {
                return;
            }

            array[0] = valueToWrite;

            int count = 1;
            while (count <= length / 2)
            {
                Array.Copy(array, 0, array, count, count);
                count *= 2;
            }

            Array.Copy(array, 0, array, count, length - count);
        }
    }
}

