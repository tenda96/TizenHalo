using System;
using System.Collections.Generic;
using System.IO;

namespace TizenAmbilight
{
    // Shared by the UI and backend, both installed in the SAME Tizen package.
    public static class ServiceHeartbeat
    {
        public const string Version = "tizenhalo-1.0.0";
        public const string ServiceId = "io.github.tenda96.TizenHalo.Service";
        public const string FileName = "tizenhalo-service-state.json";
        static string _path;
        static DateTime _lastWrite;
        public static Dictionary<string,object> Details=new Dictionary<string,object>();
        public static string SettingsUrl="",SettingsError="";
        public static void Initialize(string sharedData)
        {
            Directory.CreateDirectory(sharedData);
            _path = Path.Combine(sharedData, FileName);
        }
        public static long UnixMs(DateTime utc)
        {
            return (utc.ToUniversalTime().Ticks - 621355968000000000L) / 10000;
        }
        public static bool IsFresh(Dictionary<string, object> state, DateTime utc)
        {
            if (state == null || !state.ContainsKey("version") ||
                Convert.ToString(state["version"]) != Version || !state.ContainsKey("updated_ms")) return false;
            try
            {
                double age = UnixMs(utc) - Convert.ToDouble(state["updated_ms"]);
                string phase=Convert.ToString(state["phase"]);
                return age >= -2000 && age <= 5000 && (phase=="running"||phase=="paused"||phase=="disabled");
            }
            catch { return false; }
        }
        public static void Publish(string phase, long captured, long sent, string status, bool force=false)
        {
            if (_path == null || (!force && (DateTime.UtcNow-_lastWrite).TotalSeconds < 1)) return;
            try
            {
                var state = new Dictionary<string, object> {
                    {"version", Version}, {"phase", phase}, {"updated_ms", UnixMs(DateTime.UtcNow)},
                    {"capture_ok", captured}, {"wled_sent", sent}, {"status", status ?? ""},
                    {"settings_url",SettingsUrl},{"settings_error",SettingsError}
                };
                foreach(var pair in Details)state[pair.Key]=pair.Value;
                File.WriteAllText(_path, Json.ToJson(state));
                _lastWrite = DateTime.UtcNow;
            }
            catch { /* UI must show a timeout if this handshake is unavailable. */ }
        }
    }
}
