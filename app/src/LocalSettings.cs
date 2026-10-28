using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace TizenAmbilight
{
    // All persistence happens on the TV. The render thread alone applies snapshots.
    public static class LocalSettings
    {
        static readonly object Gate=new object();
        static Dictionary<string,object> _values;
        static string _path;
        static long _revision;
        public static long Revision { get { lock(Gate) return _revision; } }
        public static bool Ready { get { lock(Gate) return _values!=null; } }
        public static string Error="";
        public static Dictionary<string,object> LiveStatus=new Dictionary<string,object>();
        static bool IsHexColour(string s)
        { foreach(char c in s) if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F'))) return false; return true; }
        public static Dictionary<string,object> Defaults()
        {
            return new Dictionary<string,object> {
                {"wledIp",""},{"wledPort",21324},{"ledCount",100},{"customLayout",false},
                {"mapOffset",0},{"mapReverse",false},{"manualSides",false},
                {"layoutMode","standard"},{"ledLayout",""},{"captureMethod",CaptureProfile.Current.DefaultMethod},
                {"ledsTop",30},{"ledsRight",20},{"ledsBottom",30},{"ledsLeft",20},
                {"outputEnabled",true},{"targetFps",CaptureProfile.Current.DefaultFps},{"captureWidth",960},{"captureHeight",540},
                {"interpolation",true},{"outputFps",30},{"precisePacing",true},{"hdrMode","auto"},{"alpha",.5},{"alphaFast",.85},
                {"saturation",1.0},{"gain",1.0},{"gamma",1.0},{"black",10},
                {"calRed",1.0},{"calGreen",1.0},{"calBlue",1.0},{"calSat",1.0},
                {"calRedTo","#ff0000"},{"calGreenTo","#00ff00"},{"calBlueTo","#0000ff"},
                {"adaptiveGamma",false},{"bars",true},{"barRows",true},{"barThresh",18},{"barTest",0},
                {"punch",0},{"enableLegacyControl",false}
            };
        }
        public static void Initialize(string directory)
        {
            lock(Gate)
            {
                Directory.CreateDirectory(directory);
                _path=Path.Combine(directory,"tizenhalo-settings.json");
                _values=SeedFromConfigFile(Defaults());
                if (File.Exists(_path))
                {
                    try {
                        var saved=ConfigFile.Parse(File.ReadAllText(_path));
                        // A package-profile change replaces only an incompatible method;
                        // retain the owner's saved WLED, colors and custom layout.
                        CaptureProfile.Current.NormalizeSaved(saved);
                        _values=MergeAndValidate(_values,saved);
                    }
                    catch(Exception e) { Error="Saved settings are invalid: "+e.Message; }
                }
                _revision++;
            }
        }
        /// <summary>Values from the packaged config.json (for example filled in by an installer form)
        /// become the starting point instead of the built-in defaults. Saved settings still win.
        /// Each key is validated on its own, so one bad value cannot discard the others.</summary>
        static Dictionary<string,object> SeedFromConfigFile(Dictionary<string,object> defaults)
        {
            var next=defaults;
            foreach(var key in new List<string>(defaults.Keys))
            {
                object value=null;double d;string text;
                if(defaults[key] is bool) { if(ConfigFile.TryGetDouble(key,out d)) value=d!=0; }
                else if(defaults[key] is string) { if(ConfigFile.TryGetString(key,out text)) value=text.Trim(); }
                else if(ConfigFile.TryGetDouble(key,out d)) value=d;
                if(value==null) continue;
                try { next=MergeAndValidate(next,new Dictionary<string,object>{{key,value}}); }
                catch(Exception) { }
            }
            return next;
        }
        public static Dictionary<string,object> Snapshot()
        { lock(Gate) return new Dictionary<string,object>(_values??Defaults()); }
        public static bool TryDouble(string key,out double value)
        {
            lock(Gate)
            {
                object v;
                if (_values!=null && _values.TryGetValue(key,out v))
                { try { value=Convert.ToDouble(v,CultureInfo.InvariantCulture); return true; } catch { } }
                value=0; return false;
            }
        }
        public static bool TryString(string key,out string value)
        {
            lock(Gate)
            {
                object v;
                if (_values!=null && _values.TryGetValue(key,out v) && v is string)
                { value=(string)v; return true; }
                value=null; return false;
            }
        }
        public static void Save(Dictionary<string,object> patch)
        {
            lock(Gate)
            {
                var next=MergeAndValidate(_values??Defaults(),patch);
                if (_path==null) throw new InvalidOperationException("TV storage is not initialized");
                string tmp=_path+".new";
                File.WriteAllText(tmp,Json.ToJson(next));
                if (File.Exists(_path)) File.Replace(tmp,_path,null);
                else File.Move(tmp,_path);
                _values=next; _revision++; Error="";
            }
        }
        static readonly Dictionary<string,double[]> Ranges=new Dictionary<string,double[]> {
            {"wledPort",new[]{1d,65535}},{"ledCount",new[]{4d,490}},{"mapOffset",new[]{0d,489}},
            {"outputFps",new[]{30d,60}},{"targetFps",new[]{5d,60}},{"captureWidth",new[]{64d,1280}},{"captureHeight",new[]{64d,720}},
            {"ledsTop",new[]{1d,490}},{"ledsRight",new[]{1d,490}},{"ledsBottom",new[]{1d,490}},{"ledsLeft",new[]{1d,490}},
            {"alpha",new[]{.05,1}},{"alphaFast",new[]{.05,1}},{"saturation",new[]{0d,3}},
            {"calRed",new[]{0d,2}},{"calGreen",new[]{0d,2}},{"calBlue",new[]{0d,2}},{"calSat",new[]{0d,3}},
            {"gain",new[]{.1,2}},{"gamma",new[]{1d,3}},{"black",new[]{0d,60}},
            {"barThresh",new[]{17d,40}},{"barTest",new[]{0d,2}},{"punch",new[]{0d,1}}
        };
        static readonly HashSet<string> Integers=new HashSet<string>{"wledPort","ledCount","mapOffset","outputFps","targetFps","captureWidth","captureHeight","ledsTop","ledsRight","ledsBottom","ledsLeft","barThresh","barTest"};
        static readonly HashSet<string> Booleans=new HashSet<string>{"interpolation","customLayout","manualSides","mapReverse","outputEnabled","precisePacing","adaptiveGamma","bars","barRows","enableLegacyControl"};
        public static Dictionary<string,object> MergeAndValidate(Dictionary<string,object> current,Dictionary<string,object> patch)
        {
            if(patch==null) throw new ArgumentException("A JSON object is required");
            var next=new Dictionary<string,object>(current);
            foreach(var kv in patch)
            {
                if(!next.ContainsKey(kv.Key)) throw new ArgumentException("Unknown setting: "+kv.Key);
                if(Booleans.Contains(kv.Key))
                { if(!(kv.Value is bool)) throw new ArgumentException(kv.Key+": must be true or false"); next[kv.Key]=kv.Value; }
                else if(Ranges.ContainsKey(kv.Key))
                {
                    if(kv.Value is string || kv.Value is bool || kv.Value==null) throw new ArgumentException(kv.Key+": number required");
                    double n=Convert.ToDouble(kv.Value,CultureInfo.InvariantCulture); var range=Ranges[kv.Key];
                    if(double.IsNaN(n)||double.IsInfinity(n)||n<range[0]||n>range[1]||(Integers.Contains(kv.Key)&&n!=Math.Truncate(n)))
                        throw new ArgumentException(kv.Key+": value out of range");
                    next[kv.Key]=n;
                }
                else if(kv.Key=="wledIp")
                {
                    // Empty means "no controller configured yet": keep it, or every other saved
                    // setting would be rejected on the next start.
                    if(kv.Value is string&&((string)kv.Value).Trim().Length==0) { next[kv.Key]=""; continue; }
                    IPAddress ip;
                    if(!(kv.Value is string)||!IPAddress.TryParse(((string)kv.Value).Trim(),out ip)||ip.AddressFamily!=AddressFamily.InterNetwork||ip.Equals(IPAddress.Any)||ip.GetAddressBytes()[0]>=224)
                        throw new ArgumentException("Enter the IPv4 address of the WLED controller");
                    next[kv.Key]=ip.ToString();
                }
                else if(kv.Key=="calRedTo"||kv.Key=="calGreenTo"||kv.Key=="calBlueTo")
                {
                    string hex=kv.Value as string; if(hex!=null)hex=hex.Trim().TrimStart('#');
                    if(hex==null||hex.Length!=6||!IsHexColour(hex)) throw new ArgumentException(kv.Key+": use a #RRGGBB colour");
                    next[kv.Key]="#"+hex.ToLowerInvariant();
                }
                else if(kv.Key=="hdrMode")
                {
                    string mode=kv.Value as string;
                    if(mode!="auto"&&mode!="sdr"&&mode!="hdr"&&mode!="hlg") throw new ArgumentException("Invalid HDR mode");
                    next[kv.Key]=mode;
                }
                else if(kv.Key=="captureMethod") {
                    if(!CaptureProfile.Current.AllowsMethod(kv.Value as string))throw new ArgumentException("Capture method unavailable in this package profile");
                    next[kv.Key]=kv.Value;
                }
                else if(kv.Key=="layoutMode") {
                    string mode=kv.Value as string;if(mode=="preset")mode="standard";if(mode!="standard"&&mode!="custom")throw new ArgumentException("Invalid layout mode");next[kv.Key]=mode;
                }
                else if(kv.Key=="ledLayout") {
                    string value=kv.Value as string;if(value==null)throw new ArgumentException("Layout JSON required");
                    next[kv.Key]=value.Length==0?"":Json.ToJson(CustomLayout.Parse(value));
                }
            }
            // Older clients only send customLayout: map it onto layoutMode. A custom layout needs an
            // uploaded ledLayout, otherwise the standard frame stays in use.
            if(patch.ContainsKey("customLayout")&&!patch.ContainsKey("layoutMode"))
                next["layoutMode"]=(bool)next["customLayout"]&&((string)next["ledLayout"]).Length>0?"custom":"standard";
            next["customLayout"]=(string)next["layoutMode"]=="custom";
            int outputRate=Convert.ToInt32(next["outputFps"]);
            if(outputRate!=30&&outputRate!=60)throw new ArgumentException("Interpolated output: choose 30 or 60 Hz");
            int count=Convert.ToInt32(next["ledCount"]);
            if((string)next["layoutMode"]=="custom" && CustomLayout.Count(CustomLayout.Parse((string)next["ledLayout"]))!=count)
                throw new ArgumentException("The LED count must match the uploaded layout");
            if(!(bool)next["customLayout"] && (bool)next["manualSides"] &&
               Convert.ToInt32(next["ledsTop"])+Convert.ToInt32(next["ledsRight"])+Convert.ToInt32(next["ledsBottom"])+Convert.ToInt32(next["ledsLeft"])!=count)
                throw new ArgumentException("The four side counts must add up to the LED count");
            if(Convert.ToInt32(next["mapOffset"])>=count) throw new ArgumentException("The first LED position must be lower than the LED count");
            if(Convert.ToDouble(next["captureWidth"])*Convert.ToDouble(next["captureHeight"])>VideoPlaneCapture.YBytes) throw new ArgumentException("Capture size too large");
            if(CaptureProfile.Current.VideoAbi==VideoPlaneAbi.S84F.Id &&
               !VideoPlaneAbi.S84F.FitsRequest(Convert.ToInt32(next["captureWidth"]),Convert.ToInt32(next["captureHeight"])))
                throw new ArgumentException("S84F profile supports requests up to 960x540");
            if((Convert.ToInt32(next["captureWidth"])&1)!=0||(Convert.ToInt32(next["captureHeight"])&1)!=0)throw new ArgumentException("YUV420 capture needs an even width and height");
            if(Convert.ToDouble(next["alphaFast"])<Convert.ToDouble(next["alpha"])) next["alphaFast"]=next["alpha"];
            return next;
        }
    }
}
