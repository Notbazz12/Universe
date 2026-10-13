using System;
using System.Collections.Generic;
using System.IO;

namespace NoFences.Model
{
    /// <summary>
    /// Helper for checking and validating file filters for specialized fences (e.g. Images, Tasks).
    /// </summary>
    public static class FenceFilterHelper
    {
        public static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".ico", ".tiff", ".jfif", ".avif"
        };

        public static readonly HashSet<string> TaskExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".md", ".rtf", ".csv", ".todo", ".task", ".log"
        };

        public static bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            return ImageExtensions.Contains(ext);
        }

        public static bool IsTaskFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            if (TaskExtensions.Contains(ext)) return true;

            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return name.Contains("tarea") || name.Contains("task") ||
                   name.Contains("todo") || name.Contains("to-do") ||
                   name.Contains("pendiente") || name.Contains("nota") ||
                   name.Contains("note");
        }

        public static bool MatchesFilter(string path, string filterType)
        {
            if (string.IsNullOrEmpty(filterType) || filterType.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;

            if (filterType.Equals("Images", StringComparison.OrdinalIgnoreCase))
                return IsImageFile(path);

            if (filterType.Equals("Tasks", StringComparison.OrdinalIgnoreCase))
                return IsTaskFile(path);

            return true;
        }
    }
}
