using System;
using TizenAmbilight;

static class CaptureBackendStartupTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        Func<string> forbidden=()=>{throw new Exception("Unselected library was loaded");};
        Check(CaptureBackendStartup.Initialize("ppi",forbidden,forbidden)==null,
              "RGB must reach its worker even when both full-frame libraries are forbidden");
        Check(CaptureBackendStartup.Initialize("video_plane",()=>"video denied",forbidden)=="video denied",
              "Selected video error must be preserved");
        Check(CaptureBackendStartup.Initialize("file",forbidden,()=>null)==null,
              "File capture must not depend on video-plane initialization");
        Check(CaptureBackendStartup.Initialize("unknown",forbidden,forbidden)!=null,
              "Unknown backend must not guess a native API");
        Console.WriteLine("PASS: independent RGB startup, selected-backend errors and no implicit native API");
    }
}
