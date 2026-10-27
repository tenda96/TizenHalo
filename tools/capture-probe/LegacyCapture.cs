using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TizenHaloCaptureProbe
{
    // Loads the owner's legacy capture library DIRECTLY and makes a few serialized calls.
    // It does NOT depend on the modern libvideo-capture: the legacy entry point initializes its
    // own dispatch table internally, so dlopen + dlsym of the legacy soname is enough.
    //
    // This is the only part of the probe that invokes native capture. It is reached only from an
    // explicit owner action, never on launch, and never drives LEDs. It runs on the TV (ARM32);
    // on the host it merely compiles — CaptureContract holds the host-tested logic.
    public static class LegacyCapture
    {
        const string Symbol = "secvideo_api_capture_video_main";
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

        static string LastError()
        {
            IntPtr e = dlerror();
            return e == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(e);
        }

        /// <summary>Run a bounded, serialized capture test and return a JSON-ready record. Makes
        /// `calls` calls (default 3), one after another, requesting width x height. No concurrency,
        /// no LED output, no settings change.</summary>
        public static Dictionary<string, object> Run(int width, int height, int calls)
        {
            var record = new Dictionary<string, object>
            {
                {"requested_width", width}, {"requested_height", height}, {"requested_calls", calls},
                {"symbol", Symbol}, {"process_bits", IntPtr.Size * 8},
                {"alloc_width", AllocWidth}, {"alloc_height", AllocHeight},
                {"note", "Legacy library loaded directly; no modern-library dependency. Static-analysis contract, first live invocation."},
            };

            IntPtr handle = IntPtr.Zero; string loaded = null;
            dlerror();
            foreach (string path in LibraryCandidates)
            {
                handle = dlopen(path, RTLD_NOW);
                if (handle != IntPtr.Zero) { loaded = path; break; }
            }
            record["library_loaded"] = loaded;
            if (handle == IntPtr.Zero) { record["dlopen_error"] = LastError() ?? "all candidates failed"; return record; }

            dlerror();
            IntPtr sym = dlsym(handle, Symbol);
            record["symbol_resolved"] = sym != IntPtr.Zero;
            if (sym == IntPtr.Zero) { record["dlsym_error"] = LastError() ?? "symbol not found"; return record; }

            var capture = Marshal.GetDelegateForFunctionPointer<CaptureFn>(sym);

            int yBytes = AllocWidth * AllocHeight;
            int cBytes = yBytes / 2;
            byte[] header = new byte[CaptureContract.HeaderBytes];
            byte[] y = new byte[yBytes];
            byte[] c = new byte[cBytes];
            GCHandle hH = GCHandle.Alloc(header, GCHandleType.Pinned);
            GCHandle hY = GCHandle.Alloc(y, GCHandleType.Pinned);
            GCHandle hC = GCHandle.Alloc(c, GCHandleType.Pinned);
            var results = new List<object>();
            try
            {
                long yAddr = hY.AddrOfPinnedObject().ToInt64();
                long cAddr = hC.AddrOfPinnedObject().ToInt64();
                IntPtr headerPtr = hH.AddrOfPinnedObject();

                for (int i = 0; i < calls; i++)
                {
                    CaptureContract.WriteRequest(header, yBytes, cBytes, yAddr, cAddr);
                    var sw = Stopwatch.StartNew();
                    int rc;
                    try { rc = capture(width, height, headerPtr); }
                    catch (Exception e)
                    {
                        results.Add(new Dictionary<string, object> { {"call", i}, {"native_exception", e.GetType().Name + ": " + e.Message} });
                        break;
                    }
                    sw.Stop();

                    var call = new Dictionary<string, object> { {"call", i}, {"rc", rc}, {"duration_ms", Math.Round(sw.Elapsed.TotalMilliseconds, 2)} };
                    if (rc == 0)
                    {
                        var res = CaptureContract.ReadResult(header, yAddr, cAddr);
                        call["result"] = res.ToJson();
                        call["geometry_fits_buffers"] = CaptureContract.GeometryFitsBuffers(res, yBytes, cBytes);
                        if (CaptureContract.GeometryFitsBuffers(res, yBytes, cBytes) && res.ColorFormat == 0)
                            call["center_sample"] = CaptureContract.SamplePixel(y, c, res.RetWidth, res.RetHeight);
                    }
                    else if (rc == -4) call["meaning"] = "protected content: capture refused by firmware";
                    results.Add(call);
                }
            }
            finally
            {
                hH.Free(); hY.Free(); hC.Free();
                // The library handle is intentionally left open for the short life of this probe run.
            }
            record["calls"] = results;
            return record;
        }
    }
}
