// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Animations;
using Nova.Internal.Collections;

namespace Nova
{
    internal static class AnimationEngineProxy
    {
        private class AnimationWithEventsWrapper<T> : AnimationEngine.IAnimationWithEventsWrapper where T : struct, IAnimationWithEvents
        {
            public static AnimationWithEventsWrapper<T> Instance = new AnimationWithEventsWrapper<T>();

            public void Canceled(AnimationID animationID)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.OnCanceled();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void Complete(AnimationID animationID)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.Complete();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void End(AnimationID animationID)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.End();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void Paused(AnimationID animationID)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.OnPaused();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void Resumed(AnimationID animationID)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.OnResumed();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void Start(AnimationID animationID, int iterations)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.Begin(iterations);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void UpdateAnimation(AnimationID animationID, float percentDone)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.Update(percentDone);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void Remove(AnimationID animationID)
            {
                StructHandle<AnimationID>.Storage<T>.Remove(animationID);
            }
        }

        private class AnimationWrapper<T> : AnimationEngine.IAnimationWrapper where T : struct, IAnimation
        {
            public static AnimationWrapper<T> Instance = new AnimationWrapper<T>();

            public void UpdateAnimation(AnimationID animationID, float percentDone)
            {
                if (!StructHandle<AnimationID>.Storage<T>.TryGetValue(animationID, out T animation))
                {
                    return;
                }

                try
                {
                    animation.Update(percentDone);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    StructHandle<AnimationID>.Storage<T>.Set(animationID, animation);
                }
            }

            public void Remove(AnimationID animationID)
            {
                StructHandle<AnimationID>.Storage<T>.Remove(animationID);
            }
        }

        public static AnimationID RunWithEvents<T>(ref T animation, float duration, int iterations) where T : struct, IAnimationWithEvents => AnimationEngine.Instance.Run(ref animation, duration, iterations, AnimationWithEventsWrapper<T>.Instance);
        public static AnimationID Run<T>(ref T animation, float duration, int iterations) where T : struct, IAnimation => AnimationEngine.Instance.Run<T>(ref animation, duration, iterations, AnimationWrapper<T>.Instance);

        public static AnimationID RunAfterWithEvents<T>(AnimationID siblingID, ref T animation, float duration, int iterations) where T : struct, IAnimationWithEvents => AnimationEngine.Instance.RunAfter(siblingID, ref animation, duration, iterations, AnimationWithEventsWrapper<T>.Instance);
        public static AnimationID RunAfter<T>(AnimationID siblingID, ref T animation, float duration, int iterations) where T : struct, IAnimation => AnimationEngine.Instance.RunAfter(siblingID, ref animation, duration, iterations, AnimationWrapper<T>.Instance);

        public static AnimationID RunTogetherWithEvents<T>(AnimationID siblingID, ref T animation) where T : struct, IAnimationWithEvents => AnimationEngine.Instance.RunTogether(siblingID, ref animation, AnimationWithEventsWrapper<T>.Instance);
        public static AnimationID RunTogether<T>(AnimationID sibling, ref T animation) where T : struct, IAnimation => AnimationEngine.Instance.RunTogether(sibling, ref animation, AnimationWrapper<T>.Instance);

        public static AnimationID RunTogetherWithEvents<T>(AnimationID sibling, ref T animation, float duration, int iterations) where T : struct, IAnimationWithEvents => AnimationEngine.Instance.RunTogether(sibling, ref animation, duration, iterations, AnimationWithEventsWrapper<T>.Instance);
        public static AnimationID RunTogether<T>(AnimationID sibling, ref T animation, float duration, int iterations) where T : struct, IAnimation => AnimationEngine.Instance.RunTogether(sibling, ref animation, duration, iterations, AnimationWrapper<T>.Instance);
    }
}
