using System;

namespace TizenAmbilight
{
    // RGB initialization belongs to its serialized sampler, after the power/output gates.
    // Selecting it must not load unrelated, potentially forbidden video/file libraries.
    public static class CaptureBackendStartup
    {
        public static string Initialize(string backend, Func<string> video, Func<string> file)
        {
            switch(backend) {
                case "video_plane": return video();
                case "file": return file();
                case "ppi": return null;
                default: return "Unknown capture backend: "+backend;
            }
        }
    }
}
