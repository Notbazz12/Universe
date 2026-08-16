using System;
using System.Threading.Tasks;

namespace NoFences.Util
{
    public class ThrottledExecution : IDisposable
    {
        private readonly TimeSpan delay;
        private DateTime lastExecution = DateTime.MinValue;
        private TimeSpan TimeSinceLastExecution => DateTime.UtcNow - lastExecution;
        private volatile bool isAwaiting;
        private bool disposed = false;

        public ThrottledExecution(TimeSpan delay)
        {
            this.delay = delay;
        }

        public async void Run(Action action)
        {
            if (disposed || action == null) return;

            try
            {
                if (TimeSinceLastExecution >= delay)
                {
                    lastExecution = DateTime.UtcNow;
                    action.Invoke();
                }
                else if (!isAwaiting)
                {
                    isAwaiting = true;
                    while (TimeSinceLastExecution < delay && !disposed)
                    {
                        var remainingMs = (int)Math.Max(1, delay.TotalMilliseconds - TimeSinceLastExecution.TotalMilliseconds);
                        await Task.Delay(remainingMs).ConfigureAwait(true);
                        if (!disposed)
                        {
                            lastExecution = DateTime.UtcNow;
                            action.Invoke();
                        }
                    }
                    isAwaiting = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThrottledExecution error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            disposed = true;
            isAwaiting = false;
        }
    }
}
