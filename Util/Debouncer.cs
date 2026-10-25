using System;
using System.Windows.Forms;

namespace NoFences.Util
{
    /// <summary>
    /// Trailing-edge debounce for the UI thread: the action runs once, after the calls stop
    /// for the configured interval (e.g. save once after a window drag ends).
    /// </summary>
    public sealed class Debouncer : IDisposable
    {
        private readonly Timer _timer;
        private Action _pending;

        public Debouncer(int milliseconds)
        {
            _timer = new Timer { Interval = milliseconds };
            _timer.Tick += (s, e) => Flush();
        }

        public void Run(Action action)
        {
            _pending = action;
            _timer.Stop();
            _timer.Start();
        }

        /// <summary>Runs the pending action immediately, if there is one.</summary>
        public void Flush()
        {
            _timer.Stop();
            var action = _pending;
            _pending = null;
            action?.Invoke();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _pending = null;
        }
    }
}
