using System;
using System.Collections.Generic;

namespace TizenAmbilight
{
    // Only entry points verified on firmware T-NKMDEUC-2743.0 (2020 Q series, Tizen 5.5).
    public sealed class CaptureMethod
    {
        public readonly string Id, Label, Library, Symbol, Note;
        public readonly bool Legacy;
        public CaptureMethod(string id,string label,string library,string symbol,bool legacy,string note)
        { Id=id;Label=label;Library=library;Symbol=symbol;Legacy=legacy;Note=note; }
    }
    public static class CaptureMethods
    {
        // Notes summarize a measured comparison on a QE55Q74T (Tizen 5.5): distinct frames per second
        // while playing video, requested at 20 fps. See docs/TECHNICAL.md.
        public static readonly CaptureMethod[] All = {
            new CaptureMethod("main","Video plane","libvideo-capture.so.0.1.0","secvideo_api_capture_video_main",false,"Reads the decoded video plane. Reliable, but heavier per call: about 13 new frames/s."),
            new CaptureMethod("main_no_flip","Video plane · no flip","libvideo-capture.so.0.1.0","secvideo_api_capture_video_main_no_flip",false,"Like Video plane, without the firmware's flip correction. The picture may come out mirrored."),
            new CaptureMethod("screen_video","Screen · video only","libvideo-capture.so.0.1.0","secvideo_api_capture_screen_video_only",false,"Post-composition picture, video layer only. Best result in tests: about 15 new frames/s. Recommended."),
            new CaptureMethod("screen_full","Screen · full composition","libvideo-capture.so.0.1.0","secvideo_api_capture_screen",false,"Everything on screen, TV menus and overlays included, so they reach the LEDs too. Lightest per call, about 14 frames/s."),
            new CaptureMethod("legacy_main","Legacy · video plane","libsec-video-capture.so.0.1.0","secvideo_api_capture_video_main",true,"Second firmware library with its own parameter layout. Slower: about 9 frames/s."),
            new CaptureMethod("legacy_screen_video","Legacy · screen video only","libsec-video-capture.so.0.1.0","secvideo_api_capture_screen_video_only",true,"Post-composition path of the legacy library. Slower: about 11 frames/s."),
            new CaptureMethod("legacy_no_flip","Legacy · no flip","libsec-video-capture.so.0.1.0","secvideo_api_capture_video_main_no_flip",true,"Legacy library without flip correction. Slower: about 10 frames/s.")
        };
        public static CaptureMethod Find(string id)
        { foreach(var m in All)if(m.Id==id)return m;throw new ArgumentException("Invalid capture method"); }
        public static object[] Describe()
        {
            var list=new List<object>();
            if(CaptureProfile.Current.Backend=="ppi")return new object[]{new Dictionary<string,object>{{"id","main"},{"label","RGB point sampler"},{"library","libvideoenhance.so"},
                {"symbol","complete RGB triplet"},{"note","This package measures RGB spots; full-frame methods are disabled. LED send rate is separate from point refresh."},{"experimental",true}}};
            foreach(var m in All)if(CaptureProfile.Current.AllowsMethod(m.Id))
                list.Add(new Dictionary<string,object>{{"id",m.Id},{"label",m.Label},{"library",m.Library},{"symbol",m.Symbol},
                {"note",CaptureProfile.Current.Id=="q74t-55"?m.Note:"S84F experimental video-plane adapter. Not validated in TizenHalo; Q74T rates do not apply."},
                {"experimental",CaptureProfile.Current.Experimental||m.Id!="screen_video"}});
            return list.ToArray();
        }
    }
}
