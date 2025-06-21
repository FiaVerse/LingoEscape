// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Internal.DataBinding
{
    internal class DataBinder<TData> : DataBinder
    {
        public override void InvokeBind(ItemView prefab, ListWrapper data, int index)
        {
            if (!data.TryGet(index, out TData userData) && index >= 0 && index < data.Count)
            {
                Debug.LogError("Mismatch between prefab and data type. Unable to invoke OnBind event");
                return;
            }


            prefab.Bind(userData);
        }

        public override void InvokeUnbind(ItemView prefab, ListWrapper data, int index)
        {
            if (!data.TryGet(index, out TData userData) && index >= 0 && index < data.Count)
            {
                Debug.LogError("Mismatch between prefab and data type. Unable to invoke OnUnbind event");
                return;
            }


            prefab.Unbind(userData);
        }
    }

    /// <summary>
    /// Non-generic base class for a data binder
    /// </summary>
    internal abstract class DataBinder
    {
        public abstract void InvokeBind(ItemView prefab, ListWrapper data, int index);
        public abstract void InvokeUnbind(ItemView prefab, ListWrapper data, int index);
    }
}
