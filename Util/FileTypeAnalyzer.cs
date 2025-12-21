using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NoFences.Util
{
    /// <summary>
    /// Analyzes file types in a collection to determine dominant type for Magic Fences
    /// </summary>
    public static class FileTypeAnalyzer
    {
        public enum FileCategory
        {
            Images,
            Videos,
            Documents,
            Code,
            Audio,
            Archives,
            Executables,
            Mixed
        }

        private static readonly Dictionary<string, FileCategory> ExtensionMap = new Dictionary<string, FileCategory>(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            { ".jpg", FileCategory.Images }, { ".jpeg", FileCategory.Images }, { ".png", FileCategory.Images },
            { ".gif", FileCategory.Images }, { ".bmp", FileCategory.Images }, { ".svg", FileCategory.Images },
            { ".webp", FileCategory.Images }, { ".ico", FileCategory.Images }, { ".tiff", FileCategory.Images },
            
            // Videos
            { ".mp4", FileCategory.Videos }, { ".avi", FileCategory.Videos }, { ".mkv", FileCategory.Videos },
            { ".mov", FileCategory.Videos }, { ".wmv", FileCategory.Videos }, { ".flv", FileCategory.Videos },
            { ".webm", FileCategory.Videos }, { ".m4v", FileCategory.Videos },
            
            // Documents
            { ".pdf", FileCategory.Documents }, { ".doc", FileCategory.Documents }, { ".docx", FileCategory.Documents },
            { ".xls", FileCategory.Documents }, { ".xlsx", FileCategory.Documents }, { ".ppt", FileCategory.Documents },
            { ".pptx", FileCategory.Documents }, { ".txt", FileCategory.Documents }, { ".rtf", FileCategory.Documents },
            { ".odt", FileCategory.Documents }, { ".ods", FileCategory.Documents },
            
            // Code
            { ".cs", FileCategory.Code }, { ".js", FileCategory.Code }, { ".ts", FileCategory.Code },
            { ".py", FileCategory.Code }, { ".java", FileCategory.Code }, { ".cpp", FileCategory.Code },
            { ".c", FileCategory.Code }, { ".h", FileCategory.Code }, { ".css", FileCategory.Code },
            { ".html", FileCategory.Code }, { ".xml", FileCategory.Code }, { ".json", FileCategory.Code },
            { ".sql", FileCategory.Code }, { ".php", FileCategory.Code }, { ".rb", FileCategory.Code },
            
            // Audio
            { ".mp3", FileCategory.Audio }, { ".wav", FileCategory.Audio }, { ".flac", FileCategory.Audio },
            { ".aac", FileCategory.Audio }, { ".ogg", FileCategory.Audio }, { ".wma", FileCategory.Audio },
            { ".m4a", FileCategory.Audio },
            
            // Archives
            { ".zip", FileCategory.Archives }, { ".rar", FileCategory.Archives }, { ".7z", FileCategory.Archives },
            { ".tar", FileCategory.Archives }, { ".gz", FileCategory.Archives }, { ".bz2", FileCategory.Archives },
            
            // Executables
            { ".exe", FileCategory.Executables }, { ".msi", FileCategory.Executables }, { ".bat", FileCategory.Executables },
            { ".cmd", FileCategory.Executables }, { ".ps1", FileCategory.Executables }
        };

        public static FileCategory AnalyzeDominantType(IEnumerable<string> filePaths)
        {
            if (filePaths == null || !filePaths.Any())
                return FileCategory.Mixed;

            var categoryCounts = new Dictionary<FileCategory, int>();

            foreach (var path in filePaths)
            {
                var ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext))
                    continue;

                if (ExtensionMap.TryGetValue(ext, out var category))
                {
                    if (!categoryCounts.ContainsKey(category))
                        categoryCounts[category] = 0;
                    categoryCounts[category]++;
                }
            }

            if (categoryCounts.Count == 0)
                return FileCategory.Mixed;

            // Find dominant category (>50% of files)
            var totalFiles = filePaths.Count();
            var dominantCategory = categoryCounts.OrderByDescending(kv => kv.Value).First();

            if (dominantCategory.Value > totalFiles * 0.5)
                return dominantCategory.Key;

            return FileCategory.Mixed;
        }

        public static System.Drawing.Color GetMagicColor(FileCategory category)
        {
            switch (category)
            {
                case FileCategory.Images:
                    return System.Drawing.Color.FromArgb(200, 100, 120, 255);      // Blue/Purple
                case FileCategory.Videos:
                    return System.Drawing.Color.FromArgb(200, 255, 60, 100);       // Red/Pink
                case FileCategory.Documents:
                    return System.Drawing.Color.FromArgb(200, 60, 200, 100);       // Green
                case FileCategory.Code:
                    return System.Drawing.Color.FromArgb(200, 255, 150, 50);       // Orange
                case FileCategory.Audio:
                    return System.Drawing.Color.FromArgb(200, 200, 100, 255);      // Purple
                case FileCategory.Archives:
                    return System.Drawing.Color.FromArgb(200, 150, 150, 150);      // Gray
                case FileCategory.Executables:
                    return System.Drawing.Color.FromArgb(200, 255, 200, 50);       // Yellow
                default:
                    return System.Drawing.Color.FromArgb(200, 80, 80, 80);         // Dark Gray
            }
        }
    }
}
