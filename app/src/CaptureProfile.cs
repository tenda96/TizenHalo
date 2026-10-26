using System;
using System.Collections.Generic;
using System.IO;

namespace TizenAmbilight
{
    // Installed package metadata, separate from editable user settings. Never infer a
    // private writable ABI from a Tizen version, exported symbol or a browser value.
    public sealed class CaptureProfile
    {
        public readonly string Id, Framework, ApiVersion, Backend, VideoAbi, DefaultMethod;
        public readonly int DefaultFps;
        public readonly bool Experimental;
        public static CaptureProfile Current { get; private set; } =
            new CaptureProfile("q74t-55","tizen55","5.5","video_plane","q74t-55-native-v1","screen_video",15,false);
        public static string Error { get; private set; }
        static bool _loaded;

        CaptureProfile(string id,string framework,string api,string backend,string abi,string method,int fps,bool experimental)
        {Id=id;Framework=framework;ApiVersion=api;Backend=backend;VideoAbi=abi;DefaultMethod=method;DefaultFps=fps;Experimental=experimental;}

        public static CaptureProfile Parse(string json)
        {
            var d=ConfigFile.Parse(json);
            string id=Text(d,"id"), framework=Text(d,"target_framework"), api=Text(d,"api_version"),
                   backend=Text(d,"capture_backend"), abi=Text(d,"video_abi"), method=Text(d,"default_method");
            double fps;object value;
            if(!d.TryGetValue("default_capture_fps",out value)||!(value is double))throw new FormatException("Missing profile FPS");
            fps=(double)value;
            if(fps<1||fps>30||fps!=(int)fps)throw new FormatException("Invalid profile FPS");
            if(!d.ContainsKey("experimental")||!(d["experimental"] is bool))throw new FormatException("Missing profile experimental flag");
            bool experimental=(bool)d["experimental"];
            bool valid=id=="q74t-55" && framework=="tizen55" && api=="5.5" && backend=="video_plane" && abi=="q74t-55-native-v1" && method=="screen_video" && !experimental;
            valid|=id=="rgb-55" && framework=="tizen55" && api=="5.5" && backend=="ppi" && abi=="none" && method=="main" && experimental;
            valid|=id=="s84f-90" && framework=="tizen90" && api=="9.0" && backend=="video_plane" && abi=="s84f-90-native-v1" && method=="main" && experimental;
            if(!valid)throw new FormatException("Unsupported capture profile combination: "+id);
            return new CaptureProfile(id,framework,api,backend,abi,method,(int)fps,experimental);
        }
        static string Text(Dictionary<string,object> d,string key)
        {object v;if(!d.TryGetValue(key,out v)||!(v is string))throw new FormatException("Missing profile field: "+key);return (string)v;}

        public static void LoadOnce(params string[] paths)
        {
            if(_loaded)return;_loaded=true;
            try {
                foreach(string path in paths)if(!string.IsNullOrEmpty(path)&&File.Exists(path)) {
                    Current=Parse(File.ReadAllText(path));Error=null;return;
                }
                throw new FileNotFoundException("Package capture-profile.json is missing");
            } catch(Exception e) {
                Error=e.GetType().Name+": "+e.Message;
                Current=new CaptureProfile("invalid","","","disabled","none","main",15,true);
            }
        }
        public bool AllowsMethod(string method)
        {if(Id=="q74t-55"){try{CaptureMethods.Find(method);return true;}catch(ArgumentException){return false;}}return (Id=="s84f-90"||Id=="rgb-55")&&method=="main";}
        public string ResolveMethod(string method) {return AllowsMethod(method)?method:DefaultMethod;}
        public void NormalizeSaved(Dictionary<string,object> saved)
        {
            object method;if(saved.TryGetValue("captureMethod",out method))saved["captureMethod"]=ResolveMethod(method as string);
            if(VideoAbi==VideoPlaneAbi.S84F.Id) {
                object w,h;
                int width=960,height=540;
                try {
                    if(saved.TryGetValue("captureWidth",out w))width=Convert.ToInt32(w);
                    if(saved.TryGetValue("captureHeight",out h))height=Convert.ToInt32(h);
                }catch{width=0;height=0;}
                if(!VideoPlaneAbi.S84F.FitsRequest(width,height)) {
                    saved["captureWidth"]=960d;saved["captureHeight"]=540d;
                }
            }
        }
        public string ResolveBackend(string requested)
        {
            if(Id=="rgb-55")return "ppi";
            if(Id=="q74t-55"&&(requested=="video_plane"||requested=="ppi"||requested=="file"))return requested;
            if(Id=="s84f-90"&&(requested=="video_plane"||requested=="ppi"))return requested;
            return Backend;
        }
        public Dictionary<string,object> Describe()
        {return new Dictionary<string,object>{{"id",Id},{"target_framework",Framework},{"api_version",ApiVersion},{"capture_backend",Backend},
                {"label",Id=="q74t-55"?"Q74T reference · Tizen 5.5":Id=="rgb-55"?"RGB sampler · Tizen 5.5+ test":Id=="s84f-90"?"S84F · Tizen 9 test":"Invalid package profile"},
                {"video_abi",VideoAbi},{"experimental",Experimental},{"error",Error??""},
                {"max_capture_width",VideoAbi==VideoPlaneAbi.S84F.Id?960:1280},
                {"max_capture_height",VideoAbi==VideoPlaneAbi.S84F.Id?540:720}};}
    }
}
