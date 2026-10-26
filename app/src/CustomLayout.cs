using System;
using System.Collections.Generic;
namespace TizenAmbilight
{
    // Array index is the physical LED address. Optional x/y are preview positions only.
    public static class CustomLayout
    {
        public static Dictionary<string,object> Parse(string json)
        {
            object root=ConfigFile.ParseDocument(json), rows=root;
            string name="My layout";
            var doc=root as Dictionary<string,object>;
            if(doc!=null) {
                object v;
                if(doc.TryGetValue("schema",out v) && Number(v,"schema")!=1)throw new ArgumentException("Unsupported layout schema");
                if(!doc.TryGetValue("leds",out rows))throw new ArgumentException("The file must contain a leds array or a HyperHDR LED list");
                if(doc.TryGetValue("name",out v)) {
                    if(!(v is string)||((string)v).Length>64)throw new ArgumentException("Layout name: 64 characters at most");name=(string)v;
                }
            }
            var list=rows as List<object>;
            if(list==null||list.Count<4||list.Count>490)throw new ArgumentException("A layout needs 4 to 490 LEDs");
            var leds=new List<object>();
            for(int i=0;i<list.Count;i++) {
                var led=list[i] as Dictionary<string,object>;
                if(led==null)throw new ArgumentException("LED "+i+": object required");
                var clean=new Dictionary<string,object>();
                foreach(string key in new[]{"hmin","hmax","vmin","vmax"}) {
                    object value;if(!led.TryGetValue(key,out value))throw new ArgumentException("LED "+i+": missing "+key);
                    double n=Number(value,key);if(n<0||n>1)throw new ArgumentException("LED "+i+": picture coordinates must be between 0 and 1");clean[key]=Math.Round(n,4);
                }
                if((double)clean["hmin"]>=(double)clean["hmax"]||(double)clean["vmin"]>=(double)clean["vmax"])
                    throw new ArgumentException("LED "+i+": the zone needs a positive width and height");
                foreach(string key in new[]{"x","y"}) {
                    object value;if(!led.TryGetValue(key,out value))continue;double n=Number(value,key);
                    if(n< -2||n>3)throw new ArgumentException("LED "+i+": physical position must be between -2 and 3");clean[key]=Math.Round(n,4);
                }
                leds.Add(clean);
            }
            return new Dictionary<string,object>{{"schema",1},{"name",name},{"leds",leds}};
        }
        static double Number(object value,string key)
        {
            if(!(value is double)&&!(value is int)&&!(value is long))throw new ArgumentException(key+": number required");
            double n=Convert.ToDouble(value);if(double.IsNaN(n)||double.IsInfinity(n))throw new ArgumentException(key+": finite number required");return n;
        }
        public static int Count(Dictionary<string,object> doc){return ((List<object>)doc["leds"]).Count;}
        public static double[,] Rectangles(Dictionary<string,object> doc)
        {
            var list=(List<object>)doc["leds"];var rectangles=new double[list.Count,4];
            for(int i=0;i<list.Count;i++){var r=(Dictionary<string,object>)list[i];int k=0;
                foreach(string key in new[]{"hmin","hmax","vmin","vmax"})rectangles[i,k++]=Convert.ToDouble(r[key]);}
            return rectangles;
        }
        public static Dictionary<string,object> Current()
        {
            var settings=LocalSettings.Snapshot();string mode=(string)settings["layoutMode"];
            if(mode=="custom")return Parse((string)settings["ledLayout"]);
            int total=Convert.ToInt32(settings["ledCount"]);
            var sides=new[]{Convert.ToInt32(settings["ledsTop"]),Convert.ToInt32(settings["ledsRight"]),Convert.ToInt32(settings["ledsBottom"]),Convert.ToInt32(settings["ledsLeft"])};
            int width=Convert.ToInt32(settings["captureWidth"]),height=Convert.ToInt32(settings["captureHeight"]);
            object resolution;if(LocalSettings.LiveStatus.TryGetValue("resolution",out resolution)) {
                var parts=Convert.ToString(resolution).Split('x');int w,h;
                if(parts.Length==2&&int.TryParse(parts[0],out w)&&int.TryParse(parts[1],out h)&&w>=64&&h>=64&&w<=960&&h<=540){width=w;height=h;}
            }
            var zones=new ZoneLayout(width,height,total,.08,false,(bool)settings["manualSides"]?sides:null);
            var rows=new List<object>();
            foreach(var z in zones.Zones)rows.Add(new Dictionary<string,object>{{"hmin",(double)z.X0/width},{"hmax",(double)z.X1/width},{"vmin",(double)z.Y0/height},{"vmax",(double)z.Y1/height}});
            var mapped=new List<object>();int offset=Convert.ToInt32(settings["mapOffset"]);bool reverse=(bool)settings["mapReverse"];
            for(int i=0;i<total;i++)mapped.Add(rows[((reverse?-i:i)+offset+total)%total]);
            return new Dictionary<string,object>{{"schema",1},{"name","TV frame"},{"leds",mapped}};
        }
    }
}
