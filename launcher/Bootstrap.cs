using System;
using System.Runtime.InteropServices;
using System.Threading;
using TizenAmbilight;
namespace TizenHaloLauncher {
 public static class Bootstrap {
  [DllImport("libcapi-appfw-app-common.so.0",CallingConvention=CallingConvention.Cdecl)]
  static extern int app_get_id(out IntPtr id);
  [DllImport("libc.so.6",CallingConvention=CallingConvention.Cdecl)]
  static extern void free(IntPtr pointer);
  static void Main(string[] args) {
   try {
    IntPtr pointer;int rc=app_get_id(out pointer);
    if(rc!=0||pointer==IntPtr.Zero)throw new InvalidOperationException("app_get_id rc="+rc);
    string id;
    try{id=Marshal.PtrToStringAnsi(pointer);}finally{free(pointer);}
    StartupTrace.ApplicationId=id;
    var role=LaunchRouting.Resolve(id);
    StartupTrace.Send("bootstrap-"+role);
    if(role==LaunchRole.CaptureService)TizenAmbilight.App.RunService(args);
    else App.RunMenu(args);
   }catch(Exception e){StartupTrace.Send("bootstrap-fatal",e);Thread.Sleep(1600);}
  }
 }
}
