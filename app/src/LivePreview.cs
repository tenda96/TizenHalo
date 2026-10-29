using System;
using System.Diagnostics;

namespace TizenAmbilight
{
    /// <summary>
    /// LED Studio's live preview. While the editor keeps sending frames, the service shows them on the
    /// strip instead of the picture, so drawing a segment lights exactly those LEDs. A frame expires after
    /// <see cref="ExpiryMs"/>: a closed or crashed page hands the strip back to the ambilight on its own.
    /// Written by the settings server thread, read by the render loop.
    /// </summary>
    public static class LivePreview
    {
        public const int ExpiryMs = 3000;
        static readonly object Gate = new object();
        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static byte[] _rgb;
        static long _stampMs;

        public static long NowMs() { return Clock.ElapsedMilliseconds; }

        /// <summary>One "rrggbb" per LED, in strip order (LED 0 first), 1 to 490 LEDs.</summary>
        public static void Set(string hex, long nowMs)
        {
            if (hex == null || hex.Length == 0 || hex.Length % 6 != 0 || hex.Length > WledOutput.MaxDrgbLeds * 6)
                throw new ArgumentException("Preview: 1 to 490 LEDs as rrggbb hex");
            var rgb = new byte[hex.Length / 2];
            for (int i = 0; i < rgb.Length; i++)
            {
                int hi = Digit(hex[2 * i]), lo = Digit(hex[2 * i + 1]);
                if (hi < 0 || lo < 0) throw new ArgumentException("Preview: hex digits only");
                rgb[i] = (byte)(hi * 16 + lo);
            }
            lock (Gate) { _rgb = rgb; _stampMs = nowMs; }
        }

        public static void Stop() { lock (Gate) _rgb = null; }

        /// <summary>The frame to show now as R,G,B bytes, or null when no editor is previewing.</summary>
        public static byte[] Current(long nowMs)
        {
            lock (Gate) return _rgb != null && nowMs - _stampMs <= ExpiryMs ? _rgb : null;
        }

        static int Digit(char c)
        {
            return c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
        }
    }
}
