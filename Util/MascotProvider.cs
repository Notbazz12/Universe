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

        /// <summary>A colour variant of the slime: hue shift (degrees) and saturation factor.</summary>
        public class Skin
        {
            public string Id, NameEn, NameEs;
            public float Hue, Saturation;
            public Skin(string id, string en, string es, float hue, float sat) { Id = id; NameEn = en; NameEs = es; Hue = hue; Saturation = sat; }
        }

        // The original slime is cyan (~190°); shifts are relative to it
        public static readonly Skin[] Skins =
        {
            new Skin("Cosmic",  "Cosmic Slime",   "Slime Cósmico",    0f,   1f),
            new Skin("Sakura",  "Sakura Slime",   "Slime Sakura",     140f, 1f),
            new Skin("Emerald", "Emerald Slime",  "Slime Esmeralda",  -55f, 1.05f),
            new Skin("Solar",   "Solar Slime",    "Slime Solar",      -150f, 1.15f),
            new Skin("Nebula",  "Nebula Slime",   "Slime Nebulosa",   80f,  1.1f),
            new Skin("Sapphire", "Sapphire Slime", "Slime Zafiro",    45f,  1.25f),
            new Skin("Shadow",  "Shadow Slime",   "Slime Sombra",     0f,   0.15f),
        };

        public static Skin CurrentSkin
        {
            get
            {
                var id = Model.AppConfig.Load().MascotSkin;
                foreach (var s in Skins) if (s.Id == id) return s;
                return Skins[0];
            }
        }

        /// <summary>Switches the colour variant; every mascot (desktop pet and fence headers) reloads.</summary>
        public static void SetSkin(string id)
        {
            var cfg = Model.AppConfig.Load();
            if (cfg.MascotSkin == id) return;
            cfg.MascotSkin = id;
            cfg.Save();
            // shortcut: old images are not disposed (a fence may still be painting one); a few MB per switch, fine for a manual action
            _idleFrames = _walkFrames = _jumpFrames = _reactFrames = _sleepFrames = null;
            _happy = _wink = _sleep = _excited = _love = _jump = null;
            SkinChanged?.Invoke(null, EventArgs.Empty);
        }

        public static event EventHandler SkinChanged;

        /// <summary>Hue-rotates and saturates a sprite with a colour matrix (luminance preserving).</summary>
        private static Image Recolor(Image src)
        {
            var skin = CurrentSkin;
            if (src == null || (skin.Hue == 0f && skin.Saturation == 1f)) return src;

            double a = skin.Hue * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            const float lr = 0.213f, lg = 0.715f, lb = 0.072f;
            // Hue rotation matrix (rows = output r,g,b) followed by saturation scaling
            float[,] h =
            {
                { (float)(lr + c * (1 - lr) + s * -lr),        (float)(lg + c * -lg + s * -lg),        (float)(lb + c * -lb + s * (1 - lb)) },
                { (float)(lr + c * -lr + s * 0.143),           (float)(lg + c * (1 - lg) + s * 0.140), (float)(lb + c * -lb + s * -0.283) },
                { (float)(lr + c * -lr + s * -(1 - lr)),       (float)(lg + c * -lg + s * lg),         (float)(lb + c * (1 - lb) + s * lb) },
            };
            float k = skin.Saturation;
            float[,] m = new float[3, 3];
            for (int r = 0; r < 3; r++)
                for (int col = 0; col < 3; col++)
                {
                    float lum = col == 0 ? lr : col == 1 ? lg : lb;
                    // saturation: blend the rotated colour with its luminance (S x H simplifies to this)
                    m[r, col] = lum * (1 - k) + h[r, col] * k;
                }

            // ColorMatrix is row-vector based: element [input, output]
            var cm = new System.Drawing.Imaging.ColorMatrix(new[]
            {
                new[] { m[0, 0], m[1, 0], m[2, 0], 0f, 0f },
                new[] { m[0, 1], m[1, 1], m[2, 1], 0f, 0f },
                new[] { m[0, 2], m[1, 2], m[2, 2], 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f },
            });

            var bmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var ia = new System.Drawing.Imaging.ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            src.Dispose();
            return bmp;
        }

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
                        return Recolor(new Bitmap(original)); // decouple from stream, apply skin
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
                                frames.Add(Recolor(new Bitmap(original)));
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
