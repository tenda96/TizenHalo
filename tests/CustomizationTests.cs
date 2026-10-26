using System;
using System.Collections.Generic;
using System.IO;
using TizenAmbilight;
static class CustomizationTests
{
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static void Reject(Action action,string why){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(FormatException){rejected=true;}Check(rejected,why);}
 public static void Run()
 {
  int delay;var empty=new VideoPlaneResult{Rc=0,Error="Q74T unsupported returned plane geometry: 0x0",CaptureMs=.2};
  Check(CaptureRetry.TryDelay(empty,20,700,30,out delay,"screen_video")&&delay>=33,"Screen-video fast empty frame recovery must stay bounded at frame cadence");
  Check(!CaptureRetry.TryDelay(empty,31,700,30,out delay,"screen_video"),"Retry count remains bounded");
  Check(!CaptureRetry.TryDelay(empty,20,1001,30,out delay,"screen_video"),"Long idle remains on backoff");
  Check(!CaptureRetry.TryDelay(empty,20,700,15,out delay,"screen_video"),"Stable 15-fps profile unchanged");
  Check(!CaptureRetry.TryDelay(empty,20,700,30,out delay,"main"),"Other capture methods unchanged");
  empty.CaptureMs=20;Check(!CaptureRetry.TryDelay(empty,20,700,30,out delay,"screen_video"),"Slow native failures must not receive extended retry");
  var rows=new List<object>();
  for(int i=0;i<4;i++)rows.Add(new Dictionary<string,object>{{"hmin",i*.25},{"hmax",(i+1)*.25},{"vmin",.9},{"vmax",1d},{"x",i*.25},{"y",1.3}});
  var doc=CustomLayout.Parse(Json.ToJson(new Dictionary<string,object>{{"schema",1},{"name","TV and stand"},{"leds",rows}}));
  Check(CustomLayout.Count(doc)==4,"Custom four-LED strip must be accepted, including furniture positions");
  Check(CustomLayout.Count(CustomLayout.Parse(Json.ToJson(rows)))==4,"Bare HyperHDR array import");
  IntPtr header=System.Runtime.InteropServices.Marshal.AllocHGlobal(80);
  try {
   for(int i=0;i<80;i++)System.Runtime.InteropServices.Marshal.WriteByte(header,i,0);
   System.Runtime.InteropServices.Marshal.WriteInt32(header,8,720);System.Runtime.InteropServices.Marshal.WriteInt32(header,12,540);
   System.Runtime.InteropServices.Marshal.WriteInt32(header,0x30,123456789); // legacy timestamp, never a pitch
   var frame=new VideoPlaneResult{Rc=0};VideoPlaneCapture.ReadResultHeader(header,true,frame);
   Check(VideoPlaneCapture.SupportedGeometry(frame)&&frame.CPitch==0,"Legacy timestamps must not become modern pitch/interlace fields");
   System.Runtime.InteropServices.Marshal.WriteInt32(header,0x18,1);VideoPlaneCapture.ReadResultHeader(header,true,frame);
   Check(frame.Error!=null,"Legacy non-YUV420 result must be rejected");
   // Q74T updateParameters: +24 interlaced, +28/+2c pitches, +30 timestamp.
   // Use different strides and a non-pitch timestamp so an offset shift cannot pass.
   System.Runtime.InteropServices.Marshal.WriteInt32(header,0x18,0);
   System.Runtime.InteropServices.Marshal.WriteInt32(header,0x28,960);System.Runtime.InteropServices.Marshal.WriteInt32(header,0x2c,768);
   System.Runtime.InteropServices.Marshal.WriteInt64(header,0x30,123456789012L);
   frame=new VideoPlaneResult{Rc=0};VideoPlaneCapture.ReadResultHeader(header,false,frame);
   Check(frame.YPitch==960&&frame.CPitch==768&&frame.NativeTimestamp==123456789012L&&VideoPlaneCapture.SupportedGeometry(frame),"Q74T modern pitches must not read the timestamp");
   System.Runtime.InteropServices.Marshal.WriteInt32(header,0x24,1);
   frame=new VideoPlaneResult{Rc=0};VideoPlaneCapture.ReadResultHeader(header,false,frame);
   Check(frame.Interlaced==1&&!VideoPlaneCapture.SupportedGeometry(frame),"Q74T interlace field must be read at native offset +24");
   Check(VideoPlaneCapture.SetRequestSize(1280,720),"HD probe must fit the expanded caller-owned buffers");
   Check(!VideoPlaneCapture.SetRequestSize(1281,720)&&!VideoPlaneCapture.SetRequestSize(720,405),"Reject oversized or odd YUV420 requests");
   Check(VideoPlaneCapture.SetRequestSize(960,540),"Restore reference request after allocation tests");
  }finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(header);}
  Reject(()=>CustomLayout.Parse("[]"),"Empty layout");
  Reject(()=>CustomLayout.Parse("{\"leds\":[]}"),"Missing zones");
  Reject(()=>CustomLayout.Parse(Json.ToJson(new Dictionary<string,object>{{"schema",2},{"leds",rows}})),"Unknown schema");
  var bad=(Dictionary<string,object>)rows[0];bad["hmax"]=0d;
  Reject(()=>CustomLayout.Parse(Json.ToJson(rows)),"Empty or reversed rectangle");bad["hmax"]=.25;bad["x"]=4d;
  Reject(()=>CustomLayout.Parse(Json.ToJson(rows)),"Off-screen preview position bounds");bad["x"]=0d;bad["vmin"]="0.9";
  Reject(()=>CustomLayout.Parse(Json.ToJson(rows)),"Numeric strings must be rejected");bad["vmin"]=.9;
  var many=new List<object>();for(int i=0;i<491;i++)many.Add(rows[0]);
  Reject(()=>CustomLayout.Parse(Json.ToJson(many)),"DRGB packet limit must be enforced");
  Reject(()=>ConfigFile.Parse("{\"nested\":[]}"),"Flat settings parser must still reject nested values");
  string temp=Path.Combine(Path.GetTempPath(),"q74t-custom-"+Guid.NewGuid().ToString("N"));
  try {
   LocalSettings.Initialize(temp);
   LocalSettings.Save(new Dictionary<string,object>{{"layoutMode","custom"},{"ledLayout",Json.ToJson(doc)},{"ledCount",4},{"mapOffset",0}});
   string before=File.ReadAllText(Path.Combine(temp,"tizenhalo-settings.json"));
   Reject(()=>LocalSettings.Save(new Dictionary<string,object>{{"ledCount",5}}),"Layout/count mismatch must fail atomically");
   Check(before==File.ReadAllText(Path.Combine(temp,"tizenhalo-settings.json")),"Rejected save must preserve previous settings");
   LocalSettings.Initialize(temp);Check(CustomLayout.Count(CustomLayout.Current())==4,"Layout must survive restart");
   RectLayout.ActiveRectangles=CustomLayout.Rectangles(CustomLayout.Current());RectLayout.Enabled=true;
   var layout=new ZoneLayout(720,540,4,.08);Check(layout.Zones[3].X0==540&&layout.Zones[0].Y0==486,"Capture zones must use imported normalized geometry");
   var colors=new ZoneColors(4);for(int i=0;i<4;i++)colors.R[i]=(byte)(20+i*30);
   var packet=WledOutput.BuildDrgbMapped(colors,1,1,1,0,2,true,4);
   Check(packet[2]==20&&packet[11]==110,"Uploaded layouts must preserve physical order without a second rotation");
   LocalSettings.Save(new Dictionary<string,object>{{"captureMethod","main_no_flip"}});
   Check((string)LocalSettings.Snapshot()["captureMethod"]=="main_no_flip","Capture method persistence");
   Reject(()=>LocalSettings.Save(new Dictionary<string,object>{{"captureMethod","guessed_api"}}),"Only firmware-verified method IDs");
   var migrated=LocalSettings.MergeAndValidate(LocalSettings.Defaults(),new Dictionary<string,object>{{"customLayout",false},{"ledCount",100}});
   Check((string)migrated["layoutMode"]=="standard","Version 11 settings migration");
   Check(CaptureMethods.All.Length==7&&CaptureMethods.Find("legacy_main").Legacy,"Legacy ABI must be explicitly distinguished");
   Check(!CaptureMethods.Find("screen_full").Legacy&&CaptureMethods.Find("screen_full").Symbol=="secvideo_api_capture_screen","Full screen uses verified modern ABI");
   Check(CaptureMethods.Find("legacy_screen_video").Legacy&&CaptureMethods.Find("legacy_screen_video").Symbol=="secvideo_api_capture_screen_video_only","Legacy post-composition uses its dedicated ABI");
  } finally {RectLayout.ActiveRectangles=null;RectLayout.Enabled=false;if(Directory.Exists(temp))Directory.Delete(temp,true);}
  Console.WriteLine("PASS: custom layout validation/import, atomic persistence, furniture positions, capture zones, physical order, method IDs and v11 migration");
 }
}
