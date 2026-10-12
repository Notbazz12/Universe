using System;
using System.Collections.Generic;
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

        private static Image[] _idleFrames;
        private static Image[] _walkFrames;
        private static Image[] _jumpFrames;
        private static Image[] _reactFrames;
        private static Image[] _sleepFrames;

        private static bool _initialized = false;

        public static string GetMascotDir()
        {
            // 1. Try assembly directory (Universe.exe folder)
            try
            {
                var asm = typeof(MascotProvider).Assembly;
                if (!string.IsNullOrEmpty(asm.Location))
                {
                    string asmDir = Path.GetDirectoryName(asm.Location);
                    if (!string.IsNullOrEmpty(asmDir))
                    {
                        string candidate = Path.Combine(asmDir, "Resources", "Mascot");
                        if (Directory.Exists(candidate)) return candidate;

                        candidate = Path.Combine(asmDir, "..", "..", "..", "Resources", "Mascot");
                        if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);

                        candidate = Path.Combine(asmDir, "..", "..", "Resources", "Mascot");
                        if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
                    }
                }
            }
            catch { }

            // 2. Try Application.StartupPath
            try
            {
                string appDir = Application.StartupPath;
                if (!string.IsNullOrEmpty(appDir))
                {
                    string candidate = Path.Combine(appDir, "Resources", "Mascot");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            catch { }

            // 3. Try AppDomain.CurrentDomain.BaseDirectory
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir))
                {
                    string candidate = Path.Combine(baseDir, "Resources", "Mascot");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            catch { }

            // 4. Try standard install path
            string installed = @"C:\Program Files (x86)\Universe\Resources\Mascot";
            if (Directory.Exists(installed)) return installed;

            // 5. Try known workspace path
            string devPath = @"D:\PROGRAMAS_MIOS\NoFences\NoFences\Resources\Mascot";
            if (Directory.Exists(devPath)) return devPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Mascot");
        }

        private static Image LoadSafe(string fileName)
        {
            try
            {
                string dir = GetMascotDir();
                string path = Path.Combine(dir, fileName);
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

        public static Image[] LoadAnimationFrames(string animName, int count)
        {
            try
            {
                string dir = GetMascotDir();
                string animDir = Path.Combine(dir, "Animations", animName);
                if (!Directory.Exists(animDir))
                {
                    animDir = Path.Combine(dir, animName);
                }

                if (Directory.Exists(animDir))
                {
                    var frames = new List<Image>();
                    for (int i = 0; i < count; i++)
                    {
                        string framePath = Path.Combine(animDir, $"{animName}_{i:D2}.png");
                        if (File.Exists(framePath))
                        {
                            using (var fs = new FileStream(framePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                var original = Image.FromStream(fs);
                                frames.Add(new Bitmap(original));
                            }
                        }
                    }
                    if (frames.Count > 0) return frames.ToArray();
                }
            }
            catch { }
            return null;
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _idleFrames = LoadAnimationFrames("idle", 5);
            _walkFrames = LoadAnimationFrames("walk", 5);
            _jumpFrames = LoadAnimationFrames("jump", 5);
            _reactFrames = LoadAnimationFrames("react", 5);
            _sleepFrames = LoadAnimationFrames("sleep", 5);

            _happy = LoadSafe("mascot_happy.png");
            _wink = LoadSafe("mascot_wink.png");
            _sleep = LoadSafe("mascot_sleep.png");
            _excited = LoadSafe("mascot_excited.png");
            _love = LoadSafe("mascot_love.png");
            _jump = LoadSafe("mascot_jump.png");
        }

        // Animation frame sequences
        public static Image[] IdleFrames => _idleFrames ?? (_idleFrames = LoadAnimationFrames("idle", 5));
        public static Image[] WalkFrames => _walkFrames ?? (_walkFrames = LoadAnimationFrames("walk", 5));
        public static Image[] JumpFrames => _jumpFrames ?? (_jumpFrames = LoadAnimationFrames("jump", 5));
        public static Image[] ReactFrames => _reactFrames ?? (_reactFrames = LoadAnimationFrames("react", 5));
        public static Image[] SleepFrames => _sleepFrames ?? (_sleepFrames = LoadAnimationFrames("sleep", 5));

        // Static or representative single sprites
        public static Image Happy => _happy ?? (_happy = LoadSafe("mascot_happy.png") ?? (_idleFrames != null && _idleFrames.Length > 0 ? _idleFrames[0] : null));
        public static Image Wink => _wink ?? (_wink = LoadSafe("mascot_wink.png") ?? (_reactFrames != null && _reactFrames.Length > 1 ? _reactFrames[1] : null));
        public static Image Sleep => _sleep ?? (_sleep = LoadSafe("mascot_sleep.png") ?? (_sleepFrames != null && _sleepFrames.Length > 0 ? _sleepFrames[0] : null));
        public static Image Excited => _excited ?? (_excited = LoadSafe("mascot_excited.png") ?? (_reactFrames != null && _reactFrames.Length > 2 ? _reactFrames[2] : null));
        public static Image Love => _love ?? (_love = LoadSafe("mascot_love.png") ?? (_reactFrames != null && _reactFrames.Length > 3 ? _reactFrames[3] : null));
        public static Image Jump => _jump ?? (_jump = LoadSafe("mascot_jump.png") ?? (_jumpFrames != null && _jumpFrames.Length > 2 ? _jumpFrames[2] : null));
    }
}
