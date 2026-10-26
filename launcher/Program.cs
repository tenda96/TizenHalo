using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Tizen.Applications;
using ElmSharp;
using Net.Codecrete.QrCodeGenerator;
using TizenAmbilight;

namespace TizenHaloLauncher
{
    class App : CoreUIApplication
    {
        Window window;
        Label status,detail,url,fps,signal,controller,configuration,footer,qrPlaceholder;
        bool ready;
        Button lightButton,videoButton;
        bool lightOn=true;
        DateTime lastToggle=DateTime.MinValue;
        double scale=1,offsetX,offsetY;
        sealed class Placement {public EvasObject Item;public int X,Y,W,H;}
        sealed class FontStyle {public int Size;public string Color;}
        readonly List<Placement> placements=new List<Placement>();
        readonly Dictionary<Label,FontStyle> fonts=new Dictionary<Label,FontStyle>();
        Image qr;
        readonly List<EvasObject> qrBlocks=new List<EvasObject>();
        string statePath,qrUrl,launchError;
        DateTime requested=DateTime.MinValue;
        IntPtr timer;
        AppControl launchControl;
        bool launchDiagnosed;
        static void Trace(string phase,Exception error=null) { StartupTrace.Send(phase,error); }
        static string Escape(string value) { return (value??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;"); }
        void Style(Label label,int size,string color) {
            fonts[label]=new FontStyle{Size=size,Color=color};
            label.TextStyle="DEFAULT='font_size="+Math.Max(12,(int)(size*scale))+" color="+color+" align=left valign=top wrap=word'";
        }
        void Place(EvasObject item,int x,int y,int w,int h) {
            placements.Add(new Placement{Item=item,X=x,Y=y,W=w,H=h});
            item.Move((int)(offsetX+x*scale),(int)(offsetY+y*scale));item.Resize((int)(offsetX+(x+w)*scale)-(int)(offsetX+x*scale),(int)(offsetY+(y+h)*scale)-(int)(offsetY+y*scale));
        }
        void ApplyViewport() {
            var bounds=window.Geometry;var screen=window.ScreenSize;
            int w=bounds.Width>=640?bounds.Width:screen.Width,h=bounds.Height>=360?bounds.Height:screen.Height;
            if(w<640||h<360)return;
            scale=Math.Min(w/1920.0,h/1080.0);offsetX=(w-1920*scale)/2;offsetY=(h-1080*scale)/2;
            foreach(var p in placements){p.Item.Move((int)(offsetX+p.X*scale),(int)(offsetY+p.Y*scale));p.Item.Resize((int)(offsetX+(p.X+p.W)*scale)-(int)(offsetX+p.X*scale),(int)(offsetY+(p.Y+p.H)*scale)-(int)(offsetY+p.Y*scale));}
            foreach(var f in fonts)f.Key.TextStyle="DEFAULT='font_size="+Math.Max(12,(int)(f.Value.Size*scale))+" color="+f.Value.Color+" align=left valign=top wrap=word'";
        }
        void ToggleLights() {
            if((DateTime.UtcNow-lastToggle).TotalMilliseconds<500)return;lastToggle=DateTime.UtcNow;
            try {
                var control=new AppControl(true){ApplicationId=ServiceHeartbeat.ServiceId,Operation=AppControlOperations.Default};
                launchControl=control;control.ExtraData.Add("q74t_command","toggle_output");
                AppControl.SendLaunchRequest(control);detail.Text="Switching the lights…";
            }catch(Exception e){SetState("Command failed",e.Message,"#ffb389");Trace("launcher-toggle-error",e);}
        }
        Label Text(string text,int x,int y,int w,int h,int size,string color="#edf5fa")
        {
            var label=new Label(window);Style(label,size,color);
            label.Text=Escape(text);Place(label,x,y,w,h);label.Show();return label;
        }
        static double Number(Dictionary<string,object> state,string key,double fallback=0) {
            object value;try{return state.TryGetValue(key,out value)?Convert.ToDouble(value,CultureInfo.InvariantCulture):fallback;}catch{return fallback;}
        }
        void SetState(string title,string description,string color="#52e2bb") {
            status.Text=Escape(title);detail.Text=Escape(description);Style(status,42,color);
        }
        void Panel(int x,int y,int w,int h,Color color) {
            var rect=new Rectangle(window){Color=color};Place(rect,x,y,w,h);rect.Show();
        }
        void DrawQr(string address) {
            foreach(var block in qrBlocks)block.Hide();
            placements.RemoveAll(p=>qrBlocks.Contains(p.Item));qrBlocks.Clear();
            var code=QrCode.EncodeText(address,QrCode.Ecc.Medium);
            int module=Math.Max(1,284/(code.Size+8));
            int startX=1368+(284-(code.Size+8)*module)/2+4*module;
            int startY=485+(284-(code.Size+8)*module)/2+4*module;
            for(int y=0;y<code.Size;y++)for(int x=0;x<code.Size;x++)if(code.GetModule(x,y)) {
                var rect=new Rectangle(window){Color=new Color(0,0,0)};
                Place(rect,startX+x*module,startY+y*module,module,module);rect.Show();qrBlocks.Add(rect);
            }
        }
        void ReturnOrRetry() { if(ready)Exit();else LaunchService(); }
        protected override void OnCreate()
        {
            base.OnCreate();Trace("launcher-created");

            try {
                Elementary.Initialize();
                window=new Window("TizenHalo");var screen=window.ScreenSize;
                window.Resize(screen.Width>=640?screen.Width:1920,screen.Height>=360?screen.Height:1080);
                window.FocusHighlightEnabled=true;
                window.Resized+=(sender,e)=>{try{ApplyViewport();}catch{}};
                window.BackButtonPressed+=(s,e)=>Exit();
                var bg=new Background(window){Color=new Color(9,14,19)};
                window.AddResizeObject(bg);bg.Show();
                Panel(96,326,1060,470,new Color(20,35,46));
                Panel(1196,326,628,604,new Color(20,35,46));
                foreach(int x in new[]{134,468,802})Panel(x,550,316,150,new Color(14,27,37));
                Panel(1360,477,300,300,new Color(255,255,255));
                Text("TIZENHALO",96,74,1500,60,30,"#52e2bb");
                Text("Light that follows the picture.",96,150,1500,100,60);
                Text("Adjust it from your phone. Everything runs on the TV.",100,244,1600,58,28);
                Text("LIGHTS",134,352,980,40,21,"#9cb0bf");
                Text("CAPTURE / OUTPUT FPS",158,574,280,42,18,"#9cb0bf");
                Text("VIDEO SIGNAL",492,574,280,42,18,"#9cb0bf");
                Text("WLED CONTROLLER",826,574,280,42,18,"#9cb0bf");
                Text("SETUP",134,731,200,38,18,"#9cb0bf");
                Text("Settings",1236,368,548,62,34,"#52e2bb");
                Text("Phone and TV on the same network",1236,431,548,42,22);
                Text("Scan the QR code or open:",1236,807,548,45,23);
                Text("In video apps, press Play to start the lights.",100,992,1680,48,26,"#9cb0bf");
                status=Text("Starting the lights…",134,395,984,62,42,"#52e2bb");
                detail=Text("Waiting for the capture service on the TV.",134,459,984,47,25);
                fps=Text("—",158,618,268,62,38);
                signal=Text("Waiting",492,618,268,62,32);
                controller=Text("Waiting",826,625,268,62,29);
                configuration=Text("Loading…",338,724,780,48,25);
                url=Text("Available once the service is ready",1236,850,548,62,25,"#52e2bb");
                footer=Text("Arrows: select  ·  OK: run  ·  Back: video",120,936,1030,42,22);
                lightButton=new Button(window){Text="Lights off"};Place(lightButton,108,848,490,64);lightButton.Show();
                videoButton=new Button(window){Text="Back to video"};Place(videoButton,652,848,490,64);videoButton.Show();
                lightButton.Clicked+=(sender,e)=>ToggleLights();videoButton.Clicked+=(sender,e)=>ReturnOrRetry();
                lightButton.KeyDown+=(sender,e)=>{if(e.KeyName=="Right"||e.KeyName=="Down")videoButton.SetFocus(true);};
                videoButton.KeyDown+=(sender,e)=>{if(e.KeyName=="Left"||e.KeyName=="Up")lightButton.SetFocus(true);};
                qrPlaceholder=Text("The QR code appears here once started.",1380,577,260,100,24,"#20383b");
                qr=new Image(window);Place(qr,1368,485,284,284);
                window.Show();
                statePath=Path.Combine(Application.Current.DirectoryInfo.Data,ServiceHeartbeat.FileName);
                timer=EcoreMainloop.AddTimer(.5,()=>{Refresh();return true;});
                window.Show();ApplyViewport();lightButton.SetFocus(true);
                Trace("launcher-menu-visible");
                EcoreMainloop.Post(()=>LaunchService());Refresh();
            }catch(Exception e){Trace("launcher-ui-error",e);if(status!=null)status.Text="Menu error";if(detail!=null)detail.Text=e.Message;if(window!=null)window.Show();}
        }
        void LaunchService()
        {
            if((DateTime.UtcNow-requested).TotalSeconds<2)return;
            requested=DateTime.UtcNow;launchError=null;launchDiagnosed=false;
            if(status!=null)SetState("Starting the lights…","Waiting for the capture service on the TV.");
            try {
                var control=new AppControl(true){ApplicationId=ServiceHeartbeat.ServiceId,Operation=AppControlOperations.Default,LaunchMode=AppControlLaunchMode.Single};
                launchControl=control;
                control.ExtraData.Add("q74t_command","run");
                AppControl.SendLaunchRequest(control,(request,reply,result)=>{
                    Trace("launcher-service-result-"+result);
                    if(result==AppControlReplyResult.Failed)launchError="The TV reported an error starting the service.";
                });
                Trace("launcher-service-requested");
            }catch(Exception e){launchError=e.Message;Trace("launcher-service-error",e);}
        }
        static string Value(Dictionary<string,object> state,string key)
        {object v;return state.TryGetValue(key,out v)?Convert.ToString(v):"";}
        void Refresh()
        {
            if(status==null)return;
            if(requested!=DateTime.MinValue&&!launchDiagnosed&&(DateTime.UtcNow-requested).TotalSeconds>5) {
                launchDiagnosed=true;
                try { Trace("launcher-service-running-"+ApplicationManager.IsRunning(ServiceHeartbeat.ServiceId)); }
                catch(Exception e){Trace("launcher-service-check-error",e);}
            }
            try {
                Dictionary<string,object> state=null;
                try{state=ConfigFile.Parse(File.ReadAllText(statePath));}catch{}
                if(state!=null&&Value(state,"version")==ServiceHeartbeat.Version&&Value(state,"phase")=="error") {
                    ready=false;SetState("The capture service reported an error","Press OK to retry. Details are in the diagnostics.","#ffb389");return;
                }
                ready=ServiceHeartbeat.IsFresh(state,DateTime.UtcNow);
                if(ready) {
                    string phase=Value(state,"phase"),address=Value(state,"settings_url");
                    lightOn=Value(state,"output_enabled")!="False";
                    lightButton.Text=lightOn?"Lights off":"Lights on";
                    videoButton.Text="Back to video";
                    double age=Number(state,"last_frame_age_ms",-1);
                    if(phase=="disabled")SetState("Lights disabled","Turn them back on from the settings page.","#ffca8b");
                    else if(phase=="paused")SetState("Capture paused","Press OK to start again.","#ffca8b");
                    else if(age>=0&&age<1500)SetState("Lights in sync","You can go back to the video; the lights keep running.");
                    else if(Value(state,"status").Contains("rc=-4"))SetState("This video is protected","The TV does not share the picture of this content.","#ffca8b");
                    else SetState("Ready. Start a video.","Press Back, open YouTube or another app and start playback.");
                    fps.Text=Number(state,"fps").ToString("0.0",CultureInfo.InvariantCulture)+" / "+
                        (Value(state,"interpolation")=="True"?Number(state,"output_fps"):Number(state,"fps")).ToString("0.0",CultureInfo.InvariantCulture);
                    string detected=Value(state,"hdr_detected"),hdrMode=Value(state,"hdr_mode");
                    if(hdrMode!="auto")detected=hdrMode=="hlg"?"HLG":hdrMode=="hdr"?"HDR10":"SDR";
                    else if(detected.Length>18)detected="n/a";
                    signal.Text=Escape((detected.Length==0?"—":detected)+(hdrMode=="auto"?" · Auto":" · Manual"));
                    controller.Text=Escape(Value(state,"wled_ip"));
                    string dimensions=Value(state,"resolution");
                    configuration.Text=Number(state,"led_count",0).ToString("0")+" LEDs · "+
                        (Value(state,"custom_layout")=="True"?"Custom layout":"Standard frame")+(dimensions.Length>0?" · "+dimensions:"");
                    footer.Text="Arrows: select  ·  OK: run  ·  Back: video";
                    url.Text=Escape(address.Length==0?"Address not available yet":address);
                    if(phase=="paused") {ready=false;videoButton.Text="Resume capture";}
                    if(Value(state,"settings_error").Length>0) {
                        url.Text="Web settings unavailable";foreach(var block in qrBlocks)block.Hide();qrUrl=null;qrPlaceholder.Text="See the diagnostics for details.";qrPlaceholder.Show();
                    } else if(address.Length>0&&address!=qrUrl) {
                        DrawQr(address);qrPlaceholder.Hide();qrUrl=address;
                    }
                } else if(launchError!=null) {
                    SetState("Start failed",launchError,"#ffb389");
                    videoButton.Text="Retry start";
                } else if(requested!=DateTime.MinValue&&(DateTime.UtcNow-requested).TotalSeconds>12) {
                    SetState("The capture service is not responding","Press OK to retry, or Back to return to the video.","#ffb389");
                    videoButton.Text="Retry start";
                }
            }catch(Exception e){Trace("launcher-refresh-error",e);}
        }
        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {base.OnAppControlReceived(e);if(window!=null)EcoreMainloop.Post(()=>LaunchService());}
        protected override void OnResume(){base.OnResume();if(window!=null)EcoreMainloop.Post(()=>LaunchService());}
        protected override void OnTerminate(){if(timer!=IntPtr.Zero)EcoreMainloop.RemoveTimer(timer);base.OnTerminate();}
        public static void RunMenu(string[] args)
        {Trace("launcher-main");try{new App().Run(args);}catch(Exception e){Trace("launcher-fatal",e);Thread.Sleep(1600);}}
    }
}
