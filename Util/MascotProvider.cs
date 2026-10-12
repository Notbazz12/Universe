using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NoFences.Util
{
    public static class MascotProvider
    {
        private static Image _happy;
        private static Image _wink;
        private static Image _sleep;
        private static Image _excited;
        private static Image _love;
        private static Image _jump;
        private static bool _initialized = false;

        private static string GetMascotDir()
        {
            // Look relative to executable or base directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dir = Path.Combine(baseDir, "Resources", "Mascot");
            if (Directory.Exists(dir)) return dir;

            // Fallback for dev / project tree
            string projectDir = Path.Combine(baseDir, "..", "..", "Resources", "Mascot");
            if (Directory.Exists(projectDir)) return Path.GetFullPath(projectDir);

            return dir;
        }

        private static Image LoadSafe(string fileName)
        {
            try
            {
                string path = Path.Combine(GetMascotDir(), fileName);
                if (File.Exists(path))
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var original = Image.FromStream(fs);
                        return new Bitmap(original); // Decouple from stream
                    }
                }
            }
            catch { }
            return null;
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _happy = LoadSafe("mascot_happy.png");
            _wink = LoadSafe("mascot_wink.png");
            _sleep = LoadSafe("mascot_sleep.png");
            _excited = LoadSafe("mascot_excited.png");
            _love = LoadSafe("mascot_love.png");
            _jump = LoadSafe("mascot_jump.png");
        }

        public static Image Happy => _happy ?? (_happy = LoadSafe("mascot_happy.png"));
        public static Image Wink => _wink ?? (_wink = LoadSafe("mascot_wink.png"));
        public static Image Sleep => _sleep ?? (_sleep = LoadSafe("mascot_sleep.png"));
        public static Image Excited => _excited ?? (_excited = LoadSafe("mascot_excited.png"));
        public static Image Love => _love ?? (_love = LoadSafe("mascot_love.png"));
        public static Image Jump => _jump ?? (_jump = LoadSafe("mascot_jump.png"));
    }
}
