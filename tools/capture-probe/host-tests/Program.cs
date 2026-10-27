using System;
using System.Collections.Generic;
using TizenHaloCaptureProbe;

// Host tests for the RU7100 capture contract. They exercise the header read/write logic and the
// chroma-order handling on synthetic data. No ARM code and no native library are involved.
class Tests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void W32(byte[] b, int at, int v) { b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); b[at + 2] = (byte)(v >> 16); b[at + 3] = (byte)(v >> 24); }
    static void W64(byte[] b, int at, long v) { W32(b, at, (int)v); W32(b, at + 4, (int)(v >> 32)); }

    // Simulate a successful native copy: write the returned fields at their documented offsets.
    static void FakeNativeSuccess(byte[] h, int w, int ht, int fmt, int prot, int threeD, int flip, long ts)
    {
        W32(h, CaptureContract.OffRetWidth, w);
        W32(h, CaptureContract.OffRetHeight, ht);
        W32(h, CaptureContract.OffColorFormat, fmt);
        W32(h, CaptureContract.OffProtected, prot);
        W32(h, CaptureContract.Off3DMode, threeD);
        W32(h, CaptureContract.OffFlip, flip);
        W64(h, CaptureContract.OffTimestamp, ts);
        // The RU7100 copy path never writes +0x24/+0x28/+0x2c. Put noise there to prove we ignore it.
        W32(h, 0x24, unchecked((int)0xdeadbeef));
        W32(h, 0x28, unchecked((int)0xdeadbeef));
        W32(h, 0x2c, unchecked((int)0xdeadbeef));
    }

    static void Main()
    {
        // 1. WriteRequest advertises caps and pointers at the right offsets; round-trip read.
        var h = new byte[CaptureContract.HeaderBytes];
        for (int i = 0; i < h.Length; i++) h[i] = 0x5a;        // ensure WriteRequest zeroes padding
        long yAddr = 0x12345678, cAddr = 0x7fabcd00;           // 32-bit-range addresses
        CaptureContract.WriteRequest(h, 3840 * 2176, 3840 * 2176 / 2, yAddr, cAddr);
        Check(BitConverter.ToInt32(h, 0x10) == (int)yAddr && BitConverter.ToInt32(h, 0x14) == (int)cAddr, "Pointers must sit at +0x10/+0x14");
        Check(BitConverter.ToInt32(h, 0) == 3840 * 2176, "Y capacity at +0x00");
        bool allZeroTail = true; for (int i = 0x40; i < h.Length; i++) if (h[i] != 0) allZeroTail = false;
        Check(allZeroTail, "WriteRequest must zero the header padding");

        FakeNativeSuccess(h, 640, 360, 0, 0, 0, 3, 0x1122334455L);
        var r = CaptureContract.ReadResult(h, yAddr, cAddr);
        Check(r.RetWidth == 640 && r.RetHeight == 360, "Returned dimensions at +0x08/+0x0c");
        Check(r.ColorFormat == 0 && r.Protected == 0 && r.ThreeD == 0 && r.Flip == 3, "Format/protection/3D/flip offsets");
        Check(r.Timestamp == 0x1122334455L, "64-bit timestamp at +0x30");
        Check(r.PointersUnchanged, "Caller pointers must read back unchanged");
        Check(CaptureContract.ColorFormatName(0) == "420_planar_or_nv21", "Enum 0 must stay ambiguous in its name");
        Check(CaptureContract.ColorFormatName(1) == "422" && CaptureContract.ColorFormatName(2) == "444", "422/444 names");

        // 2. The modern pitch/interlace noise at +0x24/+0x28/+0x2c must not leak into the result.
        Check(r.RetWidth == 640 && r.ColorFormat == 0, "Pitch/interlace region must be ignored");

        // 3. A swapped-in native pointer is detected.
        W32(h, CaptureContract.OffYPointer, 0x0bad0bad);
        Check(!CaptureContract.ReadResult(h, yAddr, cAddr).PointersUnchanged, "A replaced Y pointer must be reported");

        // 4. Geometry bounds against the advertised buffers.
        int yBytes = 3840 * 2176, cBytes = yBytes / 2;
        Check(CaptureContract.GeometryFitsBuffers(new CaptureContract.Result { RetWidth = 640, RetHeight = 360 }, yBytes, cBytes), "640x360 fits");
        Check(CaptureContract.GeometryFitsBuffers(new CaptureContract.Result { RetWidth = 3840, RetHeight = 2160 }, yBytes, cBytes), "UHD fits the UHD buffers");
        Check(!CaptureContract.GeometryFitsBuffers(new CaptureContract.Result { RetWidth = 641, RetHeight = 360 }, yBytes, cBytes), "Odd width rejected");
        Check(!CaptureContract.GeometryFitsBuffers(new CaptureContract.Result { RetWidth = 3840, RetHeight = 4320 }, yBytes, cBytes), "Oversized height rejected");
        Check(!CaptureContract.GeometryFitsBuffers(new CaptureContract.Result { RetWidth = 0, RetHeight = 0 }, yBytes, cBytes), "Empty geometry rejected");

        // 5. Chroma order: enum 0 cannot decide NV12 vs NV21, so both readings are reported from
        //    the same bytes. A later adapter picks the right one from a known captured color.
        int w = 8, ht = 4, cw = w / 2;
        var y = new byte[w * ht];
        var c = new byte[cw * (ht / 2) * 2];
        for (int i = 0; i < y.Length; i++) y[i] = 200;
        // Centre chroma block (cx=2,cy=1): write first=10 (U if NV12), second=240 (V if NV12).
        int at = 2 * (1 * cw + 2); c[at] = 10; c[at + 1] = 240;
        int first, second; CaptureContract.ReadChromaPair(c, cw, 2, 1, out first, out second);
        Check(first == 10 && second == 240, "Chroma pair read at the block offset");
        var sample = CaptureContract.SamplePixel(y, c, w, ht);
        var nv12 = (Dictionary<string, object>)sample["if_nv12"];
        var nv21 = (Dictionary<string, object>)sample["if_nv21"];
        Check((int)nv12["u"] == 10 && (int)nv12["v"] == 240, "NV12 reading labels first byte U");
        Check((int)nv21["v"] == 10 && (int)nv21["u"] == 240, "NV21 reading swaps U and V");
        Check((int)sample["y_center"] == 200, "Centre Y sampled");

        // 6. Out-of-range chroma access is safe.
        CaptureContract.ReadChromaPair(c, cw, 999, 999, out first, out second);
        Check(first == -1 && second == -1, "Out-of-range chroma read returns -1, never throws");

        // 7. Grid sampling finds a bright coloured block in a mostly dark frame, and reports its
        //    colour both ways, so a dark centre no longer hides real content.
        int gw = 32, gh = 16, gcw = gw / 2;
        var gy = new byte[gw * gh];
        var gc = new byte[gcw * (gh / 2) * 2];
        for (int i = 0; i < gy.Length; i++) gy[i] = 4;                 // dark everywhere
        for (int yy = 8; yy < 12; yy++) for (int xx = 16; xx < 24; xx++) gy[yy * gw + xx] = 200;  // bright block
        for (int cy = 4; cy < 6; cy++) for (int cx = 8; cx < 12; cx++)  // red-ish chroma under that block
        { int at2 = 2 * (cy * gcw + cx); gc[at2] = 90; gc[at2 + 1] = 240; }
        var grid = CaptureContract.SampleGrid(gy, gc, gw, gh);
        Check((int)grid["y_min"] == 4 && (int)grid["y_max"] == 200, "Grid Y min/max span the dark field and the bright block");
        Check((double)grid["lit_fraction"] > 0.0 && (double)grid["lit_fraction"] < 1.0, "Only part of the frame is lit");
        var bright = (Dictionary<string, object>)grid["brightest"];
        Check((int)bright["y_value"] == 200 && (int)bright["x"] >= 16 && (int)bright["x"] < 24, "Brightest point lands in the bright block");
        var bnv12 = (Dictionary<string, object>)bright["if_nv12"];
        var bnv21 = (Dictionary<string, object>)bright["if_nv21"];
        Check((int)bnv12["u"] == 90 && (int)bnv12["v"] == 240, "NV12 reading of the bright colour");
        Check((int)bnv21["v"] == 90 && (int)bnv21["u"] == 240, "NV21 reading swaps the bright colour");
        Check(((List<object>)grid["probes"]).Count == 5, "Five fixed probe points reported");
        var empty = CaptureContract.SampleGrid(null, null, 0, 0);
        Check(empty.ContainsKey("error"), "No frame yields an error, not a throw");

        // 8. RGBA grid sampling (as from a decoded PNG screenshot): colour comes back directly.
        int rw = 32, rh = 16; var rgba = new byte[rw * rh * 4];
        for (int i = 0; i < rw * rh; i++) { rgba[i * 4 + 0] = 6; rgba[i * 4 + 1] = 8; rgba[i * 4 + 2] = 10; rgba[i * 4 + 3] = 255; }
        for (int yy = 6; yy < 10; yy++) for (int xx = 20; xx < 28; xx++)   // bright red block
        { int a = (yy * rw + xx) * 4; rgba[a] = 230; rgba[a + 1] = 20; rgba[a + 2] = 20; }
        var rg = CaptureContract.SampleGridRgba(rgba, rw, rh);
        Check((int)rg["luma_max"] > 60 && (int)rg["luma_min"] < 20, "RGBA luma spans dark field and bright block");
        Check((double)rg["lit_fraction"] > 0.0 && (double)rg["lit_fraction"] < 1.0, "Only part of the RGBA frame is lit");
        var rbright = (Dictionary<string, object>)rg["brightest"];
        Check((int)rbright["r"] == 230 && (int)rbright["g"] == 20 && (int)rbright["b"] == 20, "Brightest RGBA point reports the red directly");
        Check((int)rbright["x"] >= 20 && (int)rbright["x"] < 28, "Brightest RGBA point lands in the red block");
        var rgEmpty = CaptureContract.SampleGridRgba(new byte[4], 100, 100);
        Check(rgEmpty.ContainsKey("error"), "Undersized RGBA buffer yields an error, not a throw");

        Console.WriteLine("PASS: header offsets/round-trip, ignored modern pitch region, pointer ownership, buffer bounds, NV12/NV21 chroma order, YUV and RGBA grid sampling");
    }
}
