// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class Vector3Extensions
    {
        public static Vector3 DividedBy(this Vector3 numerator, Vector3 denominator, float divideByZeroValue = 0, bool validate = true)
        {
            if (!validate)
            {
                return new Vector3(numerator.x / denominator.x,
                                   numerator.y / denominator.y,
                                   numerator.z / denominator.z);
            }

            return new Vector3(denominator.x == 0 ? divideByZeroValue : numerator.x / denominator.x,
                               denominator.y == 0 ? divideByZeroValue : numerator.y / denominator.y,
                               denominator.z == 0 ? divideByZeroValue : numerator.z / denominator.z);
        }
    }
}
