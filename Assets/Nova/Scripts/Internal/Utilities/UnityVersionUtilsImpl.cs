// Copyright (c) Supernova Technologies LLC
using Nova.Compat;

namespace Nova.Internal.Utilities
{
    internal class UnityVersionUtilsImpl : UnityVersionUtils.Impl
    {
        public override UnityVersionUtils.UnityVersion Version
        {
            get
            {
#if UNITY_2022_1_OR_NEWER
                return UnityVersionUtils.UnityVersion.V2022;
#elif UNITY_2021_1_OR_NEWER
                return UnityVersionUtils.UnityVersion.V2021;
#else
                return UnityVersionUtils.UnityVersion.V2020;
#endif
            }
        }

        public override bool NewTMP =>
#if TMP_UV4
            true;
#else
            false;
#endif

        public static void Init()
        {
            UnityVersionUtils.Instance = new UnityVersionUtilsImpl();
        }
    }
}
