using System;
using TizenAmbilight;

class Program
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    static void Main(string[] args)
    {
        if(args.Length>0&&args[0]=="profile") {CaptureProfileTests.Installed(args[1]);return;}
        if(args.Length>0&&args[0]=="profile-invalid") {CaptureProfileTests.Invalid(args[1]);return;}
        if(args.Length>0&&args[0]=="interpolation") {
            using(var receiver=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0))) {
                receiver.Client.ReceiveTimeout=600;
                int port=((System.Net.IPEndPoint)receiver.Client.LocalEndPoint).Port;
                var endpoint=new System.Net.IPEndPoint(System.Net.IPAddress.Any,0);
                InterpolatedOutput.Submit("127.0.0.1",port,new byte[]{2,1,100,0,0},30);
                receiver.Receive(ref endpoint);
                System.Threading.Thread.Sleep(80);
                InterpolatedOutput.Submit("127.0.0.1",port,new byte[]{2,1,0,0,100},30);
                bool middle=false;int received=0;
                var timer=System.Diagnostics.Stopwatch.StartNew();
                while(timer.ElapsedMilliseconds<230) {
                    var frame=receiver.Receive(ref endpoint);received++;
                    if(frame[2]>0&&frame[2]<100&&frame[4]>0&&frame[4]<100)middle=true;
                }
                Check(middle&&received>=4,"Independent output must send intermediate frames");
                WledOutput.SendBlackout("127.0.0.1",port,1);
                byte[] last=null;while(receiver.Available>0)last=receiver.Receive(ref endpoint);
                Check(last!=null&&last[2]==0&&last[3]==0&&last[4]==0,"Blackout must be the final packet");
                System.Threading.Thread.Sleep(150);Check(receiver.Available==0,"No interpolated packet may relight after blackout");
                Console.WriteLine("PASS: loopback interpolation sends intermediate packets; blackout stops sender without relighting");
            }
            return;
        }
        if(args.Length>0&&args[0]=="trace-server") {
            LocalSettings.Initialize(args[1]);TraceTests.Run();
            LocalSettings.LiveStatus=new System.Collections.Generic.Dictionary<string,object>{{"output_enabled",false}};
            SettingsServer.Start(args[2]);Console.WriteLine("TRACE_FIXTURE_READY "+SettingsServer.Url);
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);return;
        }
        if(args.Length>0&&args[0]=="server")
        {
            LocalSettings.Initialize(args[1]);
            SettingsServer.Start(args[2]);
            Console.WriteLine("SERVER_READY "+SettingsServer.Url+" "+SettingsServer.Error);
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            return;
        }
        RgbMeasureApiTests.Run();
        CaptureBackendStartupTests.Run();
        VideoPlaneAbiTests.Run();
        CustomizationTests.Run();
        TraceTests.Run();
        Check(LaunchRouting.Resolve("io.github.tenda96.TizenHalo")==LaunchRole.Menu,"Icon must select menu");
        Check(LaunchRouting.Resolve("io.github.tenda96.TizenHalo.Service")==LaunchRole.CaptureService,"Service must never select menu");
        foreach(string unexpected in new string[]{null,"","io.github.tenda96.TizenHalo.Other"}) {
            bool roleRejected=false;try{LaunchRouting.Resolve(unexpected);}catch(ArgumentException){roleRejected=true;}
            Check(roleRejected,"Unknown identity must not start another menu or capture loop");
        }
        Console.WriteLine("PASS: one executable selects capture service or menu by app identity; unknown identities rejected");
        int retryDelay;
        var notReady=new VideoPlaneResult{Rc=-1};
        Check(CaptureRetry.TryDelay(notReady,1,40,30,out retryDelay)&&retryDelay>=33,"Recent transient frame should retry at bounded cadence");
        Check(CaptureRetry.TryDelay(new VideoPlaneResult{Rc=0,Error="Q74T unsupported returned plane geometry: 0x0"},3,180,25,out retryDelay)&&retryDelay==40,"Observed empty geometry should retry briefly");
        Check(!CaptureRetry.TryDelay(notReady,4,200,30,out retryDelay),"Persistent failures must use normal backoff");
        Check(!CaptureRetry.TryDelay(notReady,1,1500,30,out retryDelay),"Idle source must not be polled rapidly");
        Check(!CaptureRetry.TryDelay(notReady,1,-1,30,out retryDelay),"No successful video must retain startup backoff");
        Check(!CaptureRetry.TryDelay(notReady,1,20,15,out retryDelay),"Stable 15 fps profile must keep its original retry behavior");
        Check(!CaptureRetry.TryDelay(new VideoPlaneResult{Rc=-4},1,20,30,out retryDelay),"Protected video must not enter quick retries");
        Check(!CaptureRetry.TryDelay(new VideoPlaneResult{Rc=-1,Error="native: failure"},1,20,30,out retryDelay),"Native exceptions must keep normal backoff");
        Check(!CaptureRetry.TryDelay(new VideoPlaneResult{Rc=0,RetWidth=1920,RetHeight=1080,Error="Q74T unsupported returned plane geometry: 1920x1080"},1,20,30,out retryDelay),"Out-of-range geometry must keep safety rejection");
        Console.WriteLine("PASS: bounded recovery for recent transient frames; persistent, DRM and invalid frames keep backoff");
        var blend=new PacketInterpolator();byte[] sample={2,4,0,0,0};
        blend.Push(sample,0);sample[2]=255;
        Check(blend.Render(0)[2]==0,"Interpolator must own submitted bytes");
        blend.Push(new byte[]{2,4,100,0,200},100);
        Check(blend.Render(150)[2]==50&&blend.Render(150)[4]==100,"Interpolation midpoint must be linear");
        Check(blend.Render(500)[2]==100&&blend.Render(500)[0]==2&&blend.Render(500)[1]==4,"Interpolation must clamp and preserve protocol header");
        blend.Clear();Check(blend.Render(600)==null,"Clear must discard stale targets");
        Console.WriteLine("PASS: interpolation midpoint, copied inputs, header, clamping and clear");
        RectLayout.Enabled = false;
        foreach (int width in new[] { 960, 640 })
        {
            int height = width * 9 / 16;
            var layout = new ZoneLayout(width, height, 300, 0.08);
            Check(layout.Zones.Length == 300, "All LEDs must have a zone");
            foreach (var z in layout.Zones)
                Check(z.X0 >= 0 && z.X1 <= width && z.X1 > z.X0 &&
                      z.Y0 >= 0 && z.Y1 <= height && z.Y1 > z.Y0, "Rectangle out of bounds");
            Check(layout.TopCount + layout.RightCount + layout.BottomCount + layout.LeftCount == 300,
                  "Every LED belongs to exactly one edge");
            var pattern = RectLayout.TestColors(layout);
            for (int i = 0; i < 300; i++)
                if (layout.Zones[i].Edge == "bottom") Check(pattern.B[i] == 96, "Edge test must follow the zone edge");
        }
        var colors = new ZoneColors(300);
        for (int i=0; i<300; i++)
        {
            colors.R[i]=(byte)(i%256); colors.G[i]=(byte)((i*3)%256); colors.B[i]=(byte)((i*7)%256);
        }
        var packet = WledOutput.BuildDrgbMapped(colors, 1, 1, 1, 0, 0, false, 300);
        Check(packet.Length == 902 && packet[0] == 2 && packet[1] == 1, "Invalid DRGB packet");
        for (int i=0; i<300; i++)
            Check(packet[2+3*i]==colors.R[i] && packet[3+3*i]==colors.G[i] &&
                  packet[4+3*i]==colors.B[i], "Unrotated standard mapping changed LED " + i);
        // Without a custom layout, the installer's offset rotates the standard frame onto the strip.
        packet = WledOutput.BuildDrgbMapped(colors, 1, 1, 1, 0, 1, false, 300);
        Check(packet[2]==colors.R[1] && packet[2+3*299]==colors.R[0], "First LED offset must rotate the frame");
        // Colour calibration (output stage): defaults are a no-op; trims scale channels; saturation 0 greys.
        var cz = new ZoneColors(4);
        for (int i=0; i<4; i++){ cz.R[i]=100; cz.G[i]=200; cz.B[i]=250; }
        var cp = WledOutput.BuildDrgbMapped(cz,1,1,1,0,0,false,4);
        Check(cp[2]==100 && cp[3]==200 && cp[4]==250, "Calibration defaults must leave colour unchanged");
        cp = WledOutput.BuildDrgbMapped(cz,1,1,1,0,0,false,4, 1.0,0.5,1.0,1.0);
        Check(cp[2]==100 && cp[4]==250 && cp[3]>=99 && cp[3]<=101, "Green strength 0.5 halves green only");
        cp = WledOutput.BuildDrgbMapped(cz,1,1,1,0,0,false,4, 1.0,1.0,0.0,1.0);
        Check(cp[4]==0 && cp[2]==100 && cp[3]==200, "Blue strength 0 removes only blue");
        cp = WledOutput.BuildDrgbMapped(cz,1,1,1,0,0,false,4, 1.0,1.0,1.0,0.0);
        Check(cp[2]==cp[3] && cp[3]==cp[4], "Saturation 0 collapses to grey");
        // Target-colour matrix: default primaries are identity; a target with green shifts red toward
        // orange (adds green), and does not touch other colours.
        var tz = new ZoneColors(4); for (int i=0;i<4;i++){ tz.R[i]=255; tz.G[i]=0; tz.B[i]=0; }
        cp = WledOutput.BuildDrgbMapped(tz,1,1,1,0,0,false,4, 1.0,1.0,1.0,1.0);
        Check(cp[2]==255 && cp[3]==0 && cp[4]==0, "Default target colours leave red unchanged");
        cp = WledOutput.BuildDrgbMapped(tz,1,1,1,0,0,false,4, 1.0,1.0,1.0,1.0, 0xFF7800, 0x00FF00, 0x0000FF);
        Check(cp[2]==255 && cp[3]>=118 && cp[3]<=122 && cp[4]==0, "Red target toward orange adds green to reds");
        var bz = new ZoneColors(4); for (int i=0;i<4;i++){ bz.B[i]=255; }
        cp = WledOutput.BuildDrgbMapped(bz,1,1,1,0,0,false,4, 1.0,1.0,1.0,1.0, 0xFF7800, 0x00FF00, 0x0000FF);
        Check(cp[2]==0 && cp[3]==0 && cp[4]==255, "A red target must not change blue");
        Check(WledOutput.BuildBlackout(300).Length == 902, "Blackout must cover every LED");
        Check(VideoPlaneCapture.Init().Contains("32-bit"), "Host must not call the ARM32 ABI");
        Check(VideoPlaneCapture.SupportedGeometry(new VideoPlaneResult { RetWidth=720, RetHeight=540 }),
              "Accept Q74T's observed successful frame without pitch outputs");
        Check(VideoPlaneCapture.SupportedGeometry(new VideoPlaneResult { RetWidth=720, RetHeight=540, YPitch=960, CPitch=960 }),
              "Accept explicit valid pitches");
        Check(!VideoPlaneCapture.SupportedGeometry(new VideoPlaneResult { RetWidth=1920, RetHeight=1080 }),
              "Reject frames exceeding buffers");
        Check(!VideoPlaneCapture.SupportedGeometry(new VideoPlaneResult { RetWidth=720, RetHeight=540, YPitch=1281 }),
              "Reject strides exceeding buffers");
        var luma = new byte[720*540];
        var chroma = new byte[720*540/2];
        for (int i=0; i<luma.Length; i++) luma[i]=128;
        for (int i=0; i<chroma.Length; i++) chroma[i]=128;
        for (int row=0; row<540; row++)
            if (row<80 || row>=460)
                for (int x=0; x<720; x++) luma[row*720+x]=16;
        int activeTop,activeBottom;
        VideoPlaneCapture.DetectActiveArea(luma,720,540,18,out activeTop,out activeBottom);
        Check(activeTop==80 && activeBottom==460, "Detect upper/lower black bars");
        var cropLayout = new ZoneLayout(720,540,300,.08);
        int topZone = Array.FindIndex(cropLayout.Zones, z => z.Edge == "top" && z.X0 >= 300);
        int before = cropLayout.ExtractYuv(luma,chroma,720,540,1,1,1,1,false,false).R[topZone];
        int after = cropLayout.ExtractYuv(luma,chroma,720,540,1,1,1,1,false,false,
                                        720,720,1,activeTop,activeBottom).R[topZone];
        Check(before==0 && after>100, "Top LEDs must sample picture below the upper bar");
        Array.Clear(luma,0,luma.Length);
        VideoPlaneCapture.DetectActiveArea(luma,720,540,18,out activeTop,out activeBottom);
        Check(activeTop==0 && activeBottom==540, "A black scene must not collapse the image crop");
        Check(new ZoneLayout(960, 540, 73, .08).Zones.Length == 73, "Standard layout regressed");
        var now=DateTime.UtcNow;
        var state=new System.Collections.Generic.Dictionary<string,object> {
            {"version",ServiceHeartbeat.Version},{"phase","running"},{"updated_ms",ServiceHeartbeat.UnixMs(now)}
        };
        Check(ServiceHeartbeat.IsFresh(state,now),"A fresh running backend must confirm launch");
        state["phase"]="starting";
        Check(!ServiceHeartbeat.IsFresh(state,now),"A launch request alone must not show success");
        state["phase"]="running";
        Check(!ServiceHeartbeat.IsFresh(state,now.AddSeconds(6)),"A stale heartbeat must not confirm launch");
        state["version"]="old";
        Check(!ServiceHeartbeat.IsFresh(state,now),"An old backend must not confirm the new UI launch");
        var settings=LocalSettings.Defaults();
        bool rejected=false;
        try {LocalSettings.MergeAndValidate(settings,new System.Collections.Generic.Dictionary<string,object>{{"wledIp","not-an-ip"}});}catch(ArgumentException){rejected=true;}
        Check(rejected,"Reject invalid WLED address");
        rejected=false;
        try {LocalSettings.MergeAndValidate(settings,new System.Collections.Generic.Dictionary<string,object>{{"ledCount",1000d}});}catch(ArgumentException){rejected=true;}
        Check(rejected,"LED count must stay within the DRGB packet limit");
        // A fresh install must start colour-neutral (no hidden preset) and with the diagnostic off.
        Check(Convert.ToDouble(settings["saturation"])==1.0 && Convert.ToDouble(settings["gamma"])==1.0,
              "Fresh install must start with neutral saturation and gamma");
        Check(Convert.ToInt32(settings["black"])==10, "Default black threshold must be 10");
        Check(Convert.ToInt32(settings["barTest"])==0, "Fake black bars must default off");
        rejected=false;
        try {LocalSettings.MergeAndValidate(settings,new System.Collections.Generic.Dictionary<string,object>{{"barTest",3d}});}catch(ArgumentException){rejected=true;}
        Check(rejected,"barTest out of range must be rejected");
        Check(Convert.ToInt32(LocalSettings.MergeAndValidate(settings,new System.Collections.Generic.Dictionary<string,object>{{"barTest",2d}})["barTest"])==2,
              "barTest must accept the pillarbox value");
        string temp=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"q74t-tests-"+Guid.NewGuid().ToString("N"));
        try
        {
            LocalSettings.Initialize(temp);
            LocalSettings.Save(new System.Collections.Generic.Dictionary<string,object>{{"wledIp","192.0.2.99"},{"customLayout",false},{"ledCount",100d}});
            LocalSettings.Initialize(temp);
            Check((string)LocalSettings.Snapshot()["wledIp"]=="192.0.2.99","Settings survive reinitialization");
            Check(Convert.ToInt32(LocalSettings.Snapshot()["ledCount"])==100,"LED count survives reinitialization");
            ServiceHeartbeat.Initialize(temp);ServiceHeartbeat.Publish("running",123,122,"test",true);
            var saved=ConfigFile.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(temp,ServiceHeartbeat.FileName)));
            Check(ServiceHeartbeat.IsFresh(saved,DateTime.UtcNow),"UI can read serialized backend acknowledgement");
        }
        finally {if(System.IO.Directory.Exists(temp))System.IO.Directory.Delete(temp,true);}
        // config.json (for example filled in by an install form) seeds the first-run settings;
        // invalid values are skipped one by one and saved settings still win.
        temp=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"tizenhalo-seed-"+Guid.NewGuid().ToString("N"));
        try
        {
            ConfigFile.Set(new System.Collections.Generic.Dictionary<string,object>{{"wledIp","192.0.2.50"},{"ledCount",60d},{"mapReverse",1d},{"targetFps",0d}});
            LocalSettings.Initialize(temp);
            var seeded=LocalSettings.Snapshot();
            Check((string)seeded["wledIp"]=="192.0.2.50"&&Convert.ToInt32(seeded["ledCount"])==60&&(bool)seeded["mapReverse"],"config.json must seed first-run settings");
            Check(Convert.ToInt32(seeded["targetFps"])==15,"Invalid config.json values must keep the default");
            LocalSettings.Save(new System.Collections.Generic.Dictionary<string,object>{{"ledCount",80d}});
            LocalSettings.Initialize(temp);
            Check(Convert.ToInt32(LocalSettings.Snapshot()["ledCount"])==80,"Saved settings must win over config.json");
        }
        finally {ConfigFile.Set(null);if(System.IO.Directory.Exists(temp))System.IO.Directory.Delete(temp,true);}
        RectLayout.StandardTop=30;RectLayout.StandardRight=20;RectLayout.StandardBottom=30;RectLayout.StandardLeft=20;
        var split=new ZoneLayout(960,540,100,.08);
        Check(split.TopCount==30&&split.RightCount==20&&split.BottomCount==30&&split.LeftCount==20,"Manual side counts preserved");
        RectLayout.StandardTop=RectLayout.StandardRight=RectLayout.StandardBottom=RectLayout.StandardLeft=0;
        // "Fake black bars" diagnostic: letterbox leaves ONLY top/bottom LEDs off, pillarbox leaves
        // ONLY left/right off, so the installer sees which physical LEDs fall on each side.
        var barLayout = new ZoneLayout(960,540,100,.08);
        var letterbox = RectLayout.BarTestColors(barLayout,1);
        var pillarbox = RectLayout.BarTestColors(barLayout,2);
        for (int i=0;i<barLayout.Total;i++)
        {
            string e = barLayout.Zones[i].Edge;
            bool sides = e=="left"||e=="right", topbot = e=="top"||e=="bottom";
            bool letterLit = letterbox.R[i]!=0||letterbox.G[i]!=0||letterbox.B[i]!=0;
            bool pillarLit = pillarbox.R[i]!=0||pillarbox.G[i]!=0||pillarbox.B[i]!=0;
            Check(letterLit==sides, "Fake letterbox must switch off exactly the top and bottom LEDs");
            Check(pillarLit==topbot, "Fake pillarbox must switch off exactly the left and right LEDs");
        }
        TizenHaloLauncher.QrPng.Save("http://192.0.2.10:8793",System.IO.Path.Combine(AppContext.BaseDirectory,"settings-qr.png"));
        Console.WriteLine("PASS: black-bar crop, local persistence, config.json seeding, settings validation, fresh launch handshake, manual sides, QR generated");
        Console.WriteLine("PASS: standard layout at 960x540 and 640x360, DRGB mapping and offset, blackout, ARM32 guard");
    }
}
