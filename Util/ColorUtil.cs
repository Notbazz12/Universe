using System;
using System.Drawing;

namespace NoFences.Util
{
    public static class ColorUtil
    {
        public struct HSL
        {
            public float H; // 0-360
            public float S; // 0-1
            public float L; // 0-1
        }

        public static HSL FromColor(Color c)
        {
            float r = c.R / 255f;
            float g = c.G / 255f;
            float b = c.B / 255f;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));

            HSL hsl = new HSL();
            hsl.L = (max + min) / 2f;

            if (max == min)
            {
                hsl.H = 0;
                hsl.S = 0;
            }
            else
            {
                float d = max - min;
                hsl.S = hsl.L > 0.5f ? d / (2f - max - min) : d / (max + min);

                if (max == r)
                    hsl.H = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g)
                    hsl.H = (b - r) / d + 2;
                else
                    hsl.H = (r - g) / d + 4;

                hsl.H *= 60;
            }

            return hsl;
        }

        public static Color ToColor(HSL hsl, int alpha = 255)
        {
            float r, g, b;

            if (hsl.S == 0)
            {
                r = g = b = hsl.L;
            }
            else
            {
                float q = hsl.L < 0.5f ? hsl.L * (1 + hsl.S) : hsl.L + hsl.S - hsl.L * hsl.S;
                float p = 2 * hsl.L - q;

                r = HueToRGB(p, q, hsl.H / 360f + 1f / 3f);
                g = HueToRGB(p, q, hsl.H / 360f);
                b = HueToRGB(p, q, hsl.H / 360f - 1f / 3f);
            }

            return Color.FromArgb(alpha, (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static float HueToRGB(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
}
