// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;

namespace Nova
{
    internal class NovaSettingsSystem : System<NovaSettingsSystem>
    {
        protected override void Dispose()
        {
            Internal.NovaSettings.Dispose();
        }

        protected override void Init()
        {
            Internal.NovaSettings.OnInitRequested += LazyInit;
        }

        private static void LazyInit()
        {
            if (NovaSettings.Initialized)
            {
                Internal.NovaSettings.Init(NovaSettings.Instance);
            }
        }
    }
}
