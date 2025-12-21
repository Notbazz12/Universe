using System;
using System.Threading.Tasks;

namespace NoFences.Util
{
    public class ThrottledExecution : IDisposable
    {
        private TimeSpan delay;

        private DateTime lastExecution = DateTime.Now;

        private TimeSpan TimeSinceLastExecution => DateTime.Now - lastExecution;

        private volatile bool isAwaiting;
        private bool disposed = false;

        public ThrottledExecution(TimeSpan delay)
        {
            this.delay = delay;
        }

        public async void Run(Action action)
        {
            if (disposed) return;
            
            if (TimeSinceLastExecution > delay)
                action.Invoke();
            else if (!isAwaiting)
            {
                isAwaiting = true;
                while (TimeSinceLastExecution < delay && !disposed)
                {
                    await Task.Delay((int)(delay.TotalMilliseconds - TimeSinceLastExecution.TotalMilliseconds));
                    if (!disposed)
                        action.Invoke();
                }
                isAwaiting = false;
            }
            lastExecution = DateTime.Now;
        }

        public void Dispose()
        {
            disposed = true;
        }
    }
}
