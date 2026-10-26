using System;
using System.Collections.Generic;
using System.IO;
using TizenAmbilight;

static class CaptureProfileTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Installed(string path)
    {
        CaptureProfile.LoadOnce(path);
        Check(CaptureProfile.Error==null,"Profile failed: "+CaptureProfile.Error);
        var profile=CaptureProfile.Current;
        var wrong=ConfigFile.Parse(File.ReadAllText(path));wrong["video_abi"]="guessed-layout";
        bool invalid=false;try{CaptureProfile.Parse(Json.ToJson(wrong));}catch(FormatException){invalid=true;}
        Check(invalid,"Unknown native ABI accepted in package metadata");
        var defaults=LocalSettings.Defaults();
        Check((string)defaults["captureMethod"]==profile.DefaultMethod&&Convert.ToInt32(defaults["targetFps"])==profile.DefaultFps,"Profile defaults not applied");
        string temp=Path.Combine(Path.GetTempPath(),"halo-profile-"+Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(temp);
            var leds=new List<object>();for(int i=0;i<4;i++)leds.Add(new Dictionary<string,object>{{"hmin",i*.25},{"hmax",(i+1)*.25},{"vmin",.9},{"vmax",1d}});
            string layout=Json.ToJson(leds);
            File.WriteAllText(Path.Combine(temp,"tizenhalo-settings.json"),Json.ToJson(new Dictionary<string,object>{
                {"captureMethod","screen_full"},{"captureWidth",1280d},{"captureHeight",720d},
                {"wledIp","192.0.2.4"},{"ledCount",4d},{"layoutMode","custom"},{"ledLayout",layout}}));
            LocalSettings.Initialize(temp);
            Check(LocalSettings.Error=="","Profile migration lost settings: "+LocalSettings.Error);
            var settings=LocalSettings.Snapshot();
            Check((string)settings["wledIp"]=="192.0.2.4"&&CustomLayout.Count(CustomLayout.Parse((string)settings["ledLayout"]))==4,"WLED/custom layout lost on profile change");
            Check((string)settings["captureMethod"]==(profile.Id=="q74t-55"?"screen_full":"main"),"Incompatible saved method not normalized");
            if(profile.Id!="q74t-55") {
                bool rejected=false;try{LocalSettings.Save(new Dictionary<string,object>{{"captureMethod","legacy_main"}});}catch(ArgumentException){rejected=true;}
                Check(rejected,"Foreign native method accepted through browser settings");
            }
            if(profile.Id=="rgb-55") {
                Check(profile.ResolveBackend("video_plane")=="ppi"&&profile.ResolveBackend("file")=="ppi","User config escaped forced RGB");
                Check(VideoPlaneCapture.Init().Contains("No full-frame ABI"),"RGB package attempted native video initialization");
            }else Check(VideoPlaneCapture.Init().Contains("32-bit"),"Host must reject native ARM32 capture");
            if(profile.Id=="s84f-90") {
                Check(Convert.ToInt32(settings["captureWidth"])==960,"Old oversized request not migrated");
                Check(!profile.AllowsMethod("screen_full")&&!VideoPlaneCapture.SetRequestSize(1280,720),"Q74T profile leaked into S84F");
                bool oversized=false;try{LocalSettings.Save(new Dictionary<string,object>{{"captureWidth",1280d},{"captureHeight",720d}});}catch(ArgumentException){oversized=true;}
                Check(oversized,"Browser request exceeded S84F allocations");
                Check(VideoPlaneCapture.SelectMethod("screen_full").Contains("unavailable"),"Foreign entry point was not blocked before loading");
                Check(VideoPlaneCapture.SelectMethod("main").Contains("32-bit"),"Returning to an allowed method retained the previous rejection");
            }
            Check(CaptureMethods.Describe().Length==(profile.Id=="q74t-55"?7:1),"Method list is not profile-specific");
            // Even replacing package metadata in the filesystem cannot change ABI mid-process.
            CaptureProfile.LoadOnce("/missing/profile.json");Check(CaptureProfile.Current==profile,"Capture profile changed after initialization");
        }finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
        Console.WriteLine("PASS: "+profile.Id+" package defaults, settings/layout preservation, locked native scope and host guard");
    }
    public static void Invalid(string path)
    {
        CaptureProfile.LoadOnce(path);
        Check(CaptureProfile.Error!=null&&CaptureProfile.Current.Backend=="disabled","Missing/malformed profile must disable capture");
        Func<string> forbidden=()=>{throw new Exception("Native initializer called for invalid profile");};
        Check(CaptureBackendStartup.Initialize(CaptureProfile.Current.ResolveBackend("video_plane"),forbidden,forbidden)!=null,"Invalid profile fell back to Q74T");
        Console.WriteLine("PASS: invalid/missing package metadata never guesses Q74T capture");
    }
}
