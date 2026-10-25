using System;
using System.IO;
using System.Text;

namespace NoFences.Persistence
{
    /// <summary>
    /// Crash-safe file writes: data is flushed to disk in a temp file and then swapped in
    /// with File.Replace (atomic on NTFS), keeping the previous version as .bak.
    /// </summary>
    internal static class AtomicFile
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void WriteAllText(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            var bytes = Utf8NoBom.GetBytes(contents);

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // FlushFileBuffers: data is on disk before the swap
            }

            if (File.Exists(path))
                File.Replace(tmp, path, path + ".bak", true);
            else
                File.Move(tmp, path);
        }

        /// <summary>Returns the first candidate (live file, then .bak) that parse accepts.</summary>
        public static T ReadWithRecovery<T>(string path, Func<string, T> parse) where T : class
        {
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    var result = parse(File.ReadAllText(candidate));
                    if (result != null) return result;
                }
                catch
                {
                    // try next candidate
                }
            }
            return null;
        }
    }
}
