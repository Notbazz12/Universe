using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NoFences.Persistence
{
    /// <summary>
    /// Debounced, coalescing background file writer. Each Schedule restarts the timer and only
    /// the latest contents per path are written. Call Flush() on shutdown.
    /// </summary>
    public sealed class DeferredFileWriter : IDisposable
    {
        private readonly object _gate = new object();
        private readonly object _flushLock = new object();
        private readonly Dictionary<string, string> _dirty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer _timer;
        private readonly TimeSpan _delay;
        private readonly Action<string, Exception> _onError;

        public DeferredFileWriter(TimeSpan delay, Action<string, Exception> onError)
        {
            _delay = delay;
            _onError = onError;
            _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Schedule(string path, string contents)
        {
            lock (_gate) _dirty[path] = contents;
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }

        /// <summary>Drops any pending write for a path (e.g. before deleting its fence).</summary>
        public void Discard(string path)
        {
            lock (_gate) _dirty.Remove(path);
        }

        public bool HasPending(string path)
        {
            lock (_gate) return _dirty.ContainsKey(path);
        }

        public void Flush()
        {
            lock (_flushLock)
            {
                KeyValuePair<string, string>[] batch;
                lock (_gate)
                {
                    batch = _dirty.ToArray();
                    _dirty.Clear();
                }

                foreach (var kv in batch)
                {
                    try
                    {
                        AtomicFile.WriteAllText(kv.Key, kv.Value);
                    }
                    catch (Exception ex)
                    {
                        _onError?.Invoke(kv.Key, ex);
                        // keep it for the next flush unless a newer version arrived meanwhile
                        lock (_gate)
                        {
                            if (!_dirty.ContainsKey(kv.Key)) _dirty[kv.Key] = kv.Value;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            Flush();
        }
    }
}
