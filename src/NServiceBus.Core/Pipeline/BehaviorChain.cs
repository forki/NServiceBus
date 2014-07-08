namespace NServiceBus.Pipeline
{
    using System;
    using System.Collections.Generic;
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
            try
            {
                context.SetChain(this);
                sessionId = Guid.NewGuid().ToString();
                InvokeNext(context);
            }
            catch
            {
                if (preservedRootException != null)
                {
                    preservedRootException.Throw();
                }

                throw;
            }
        }

        void InvokeNext(T context)
        {
            if (itemDescriptors.Count == 0)
            {
                return;
            }

            var behaviorType = itemDescriptors.Dequeue();

            try
            {
                var instance = (IBehavior<T>) context.Builder.Build(behaviorType);
                var step = new Step
                {
                    Behavior = behaviorType,
                    StepId = lookupSteps[behaviorType].StepId,
                    SessionId = sessionId
                };
                pipelineExecutor.InvokeStepStarted(step);
                instance.Invoke(context, () => InvokeNext(context));
                pipelineExecutor.InvokeStepEnded(step);
            }
            catch (Exception exception)
            {
                if (preservedRootException == null)
                {
                    preservedRootException = ExceptionDispatchInfo.Capture(exception);
                }

                throw;
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
        string sessionId;
        Stack<Queue<Type>> snapshots = new Stack<Queue<Type>>();
    }
}