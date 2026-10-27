using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TizenHaloCaptureProbe
{
    // Loads the owner's legacy capture library DIRECTLY and makes a few serialized calls, trying both
    // the video plane and the full-screen composition. No dependency on the modern library: the legacy
    // entry points initialize their own dispatch table internally.
    //
    // This is the only part of the probe that invokes native capture. It is reached only from an
    // explicit owner action, never on launch, and never drives LEDs. It runs on the TV (ARM32); on the
    // host it only compiles — CaptureContract holds the host-tested logic.
    public static class LegacyCapture
    {
        // video_main reads the video plane only; screen_full is the whole composition, so it also
        // captures a still image or the menu when no video is playing.
        static readonly string[] MethodLabels = { "video_main", "screen_full" };
        static readonly string[] MethodSymbols = { "secvideo_api_capture_video_main", "secvideo_api_capture_screen" };

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

        /// <summary>Run a bounded, serialized capture test and return a JSON-ready record. For each
        /// method it makes `callsPerMethod` calls in a row, requesting width x height. No concurrency,
        /// no LED output, no settings change.</summary>
        public static Dictionary<string, object> Run(int width, int height, int callsPerMethod)
        {
            var record = new Dictionary<string, object>
            {
                {"requested_width", width}, {"requested_height", height}, {"calls_per_method", callsPerMethod},
                {"process_bits", IntPtr.Size * 8}, {"alloc_width", AllocWidth}, {"alloc_height", AllocHeight},
                {"note", "Legacy library loaded directly; no modern-library dependency. Static-analysis contract, live invocation."},
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

            int yBytes = AllocWidth * AllocHeight;
            int cBytes = yBytes / 2;
            byte[] header = new byte[CaptureContract.HeaderBytes];
            byte[] y = new byte[yBytes];
            byte[] c = new byte[cBytes];
            GCHandle hH = GCHandle.Alloc(header, GCHandleType.Pinned);
            GCHandle hY = GCHandle.Alloc(y, GCHandleType.Pinned);
            GCHandle hC = GCHandle.Alloc(c, GCHandleType.Pinned);
            var methods = new List<object>();
            try
            {
                long yAddr = hY.AddrOfPinnedObject().ToInt64();
                long cAddr = hC.AddrOfPinnedObject().ToInt64();
                IntPtr headerPtr = hH.AddrOfPinnedObject();

                for (int m = 0; m < MethodSymbols.Length; m++)
                {
                    var method = new Dictionary<string, object> { {"method", MethodLabels[m]}, {"symbol", MethodSymbols[m]} };
                    dlerror();
                    IntPtr sym = dlsym(handle, MethodSymbols[m]);
                    method["symbol_resolved"] = sym != IntPtr.Zero;
                    if (sym == IntPtr.Zero) { method["dlsym_error"] = LastError() ?? "symbol not found"; methods.Add(method); continue; }

                    var capture = Marshal.GetDelegateForFunctionPointer<CaptureFn>(sym);
                    var calls = new List<object>();
                    for (int i = 0; i < callsPerMethod; i++)
                    {
                        CaptureContract.WriteRequest(header, yBytes, cBytes, yAddr, cAddr);
                        var sw = Stopwatch.StartNew();
                        int rc;
                        try { rc = capture(width, height, headerPtr); }
                        catch (Exception e)
                        {
                            calls.Add(new Dictionary<string, object> { {"call", i}, {"native_exception", e.GetType().Name + ": " + e.Message} });
                            break;
                        }
                        sw.Stop();

                        var call = new Dictionary<string, object> { {"call", i}, {"rc", rc}, {"duration_ms", Math.Round(sw.Elapsed.TotalMilliseconds, 2)} };
                        if (rc == 0)
                        {
                            var res = CaptureContract.ReadResult(header, yAddr, cAddr);
                            call["result"] = res.ToJson();
                            bool fits = CaptureContract.GeometryFitsBuffers(res, yBytes, cBytes);
                            call["geometry_fits_buffers"] = fits;
                            if (fits && res.ColorFormat == 0)
                                call["sample"] = CaptureContract.SampleGrid(y, c, res.RetWidth, res.RetHeight);
                        }
                        else if (rc == -4) call["meaning"] = "protected content: capture refused by firmware";
                        calls.Add(call);
                    }
                    method["calls"] = calls;
                    methods.Add(method);
                }
            }
            finally
            {
                hH.Free(); hY.Free(); hC.Free();
                // The library handle is left open for the short life of this probe run.
            }
            record["methods"] = methods;
            return record;
        }
    }
}
