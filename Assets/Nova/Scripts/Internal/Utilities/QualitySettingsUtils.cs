// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Utilities
{
    internal class QualitySettingsUtils
    {
        public abstract class Impl
        {
            public abstract int GlobalTextureMipMapLimit { get; }
        }

        public static Impl Instance = null;

        public static int GlobalTextureMipMapLimit => Instance != null ? Instance.GlobalTextureMipMapLimit : 0;
    }
}
