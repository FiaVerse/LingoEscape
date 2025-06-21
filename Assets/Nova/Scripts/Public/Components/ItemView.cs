// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Events;
using System;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// An abstract base class to be inherited by all user-defined, data-bindable <see cref="ItemView"/>.<see cref="ItemView.Visuals">Visuals</see> types.
    /// </summary>
    /// <remarks>All classes which inherit from <see cref="ItemVisuals"/> can be targeted by the UIBlock event system.</remarks>
    /// <seealso cref="ItemView"/>
    /// <seealso cref="ListView"/>
    /// <seealso cref="GridView"/>
    /// <seealso cref="UIBlockExtensions.AddGestureHandler{TInteractionEvent, TTarget}(UIBlock, UIEventHandler{TInteractionEvent, TTarget}))"/>
    /// <seealso cref="UIBlockExtensions.RemoveGestureHandler{TInteractionEvent, TTarget}(UIBlock, UIEventHandler{TInteractionEvent, TTarget})"/>
    /// <seealso cref="ListView.AddDataBinder{TData, TVisuals}(UIEventHandler{Data.OnBind{TData}, TVisuals, int})"/>
    /// <seealso cref="ListView.RemoveDataBinder{TData, TVisuals}(UIEventHandler{Data.OnBind{TData}, TVisuals, int})"/>
    /// <seealso cref="ListView.AddGestureHandler{TEvent, TVisuals}(UIEventHandler{TEvent, TVisuals, int})"/>
    /// <seealso cref="ListView.RemoveGestureHandler{TEvent, TVisuals}(UIEventHandler{TEvent, TVisuals, int})"/>
    [Serializable]
    public abstract class ItemVisuals : IEventTarget
    {
        /// <summary>
        /// The containing <see cref="ItemView"/>.
        /// </summary>
        [NonSerialized, HideInInspector]
        public ItemView View = null;
    }

    /// <summary>
    /// A callback to provide a list item prefab to the requesting <see cref="ListView"/> or <see cref="GridView"/> which will represent the object in the data source at the given index.
    /// </summary>
    /// <typeparam name="T">The index type of the underlying data source</typeparam>
    /// <param name="index">The index into the underlying data source of the object the provided prefab will represent</param>
    /// <param name="sourcePrefab">The source prefab (<i>not</i> an instance) for the <see cref="ListView"/> to clone or pull from the list item prefab pool</param>
    /// <returns>
    /// <see langword="true"/> if the caller wishes to use an instance of the provided source prefab<br/>
    /// <see langword="false"/> if the caller wishes to have the <see cref="ListView"/> attempt to use a fallback prefab option
    /// </returns>
    public delegate bool PrefabProviderCallback<T>(T index, out ItemView sourcePrefab);

    /// <summary>
    /// A UI Component which supports dynamic serialization of user-defined sets of visual fields (e.g. <see cref="Nova.UIBlock"/>s, any other <see cref="MonoBehaviour"/>s, etc.).
    /// The <see cref="ItemView"/> acts as a "middle man" when binding user-provided data types to a <see cref="ListView"/> or <see cref="GridView"/>
    /// </summary>
    [RequireComponent(typeof(UIBlock)), DisallowMultipleComponent]
    [HelpURL("https://novaui.io/manual/ItemView.html")]
    [AddComponentMenu("Nova/Item View")]
    public sealed class ItemView : MonoBehaviour, IEventTargetProvider, IGameObjectActiveReceiver
    {
        #region Public
        /// <summary>
        /// The set of visual fields configured in the Editor.
        /// </summary>
        public ItemVisuals Visuals
        {
            get
            {
                if (visuals != null)
                {
                    visuals.View = this;
                }

                return visuals;
            }
        }

        /// <summary>
        /// The <see cref="Nova.UIBlock"/> attached to <c>this.gameObject</c>.
        /// </summary>
        public UIBlock UIBlock
        {
            get
            {
                if (_uiBlock == null)
                {
                    TryGetComponent(out _uiBlock);
                }

                return _uiBlock;
            }
        }

        /// <summary>
        /// Retrieve <see cref="Visuals"/> as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to cast, must inherit from <see cref="ItemVisuals"/>.</typeparam>
        /// <param name="visuals"><see cref="Visuals"/> casted to type <typeparamref name="T"/>, if it can be casted.</param>
        /// <returns><see langword="false"/> if <see cref="Visuals"/> is not of type <typeparamref name="T"/>.</returns>
        public bool TryGetVisuals<T>(out T visuals) where T : ItemVisuals
        {
            visuals = Visuals as T;
            return visuals != null;
        }
        #endregion

        #region Internal
        [SerializeReference, SerializeField]
        private ItemVisuals visuals = default;

        [NonSerialized]
        private UIBlockActivator activator = null;

        [NonSerialized]
        private bool initialized = false;

        [NonSerialized]
        private UIBlock _uiBlock = null;

        internal Type TypeOfVisuals => HasVisuals ? Visuals.GetType() : null;
        internal bool HasVisuals => Visuals != null;

        internal void Bind<TData>(TData data)
        {
            if (!HasVisuals)
            {
                // nothing to bind
                return;
            }

            UIBlock.FireEvent(Data.Bind(this, data), TypeOfVisuals);
        }

        internal void Unbind<TData>(TData data)
        {
            if (!HasVisuals)
            {
                // nothing to unbind
                return;
            }

            UIBlock.FireEvent(Data.Unbind(this, data), TypeOfVisuals);
        }

        bool IEventTargetProvider.TryGetTarget(IEventTarget receiver, Type _, out IEventTarget target)
        {
            target = Visuals;
            return true;
        }

        void IGameObjectActiveReceiver.HandleOnEnable()
        {
            EnsureInitialized();
        }

        void IGameObjectActiveReceiver.HandleOnDisable() { }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (activator != null)
            {
                activator.Unregister(this);
            }

            UIBlock.UnregisterEventTargetProvider(this);
        }

        private void EnsureInitialized()
        {
            if (!NovaApplication.InPlayer(this))
            {
                return;
            }

            if (initialized)
            {
                return;
            }

            if (TryGetComponent(out activator))
            {
                activator.Register(this);
            }

            UIBlock.RegisterEventTargetProvider(this);
            initialized = true;
        }

        Type IEventTargetProvider.BaseTargetableType => typeof(ItemVisuals);

        #endregion
    }
}
