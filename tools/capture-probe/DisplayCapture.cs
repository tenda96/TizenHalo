using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TizenHaloCaptureProbe
{
    // Captures a PNG screenshot via dc_request_capture_to_file_sync from libdisplay-capture-api.so.0,
    // then decodes it with the existing PNG decoder and samples the RGB. Unlike the secvideo path, this
    // display-capture API normally captures the whole screen composition, so it is the candidate that
    // may actually see on-screen colour. The _dc_capt_req_data offsets are the same family as the Q74T
    // build; they are confirmed exported on this TV but the struct ABI is assumed, so a wrong field
    // fails cleanly (the library validates app type / capture mode) rather than overflowing.
    //
    // Runs only from an explicit owner action, never on launch, never drives LEDs.
    public sealed class DisplayCapture : IDisposable
    {
        public const string Symbol = "dc_request_capture_to_file_sync";
        static readonly string[] LibraryCandidates =
        {
            "/usr/lib/libdisplay-capture-api.so.0.0",
            "/usr/lib/libdisplay-capture-api.so.0",
            "/usr/lib/libdisplay-capture-api.so",
        };

        const int ReqSize = 296;
        const int OffAppType = 0x00, OffMode = 0x04, OffCompress = 0x08, OffQuality = 0x0c;
        const int OffDir = 0x10, OffName = 0x14, OffWidth = 0x18, OffHeight = 0x1c;
        const int OffRetWidth = 0x20, OffRetHeight = 0x24, OffRetPath = 0x28, RetPathLen = 256;

        const int RTLD_NOW = 0x00002;
        [DllImport("libdl.so.2", EntryPoint = "dlopen", CharSet = CharSet.Ansi)] static extern IntPtr dlopen(string file, int flag);
        [DllImport("libdl.so.2", EntryPoint = "dlsym", CharSet = CharSet.Ansi)] static extern IntPtr dlsym(IntPtr handle, string name);
        [DllImport("libdl.so.2", EntryPoint = "dlerror")] static extern IntPtr dlerror();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int CaptureToFileFn(IntPtr request);

        IntPtr handle, sym;
        public string LibraryLoaded { get; private set; }
        public string OpenError { get; private set; }

        public DisplayCapture()
        {
            dlerror();
            foreach (string path in LibraryCandidates)
            {
                handle = dlopen(path, RTLD_NOW);
                if (handle != IntPtr.Zero) { LibraryLoaded = path; break; }
            }
            if (handle == IntPtr.Zero) { OpenError = LastError() ?? "all candidates failed"; return; }
            dlerror(); sym = dlsym(handle, Symbol);
            if (sym == IntPtr.Zero) OpenError = LastError() ?? "symbol not found";
        }

        public bool Ready { get { return handle != IntPtr.Zero && sym != IntPtr.Zero; } }
        static string LastError() { IntPtr e = dlerror(); return e == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(e); }

        /// <summary>One screenshot to a PNG in outDir, decoded and sampled. label just tags the record.</summary>
        public Dictionary<string, object> CaptureOnce(string outDir, string fileName, int width, int height, int compress = 0)
        {
            var record = new Dictionary<string, object> { {"symbol", Symbol}, {"library_loaded", LibraryLoaded} };
            if (!Ready) { record["error"] = OpenError ?? "library not ready"; return record; }

            var capture = Marshal.GetDelegateForFunctionPointer<CaptureToFileFn>(sym);
            IntPtr req = Marshal.AllocHGlobal(ReqSize), dir = IntPtr.Zero, name = IntPtr.Zero;
            string target = Path.Combine(outDir, fileName);
            try { if (File.Exists(target)) File.Delete(target); } catch { }
            try
            {
                for (int i = 0; i < ReqSize; i++) Marshal.WriteByte(req, i, 0);
                dir = Marshal.StringToHGlobalAnsi(outDir);
                name = Marshal.StringToHGlobalAnsi(fileName);
                Marshal.WriteInt32(req, OffAppType, 4);     // source
                Marshal.WriteInt32(req, OffMode, 0);
                Marshal.WriteInt32(req, OffCompress, compress);    // 0 = PNG, 1 = JPEG
                Marshal.WriteInt32(req, OffQuality, 90);
                Marshal.WriteIntPtr(req, OffDir, dir);
                Marshal.WriteIntPtr(req, OffName, name);
                Marshal.WriteInt32(req, OffWidth, width);
                Marshal.WriteInt32(req, OffHeight, height);

                var sw = Stopwatch.StartNew();
                int rc;
                try { rc = capture(req); }
                catch (Exception e) { record["native_exception"] = e.GetType().Name + ": " + e.Message; return record; }
                sw.Stop();
                record["rc"] = rc; record["duration_ms"] = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                record["ret_width"] = Marshal.ReadInt32(req, OffRetWidth);
                record["ret_height"] = Marshal.ReadInt32(req, OffRetHeight);
                string retPath = Marshal.PtrToStringAnsi(IntPtr.Add(req, OffRetPath), RetPathLen);
                if (retPath != null) { int z = retPath.IndexOf('\0'); if (z >= 0) retPath = retPath.Substring(0, z); }
                record["ret_path"] = retPath;

                string file = !string.IsNullOrEmpty(retPath) && File.Exists(retPath) ? retPath
                            : File.Exists(target) ? target : null;
                record["file_written"] = file != null;
                if (file == null) return record;
                long bytes = new FileInfo(file).Length; record["file_bytes"] = bytes;
                try
                {
                    var dsw = Stopwatch.StartNew();
                    var img = compress == 1 ? TizenAmbilight.JpegDecoder.DecodeFile(file) : TizenAmbilight.PngDecoder.DecodeFile(file);
                    dsw.Stop();
                    record["decode_ms"] = Math.Round(dsw.Elapsed.TotalMilliseconds, 2);
                    record["decoded"] = new Dictionary<string, object> { {"width", img.Width}, {"height", img.Height} };
                    record["sample"] = CaptureContract.SampleGridRgba(img.Rgba, img.Width, img.Height);
                }
                catch (Exception e) { record["decode_error"] = e.GetType().Name + ": " + e.Message; }
                try { File.Delete(file); } catch { }
            }
            finally
            {
                if (name != IntPtr.Zero) Marshal.FreeHGlobal(name);
                if (dir != IntPtr.Zero) Marshal.FreeHGlobal(dir);
                Marshal.FreeHGlobal(req);
            }
            return record;
        }

        public void Dispose() { handle = IntPtr.Zero; sym = IntPtr.Zero; }
    }
}
