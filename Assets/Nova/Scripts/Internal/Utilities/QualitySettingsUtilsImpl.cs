// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Internal.Utilities
{
    internal class QualitySettingsUtilsImpl : QualitySettingsUtils.Impl
    {
        public override int GlobalTextureMipMapLimit =>
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit;
#else
            QualitySettings.masterTextureLimit;
#endif

        public static void Init()
        {
            QualitySettingsUtils.Instance = new QualitySettingsUtilsImpl();
        }
    }
}
