// Copyright (c) Supernova Technologies LLC
//#define CONDITIONALLY_PARALLEL

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Nova.Compat
{
    [JobProducerType(typeof(INovaJobExtensions.JobProducer<>))]
    internal interface INovaJob : IJob { }

    [JobProducerType(typeof(INovaJobParallelForExtensions.JobParallelForProducer<>))]
    internal interface INovaJobParallelFor : IJobParallelFor
    { }

    internal static class INovaJobExtensions
    {
        public static unsafe void RunByRef<T>(this ref T jobData) where T : struct, INovaJob
        {
            JobsUtility.JobScheduleParameters scheduleParams = JobProducer<T>.RunParams;
            scheduleParams.JobDataPtr = new IntPtr(UnsafeUtility.AddressOf(ref jobData));

            JobsUtility.Schedule(ref scheduleParams);
        }

        public static unsafe JobHandle NovaScheduleByRef<T>(this ref T jobData, JobHandle dependsOn = default) where T : struct, INovaJob
        {
            JobsUtility.JobScheduleParameters scheduleParams = JobProducer<T>.ScheduleParams;
            scheduleParams.JobDataPtr = new IntPtr(UnsafeUtility.AddressOf(ref jobData));
            scheduleParams.Dependency = dependsOn;

            return JobsUtility.Schedule(ref scheduleParams);
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct JobProducer<T> where T : struct, INovaJob
        {
            public static readonly JobsUtility.JobScheduleParameters RunParams = new JobsUtility.JobScheduleParameters(null, ReflectionData, default, ScheduleMode.Run);
            public static readonly JobsUtility.JobScheduleParameters ScheduleParams = new JobsUtility.JobScheduleParameters(null, ReflectionData, default, ScheduleMode.Single);

            private static IntPtr reflectionData;

            public static IntPtr ReflectionData
            {
                get
                {
                    if (reflectionData == IntPtr.Zero)
                    {
                        reflectionData = JobsUtility.CreateJobReflectionData(typeof(T), typeof(T), (ExecuteJobFunction)Execute);
                    }

                    return reflectionData;
                }
            }

            private delegate void ExecuteJobFunction(ref T jobData);

            [Obfuscation]
            private static void Execute(ref T jobData)
            {
                jobData.Execute();
            }
        }
    }

    internal static class INovaJobParallelForExtensions
    {
        public static unsafe JobHandle NovaScheduleByRef<T>(this ref T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, INovaJobParallelFor
        {

            JobsUtility.JobScheduleParameters scheduleParams = JobParallelForProducer<T>.ParallelScheduleParams;
            scheduleParams.JobDataPtr = new IntPtr(UnsafeUtility.AddressOf(ref jobData));
            scheduleParams.Dependency = dependsOn;
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, arrayLength, innerloopBatchCount);
        }

        public static unsafe JobHandle ScheduleByRef<T>(this ref T jobData, int* arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, INovaJobParallelFor
        {
            JobsUtility.JobScheduleParameters scheduleParams = JobParallelForProducer<T>.ParallelScheduleParams;
            scheduleParams.JobDataPtr = new IntPtr(UnsafeUtility.AddressOf(ref jobData));
            scheduleParams.Dependency = dependsOn;

            // This is the math that IJobParallelForDeferExtensions does...not sure why
            var forEachListPtr = (byte*)arrayLength - sizeof(void*);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref scheduleParams, innerloopBatchCount, forEachListPtr, null);
        }

        public static unsafe JobHandle ScheduleByRef<T,U>(this ref T jobData, NativeList<U> list, int innerloopBatchCount, JobHandle dependsOn = default) 
            where T : struct, INovaJobParallelFor
            where U : unmanaged
        {
            JobsUtility.JobScheduleParameters scheduleParams = JobParallelForProducer<T>.ParallelScheduleParams;
            scheduleParams.JobDataPtr = new IntPtr(UnsafeUtility.AddressOf(ref jobData));
            scheduleParams.Dependency = dependsOn;

            void* atomicSafetyHandlePtr = null;
            // Calculate the deferred atomic safety handle before constructing JobScheduleParameters so
            // DOTS Runtime can validate the deferred list statically similar to the reflection based
            // validation in Big Unity.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list);
            atomicSafetyHandlePtr = UnsafeUtility.AddressOf(ref safety);
#endif

            // This is the math that IJobParallelForDeferExtensions does...not sure why
            var forEachListPtr = NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref list);
            return JobsUtility.ScheduleParallelForDeferArraySize(ref scheduleParams, innerloopBatchCount, forEachListPtr, atomicSafetyHandlePtr);
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct JobParallelForProducer<T> where T : struct, INovaJobParallelFor
        {
            public static readonly JobsUtility.JobScheduleParameters ParallelScheduleParams = new JobsUtility.JobScheduleParameters(null, ParallelReflectionData, default, ScheduleMode.Parallel);

            private static IntPtr parallelReflectionData;

            public static IntPtr ParallelReflectionData
            {
                get
                {
                    if (parallelReflectionData == IntPtr.Zero)
                    {
                        parallelReflectionData = JobsUtility.CreateJobReflectionData(typeof(T), typeof(T), (ExecuteJobParallel)Execute);
                    }

                    return parallelReflectionData;
                }
            }

            private delegate void ExecuteJobParallel(ref T jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            [Obfuscation]
            private static unsafe void Execute(ref T jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out int begin, out int end))
                {
                    for (var i = begin; i < end; ++i)
                    {
                        jobData.Execute(i);
                    }
                }
            }

        }
    }
}
