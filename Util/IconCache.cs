using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Threading;

namespace NoFences.Util
{
    /// <summary>
    /// Loads file icons / thumbnails on a single background STA thread (shell COM objects
    /// need STA) so painting never blocks on disk, network or shell calls. A single consumer
    /// also bounds memory use while decoding large images.
    /// </summary>
    public sealed class IconCache
    {
        public static readonly IconCache Shared = new IconCache();

        private struct Job
        {
            public string Path;
            public Func<Icon> Loader;
        }

        private readonly ConcurrentDictionary<string, Icon> _icons = new ConcurrentDictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _queued = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private readonly BlockingCollection<Job> _queue = new BlockingCollection<Job>();

        /// <summary>Raised on the loader thread after an icon becomes available; subscribers must marshal.</summary>
        public event EventHandler Loaded;

        private IconCache()
        {
            var thread = new Thread(Worker) { IsBackground = true, Name = "NoFences.IconLoader" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <summary>Returns the cached icon, or the placeholder while it loads in the background.</summary>
        public Icon GetOrQueue(string path, Func<Icon> loader, Icon placeholder)
        {
            if (_icons.TryGetValue(path, out var icon)) return icon;

            if (_queued.TryAdd(path, 0))
                _queue.Add(new Job { Path = path, Loader = loader });

            return placeholder;
        }

        /// <summary>Forgets a cached icon so it is reloaded the next time it is requested.
        /// The old icon is not disposed: a paint on the UI thread may still be using it.</summary>
        public void Invalidate(string path)
        {
            _icons.TryRemove(path, out _);
        }

        private void Worker()
        {
            foreach (var job in _queue.GetConsumingEnumerable())
            {
                try
                {
                    var icon = job.Loader();
                    if (icon != null) _icons[job.Path] = icon;
                }
                catch
                {
                    // keep the placeholder
                }
                finally
                {
                    _queued.TryRemove(job.Path, out _);
                }

                try { Loaded?.Invoke(this, EventArgs.Empty); } catch { }
            }
        }
    }
}
