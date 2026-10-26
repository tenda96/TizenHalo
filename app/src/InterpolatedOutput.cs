using System;
using System.Diagnostics;
using System.Threading;
namespace TizenAmbilight {
 public static class InterpolatedOutput {
  static readonly object Gate=new object();
  static readonly AutoResetEvent Wake=new AutoResetEvent(false);
  static readonly Stopwatch Clock=Stopwatch.StartNew();
  static readonly PacketInterpolator Blend=new PacketInterpolator();
  static Thread worker;static bool active;static string ip;static int port,rate;
  static double updated,meterStart;static long meterCount,sent,failed;
  public static double ActualFps {get;private set;}
  public static long Sent {get{return Interlocked.Read(ref sent);}}
  public static long Failed {get{return Interlocked.Read(ref failed);}}
  public static string LastError {get;private set;}
  public static void Submit(string host,int udpPort,byte[] packet,int fps){
   lock(Gate){
    bool reset=!active||ip!=host||port!=udpPort;
    if(reset){Blend.Clear();meterStart=Clock.Elapsed.TotalMilliseconds;meterCount=0;ActualFps=0;}
    ip=host;port=udpPort;rate=fps;updated=Clock.Elapsed.TotalMilliseconds;
    Blend.Push(packet,updated);active=true;
    if(worker==null){worker=new Thread(Run){IsBackground=true};worker.Start();}
    if(reset)Wake.Set();
   }
  }
  public static void Stop(){lock(Gate){active=false;ActualFps=0;Blend.Clear();}Wake.Set();}
  static void Run(){
   while(true){
    int wait=1000;
    lock(Gate){
     double now=Clock.Elapsed.TotalMilliseconds;
     if(active&&now-updated>1100){active=false;ActualFps=0;Blend.Clear();}
     if(active){
      LastError=WledOutput.Send(ip,port,Blend.Render(now));
      if(LastError==null){Interlocked.Increment(ref sent);meterCount++;}else Interlocked.Increment(ref failed);
      double elapsed=now-meterStart;if(elapsed>=1000){ActualFps=meterCount*1000/elapsed;meterCount=0;meterStart=now;}
      wait=Math.Max(1,(int)Math.Round(1000.0/Math.Max(1,rate)));
     }
    }
    Wake.WaitOne(wait);
   }
  }
 }
}
