// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;
using System.Collections.Generic;

namespace Nova.Internal
{
    internal class DataStoreSystem : System<DataStoreSystem>
    {
        internal List<IDataStore> dataStores = new List<IDataStore>()
        {
            new Hierarchy.HierarchyDataStore(),
            new Layouts.LayoutDataStore(),
            new Rendering.RenderingDataStore(),
        };

        protected override void Dispose()
        {
            for (int i = 0; i < dataStores.Count; ++i)
            {
                dataStores[i].Dispose();
            }
        }

        protected override void Init()
        {
            for (int i = 0; i < dataStores.Count; ++i)
            {
                dataStores[i].Init();
            }
        }
    }
}

