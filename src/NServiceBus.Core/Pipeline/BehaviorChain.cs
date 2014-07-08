namespace NServiceBus.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.ExceptionServices;

    class BehaviorChain<T> where T : BehaviorContext
    {
        public BehaviorChain(IEnumerable<Type> behaviorList, PipelineExecutor pipelineExecutor)
        {
            this.pipelineExecutor = pipelineExecutor;
            foreach (var behaviorType in behaviorList)
            {
                itemDescriptors.Enqueue(behaviorType);
            }

            PopulateLookupTable(pipelineExecutor);
        }

        static void PopulateLookupTable(PipelineExecutor executor)
        {
            if (lookupSteps == null)
            {
                lock (lockObj)
                {
                    if (lookupSteps == null)
                    {
                        lookupSteps = executor.Incoming.Concat(executor.Outgoing).ToDictionary(rs => rs.BehaviorType);
                    }
                }
            }
        }

        public void Invoke(T context)
        {
            Stopwatch duration = null;
            var pipeId = Guid.NewGuid().ToString();

            try
            {
                context.SetChain(this);

                pipelineExecutor.InvokePipeStarted(new PipeStarted
                {
                    PipeId = pipeId
                });
                duration = Stopwatch.StartNew();
                InvokeNext(context, pipeId);
            }
            catch
            {
                if (preservedRootException != null)
                {
                    preservedRootException.Throw();
                }

                throw;
            }
            finally
            {
                var elapsed = TimeSpan.Zero;
                if (duration != null)
                {
                    duration.Stop();
                    elapsed = duration.Elapsed;
                }

                pipelineExecutor.InvokePipeEnded(new PipeEnded
                {
                    PipeId = pipeId,
                    Duration = elapsed
                });
            }
        }

        void InvokeNext(T context, string pipeId)
        {
            if (itemDescriptors.Count == 0)
            {
                return;
            }

            var behaviorType = itemDescriptors.Dequeue();
            Stopwatch duration = null;

            try
            {
                var instance = (IBehavior<T>) context.Builder.Build(behaviorType);
                pipelineExecutor.InvokeStepStarted(new StepStarted
                {
                    Behavior = behaviorType,
                    StepId = lookupSteps[behaviorType].StepId,
                    PipeId = pipeId
                });
                duration = Stopwatch.StartNew();
                instance.Invoke(context, () => InvokeNext(context, pipeId));
                
            }
            catch (Exception exception)
            {
                if (preservedRootException == null)
                {
                    preservedRootException = ExceptionDispatchInfo.Capture(exception);
                }

                throw;
            }
            finally
            {
                var elapsed = TimeSpan.Zero;
                if (duration != null)
                {
                    duration.Stop();
                    elapsed = duration.Elapsed;
                }
                
                pipelineExecutor.InvokeStepEnded(new StepEnded
                    {
                        StepId = lookupSteps[behaviorType].StepId,
                        PipeId = pipeId,
                        Duration = elapsed
                    });
            }
        }

        public void TakeSnapshot()
        {
            snapshots.Push(new Queue<Type>(itemDescriptors));
        }

        public void DeleteSnapshot()
        {
            itemDescriptors = new Queue<Type>(snapshots.Pop());
        }

// ReSharper disable StaticFieldInGenericType
        static Dictionary<Type, RegisterStep> lookupSteps;
        static object lockObj = new object();
// ReSharper restore StaticFieldInGenericType
        readonly PipelineExecutor pipelineExecutor;
        Queue<Type> itemDescriptors = new Queue<Type>();
        ExceptionDispatchInfo preservedRootException;
        Stack<Queue<Type>> snapshots = new Stack<Queue<Type>>();
    }
}