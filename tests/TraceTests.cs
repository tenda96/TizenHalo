using System;
using System.Collections.Generic;
using System.Diagnostics;
using TizenAmbilight;
public static class TraceTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static long T(double ms){return (long)(ms*Stopwatch.Frequency/1000);}
 public static void Run(){
  CaptureTrace.Reset("unit-timing",false);long start=CaptureTrace.Ticks();
  var bad=new VideoPlaneResult{Rc=-1};
  foreach(double ms in new[]{1d,10d,100d}){
   var token=CaptureTrace.Begin(720,540);token.Start=start;token.Target=30;
   CaptureTrace.Complete(token,bad,start+T(1),start+T(1+ms),start+T(2+ms),true,2);start+=T(200);
  }
  var summary=CaptureTrace.Summary();var distribution=(Dictionary<string,object>)summary["native_ms_all"];
  Check(Math.Abs(Convert.ToDouble(distribution["p50"])-10)<.01,"Native median must exclude wrapper/copy time");
  var page=CaptureTrace.Page(0,1000);var entries=(List<object>)page["entries"];
  var first=(Dictionary<string,object>)entries[0];
  Check(Math.Abs(Convert.ToDouble(first["wrapper_ms"])-3)<.01,"Wrapper duration must be separate");
  string json=Json.ToJson(summary);Check(json.Contains("\"by_outcome\":{\"rc_nonzero\":3}"),"Outcome counts must serialize as an object");
  var previous=CaptureTrace.Begin(720,540);CaptureTrace.Reset("generation",false);
  CaptureTrace.Complete(previous,bad,start,start+T(3),start+T(4),true,2);
  Check(Convert.ToInt64(CaptureTrace.Summary()["total_recorded"])==0,"In-flight records crossing reset must not pollute new session");
  CaptureTrace.Reset("wrap",false);
  for(int i=0;i<CaptureTrace.Capacity+5;i++){
   var token=CaptureTrace.Begin(128,72);CaptureTrace.Complete(token,bad,0,0,CaptureTrace.Ticks(),false,0);
  }
  page=CaptureTrace.Page(0,3);entries=(List<object>)page["entries"];
  Check(Convert.ToInt64(((Dictionary<string,object>)entries[0])["seq"])==5,"Ring must export oldest retained record first");
  Check(Convert.ToInt64(page["overwritten"])==5,"Overwrites must be explicit");
  Check(Convert.ToInt32(CaptureTrace.Page(20000,3)["next_offset"])==20000,"End pagination must be empty");
  var image=new VideoPlaneResult{Rc=0,RetWidth=400,RetHeight=200,W=400,H=200,YStride=400,Y=new byte[80000],C=new byte[40000]};
  int id=1234;for(int i=0;i<20;i++){
   int bit=i<8?(0xb4>>(7-i))&1:(id>>(19-i))&1;
   image.Y[14*400+(int)((i+.5)*400/20)]=(byte)(bit==1?235:16);
  }
  Check(TraceFrameMarker.Read(image)==id,"Synthetic marker decoder must match source layout");
  image.Y[14*400+10]=16;
  Check(TraceFrameMarker.Read(image)==-1,"Invalid marker magic must be rejected");
  CaptureTrace.Reset("host-ready",false);
  Console.WriteLine("PASS: native vs wrapper timing, JSON object counts, reset generations, ring pagination and synthetic frame markers");
 }
}
