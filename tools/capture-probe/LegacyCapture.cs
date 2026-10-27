using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TizenHaloCaptureProbe
{
    // Loads the owner's legacy capture library DIRECTLY (no dependency on the modern library: the
    // legacy entry points initialize their own dispatch table internally) and exposes one capture per
    // call, so the caller can draw a known colour, capture it, redraw and capture again.
    //
    // This is the only part of the probe that invokes native capture. It is reached only from an
    // explicit owner action, never on launch, and never drives LEDs. It runs on the TV (ARM32); on the
    // host it only compiles — CaptureContract holds the host-tested logic.
    public sealed class LegacyCapture : IDisposable
    {
        public const string VideoMainSymbol = "secvideo_api_capture_video_main";
        public const string ScreenSymbol = "secvideo_api_capture_screen";

        static readonly string[] LibraryCandidates =
        {
            "/usr/lib/libsec-video-capture.so.0.1.0",
            "/usr/lib/libsec-video-capture.so.0",
            "/usr/lib/libsec-video-capture.so",
        };

        // Caller buffers large enough for a UHD panel, so the native capacity check passes for any
        // plausible returned geometry instead of failing before it writes the dimensions.
        const int AllocWidth = 3840, AllocHeight = 2176;

        const int RTLD_NOW = 0x00002;
        [DllImport("libdl.so.2", EntryPoint = "dlopen", CharSet = CharSet.Ansi)] static extern IntPtr dlopen(string file, int flag);
        [DllImport("libdl.so.2", EntryPoint = "dlsym", CharSet = CharSet.Ansi)] static extern IntPtr dlsym(IntPtr handle, string name);
        [DllImport("libdl.so.2", EntryPoint = "dlerror")] static extern IntPtr dlerror();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int CaptureFn(int width, int height, IntPtr header);

        IntPtr handle;
        public string LibraryLoaded { get; private set; }
        public string OpenError { get; private set; }

        readonly int yBytes, cBytes;
        readonly byte[] header, y, c;
        GCHandle hH, hY, hC;
        long yAddr, cAddr; IntPtr headerPtr;
        readonly Dictionary<string, IntPtr> symbols = new Dictionary<string, IntPtr>();

        public LegacyCapture()
        {
            yBytes = AllocWidth * AllocHeight; cBytes = yBytes / 2;
            header = new byte[CaptureContract.HeaderBytes]; y = new byte[yBytes]; c = new byte[cBytes];
            dlerror();
            foreach (string path in LibraryCandidates)
            {
                handle = dlopen(path, RTLD_NOW);
                if (handle != IntPtr.Zero) { LibraryLoaded = path; break; }
            }
            if (handle == IntPtr.Zero) { OpenError = LastError() ?? "all candidates failed"; return; }
            hH = GCHandle.Alloc(header, GCHandleType.Pinned); hY = GCHandle.Alloc(y, GCHandleType.Pinned); hC = GCHandle.Alloc(c, GCHandleType.Pinned);
            yAddr = hY.AddrOfPinnedObject().ToInt64(); cAddr = hC.AddrOfPinnedObject().ToInt64(); headerPtr = hH.AddrOfPinnedObject();
        }

        public bool Ready { get { return handle != IntPtr.Zero; } }

        static string LastError() { IntPtr e = dlerror(); return e == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(e); }

        /// <summary>One capture call through the named entry point. Samples the returned frame so the
        /// caller (which knows what colour it just drew) can settle colour order and coverage.</summary>
        public Dictionary<string, object> CaptureOnce(string symbol, int width, int height)
        {
            var record = new Dictionary<string, object> { {"symbol", symbol} };
            if (!Ready) { record["error"] = "library not loaded"; return record; }
            IntPtr sym;
            if (!symbols.TryGetValue(symbol, out sym))
            {
                dlerror(); sym = dlsym(handle, symbol); symbols[symbol] = sym;
            }
            record["symbol_resolved"] = sym != IntPtr.Zero;
            if (sym == IntPtr.Zero) { record["dlsym_error"] = LastError() ?? "symbol not found"; return record; }

            var capture = Marshal.GetDelegateForFunctionPointer<CaptureFn>(sym);
            CaptureContract.WriteRequest(header, yBytes, cBytes, yAddr, cAddr);
            var sw = Stopwatch.StartNew();
            int rc;
            try { rc = capture(width, height, headerPtr); }
            catch (Exception e) { record["native_exception"] = e.GetType().Name + ": " + e.Message; return record; }
            sw.Stop();
            record["rc"] = rc; record["duration_ms"] = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
            if (rc == 0)
            {
                var res = CaptureContract.ReadResult(header, yAddr, cAddr);
                record["result"] = res.ToJson();
                bool fits = CaptureContract.GeometryFitsBuffers(res, yBytes, cBytes);
                record["geometry_fits_buffers"] = fits;
                if (fits && res.ColorFormat == 0) record["sample"] = CaptureContract.SampleGrid(y, c, res.RetWidth, res.RetHeight);
            }
            else if (rc == -4) record["meaning"] = "protected content: capture refused by firmware";
            return record;
        }

        public Dictionary<string, object> Environment()
        {
            return new Dictionary<string, object>
            {
                {"process_bits", IntPtr.Size * 8}, {"alloc_width", AllocWidth}, {"alloc_height", AllocHeight},
                {"library_loaded", LibraryLoaded}, {"open_error", OpenError},
                {"note", "Legacy library loaded directly; no modern-library dependency. Static-analysis contract, live invocation."},
            };
        }

        public void Dispose()
        {
            if (hH.IsAllocated) hH.Free(); if (hY.IsAllocated) hY.Free(); if (hC.IsAllocated) hC.Free();
            handle = IntPtr.Zero;
        }
    }
}
