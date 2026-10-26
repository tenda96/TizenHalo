using System;
using System.Collections.Generic;
using TizenAmbilight;

static class RgbMeasureApiTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Add(Dictionary<string,IntPtr> exports,string prefix,int start)
    {
        exports[prefix+"_get_rgb_measure_condition"]=new IntPtr(start);
        exports[prefix+"_set_rgb_measure_position"]=new IntPtr(start+1);
        exports[prefix+"_get_rgb_measure_pixel"]=new IntPtr(start+2);
    }
    static RgbMeasureApi Find(Dictionary<string,IntPtr> exports)
    {return RgbMeasureApi.Resolve(name=>{IntPtr p;return exports.TryGetValue(name,out p)?p:IntPtr.Zero;});}
    public static void Run()
    {
        foreach(string family in new[]{"ppi_ve","ve","cs_ve"}) {
            var exports=new Dictionary<string,IntPtr>();Add(exports,family,10);
            var result=Find(exports);
            Check(result.Complete && result.Family==family,"Complete family not selected: "+family);
            Check(result.Condition==new IntPtr(10) && result.Position==new IntPtr(11) && result.Pixel==new IntPtr(12),"Wrong family pointers");
            foreach(string suffix in new[]{"_get_rgb_measure_condition","_set_rgb_measure_position","_get_rgb_measure_pixel"}) {
                var partial=new Dictionary<string,IntPtr>(exports);partial.Remove(family+suffix);
                Check(!Find(partial).Complete,"Partial API accepted: "+family+suffix);
            }
        }
        var mixed=new Dictionary<string,IntPtr>{{"ppi_ve_get_rgb_measure_condition",new IntPtr(1)},
            {"ve_set_rgb_measure_position",new IntPtr(2)},{"cs_ve_get_rgb_measure_pixel",new IntPtr(3)}};
        Check(!Find(mixed).Complete,"Mixed API families accepted");
        Add(mixed,"ve",20);Check(Find(mixed).Family=="ve","Partial ppi family hides a complete fallback");
        Add(mixed,"cs_ve",30);Check(Find(mixed).Family=="ve","Stable fallback priority changed");
        Add(mixed,"ppi_ve",40);Check(Find(mixed).Family=="ppi_ve","Existing ppi path must retain priority");
        var missing=Find(new Dictionary<string,IntPtr>());
        Check(!missing.Complete && missing.Family=="unavailable" && missing.Diagnostics.Contains("cs_ve:missing"),"Missing API diagnosis lost");
        Console.WriteLine("PASS: complete RGB families, missing members, no cross-family mixing and existing ppi priority");
    }
}
