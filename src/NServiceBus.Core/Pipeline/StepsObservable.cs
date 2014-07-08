namespace NServiceBus.Pipeline
{
    using System;
    using System.Collections.Generic;
    using Janitor;

    class StepsObservable : IObservable<Step>
    {
        public StepsObservable()
        {
            observers = new List<IObserver<Step>>();
        }

        public IDisposable Subscribe(IObserver<Step> observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }

            return new Unsubscriber(observers, observer);
        }

        public void Add(Step step)
        {
            foreach (var observer in observers)
            {
                observer.OnNext(step);
            }
        }

        public void Complete()
        {
            foreach (var observer in observers)
            {
                observer.OnCompleted();
            }
        }

        List<IObserver<Step>> observers;

        [SkipWeaving]
        class Unsubscriber : IDisposable
        {
            public Unsubscriber(List<IObserver<Step>> observers, IObserver<Step> observer)
            {
                this.observers = observers;
                this.observer = observer;
            }

            public void Dispose()
            {
                if (observer != null && observers.Contains(observer))
                {
                    observers.Remove(observer);
                }
            }

            IObserver<Step> observer;
            List<IObserver<Step>> observers;
        }
    }
}