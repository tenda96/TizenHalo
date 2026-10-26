using System;
using System.Collections.Generic;

namespace TizenAmbilight
{
    // Export discovery only: no native function is called here. The sampler still owns
    // condition validation, buffers and all measurement calls on its existing worker.
    public sealed class RgbMeasureApi
    {
        public readonly string Family, Diagnostics;
        public readonly IntPtr Condition, Position, Pixel;
        public bool Complete { get { return Condition != IntPtr.Zero && Position != IntPtr.Zero && Pixel != IntPtr.Zero; } }
        RgbMeasureApi(string family,IntPtr condition,IntPtr position,IntPtr pixel,string diagnostics)
        { Family=family;Condition=condition;Position=position;Pixel=pixel;Diagnostics=diagnostics; }

        public static RgbMeasureApi Resolve(Func<string,IntPtr> symbol)
        {
            if(symbol==null)throw new ArgumentNullException("symbol");
            string selected="unavailable";
            IntPtr condition=IntPtr.Zero,position=IntPtr.Zero,pixel=IntPtr.Zero;
            var report=new List<string>();
            // Preserve TizenHalo's existing ppi_ve path first. Platform/year alone
            // never selects a family, and partial families must not be mixed.
            foreach(string family in new[]{"ppi_ve","ve","cs_ve"}) {
                IntPtr c=symbol(family+"_get_rgb_measure_condition");
                IntPtr p=symbol(family+"_set_rgb_measure_position");
                IntPtr x=symbol(family+"_get_rgb_measure_pixel");
                bool complete=c!=IntPtr.Zero && p!=IntPtr.Zero && x!=IntPtr.Zero;
                var missing=new List<string>();
                if(c==IntPtr.Zero)missing.Add("condition");
                if(p==IntPtr.Zero)missing.Add("position");
                if(x==IntPtr.Zero)missing.Add("pixel");
                report.Add(family+":"+(complete?"complete":"missing "+string.Join("/",missing)));
                if(complete && selected=="unavailable") {selected=family;condition=c;position=p;pixel=x;}
            }
            return new RgbMeasureApi(selected,condition,position,pixel,string.Join("; ",report));
        }
    }
}
