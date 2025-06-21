// Copyright (c) Supernova Technologies LLC
namespace Nova.Compat
{
    internal class UnityVersionUtils
    {
        public enum UnityVersion
        {
            V2020 = 0,
            V2021 = 1,
            V2022 = 2,
        };

        public abstract class Impl
        {
            public abstract UnityVersion Version { get; }

            /// <summary>
            /// Hate that we have this here
            /// </summary>
            public abstract bool NewTMP { get; }
        }

        public static Impl Instance = null;

        public static bool Is2022OrNewer => Instance != null && (int)Instance.Version >= (int)UnityVersion.V2022;
        public static bool NewTMP => Instance != null && Instance.NewTMP;
    }
}

