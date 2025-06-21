// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Utilities
{
    internal static class IndexVerifier
    {
        public static bool ValidIndex(int index, int length)
        {
            return index >= 0 && index < length;
        }
    }

}
