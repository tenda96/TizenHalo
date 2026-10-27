using System;
using System.Collections.Generic;

namespace TizenHaloCaptureProbe
{
    // Caller-owned header contract for the RU7100 legacy capture core, recovered by static
    // analysis of this owner's own libsec-video-capture.so.0.1.0 (build f661ba0...).
    // Addresses in that analysis are static, not runtime. Nothing here is proven on the TV yet.
    //
    // The header is a 256-byte caller-owned block. The core checks the advertised Y/C capacities
    // BEFORE copying, so an undersized buffer makes it return an error instead of overflowing.
    // This file operates on a managed byte[] so it is fully testable on the host; at run time the
    // same byte[] is pinned and its address handed to the native call.
    public static class CaptureContract
    {
        public const int HeaderBytes = 256;

        // Byte offsets confirmed in the legacy core copy path.
        public const int OffYCapacity   = 0x00;  // caller Y capacity, checked before copy
        public const int OffCCapacity   = 0x04;  // caller chroma capacity, checked before copy
        public const int OffRetWidth    = 0x08;  // written on success
        public const int OffRetHeight   = 0x0c;  // written on success
        public const int OffYPointer    = 0x10;  // caller-owned Y destination (32-bit process)
        public const int OffCPointer    = 0x14;  // caller-owned chroma destination
        public const int OffColorFormat = 0x18;  // 0=420 (planar OR NV21), 1=422, 2=444, 3=unknown
        public const int OffProtected   = 0x1c;
        public const int Off3DMode      = 0x20;
        public const int OffTimestamp   = 0x30;  // 64-bit
        public const int OffFlip        = 0x3c;

        // This copy path does NOT write the modern interlace/pitch fields at +0x24/+0x28/+0x2c.
        // Do not read them here: they are left as zero padding.

        // Returned color-format enum. enum 0 is AMBIGUOUS: the core reports it for both planar
        // YUV420 and NV21 (interleaved VU). Chroma order must be settled from real pixels, not
        // from this enum. See GuessChromaByteOrder and the host fixtures.
        public static string ColorFormatName(int value)
        {
            switch (value)
            {
                case 0: return "420_planar_or_nv21";
                case 1: return "422";
                case 2: return "444";
                case 3: return "unknown";
                default: return "other_" + value;
            }
        }

        static void W32(byte[] b, int at, int v)
        {
            b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); b[at + 2] = (byte)(v >> 16); b[at + 3] = (byte)(v >> 24);
        }
        static int R32(byte[] b, int at)
        {
            return b[at] | b[at + 1] << 8 | b[at + 2] << 16 | b[at + 3] << 24;
        }
        static long R64(byte[] b, int at)
        {
            return (R32(b, at) & 0xffffffffL) | (long)R32(b, at + 4) << 32;
        }

        /// <summary>Zero the header, then advertise the caller buffers. Pointers are 32-bit because
        /// the capture process is ARM32. yAddress/cAddress are the pinned buffer addresses.</summary>
        public static void WriteRequest(byte[] header, int yCapacity, int cCapacity, long yAddress, long cAddress)
        {
            if (header == null || header.Length < HeaderBytes) throw new ArgumentException("header too small");
            Array.Clear(header, 0, HeaderBytes);
            W32(header, OffYCapacity, yCapacity);
            W32(header, OffCCapacity, cCapacity);
            W32(header, OffYPointer, (int)yAddress);
            W32(header, OffCPointer, (int)cAddress);
        }

        /// <summary>True when the native call left our Y/C pointers in place (it must copy into the
        /// caller buffers, never swap in its own).</summary>
        public static bool CallerPointersUnchanged(byte[] header, long yAddress, long cAddress)
        {
            return R32(header, OffYPointer) == (int)yAddress && R32(header, OffCPointer) == (int)cAddress;
        }

        public sealed class Result
        {
            public int RetWidth, RetHeight, ColorFormat, Protected, ThreeD, Flip, YCapacity, CCapacity;
            public long Timestamp;
            public bool PointersUnchanged;

            public Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>
                {
                    {"ret_width", RetWidth}, {"ret_height", RetHeight},
                    {"color_format", ColorFormat}, {"color_format_name", ColorFormatName(ColorFormat)},
                    {"protected", Protected}, {"three_d", ThreeD}, {"flip", Flip},
                    {"y_capacity", YCapacity}, {"c_capacity", CCapacity},
                    {"timestamp", Timestamp}, {"caller_pointers_unchanged", PointersUnchanged},
                };
            }
        }

        public static Result ReadResult(byte[] header, long yAddress, long cAddress)
        {
            return new Result
            {
                RetWidth = R32(header, OffRetWidth),
                RetHeight = R32(header, OffRetHeight),
                ColorFormat = R32(header, OffColorFormat),
                Protected = R32(header, OffProtected),
                ThreeD = R32(header, Off3DMode),
                Flip = R32(header, OffFlip),
                YCapacity = R32(header, OffYCapacity),
                CCapacity = R32(header, OffCCapacity),
                Timestamp = R64(header, OffTimestamp),
                PointersUnchanged = CallerPointersUnchanged(header, yAddress, cAddress),
            };
        }

        /// <summary>Is a returned geometry plausible for the buffers we advertised? The core already
        /// refuses to copy into an undersized buffer, so this only guards our own readback.</summary>
        public static bool GeometryFitsBuffers(Result r, int yBytes, int cBytes)
        {
            if (r.RetWidth < 2 || r.RetHeight < 2 || (r.RetWidth & 1) != 0 || (r.RetHeight & 1) != 0) return false;
            long y = (long)r.RetWidth * r.RetHeight;
            long c = y / 2;                 // 420: one U and one V sample per 2x2 block
            return y <= yBytes && c <= cBytes;
        }

        // ---- chroma order, settled from pixels rather than the ambiguous enum -------------------
        // For a 420 frame with chroma width = RetWidth/2 and height = RetHeight/2, the two chroma
        // bytes of block (cx,cy) sit at 2*(cy*chromaWidth + cx). NV12 stores them U then V; NV21
        // stores V then U. Reading both and labelling them by assumed order lets a later adapter
        // choose correctly after the owner captures a known color. No RGB conversion happens here.
        public static void ReadChromaPair(byte[] chroma, int chromaWidth, int cx, int cy,
                                          out int first, out int second)
        {
            int at = 2 * (cy * chromaWidth + cx);
            if (chroma == null || at < 0 || at + 1 >= chroma.Length) { first = second = -1; return; }
            first = chroma[at]; second = chroma[at + 1];
        }

        /// <summary>A small, reportable pixel sample at the frame centre: the Y value and both chroma
        /// bytes under each naming, so the owner's capture tells us the real VU/UV order offline.</summary>
        public static Dictionary<string, object> SamplePixel(byte[] y, byte[] chroma, int width, int height)
        {
            var s = new Dictionary<string, object>();
            int cxw = width / 2;
            int px = width / 2, py = height / 2;
            int yAt = py * width + px;
            s["y_center"] = (y != null && yAt >= 0 && yAt < y.Length) ? (object)(int)y[yAt] : null;
            int first, second;
            ReadChromaPair(chroma, cxw, px / 2, py / 2, out first, out second);
            s["chroma_first_byte"] = first;              // U if NV12, V if NV21
            s["chroma_second_byte"] = second;            // V if NV12, U if NV21
            s["if_nv12"] = new Dictionary<string, object> { {"u", first}, {"v", second} };
            s["if_nv21"] = new Dictionary<string, object> { {"v", first}, {"u", second} };
            return s;
        }
    }
}
