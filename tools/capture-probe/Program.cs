using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Tizen.Applications;
using Tizen.System;
using ElmSharp;

namespace TizenHaloCaptureProbe
{
    // Experimental Tizen 5.0 CAPTURE probe. It draws known full-screen colours ITSELF and captures the
    // screen composition for each, so the owner does not have to show a photo or leave the app running
    // in the background. It can invoke the legacy capture library, but only on an explicit owner
    // action from the web page. It never captures on launch, never drives LEDs, never changes settings.
    class App : CoreUIApplication
    {
        const int Port = 8795;
        const int RequestWidth = 640, RequestHeight = 360;
        const int RenderSettleMs = 350;

        TcpListener server; volatile bool stopped;
        Window window;
        Rectangle paint;          // full-screen colour we draw, then capture
        Label state;
        string environment;
        volatile string captureReport;
        volatile bool running;

        // Known colours we paint and capture. Strong primaries make the chroma order unambiguous.
        static readonly (string name, int r, int g, int b)[] Palette =
        {
            ("red", 255, 0, 0), ("green", 0, 255, 0), ("blue", 0, 0, 255),
        };

        static string Feature(string name)
        { try { string v; return Information.TryGetValue(name, out v) ? v : "unknown"; } catch { return "unknown"; } }

        static string Address()
        {
            try { using (var udp = new UdpClient()) { udp.Connect("192.0.2.1", 9); return ((IPEndPoint)udp.Client.LocalEndPoint).Address.ToString(); } }
            catch { return "<TV-IP>"; }
        }

        void Text(string message, int y, int size, string color = "#edf5fa")
        {
            var label = new Label(window) { Text = message, TextStyle = "DEFAULT='font_size=" + size + " color=" + color + " wrap=word'" };
            var screen = window.ScreenSize; label.Move(60, y); label.Resize(Math.Max(600, screen.Width - 120), 150); label.Show();
            if (y == 400) state = label;
        }

        protected override void OnCreate()
        {
            base.OnCreate(); Elementary.Initialize();
            window = new Window("TizenHalo capture probe");
            var screen = window.ScreenSize; window.Resize(screen.Width, screen.Height);
            var bg = new Background(window) { Color = new Color(9, 14, 19) }; window.AddResizeObject(bg); bg.Show();
            window.BackButtonPressed += (s, e) => Exit();

            string platform = Feature("http://tizen.org/feature/platform.version");
            environment = TizenAmbilight.Json.ToJson(new Dictionary<string, object>
            {
                {"probe", "tizenhalo-capture-probe"}, {"probe_version", "0.4.0"},
                {"platform_version", platform}, {"model_name", Feature("http://tizen.org/feature/model_name")},
                {"process_bits", IntPtr.Size * 8},
                {"firmware", "Enter firmware version when sharing this report"},
                {"request", new Dictionary<string, object> { {"width", RequestWidth}, {"height", RequestHeight} }},
                {"capture_invoked", false},
            });

            try
            {
                server = new TcpListener(IPAddress.Any, Port); server.Start();
                new Thread(Serve) { IsBackground = true }.Start();
                Text("TizenHalo · Capture Probe (experimental)", 55, 40, "#52e2bb");
                Text("Tizen " + platform + " · " + (IntPtr.Size * 8) + "-bit. No capture has run yet.", 180, 30);
                Text("Open on your phone or computer, then start the test there:", 290, 28);
                Text("http://" + Address() + ":" + Port + "/", 400, 38, "#52e2bb");
                Text("The test paints red/green/blue on this screen and captures each. The screen will flash colours — that is normal. No LEDs are touched.", 540, 25);
                Text("Back: close the probe", 760, 25);
            }
            catch (Exception e) { Text("Probe server failed: " + e.Message, 200, 28, "#ffb389"); }

            // Full-screen colour layer, created last so it sits on top. Hidden until the test runs.
            paint = new Rectangle(window) { Color = new Color(9, 14, 19) };
            paint.Move(0, 0); paint.Resize(screen.Width, screen.Height); paint.Hide();
            window.Show();
        }

        void Serve()
        {
            while (!stopped)
            {
                try { using (var client = server.AcceptTcpClient()) { client.ReceiveTimeout = 4000; client.SendTimeout = 15000; Handle(client); } }
                catch { if (stopped) return; }
            }
        }

        static string Line(Stream stream, ref int budget)
        {
            var line = new StringBuilder();
            while (budget++ < 8192)
            {
                int ch = stream.ReadByte(); if (ch < 0) throw new IOException("Incomplete request");
                if (ch == 10) return line.ToString().TrimEnd('\r');
                if (ch != 13 && (ch < 32 || ch > 126)) throw new IOException("Invalid request header");
                line.Append((char)ch);
            }
            throw new IOException("Request header too large");
        }

        void Handle(TcpClient client)
        {
            var stream = client.GetStream(); int budget = 0;
            string first = Line(stream, ref budget); if (first.Length > 2048) return;
            string[] request = first.Split(' ');
            if (request.Length != 3 || request[0] != "GET") { Reply(stream, 405, "text/plain", Bytes("GET only")); return; }
            string host = null, origin = null, line;
            while ((line = Line(stream, ref budget)).Length > 0)
            {
                int colon = line.IndexOf(':'); if (colon < 0) continue;
                string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) host = value;
                if (key.Equals("Origin", StringComparison.OrdinalIgnoreCase)) origin = value;
            }
            string local = ((IPEndPoint)client.Client.LocalEndPoint).Address.ToString() + ":" + Port;
            if (host != local && host != "127.0.0.1:" + Port && host != "localhost:" + Port) { Reply(stream, 403, "text/plain", Bytes("Local address required")); return; }
            if (origin != null && origin != "http://" + host) { Reply(stream, 403, "text/plain", Bytes("Cross-origin request refused")); return; }

            string path = request[1];
            if (path == "/api/environment") { Reply(stream, 200, "application/json", Bytes(environment), "tizenhalo-capture-environment.json"); return; }
            if (path == "/api/capture-test") { Reply(stream, 200, "application/json", Bytes(RunCaptureTest()), "tizenhalo-capture-report.json"); return; }
            if (path == "/api/report")
            {
                if (captureReport == null) { Reply(stream, 404, "text/plain", Bytes("Run the capture test first")); return; }
                Reply(stream, 200, "application/json", Bytes(captureReport), "tizenhalo-capture-report.json"); return;
            }
            if (path != "/") { Reply(stream, 404, "text/plain", new byte[0]); return; }

            var html = new StringBuilder();
            html.Append("<!doctype html><meta name=viewport content='width=device-width'><title>TizenHalo capture probe</title>");
            html.Append("<style>body{font:18px system-ui;max-width:820px;margin:40px auto;padding:20px;background:#09121b;color:#edf5fa}a{color:#52e2bb}button{font:600 18px system-ui;background:#52e2bb;color:#06251d;border:0;border-radius:10px;padding:14px 22px;cursor:pointer}</style>");
            html.Append("<h1>TizenHalo capture probe</h1>");
            html.Append("<p>This test paints full-screen <b>red, green and blue</b> on the TV itself and tries to capture each one, as a PNG screenshot and through the video path. You don't need to show a photo or leave the app in the background. The TV screen will flash those colours for a moment — that's expected. It does <b>not</b> drive the LEDs or change any TV setting.</p>");
            html.Append("<p><button onclick=\"go()\">Run capture test</button> <span id=s></span></p>");
            html.Append("<p><a href='/api/environment'>Download environment info</a></p>");
            html.Append("<pre id=o style='white-space:pre-wrap;background:#0f1b27;padding:14px;border-radius:10px'></pre>");
            html.Append("<p>Send the downloaded report back to the maintainer, with your exact model and firmware. Close with Back on the TV.</p>");
            html.Append("<script>async function go(){var s=document.getElementById('s');s.textContent='running... the TV will flash colours';try{var r=await fetch('/api/capture-test');var t=await r.text();document.getElementById('o').textContent=t;var b=new Blob([t],{type:'application/json'});var a=document.createElement('a');a.href=URL.createObjectURL(b);a.download='tizenhalo-capture-report.json';a.click();s.textContent='done - report downloaded';}catch(e){s.textContent='error: '+e;}}</script>");
            Reply(stream, 200, "text/html; charset=utf-8", Bytes(html.ToString()));
        }

        // Paint a full-screen colour on the UI thread and wait until it is on screen.
        void PaintColour(int r, int g, int b)
        {
            using (var done = new ManualResetEventSlim(false))
            {
                EcoreMainloop.Post(() =>
                {
                    try { var sz = window.ScreenSize; paint.Move(0, 0); paint.Resize(sz.Width, sz.Height); paint.Color = new Color(r, g, b); paint.Show(); }
                    catch { }
                    finally { done.Set(); }
                });
                done.Wait(2000);
            }
            Thread.Sleep(RenderSettleMs);   // let the compositor present the colour before capturing
        }

        void HidePaint()
        {
            using (var done = new ManualResetEventSlim(false))
            { EcoreMainloop.Post(() => { try { paint.Hide(); } catch { } finally { done.Set(); } }); done.Wait(2000); }
        }

        string RunCaptureTest()
        {
            if (running) return captureReport ?? "{\"error\":\"a capture test is already running\"}";
            running = true;
            try
            {
                var record = new Dictionary<string, object>
                {
                    {"probe", "tizenhalo-capture-probe"}, {"probe_version", "0.4.0"},
                    {"platform_version", Feature("http://tizen.org/feature/platform.version")},
                    {"model_name", Feature("http://tizen.org/feature/model_name")},
                    {"firmware", "Enter firmware version when sharing this report"},
                    {"capture_invoked", true},
                };
                string dataDir;
                try { dataDir = Application.Current.DirectoryInfo.Data; } catch { dataDir = "/tmp"; }
                try
                {
                    using (var secvideo = new LegacyCapture())
                    using (var display = new DisplayCapture())
                    {
                        record["secvideo_environment"] = secvideo.Environment();
                        record["screenshot_library"] = display.LibraryLoaded;
                        record["screenshot_error"] = display.OpenError;

                        // Paint each colour, then try to capture it two ways: the PNG screenshot API
                        // (whole-screen composition, RGB) and the secvideo screen path (video only).
                        var painted = new List<object>();
                        foreach (var col in Palette)
                        {
                            PaintColour(col.r, col.g, col.b);
                            var entry = new Dictionary<string, object> { {"colour", col.name}, {"rgb", new[] { col.r, col.g, col.b }} };
                            entry["screenshot_png"] = display.Ready
                                ? display.CaptureOnce(dataDir, "probe-" + col.name + ".png", RequestWidth, RequestHeight)
                                : new Dictionary<string, object> { {"error", "screenshot library not ready: " + display.OpenError} };
                            entry["secvideo_screen"] = secvideo.Ready
                                ? secvideo.CaptureOnce(LegacyCapture.ScreenSymbol, RequestWidth, RequestHeight)
                                : new Dictionary<string, object> { {"error", "secvideo library not ready: " + secvideo.OpenError} };
                            painted.Add(entry);
                        }
                        HidePaint();
                        record["painted_captures"] = painted;
                        // For comparison: one video-plane capture (dark with no video) and one screenshot
                        // of whatever is on screen now that the colour overlay is hidden.
                        if (secvideo.Ready) record["video_plane_capture"] = secvideo.CaptureOnce(LegacyCapture.VideoMainSymbol, RequestWidth, RequestHeight);
                        if (display.Ready) record["screenshot_as_is"] = display.CaptureOnce(dataDir, "probe-asis.png", RequestWidth, RequestHeight);
                    }
                }
                catch (Exception e) { record["fatal"] = e.GetType().Name + ": " + e.Message; try { HidePaint(); } catch { } }
                captureReport = TizenAmbilight.Json.ToJson(record);
                try { if (state != null) EcoreMainloop.Post(() => state.Text = "Capture test finished. Download the report from your browser."); } catch { }
                return captureReport;
            }
            finally { running = false; }
        }

        static byte[] Bytes(string s) { return Encoding.UTF8.GetBytes(s); }
        static void Reply(Stream stream, int status, string type, byte[] body, string name = null)
        {
            string h = "HTTP/1.1 " + status + " Response\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length +
                       "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n";
            if (name != null) h += "Content-Disposition: attachment; filename=\"" + name + "\"\r\n";
            byte[] head = Encoding.ASCII.GetBytes(h + "\r\n"); stream.Write(head, 0, head.Length); stream.Write(body, 0, body.Length);
        }

        protected override void OnTerminate() { stopped = true; try { server.Stop(); } catch { } base.OnTerminate(); }
        static void Main(string[] args) { new App().Run(args); }
    }
}
