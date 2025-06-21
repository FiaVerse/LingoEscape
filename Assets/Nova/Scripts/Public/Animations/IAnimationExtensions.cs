// Copyright (c) Supernova Technologies LLC
using System;

namespace Nova
{
    /// <summary>
    /// A set of extension methods to schedule <see cref="IAnimationWithEvents"/>
    /// </summary>
    /// <seealso cref="IAnimationExtensions"/>
    /// <seealso cref="AnimationHandleExtensions"/>
    /// <seealso cref="AnimationHandleWithEventsExtensions"/>
    public static class IAnimationWithEventsExtensions
    {
        /// <summary>
        /// Queue an animation to run for <paramref name="durationInSeconds"/> starting at the end of the current frame.
        /// </summary>
        /// <typeparam name="T">The type of the <paramref name="animation"/> struct</typeparam>
        /// <param name="animation">The <see cref="IAnimation"/> struct configured to perform the animation via <see cref="IAnimation.Update(float)"/></param>
        /// <param name="durationInSeconds">The duration, in seconds, of the animation</param>
        /// <returns>An <see cref="AnimationHandle"/>, which can be used to: 
        /// <list type="table">
        /// <item><term>Chain</term> <description><see cref="AnimationHandleExtensions.Chain{T}(AnimationHandle, T, float, int)"/></description></item>
        /// <item><term>Include</term> <description><see cref="AnimationHandleExtensions.Include{T}(AnimationHandle, T)"/></description></item>
        /// <item><term>Pause</term> <description><see cref="AnimationHandleExtensions.Pause(AnimationHandle)"/></description></item>
        /// <item><term>Resume</term> <description><see cref="AnimationHandleExtensions.Resume(AnimationHandle)"/></description></item>
        /// <item><term>Cancel</term> <description><see cref="AnimationHandleExtensions.Cancel(AnimationHandle)"/></description></item>
        /// <item><term>Complete</term> <description><see cref="AnimationHandleExtensions.Complete(AnimationHandle)"/></description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">Throws when <paramref name="durationInSeconds"/> is invalid.</exception>
        public static AnimationHandle Run<T>(this T animation, float durationInSeconds) where T : struct, IAnimationWithEvents
        {
            IAnimationExtensions.ThrowIfInvalid(durationInSeconds);

            return AnimationHandle.Create(AnimationEngineProxy.RunWithEvents(ref animation, durationInSeconds, AnimationHandle.Once));
        }

        /// <summary>
        /// Queue an animation to loop <paramref name="iterations"/> times for <paramref name="durationInSeconds"/> per iteration, where <c><paramref name="iterations"/> == -1</c> indicates "until canceled", starting at the end of the current frame.
        /// </summary>
        /// <typeparam name="T">The type of the <paramref name="animation"/> struct</typeparam>
        /// <param name="animation">The <see cref="IAnimation"/> struct configured to perform the animation via <see cref="IAnimation.Update(float)"/></param>
        /// <param name="durationInSeconds">The duration, in seconds, per iteration of the animation</param>
        /// <param name="iterations">The number of iterations to perform before the animation is removed from the animation queue. <c>-1</c> indicates "infinite iterations"</param>
        /// <returns>An <see cref="AnimationHandle"/>, which can be used to: 
        /// <list type="table">
        /// <item><term>Chain</term> <description><see cref="AnimationHandleExtensions.Chain{T}(AnimationHandle, T, float, int)"/></description></item>
        /// <item><term>Include</term> <description><see cref="AnimationHandleExtensions.Include{T}(AnimationHandle, T)"/></description></item>
        /// <item><term>Pause</term> <description><see cref="AnimationHandleExtensions.Pause(AnimationHandle)"/></description></item>
        /// <item><term>Resume</term> <description><see cref="AnimationHandleExtensions.Resume(AnimationHandle)"/></description></item>
        /// <item><term>Cancel</term> <description><see cref="AnimationHandleExtensions.Cancel(AnimationHandle)"/></description></item>
        /// <item><term>Complete</term> <description><see cref="AnimationHandleExtensions.Complete(AnimationHandle)"/></description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">Throws when <paramref name="durationInSeconds"/> is invalid.</exception>
        public static AnimationHandle Loop<T>(this T animation, float durationInSeconds, int iterations = AnimationHandle.Infinite) where T : struct, IAnimationWithEvents
        {
            IAnimationExtensions.ThrowIfInvalid(durationInSeconds);

            return AnimationHandle.Create(AnimationEngineProxy.RunWithEvents(ref animation, durationInSeconds, iterations));
        }
    }

    /// <summary>
    /// A set of extension methods to schedule <see cref="IAnimation"/>s
    /// </summary>
    /// <seealso cref="IAnimationWithEventsExtensions"/>
    /// <seealso cref="AnimationHandleExtensions"/>
    /// <seealso cref="AnimationHandleWithEventsExtensions"/>
    public static class IAnimationExtensions
    {
        /// <summary>
        /// Queue an animation to run for <paramref name="durationInSeconds"/> starting at the end of the current frame.
        /// </summary>
        /// <typeparam name="T">The type of the <paramref name="animation"/> struct</typeparam>
        /// <param name="animation">The <see cref="IAnimation"/> struct configured to perform the animation via <see cref="IAnimation.Update(float)"/></param>
        /// <param name="durationInSeconds">The duration, in seconds, of the animation</param>
        /// <returns>An <see cref="AnimationHandle"/>, which can be used to: 
        /// <list type="table">
        /// <item><term>Chain</term> <description><see cref="AnimationHandleExtensions.Chain{T}(AnimationHandle, T, float, int)"/></description></item>
        /// <item><term>Include</term> <description><see cref="AnimationHandleExtensions.Include{T}(AnimationHandle, T)"/></description></item>
        /// <item><term>Pause</term> <description><see cref="AnimationHandleExtensions.Pause(AnimationHandle)"/></description></item>
        /// <item><term>Resume</term> <description><see cref="AnimationHandleExtensions.Resume(AnimationHandle)"/></description></item>
        /// <item><term>Cancel</term> <description><see cref="AnimationHandleExtensions.Cancel(AnimationHandle)"/></description></item>
        /// <item><term>Complete</term> <description><see cref="AnimationHandleExtensions.Complete(AnimationHandle)"/></description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">Throws when <paramref name="durationInSeconds"/> is invalid.</exception>
        public static AnimationHandle Run<T>(this T animation, float durationInSeconds) where T : struct, IAnimation
        {
            IAnimationExtensions.ThrowIfInvalid(durationInSeconds);

            return AnimationHandle.Create(AnimationEngineProxy.Run(ref animation, durationInSeconds, AnimationHandle.Once));
        }

        /// <summary>
        /// Queue an animation to loop <paramref name="iterations"/> times for <paramref name="durationInSeconds"/> per iteration, where <c><paramref name="iterations"/> == -1</c> indicates "until canceled", starting at the end of the current frame.
        /// </summary>
        /// <typeparam name="T">The type of the <paramref name="animation"/> struct</typeparam>
        /// <param name="animation">The <see cref="IAnimation"/> struct configured to perform the animation via <see cref="IAnimation.Update(float)"/></param>
        /// <param name="durationInSeconds">The duration, in seconds, per iteration of the animation</param>
        /// <param name="iterations">The number of iterations to perform before the animation is removed from the animation queue. <c>-1</c> indicates "infinite iterations"</param>
        /// <returns>An <see cref="AnimationHandle"/>, which can be used to: 
        /// <list type="table">
        /// <item><term>Chain</term> <description><see cref="AnimationHandleExtensions.Chain{T}(AnimationHandle, T, float, int)"/></description></item>
        /// <item><term>Include</term> <description><see cref="AnimationHandleExtensions.Include{T}(AnimationHandle, T)"/></description></item>
        /// <item><term>Pause</term> <description><see cref="AnimationHandleExtensions.Pause(AnimationHandle)"/></description></item>
        /// <item><term>Resume</term> <description><see cref="AnimationHandleExtensions.Resume(AnimationHandle)"/></description></item>
        /// <item><term>Cancel</term> <description><see cref="AnimationHandleExtensions.Cancel(AnimationHandle)"/></description></item>
        /// <item><term>Complete</term> <description><see cref="AnimationHandleExtensions.Complete(AnimationHandle)"/></description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">Throws when <paramref name="durationInSeconds"/> is invalid.</exception>
        public static AnimationHandle Loop<T>(this T animation, float durationInSeconds, int iterations = AnimationHandle.Infinite) where T : struct, IAnimation
        {
            IAnimationExtensions.ThrowIfInvalid(durationInSeconds);

            return AnimationHandle.Create(AnimationEngineProxy.Run(ref animation, durationInSeconds, iterations));
        }

        internal static void ThrowIfInvalid(float durationInSeconds)
        {
            if (durationInSeconds < 0 || float.IsInfinity(durationInSeconds) || float.IsNaN(durationInSeconds))
            {
                throw new ArgumentException($"Provided {nameof(durationInSeconds)} must be within range [0, {float.MaxValue}]. Received [{durationInSeconds}].", nameof(durationInSeconds));
            }
        }
    }
}
