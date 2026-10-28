// Part of TizenHalo, a derivative of TizenAmbilight. MIT, see LICENSE.
using System;
namespace TizenAmbilight
{
    public static class RectLayout
    {
        // Active only when the user uploaded a layout from the editor (layoutMode "custom").
        // Array index is the physical LED address; values are normalized picture rectangles.
        public static bool Enabled;
        public static double[,] ActiveRectangles;
        public static int ActiveCount { get { return ActiveRectangles==null?0:ActiveRectangles.GetLength(0); } }
        public static int StandardTop,StandardRight,StandardBottom,StandardLeft;
        public static ZoneColors TestColors(ZoneLayout layout)
        {
            var colors = new ZoneColors(layout.Total);
            for (int i=0; i<layout.Total; i++)
            {
                string edge = layout.Zones[i].Edge;
                colors.R[i] = (byte)((edge == "top" || edge == "left") ? 96 : 0);
                colors.G[i] = (byte)((edge == "right" || edge == "left") ? 96 : 0);
                colors.B[i] = (byte)(edge == "bottom" ? 96 : 0);
            }
            return colors;
        }
        // "Fake black bars" diagnostic: light the edges that stay visible and leave OFF the ones a
        // bar would cover, so the installer can watch the strip and see which physical LEDs fall on
        // each side. Lit edges keep the 4-edge test colours (top=red, right=green, bottom=blue,
        // left=yellow). mode 1 = letterbox (top+bottom covered), 2 = pillarbox (left+right covered).
        public static ZoneColors BarTestColors(ZoneLayout layout, int mode)
        {
            var colors = new ZoneColors(layout.Total);
            for (int i = 0; i < layout.Total; i++)
            {
                string edge = layout.Zones[i].Edge;
                bool covered = mode == 2 ? (edge == "left" || edge == "right")
                                         : (edge == "top" || edge == "bottom");
                if (covered) continue;   // stays 0,0,0: this is the LED a real bar would switch off
                colors.R[i] = (byte)((edge == "top" || edge == "left") ? 255 : 0);
                colors.G[i] = (byte)((edge == "right" || edge == "left") ? 255 : 0);
                colors.B[i] = (byte)(edge == "bottom" ? 255 : 0);
            }
            return colors;
        }
        public static Zone[] Create(int width, int height)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException("width/height");
            var rectangles=ActiveRectangles;
            if (rectangles == null) throw new InvalidOperationException("No custom layout loaded");
            var zones = new Zone[rectangles.GetLength(0)];
            for (int i = 0; i < zones.Length; i++)
            {
                double h0 = rectangles[i,0], h1 = rectangles[i,1];
                double v0 = rectangles[i,2], v1 = rectangles[i,3];
                int x0 = Math.Min(width-1, Math.Max(0,(int)Math.Floor(h0*width)));
                int y0 = Math.Min(height-1, Math.Max(0,(int)Math.Floor(v0*height)));
                int x1 = Math.Min(width, Math.Max(x0+1,(int)Math.Ceiling(h1*width)));
                int y1 = Math.Min(height, Math.Max(y0+1,(int)Math.Ceiling(v1*height)));
                string edge = v0 == 0 ? "top" : h0 == 0 ? "left" : h1 == 1 ? "right" : "bottom";
                zones[i] = new Zone { Edge=edge, Slot=i, X0=x0, X1=x1, Y0=y0, Y1=y1 };
            }
            return zones;
        }
    }
}
