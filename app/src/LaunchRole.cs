using System;
namespace TizenAmbilight {
 public enum LaunchRole { Menu, CaptureService }
 public static class LaunchRouting {
  public const string MenuId="io.github.tenda96.TizenHalo";
  public static LaunchRole Resolve(string id) {
   if(id==ServiceHeartbeat.ServiceId)return LaunchRole.CaptureService;
   if(id==MenuId)return LaunchRole.Menu;
   throw new ArgumentException("Unexpected application id: "+(id??"<null>"));
  }
 }
}
