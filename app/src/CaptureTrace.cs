using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace TizenAmbilight {
 public static class CaptureTrace {
  public const int Capacity=20000;
  static readonly object Gate=new object();
  static readonly long Origin=Stopwatch.GetTimestamp();
  static readonly Entry[] Buffer=new Entry[Capacity];
  static long generation=1,writes,discarded,inFlight;
  static string source="unspecified";static bool markers;
  public static int TargetFps;public static bool Interpolation;
  static int waitRequested;static double waitActual;static byte waitKind;
  public struct Token {
   public long Generation,Start;public int RequestedW,RequestedH,Target,HdrRaw;public bool Markers,Interpolation,HdrFresh,HdrPollError;
   public double HdrAge,WaitActual;public int WaitRequested;public byte WaitKind;
   public string Method;
  }
  struct Entry {
   public long Sequence,Start,NativeStart,NativeEnd,End,VideoTimestamp;
   public int RequestedW,RequestedH,Target,Rc,W,H,YPitch,CPitch,HdrRaw,Marker,WaitRequested;
   public bool Called,Interpolation,HdrFresh,HdrPollError;public byte Outcome,WaitKind;
   public double HdrAge,WaitActual;public uint Fingerprint;
   public string Method;
   public int ColorFormat,ProtectedOn,Interlaced,FlipType,YCapacity,CCapacity;
  }
  public static long Ticks(){return Stopwatch.GetTimestamp();}
  static double Ms(long ticks){return ticks*1000.0/Stopwatch.Frequency;}
  static double Since(long ticks){return Ms(ticks-Origin);}
  public static bool NativeInFlight {get{return Interlocked.Read(ref inFlight)!=0;}}
  public static Token Begin(int w,int h) {
   var t=new Token {Generation=Interlocked.Read(ref generation),Start=Ticks(),RequestedW=w,RequestedH=h,Target=TargetFps,
    Method=VideoPlaneCapture.ActiveMethod,Markers=markers,Interpolation=Interpolation,HdrRaw=HdrProbe.RawType,HdrFresh=HdrProbe.Available,HdrAge=HdrProbe.AgeMs,
    HdrPollError=!string.IsNullOrEmpty(HdrProbe.Error),WaitRequested=waitRequested,WaitActual=waitActual,WaitKind=waitKind};
   waitRequested=0;waitActual=0;waitKind=0;return t;
  }
  public static void NativeEnter(long tick){Interlocked.Exchange(ref inFlight,tick);}
  public static void NativeLeave(){Interlocked.Exchange(ref inFlight,0);}
  public static void Sleep(int ms,byte kind,bool viaPacer,bool precise=false) {
   long start=Ticks();if(viaPacer)FramePacer.Sleep(ms,precise);else Thread.Sleep(ms);
   waitRequested+=ms;waitActual+=Ms(Ticks()-start);waitKind=kind;
  }
  public static void Complete(Token t,VideoPlaneResult r,long nativeStart,long nativeEnd,long end,bool called,byte outcome) {
   // Allocation-free per capture beyond the existing result object. Fingerprint work is outside wrapper/native timings.
   var e=new Entry {Start=t.Start,NativeStart=nativeStart,NativeEnd=nativeEnd,End=end,Called=called,Outcome=outcome,
    RequestedW=t.RequestedW,RequestedH=t.RequestedH,Target=t.Target,Rc=r.Rc,W=r.RetWidth,H=r.RetHeight,YPitch=r.YPitch,CPitch=r.CPitch,
    ColorFormat=r.ColorFormat,ProtectedOn=r.ProtectedOn,Interlaced=r.Interlaced,FlipType=r.FlipType,YCapacity=r.YCapacity,CCapacity=r.CCapacity,VideoTimestamp=r.NativeTimestamp,
    HdrRaw=t.HdrRaw,HdrFresh=t.HdrFresh,HdrAge=t.HdrAge,HdrPollError=t.HdrPollError,Interpolation=t.Interpolation,
    Fingerprint=TraceFrameMarker.Fingerprint(r),Marker=t.Markers?TraceFrameMarker.Read(r):-1,
    Method=t.Method,WaitRequested=t.WaitRequested,WaitActual=t.WaitActual,WaitKind=t.WaitKind};
   lock(Gate){if(t.Generation!=generation){discarded++;return;}e.Sequence=writes;Buffer[(int)(writes%Capacity)]=e;writes++;}
  }
  public static void Reset(string label,bool decodeMarkers) {
   if(label==null||label.Length>100)throw new ArgumentException("Source label max 100 characters");
   lock(Gate){writes=0;discarded=0;source=label;markers=decodeMarkers;Interlocked.Increment(ref generation);}
   // Never restart the monotonic clock. Entries from a call crossing reset are discarded by generation.
  }
  static string Outcome(byte c){return c==5?"ok":c==0?"not_initialised":c==1?"native_exception":c==2?"rc_nonzero":c==3?"unsupported_geometry":"plane_copy_exception";}
  static Entry[] Snapshot(int offset,int limit,out long session,out long total,out string label) {
   lock(Gate){session=generation;total=writes;label=source;int have=(int)Math.Min(writes,Capacity);
    int take=Math.Max(0,Math.Min(limit,have-offset));var copy=new Entry[take];long first=writes-have+offset;
    for(int i=0;i<take;i++)copy[i]=Buffer[(int)((first+i)%Capacity)];return copy;}
  }
  public static Dictionary<string,object> Page(int offset,int limit) {
   if(offset<0)throw new ArgumentException("Negative offset");limit=Math.Max(1,Math.Min(1000,limit));
   long session,total;string label;var entries=Snapshot(offset,limit,out session,out total,out label);var rows=new List<object>(entries.Length);
   foreach(var e in entries)rows.Add(new Dictionary<string,object>{
    {"seq",e.Sequence},{"start_ms",Since(e.Start)},{"native_start_ms",e.Called?(object)Since(e.NativeStart):null},
    {"native_end_ms",e.Called?(object)Since(e.NativeEnd):null},{"end_ms",Since(e.End)},
    {"native_ms",e.Called?(object)Ms(e.NativeEnd-e.NativeStart):null},{"wrapper_ms",Ms(e.End-e.Start)},
    {"native_called",e.Called},{"rc",e.Rc},{"outcome",Outcome(e.Outcome)},{"capture_method",e.Method},
    {"requested_w",e.RequestedW},{"requested_h",e.RequestedH},{"ret_w",e.W},{"ret_h",e.H},{"y_pitch",e.YPitch},{"c_pitch",e.CPitch},
    {"target_fps",e.Target},{"interpolation",e.Interpolation},{"hdr_raw",e.HdrRaw},{"hdr_fresh",e.HdrFresh},{"hdr_age_ms",e.HdrAge},{"hdr_poll_error",e.HdrPollError},
    {"native_color_format",e.ColorFormat},{"native_protected",e.ProtectedOn},{"native_interlaced",e.Interlaced},{"native_flip",e.FlipType},
    {"native_y_capacity",e.YCapacity},{"native_c_capacity",e.CCapacity},{"native_video_timestamp",e.VideoTimestamp},
    {"fingerprint",e.Fingerprint},{"marker_id",e.Marker},{"sleep_before_requested_ms",e.WaitRequested},{"sleep_before_actual_ms",e.WaitActual},
    {"sleep_before_kind",e.WaitKind==1?"pacing":e.WaitKind==2?"retry":"other_or_none"}});
   return new Dictionary<string,object>{{"schema",2},{"session",session},{"source",label},{"stopwatch_frequency",Stopwatch.Frequency},
    {"snapshot_writes",total},{"retained",Math.Min(total,Capacity)},{"overwritten",Math.Max(0,total-Capacity)},
    {"offset",offset},{"next_offset",offset+entries.Length},{"has_more",offset+entries.Length<Math.Min(total,Capacity)},{"entries",rows}};
  }
  static Dictionary<string,object> Distribution(List<double> values) {
   values.Sort();var r=new Dictionary<string,object>{{"count",values.Count}};
   foreach(double p in new[]{.5,.9,.95,.99,1}) {
    string key=p==1?"max":"p"+(p*100).ToString("0",System.Globalization.CultureInfo.InvariantCulture);
    if(values.Count==0){r[key]=null;continue;}double x=p*(values.Count-1);int lo=(int)x,hi=(int)Math.Ceiling(x);
    r[key]=values[lo]+(values[hi]-values[lo])*(x-lo);
   }return r;
  }
  public static Dictionary<string,object> Summary() {
   long session,total;string label;var a=Snapshot(0,Capacity,out session,out total,out label);
   var native=new List<double>();var ok=new List<double>();var wrapper=new List<double>();var periods=new List<double>();var idle=new List<double>();
   var outcomes=new Dictionary<string,object>();var hdr=new Dictionary<string,object>();int changes=0,markerSamples=0,markerChanges=0;Entry previous=new Entry();bool had=false,hadGood=false;uint lastHash=0;int lastMarker=-1;
   foreach(var e in a){
    string key=Outcome(e.Outcome);outcomes[key]=outcomes.ContainsKey(key)?Convert.ToInt64(outcomes[key])+1:1;
    string mode=e.HdrFresh?e.HdrRaw.ToString():"stale_or_unknown";hdr[mode]=hdr.ContainsKey(mode)?Convert.ToInt64(hdr[mode])+1:1;
    wrapper.Add(Ms(e.End-e.Start));
    if(e.Called){native.Add(Ms(e.NativeEnd-e.NativeStart));if(e.Outcome==5)ok.Add(Ms(e.NativeEnd-e.NativeStart));
     if(had){periods.Add(Ms(e.NativeStart-previous.NativeStart));idle.Add(Ms(e.NativeStart-previous.NativeEnd));}previous=e;had=true;}
    if(e.Outcome==5){if(hadGood&&lastHash!=e.Fingerprint)changes++;lastHash=e.Fingerprint;hadGood=true;}
    if(e.Marker>=0){markerSamples++;if(lastMarker>=0&&lastMarker!=e.Marker)markerChanges++;lastMarker=e.Marker;}
   }
   double seconds=a.Length>1?Ms(a[a.Length-1].End-a[0].Start)/1000:0;
   long inCall=Interlocked.Read(ref inFlight);
   return new Dictionary<string,object>{{"schema",2},{"session",session},{"source",label},{"retained",a.Length},{"total_recorded",total},
    {"overwritten",Math.Max(0,total-Capacity)},{"reset_crossing_discarded",Interlocked.Read(ref discarded)},
    {"native_in_flight",inCall!=0},{"native_in_flight_ms",inCall==0?0:Ms(Ticks()-inCall)},{"window_s",seconds},
    {"by_outcome",outcomes},{"by_hdr_raw",hdr},{"native_ms_all",Distribution(native)},{"native_ms_ok",Distribution(ok)},
    {"wrapper_ms",Distribution(wrapper)},{"native_start_period_ms",Distribution(periods)},{"idle_after_native_ms",Distribution(idle)},
    {"fingerprint_changes",changes},{"fingerprint_changes_hz_proxy",seconds>0?changes/seconds:0},
    {"marker_valid_samples",markerSamples},{"marker_changes",markerChanges},
    {"interpretation","Periods include application pacing/retry; fingerprint changes are a proxy, not proof of distinct decoded video frames. Export while capture is disabled."}};
  }
 }
}
