// Copyright (c) Supernova Technologies LLC
//#define DEBUG_VISUALS

using Nova.Compat;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Input;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using UnityEngine;
using static Nova.Interaction;

namespace Nova
{
    /// <summary>
    /// A callback to invoke whenever navigation focus for a particular <paramref name="controlID"/> moves to a new <see cref="UIBlock"/>, <paramref name="focused"/>. 
    /// </summary>
    /// <param name="controlID">The unique identifier of the input control whose navigation focus is now set on <paramref name="focused"/>.</param>
    /// <param name="focused">The <see cref="UIBlock"/> which will now receive subsequent <see cref="Navigate.OnSelect"/> events. Can be <c>null</c>.</param>
    public delegate void NavigationFocusChangedCallback(uint controlID, UIBlock focused);

    /// <summary>
    /// The static access point to provide input navigation and selection events.
    /// </summary>
    /// <seealso cref="Navigate.OnMoveTo"/>
    /// <seealso cref="Navigate.OnMoveFrom"/>
    /// <seealso cref="Navigate.OnDirection"/>
    /// <seealso cref="Navigate.OnSelect"/>
    /// <seealso cref="Navigate.OnDeselect"/>
    public static class Navigation
    {
        /// <summary>
        /// Event fired after the navigation queue has been processed, if a new <see cref="UIBlock"/> 
        /// with an attached <see cref="GestureRecognizer"/> component receives navigation focus
        /// from a particular controlID.
        /// </summary>
        public static event NavigationFocusChangedCallback OnNavigationFocusChanged = null;

        /// <summary>
        /// Moves navigation focus to <paramref name="uiBlock"/>.
        /// </summary>
        /// <param name="uiBlock">The <see cref="UIBlock"/> used as the starting point for subsequent navigation events.</param>
        /// <param name="controlID">The unique identifier of the input control generating this <see cref="Update"/>.</param>
        /// <param name="userData">Any additional data to pass along to the receiver of the current interaction. See <see cref="Update.UserData"/>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <exception cref="System.ArgumentException">If <paramref name="controlID"/> is out of range or if <paramref name="uiBlock"/> is not navigable.</exception>
        public static void Focus(UIBlock uiBlock, uint controlID = 0, object userData = null, int layerMask = AllLayers)
        {
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                throw new System.ArgumentException($"Invalid Control ID [{controlID}]. Expected within range [0, {InputRouter.MaxControls}).");
            }

            UIBlock prev = Navigator<UIBlock>.Current(controlID);

            if (prev == uiBlock)
            {
                return;
            }

            Ray ray = CreateRay(prev, uiBlock);

            if (uiBlock != null)
            {
                if ((layerMask & (1 << uiBlock.gameObject.layer)) == 0)
                {
                    throw new System.ArgumentException($"{uiBlock.name} not on focusable layer provided by {nameof(layerMask)}.");
                }

                if (!uiBlock.TryGetComponent(out GestureRecognizer nav))
                {
                    throw new System.ArgumentException($"{uiBlock.name} not navigable. Navigation starting point must have an {nameof(Interactable)} or {nameof(Scroller)} component attached, enabled, and configured to be navigable.");
                }

                if (!nav.enabled)
                {
                    throw new System.ArgumentException($"{uiBlock.name} not navigable. Attached {nav.GetType().Name} must be enabled and configured to be navigable.");
                }

                if (!nav.Navigable)
                {
                    throw new System.ArgumentException($"{uiBlock.name} not navigable. Attached {nav.GetType().Name} must have {nameof(GestureRecognizer.Navigable)} set to \"True\".");
                }
            }

            ExecuteMove(BackFocusBuffer, NavMove.Implicit(prev, uiBlock, new Update(ray, controlID, userData), layerMask, allowAutoEntry: uiBlock != null));
        }

        /// <summary>
        /// Queues an attempt to move navigation focus to the next navigable <see cref="UIBlock"/> in the approximate local
        /// space direction to be processed at the start of the next frame, after the Nova Engine 
        /// update has run for the current frame. 
        /// </summary>
        /// <param name="direction">The direction to navigate in the local space of the current focused element.</param>
        /// <param name="controlID">The unique identifier of the input control generating this <see cref="Update"/>.</param>
        /// <param name="userData">Any additional data to pass along to the receiver of the current interaction. See <see cref="Update.UserData"/>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <exception cref="System.ArgumentException">If <paramref name="controlID"/> is out of range.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// If <see cref="Focus(UIBlock, uint, object, int)"/> has not been called for the given <paramref name="controlID"/>.
        /// </exception>
        public static void Move(Vector3 direction, uint controlID = 0, object userData = null, int layerMask = AllLayers)
        {
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                throw new System.ArgumentException($"Invalid Control ID [{controlID}]. Expected within range [0, {InputRouter.MaxControls})");
            }

            UIBlock fromUIBlock = Navigator<UIBlock>.Current(controlID);

            if (fromUIBlock == null)
            {
                throw new System.InvalidOperationException($"Unable to move navigation focus. Must call {nameof(Focus)} with a navigable {nameof(UIBlock)} and matching {nameof(controlID)} before any calls to {nameof(Move)}.");
            }

            direction.Normalize();

            GestureRecognizer currentRecognizer = fromUIBlock.InputTarget.Nav as GestureRecognizer;
            bool canJumpUpScopes = currentRecognizer != null && currentRecognizer.Navigation.GetLink(direction).Fallback == NavLinkFallback.NavigateOutsideScope;

            int scopeCount = Navigator<UIBlock>.ScopeCount(controlID);

            if (!canJumpUpScopes)
            {
                scopeCount = Mathf.Min(1, scopeCount);
            }

            IUIBlock next = null;
            MoveResult moveResult = MoveResult.None;

            if (scopeCount == 0)
            {
                // Ensure we still do a manual navigation
                // check even if there's no navigation scope
                moveResult = Navigator<UIBlock>.TryLoadNext(fromUIBlock, null, direction, layerMask, out next);
            }

            for (int i = 0; i < scopeCount; ++i)
            {
                UIBlock scope = Navigator<UIBlock>.GetScope(controlID, i);
                bool validScope = scope != null;
                bool routeToScope = validScope && scope.InputTarget.CaptureNavInput;

                if (routeToScope)
                {
                    Update directionUpdate = new Update(Navigator<UIBlock>.CreateRay(direction, scope, castInside: true), controlID, userData);
                    scope.FireEvent(Navigate.Direction(directionUpdate, scope, direction));
                    return;
                }

                moveResult = Navigator<UIBlock>.TryLoadNext(fromUIBlock, scope, direction, layerMask, out next);

                if (moveResult != MoveResult.None)
                {
                    IUIBlock position = next != null ? next : fromUIBlock;
                    Navigator<UIBlock>.EnsureInView(position, controlID, i);

                    break;
                }

                if (!validScope)
                {
                    // Move up the stack if the
                    // scope has been destroyed
                    continue;
                }

                if ((scope.InputTarget.Nav is GestureRecognizer gr) && gr.Navigation.GetLink(direction).Fallback != NavLinkFallback.NavigateOutsideScope)
                {
                    break;
                }
            }

            NavMove move = moveResult == MoveResult.Found ? NavMove.Explicit(fromUIBlock, next as UIBlock, new Update(Navigator<UIBlock>.CreateRay(direction, fromUIBlock), controlID, userData), direction, layerMask) :
                                                            NavMove.Query(controlID, direction, userData, layerMask);

            WriteQueue.Add(move);
        }

        /// <summary>
        /// Selects the currently focused <see cref="UIBlock"/>.
        /// </summary>
        /// <param name="controlID">The unique identifier of the input control generating this <see cref="Update"/>.</param>
        /// <param name="userData">Any additional data to pass along to the receiver of the current interaction. See <see cref="Update.UserData"/>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <exception cref="System.ArgumentException">If <paramref name="controlID"/> is out of range.</exception>
        public static void Select(uint controlID = 0, object userData = null, int layerMask = AllLayers)
        {
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                throw new System.ArgumentException($"Invalid Control ID [{controlID}]. Expected within range [0, {InputRouter.MaxControls})");
            }

            UIBlock uiBlock = Navigator<UIBlock>.Current(controlID);

            if (uiBlock == null)
            {
                // Nothing to select
                return;
            }

            GestureRecognizer selectedNode = uiBlock.InputTarget.Nav as GestureRecognizer;

            if (selectedNode == null)
            {
                // Selected UIBlock no longer navigable
                return;
            }

            if ((layerMask & (1 << uiBlock.gameObject.layer)) == 0)
            {
                Debug.LogWarning($"Unable to select. Focused {nameof(UIBlock)}, {uiBlock.name}, not on selectable layer provided by {nameof(layerMask)}.", uiBlock);
                return;
            }

            bool isNewSelection = Navigator<UIBlock>.GetScope(controlID) != uiBlock;

            if (!isNewSelection)
            {
                return;
            }

            Ray ray = new Ray(uiBlock.transform.position, -uiBlock.transform.forward);
            Update update = new Update(ray, controlID, userData);

            switch (selectedNode.OnSelect)
            {
                case SelectBehavior.Click:
                    {
                        uiBlock.FireEvent(Gesture.Click(update, uiBlock));
                        break;
                    }
                case SelectBehavior.FireEvents:
                    {
                        Navigator<UIBlock>.PushScope(controlID, uiBlock, layerMask);
                        uiBlock.FireEvent(Navigate.Select(update, uiBlock));
                        break;
                    }
                case SelectBehavior.ScopeNavigation:
                    {
                        Navigator<UIBlock>.PushScope(controlID, uiBlock, layerMask);
                        uiBlock.FireEvent(Navigate.Select(update, uiBlock));

                        if (uiBlock.ChildCount == 0)
                        {
                            break;
                        }

                        WriteQueue.Add(NavMove.Deferred(controlID, uiBlock, userData, layerMask));

                        break;
                    }
            }
        }

        /// <summary>
        /// Deselects the currently selected <see cref="UIBlock"/>.
        /// </summary>
        /// <param name="controlID">The unique identifier of the input control generating this <see cref="Update"/>.</param>
        /// <param name="userData">Any additional data to pass along to the receiver of the current interaction. See <see cref="Update.UserData"/>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <exception cref="System.ArgumentException">If <paramref name="controlID"/> is out of range.</exception>
        public static void Deselect(uint controlID = 0, object userData = null, int layerMask = AllLayers)
        {
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                throw new System.ArgumentException($"Invalid Control ID [{controlID}]. Expected within range [0, {InputRouter.MaxControls})");
            }

            UIBlock fromUIBlock = Navigator<UIBlock>.Current(controlID);
            UIBlock formerScope = Navigator<UIBlock>.PopScope(controlID);

            if (formerScope != null)
            {
                Ray ray = CreateRay(fromUIBlock, formerScope);

                Update update = new Update(ray, controlID, userData);

                ExecuteMove(BackFocusBuffer, NavMove.Implicit(fromUIBlock, formerScope, update, layerMask));

                formerScope.FireEvent(Navigate.Deselect(update, formerScope));
            }
        }

        /// <summary>
        /// Retrieves the <see cref="UIBlock"/> which was most recently navigated to from the provided <paramref name="controlID"/>.
        /// </summary>
        /// <param name="controlID">The unique identifier of the input control.</param>
        /// <param name="focused">The <see cref="UIBlock"/> most recently navigated to with the given <paramref name="controlID"/>.</param>
        /// <returns>If a navigated <see cref="UIBlock"/> is found and actively navigable, returns <c>true</c>. If the <see cref="UIBlock"/> was not found or is no longer navigable, returns <c>false</c>.</returns>
        public static bool TryGetFocusedUIBlock(uint controlID, out UIBlock focused)
        {
            focused = null;
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                return false;
            }

            focused = Navigator<UIBlock>.Current(controlID);

            if (focused != null && focused.InputTarget.Nav != null)
            {
                return true;
            }

            focused = null;
            return false;
        }

        /// <summary>
        /// Retrieves the actively selected <see cref="UIBlock"/> for the provided <paramref name="controlID"/>, if it exists.
        /// </summary>
        /// <remarks>
        /// If the <see cref="SelectBehavior"/> of the focused element is set to <see cref="SelectBehavior.Click"/>,
        /// it will not be considered "selected" after it's clicked. Only elements whose <see cref="SelectBehavior"/> is set
        /// to <see cref="SelectBehavior.ScopeNavigation"/> or <see cref="SelectBehavior.FireEvents"/> are "selectable".
        /// </remarks>
        /// <param name="controlID">The unique identifier of the input control.</param>
        /// <param name="selected">The actively selected <see cref="UIBlock"/> for the given <paramref name="controlID"/>.</param>
        /// <returns>If a selected <see cref="UIBlock"/> is found and actively navigable, returns <c>true</c>. If the <see cref="UIBlock"/> was not found or is no longer navigable, returns <c>false</c>.</returns>
        public static bool TryGetSelectedUIBlock(uint controlID, out UIBlock selected)
        {
            selected = null;
            if (controlID < 0 || controlID >= InputRouter.MaxControls)
            {
                return false;
            }

            selected = Navigator<UIBlock>.GetScope(controlID);

            if (selected != null && selected.InputTarget.Nav != null)
            {
                return true;
            }

            selected = null;
            return false;
        }

        /// <summary>
        /// Performs a navigation-specific raycast against all navigable <see cref="UIBlock"/>s under <paramref name="scope"/> and retrieves a <see cref="UIBlockHit"/> for the nearest navigable <see cref="UIBlock"/> in the general direction of the provided <paramref name="ray"/>.
        /// </summary>
        /// <param name="ray">A ray, in worldspace, to use for the navigation-specific raycast.</param>
        /// <param name="blockHit">A <see cref="UIBlockHit"/> for the top-most-rendered navigable <see cref="UIBlock"/> in the provided <paramref name="ray"/>'s general direction.</param>
        /// <param name="ignore">The <see cref="UIBlock"/> to exclude from the results, defaults to <c>null</c>. Useful for when a specific <see cref="UIBlock"/> should be filtered out.</param>
        /// <param name="scope">The root of <see cref="UIBlock"/>s to test against, defaults to <c>null</c>. All <see cref="UIBlock"/>s in the scene are tested when <c><paramref name="scope"/> == null</c>.</param>
        /// <param name="navigablesOnly">Only include results with navigable elements? Defaults to <c>true</c>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        /// <returns><c>true</c> if <i>any</i> <see cref="UIBlock"/> (on the <paramref name="layerMask"/>) is in the general direction of the provided <paramref name="ray"/>.</returns>
        public static bool NavCast(Ray ray, out UIBlockHit blockHit, UIBlock ignore = null, UIBlock scope = null, bool navigablesOnly = true, int layerMask = AllLayers)
        {
            DataStoreID rootID = scope == null ? DataStoreID.Invalid : scope.ID;
            DataStoreID excludeID = ignore == null ? DataStoreID.Invalid : ignore.ID;

            if (Navigator<UIBlock>.Raycast(ray, layerMask, rootID, excludeID, navigablesOnly, out HitTestResult result))
            {
                blockHit = ToPublic(result);
                return true;
            }

            blockHit = default;
            return false;
        }

        /// <summary>
        /// Performs a navigation-specific raycast against all navigable <see cref="UIBlock"/>s under <paramref name="scope"/> and populates the provided list of <see cref="UIBlockHit"/>s for all navigable <see cref="UIBlock"/>s in the general direction of the provided <paramref name="ray"/>.
        /// </summary>
        /// <param name="ray">A ray, in worldspace, to use for the navigation-specific raycast.</param>
        /// <param name="hitsToPopulate">The list to populate with all <see cref="UIBlockHit"/> collisions, sorted by closest to the ray origin and top-most-rendered (at index 0).</param>
        /// <param name="ignore">The <see cref="UIBlock"/> to exclude from the results, defaults to <c>null</c>. Useful for when a specific <see cref="UIBlock"/> should be filtered out.</param>
        /// <param name="scope">The root of <see cref="UIBlock"/>s to test against, defaults to <c>null</c>. All <see cref="UIBlock"/>s in the scene are tested when <c><paramref name="scope"/> == null</c>.</param>
        /// <param name="navigablesOnly">Only include results with navigable elements? Defaults to <c>true</c>.</param>
        /// <param name="layerMask">The gameobject layers to include, defaults to "All Layers".</param>
        public static void NavCastAll(Ray ray, List<UIBlockHit> hitsToPopulate, UIBlock ignore = null, UIBlock scope = null, bool navigablesOnly = true, int layerMask = AllLayers)
        {
            hitsToPopulate.Clear();

            DataStoreID rootID = scope == null ? DataStoreID.Invalid : scope.ID;
            DataStoreID excludeID = ignore == null ? DataStoreID.Invalid : ignore.ID;

            ReadOnlyList<HitTestResult> results = Navigator<UIBlock>.RaycastAll(ray, layerMask, rootID, excludeID, navigablesOnly);

            for (int i = 0; i < results.Count; ++i)
            {
                hitsToPopulate.Add(ToPublic(results[i]));
            }
        }

        #region Internal API
        private static List<NavMove> WriteQueue => queuedMoveBuffers[1];
        private static List<NavMove> ReadQueue => queuedMoveBuffers[0];

        private static FocusBuffer FrontFocusBuffer => focusChangeBuffers[0];
        private static FocusBuffer BackFocusBuffer => focusChangeBuffers[1];

        private static List<NavMove>[] queuedMoveBuffers = null;
        private static FocusBuffer[] focusChangeBuffers = null;

        internal static void Init()
        {
            if (!NovaApplication.IsPlaying)
            {
                return;
            }

            queuedMoveBuffers = new List<NavMove>[] { new List<NavMove>(), new List<NavMove>() };
            focusChangeBuffers = new FocusBuffer[] { new FocusBuffer(), new FocusBuffer() };

            Navigator<UIBlock>.OnCurrentPathUntracked -= HandlePathUntracked;
            Navigator<UIBlock>.OnCurrentPathUntracked += HandlePathUntracked;

            InputEngine.OnPreUpdate -= DequeueAll;
            InputEngine.OnPreUpdate += DequeueAll;

            OnNavigationFocusChanged = null;
        }

        private static void DequeueAll()
        {
            if (!NovaApplication.IsPlaying || !EngineManager.Instance.HaveUpdated)
            {
                return;
            }

            SwapBuffers();

            List<NavMove> queuedMoves = ReadQueue;
            FocusBuffer previousFrameFocusChanges = FrontFocusBuffer;

            if (queuedMoves.Count == 0 && previousFrameFocusChanges.Count == 0)
            {
                return;
            }

            for (int i = 0; i < queuedMoves.Count; ++i)
            {
                NavMove move = queuedMoves[i];

                if (move.Type == MoveType.Deferred)
                {
                    move = ProcessDeferred(move);
                }

                if (move.Type == MoveType.Query)
                {
                    move = ProcessQuery(move);
                }

                if (move.Type == MoveType.Skip)
                {
                    continue;
                }

                ExecuteMove(previousFrameFocusChanges, move);
            }

            queuedMoves.Clear();

            FocusBuffer focusChangesWhileDequeuing = BackFocusBuffer;

            ReadOnlyList<uint> controlIDs = focusChangesWhileDequeuing.Controls;

            for (int i = controlIDs.Count - 1; i >= 0; --i)
            {
                uint controlID = controlIDs[i];

                if (focusChangesWhileDequeuing.TryGet(controlID, out UIBlock focused))
                {
                    previousFrameFocusChanges.Set(controlID, focused);
                    focusChangesWhileDequeuing.Remove(controlID);
                }
            }

            controlIDs = previousFrameFocusChanges.Controls;

            for (int i = 0; i < controlIDs.Count; ++i)
            {
                uint controlID = controlIDs[i];

                if (previousFrameFocusChanges.TryGet(controlID, out UIBlock focused))
                {
                    if (focused != null)
                    {
                        // focused can be null when nothing has focus for the given control ID
                        Navigator<UIBlock>.EnsureInView(focused, controlID, 0);
                    }

                    ExecuteFocusChange(controlID, focused);
                }
            }

            previousFrameFocusChanges.Clear();
        }

        private static void SwapBuffers()
        {
            // Swap buffers to dequeue
            List<NavMove> readQueue = queuedMoveBuffers[0];
            queuedMoveBuffers[0] = queuedMoveBuffers[1];
            queuedMoveBuffers[1] = readQueue;

            FocusBuffer frontBuffer = focusChangeBuffers[0];
            focusChangeBuffers[0] = focusChangeBuffers[1];
            focusChangeBuffers[1] = frontBuffer;
        }

        private static void HandlePathUntracked(uint controlID, UIBlock fromUIBlock, UIBlock toUIBlock, int layerMask)
        {
            if (toUIBlock == null)
            {
                EnqueueFocusChange(BackFocusBuffer, fromUIBlock, toUIBlock, controlID);
                return;
            }

            UIBlock scope = Navigator<UIBlock>.GetScope(controlID);

            if (scope == toUIBlock && Navigator<UIBlock>.TryGetFirstNavigableDescendant(scope, out Vector3 direction, out Ray ray, layerMask, out UIBlock descendant))
            {
                ExecuteMove(BackFocusBuffer, NavMove.Explicit(fromUIBlock, descendant, new Update(ray, controlID), direction, layerMask));
            }
            else
            {
                ExecuteMove(BackFocusBuffer, NavMove.Implicit(fromUIBlock, toUIBlock, new Update(CreateRay(fromUIBlock, toUIBlock), controlID), layerMask));
            }
        }

        private static void ExecuteMove(FocusBuffer focusBuffer, NavMove move)
        {
            UIBlock toUIBlock = move.To;
            UIBlock fromUIBlock = move.From;
            Update update = move.Update;
            Vector3 direction = move.Direction;
            int layerMask = move.LayerMask;

            if (fromUIBlock == null && toUIBlock == null)
            {
                return;
            }

            toUIBlock = toUIBlock != null && toUIBlock.InputTarget.Nav != null ? toUIBlock : null;

            bool implicitMove = move.Type == MoveType.Implicit;

            if (!implicitMove && fromUIBlock != null && toUIBlock == null)
            {
                INavigationNode node = fromUIBlock.InputTarget.Nav;

                if (node == null || !node.UseTargetNotFoundFallback(direction))
                {
                    return;
                }

                GestureRecognizer recognizer = node as GestureRecognizer;
                NavLink link = recognizer.Navigation.GetLink(direction);

                switch (link.Fallback)
                {
                    case NavLinkFallback.DoNothing:
                        return;
                    case NavLinkFallback.FireDirectionEvent:
                        fromUIBlock.FireEvent(Navigate.Direction(update, fromUIBlock, direction));
                        return;
                    case NavLinkFallback.NavigateOutsideScope:
                        int scopeIndex = 0;
                        UIBlock current = Navigator<UIBlock>.GetScope(update.ControlID, scopeIndex++);

                        while (toUIBlock == null)
                        {
                            if (Navigator<UIBlock>.TryGetNext(current, direction, out IUIBlock next))
                            {
                                toUIBlock = next as UIBlock;
                                break;
                            }
                            
                            if (current == null)
                            {
                                break;
                            }

                            UIBlock scope = Navigator<UIBlock>.GetScope(update.ControlID, scopeIndex++);

                            if (Navigator<UIBlock>.TryGet(update.Ray, fromUIBlock, scope, layerMask, filterScope: true, out HitTestResult result))
                            {
                                toUIBlock = result.HitBlock as UIBlock;
                                break;
                            }
                            
                            current = scope;
                        }
                        break;
                }
            }

            if (toUIBlock == null && !implicitMove)
            {
                return;
            }

            EnqueueFocusChange(focusBuffer, fromUIBlock, toUIBlock, update.ControlID);

            ReadOnlyList<UIBlock> scopes = Navigator<UIBlock>.GetAncestorScopes(toUIBlock, Navigator<UIBlock>.GetScope(update.ControlID), layerMask);

            UIBlock pathRoot = scopes.Count > 0 ? scopes[0] : toUIBlock;

            while (!Navigator<UIBlock>.IsInScope(update.ControlID, pathRoot))
            {
                UIBlock poppedScope = Navigator<UIBlock>.PopScope(update.ControlID);
                poppedScope.FireEvent(Navigate.Deselect(update, poppedScope));
            }

            for (int i = 0; i < scopes.Count; ++i)
            {
                Navigator<UIBlock>.MoveTo(update.ControlID, scopes[i], layerMask);
                Select(update.ControlID, update.UserData, layerMask);
            }

            Navigator<UIBlock>.MoveTo(update.ControlID, toUIBlock, layerMask);

            Ray ray = update.Ray;

            if (fromUIBlock != null)
            {
                Vector3 fromDirection = fromUIBlock.transform.InverseTransformDirection(ray.direction);
                fromUIBlock.FireEvent(Navigate.MoveFrom(update, fromUIBlock, toUIBlock, fromDirection));
            }

            if (toUIBlock != null)
            {
                Vector3 toDirection = toUIBlock.transform.InverseTransformDirection(ray.direction);
                toUIBlock.FireEvent(Navigate.MoveTo(update, toUIBlock, fromUIBlock, toDirection));

                if (move.AllowAutoEntry && toUIBlock.InputTarget.Nav is GestureRecognizer recognizer && recognizer.AutoSelect)
                {
                    Select(update.ControlID, update.UserData, layerMask);

                    if (fromUIBlock != null)
                    {
                        // Recreate the ray so we always move to adjacent UIBlocks, even
                        // if fromUIBlock has moved between now and when the nav call was queued
                        update.Ray = Navigator<UIBlock>.CreateRay(direction, fromUIBlock);
                    }

                    if (recognizer.OnSelect == SelectBehavior.ScopeNavigation && Navigator<UIBlock>.TryGet(update.Ray, toUIBlock, toUIBlock, layerMask, filterScope: true, out HitTestResult autoResult, sortByTopLevelProximity: true))
                    {
                        ExecuteMove(focusBuffer, NavMove.Explicit(toUIBlock, autoResult.HitBlock as UIBlock, update, toDirection, layerMask));
                    }
                }
            }
        }

        private static NavMove ProcessDeferred(NavMove deferred)
        {
            UIBlock from = deferred.From;

            if (from.ChildCount == 0)
            {
                return NavMove.Skip;
            }

            uint controlID = deferred.ControlID;

            if (Navigator<UIBlock>.Current(controlID) != from)
            {
                // We've already moved to a different element,
                // so doing this work will result in an undesired
                // destination
                return NavMove.Skip;
            }

            object userData = deferred.UserData;
            int layerMask = deferred.LayerMask;

            bool fallbackToLayoutDirection = true;

            if (Navigator<UIBlock>.TryGetFirstNavigableDescendant(from, out Vector3 localDirection, out Ray ray, layerMask, out UIBlock descendant))
            {
                fallbackToLayoutDirection = false;

                if (((1 << descendant.gameObject.layer) & layerMask) != 0)
                {
                    return NavMove.Explicit(from, descendant, new Update(ray, controlID, userData), localDirection, layerMask);
                }
            }

            if (fallbackToLayoutDirection)
            {
                // Didn't find a navigable descendent, so create
                // a ray from the content's layout direction
                AutoLayout layout = from.GetAutoLayoutReadOnly();

                if (!layout.Enabled)
                {
                    return NavMove.Skip;
                }

                localDirection = Vector3.zero;
                localDirection[layout.Axis.Index()] = layout.ContentDirection;

                ray = Navigator<UIBlock>.CreateRay(localDirection, from, castInside: true);
            }

            if (Navigator<UIBlock>.TryGet(ray, from, from, layerMask, filterScope: true, out HitTestResult result))
            {
                return NavMove.Explicit(from, result.HitBlock as UIBlock, new Update(ray, controlID, userData), localDirection, layerMask);
            }

            return NavMove.Skip;
        }


        private static NavMove ProcessQuery(NavMove query)
        {
            uint controlID = query.ControlID;
            Vector3 direction = query.Direction;
            object userData = query.UserData;
            int layerMask = query.LayerMask;

            UIBlock fromUIBlock = Navigator<UIBlock>.Current(controlID);

            if (fromUIBlock == null)
            {
                // Something was focused when this query was queued,
                // but since then focus has been removed,
                // so we just drop it and move on.
                return NavMove.Skip;
            }

            UIBlock scope = Navigator<UIBlock>.GetScope(controlID);

            UIBlock toUIBlock = null;

            bool fromScope = fromUIBlock == scope;
            Ray ray = Navigator<UIBlock>.CreateRay(direction, fromUIBlock, castInside: fromScope);

            if (Navigator<UIBlock>.TryGet(ray, fromUIBlock, scope, layerMask, filterScope: true, out HitTestResult result))
            {
                toUIBlock = result.HitBlock as UIBlock;
            }
            else if (fromScope)
            {
                // Nothing hit inside the scope, adjust ray to point externally
                ray = Navigator<UIBlock>.CreateRay(direction, fromUIBlock, castInside: false);
            }

            return query.MakeExplicit(fromUIBlock, toUIBlock, new Update(ray, controlID, userData));
        }

        private static void EnqueueFocusChange(FocusBuffer buffer, UIBlock fromUIBlock, UIBlock toUIBlock, uint controlID)
        {
            if (fromUIBlock == toUIBlock)
            {
                return;
            }

            buffer.Set(controlID, toUIBlock);
        }

        private static void ExecuteFocusChange(uint controlID, UIBlock toUIBlock)
        {
            try
            {
                OnNavigationFocusChanged?.Invoke(controlID, toUIBlock);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static Ray CreateRay(UIBlock from, UIBlock to)
        {
            bool fromSomewhere = from != null;
            bool toSomewhere = to != null;

            if (!fromSomewhere && !toSomewhere)
            {
                return default;
            }

            if (!fromSomewhere)
            {
                return new Ray(to.transform.position, to.transform.forward);
            }

            if (!toSomewhere)
            {
                return new Ray(from.transform.position, from.transform.forward);
            }

            return new Ray(from.transform.position, (to.transform.position - from.transform.position).normalized);
        }

        private static UIBlockHit ToPublic(HitTestResult result)
        {
            return new UIBlockHit()
            {
                UIBlock = result.HitBlock as UIBlock,
                Position = result.HitPoint,
                Normal = result.Normal
            };
        }

        private enum MoveType
        {
            Skip,
            Implicit,
            Explicit,
            Query,
            Deferred,
        }

        private readonly struct NavMove
        {
            public readonly MoveType Type;
            public readonly bool AllowAutoEntry;

            public readonly UIBlock From;
            public readonly UIBlock To;
            public readonly Update Update;

            public readonly uint ControlID;
            public readonly Vector3 Direction;
            public readonly object UserData;
            public readonly int LayerMask;

            public static readonly NavMove Skip = new NavMove(MoveType.Skip);

            public static NavMove Implicit(UIBlock from, UIBlock to, Update update, int layerMask, bool allowAutoEntry = false) => new NavMove(from, to, update, layerMask, allowAutoEntry);
            public static NavMove Explicit(UIBlock from, UIBlock to, Update update, Vector3 direction, int layerMask) => new NavMove(from, to, update, direction, layerMask);
            public static NavMove Query(uint controlID, Vector3 direction, object userData, int layerMask) => new NavMove(controlID, direction, userData, layerMask);
            public static NavMove Deferred(uint controlID, UIBlock fromUIBlock, object userData, int layerMask) => new NavMove(fromUIBlock, controlID, userData, layerMask);

            public NavMove MakeExplicit(UIBlock from, UIBlock to, Update update)
            {
                return new NavMove(from, to, update, Direction, LayerMask);
            }

            private NavMove(MoveType type)
            {
                Type = type;
                From = null;
                To = null;
                Update = Update.Uninitialized;
                AllowAutoEntry = false;

                ControlID = uint.MaxValue;
                Direction = Vector3.zero;
                UserData = null;
                LayerMask = 0;
            }

            private NavMove(UIBlock from, UIBlock to, Update update, int layerMask, bool allowAutoEntry)
            {
                Type = MoveType.Implicit;
                From = from;
                To = to;
                Update = update;
                LayerMask = layerMask;
                AllowAutoEntry = allowAutoEntry;

                ControlID = uint.MaxValue;
                Direction = Vector3.zero;
                UserData = null;
            }

            private NavMove(UIBlock from, UIBlock to, Update update, Vector3 direction, int layerMask)
            {
                Type = MoveType.Explicit;
                From = from;
                To = to;
                Update = update;
                AllowAutoEntry = true;

                ControlID = update.ControlID;
                Direction = direction;
                UserData = update.UserData;
                LayerMask = layerMask;
            }

            private NavMove(UIBlock from, uint controlID, object userData, int layerMask)
            {
                Type = MoveType.Deferred;

                ControlID = controlID;
                UserData = userData;
                LayerMask = layerMask;
                From = from;
                AllowAutoEntry = true;

                To = null;
                Update = Update.Uninitialized;
                Direction = Vector3.zero;
            }

            private NavMove(uint controlID, Vector3 direction, object userData, int layerMask)
            {
                Type = MoveType.Query;

                ControlID = controlID;
                Direction = direction;
                UserData = userData;
                LayerMask = layerMask;
                AllowAutoEntry = true;

                From = null;
                To = null;
                Update = Update.Uninitialized;
            }
        }

        private class FocusBuffer
        {
            private struct FocusedBlock : System.IEquatable<FocusedBlock>
            {
                public readonly bool Null;
                public readonly UIBlock UIBlock;

                public static implicit operator FocusedBlock(UIBlock uiBlock) => new FocusedBlock(uiBlock);

                private FocusedBlock(UIBlock uiBlock)
                {
                    UIBlock = uiBlock;
                    Null = uiBlock == null;
                }

                public bool Equals(FocusedBlock other)
                {
                    return this.UIBlock == other.UIBlock && this.Null == other.Null;
                }
            }

            public int Count => controls.Count;
            public ReadOnlyList<uint> Controls => controls.ToReadOnly();
            private List<uint> controls = new List<uint>();
            private Dictionary<uint, FocusedBlock> focused = new Dictionary<uint, FocusedBlock>();

            public void Set(uint controlID, UIBlock uiBlock)
            {
                bool knownControl = focused.TryGetValue(controlID, out FocusedBlock current);
                bool changed = !knownControl || !current.Equals(uiBlock);

                if (!changed)
                {
                    return;
                }

                if (!knownControl)
                {
                    controls.Add(controlID);
                }

                focused[controlID] = uiBlock;
            }

            public void Remove(uint controlID)
            {
                if (!focused.Remove(controlID))
                {
                    return;
                }

                controls.Remove(controlID);
            }

            public bool TryGet(uint controlID, out UIBlock uiBlock)
            {
                bool found = focused.TryGetValue(controlID, out FocusedBlock current);
                uiBlock = found ? current.UIBlock : null;
                return found;
            }

            public void Clear()
            {
                controls.Clear();
                focused.Clear();
            }
        }

        #endregion
    }
}
