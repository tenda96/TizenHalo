# Technical notes

## Overview

One Tizen package contains two applications built from the same assembly:

- **Menu** (`io.github.tenda96.TizenHalo`): the icon in the TV's app list. An ElmSharp screen with the
  service status, a QR code to the settings page and a lights on/off button.
- **Service** (`io.github.tenda96.TizenHalo.Service`): headless, starts at boot. It captures the picture,
  computes the LED colors, sends them to WLED and serves the settings page.

`Bootstrap` reads the application id at start-up and runs the matching role; an unknown id is rejected.

## Pipeline

```
capture (YUV planes in memory) → black-bar detection → zone averages in YUV → color matrix / HDR
→ adaptive temporal smoothing → gamma, gain, black level → WLED DRGB over UDP
                                                          ↘ optional interpolation thread (30/60 Hz)
```

- **Capture.** The default backend reads the video picture straight into app-owned buffers through
  `libvideo-capture.so` (see [Capture methods](#capture-methods)). A slower fallback asks
  `libdisplay-capture-api.so` for a PNG screenshot and decodes it in the app (`captureBackend: file`).
- **Zones.** Each LED gets a rectangle of the picture: a frame around the edges by default, or the
  rectangles of an editor layout. Averaging happens per zone in YUV, so the color matrix runs once per
  zone rather than once per pixel.
- **HDR.** The video format comes from `avoc_get_hdr_type` in `libavoc.so` when `hdrMode` is `auto`.
  PQ (HDR10) and HLG are decoded and BT.2020 colors are mapped into what LEDs can show.
- **Smoothing.** An exponential moving average with a slow constant for small changes and a fast one for
  scene cuts. The constants are scaled by the real time between frames, so the response is the same at
  10, 15 or 25 frames per second.
- **Output.** WLED's DRGB realtime protocol over UDP (port 21324 by default), up to 490 LEDs, with a
  short timeout byte so a killed app cannot leave the strip latched on a color. While no frame arrives,
  black is re-sent before the timeout expires so WLED does not fall back to its own effect.
- **Interpolation.** A separate thread sends packets at 30 or 60 Hz, blending linearly from the previous
  frame's colors to the newest ones. It stops after 1.1 s without a new frame.

### Timing

- Captures are paced on a monotonic clock with a fractional period (15 fps = 66.67 ms). A static picture
  is captured less often (up to three times the period, at most 200 ms between captures).
- After 1.1 s without a usable frame the strip is blanked and kept black.
- The TV's local REST API (`http://127.0.0.1:8001/api/v2/`) tells the service when the panel is in
  standby; captures stop and the strip stays black until the panel is on again.
- Failed captures back off; a short, bounded retry is used only for the transient empty frames described
  below.

## Capture methods

All methods call exported functions of the TV's own system libraries. The structure layouts they use are
documented next to each call in `app/src/VideoPlaneCapture.cs` and `app/src/CaptureApi.cs`, and were
verified only on firmware T-NKMDEUC-2743.0. Nothing on the TV is modified.

| Method | Library | Function |
|---|---|---|
| `main` | `libvideo-capture.so.0.1.0` | `secvideo_api_capture_video_main` |
| `main_no_flip` | `libvideo-capture.so.0.1.0` | `secvideo_api_capture_video_main_no_flip` |
| `screen_video` | `libvideo-capture.so.0.1.0` | `secvideo_api_capture_screen_video_only` |
| `screen_full` | `libvideo-capture.so.0.1.0` | `secvideo_api_capture_screen` |
| `legacy_main` | `libsec-video-capture.so.0.1.0` | `secvideo_api_capture_video_main` |
| `legacy_screen_video` | `libsec-video-capture.so.0.1.0` | `secvideo_api_capture_screen_video_only` |
| `legacy_no_flip` | `libsec-video-capture.so.0.1.0` | `secvideo_api_capture_video_main_no_flip` |

The method can be switched at runtime from the settings page; it takes effect between two captures.

### Capture rate

Measured on a QE55Q74T (firmware T-NKMDEUC-2743.0) while playing video in the YouTube app, about
20–40 s per run. "Distinct frames" counts captures whose content differs from the previous one.

**All methods, requesting 20 fps:**

| Method | Distinct frames/s | Native call (median) | Notes |
|---|---|---|---|
| `screen_video` | 14.9 | 44 ms | No duplicates |
| `screen_full` | 14.2 | 25 ms | Includes menus and overlays |
| `main_no_flip` | 13.6 | 76 ms | |
| `main` | 13.0 | 76 ms | ~4% duplicates |
| `legacy_screen_video` | 11.1 | 92 ms | |
| `legacy_no_flip` | 10.3 | 84 ms | |
| `legacy_main` | 9.3 | 84 ms | ~3% duplicates |

**`screen_video`, requesting 15 vs 30 fps:**

| Requested | Calls/s | Distinct frames/s | Empty frames | Native call (median) | Longest gap |
|---|---|---|---|---|---|
| 15 fps | 14.3 | 14.3 | 0 | 34 ms | 0.37 s |
| 30 fps | 14.8 | 14.3 | 22 of 566 | 58 ms | 1.0 s |

Requesting more than the firmware can deliver does not add frames. Calls take longer and some return
success with an empty `0×0` picture. The limit sits below the public capture API and is the same for
every method, so the service paces at 15 fps and relies on interpolation for smooth output.

### Protected content

Many streaming apps withhold their picture from capture (the call reports protected content). On
TizenAmbilight's Tizen 9 target the service then falls back to the TV's built-in color sampler; the
firmware tested here does not export that sampler, so the lights go dark instead.

## Local HTTP API

The service listens on port 8793.

| Method | Path | Purpose |
|---|---|---|
| GET | `/` | Settings page |
| GET | `/layout` | LED layout editor |
| GET | `/api/status` | Live status: rates, resolution, method, errors |
| GET | `/api/settings` | Saved settings |
| POST | `/api/settings` | Change settings: a JSON object with only the keys to change |
| GET | `/api/layout` | Current layout as editor JSON |
| POST | `/api/layout` | Upload a layout and switch to it |
| GET | `/api/capture-methods` | Available methods with labels and notes |
| GET | `/api/trace?offset=&limit=` | Per-capture records (see [Diagnostics](#diagnostics)) |
| GET | `/api/trace/summary` | Timing distributions and outcome counts |
| POST | `/api/trace/reset` | Clear the trace; only while the LED output is disabled |

There is no authentication: anyone on the local network can read and change the settings. Requests are
only accepted with the TV's own address (or localhost) as `Host`, and a cross-site `Origin` is refused, so
other web pages open in a browser cannot change them. Do not expose port 8793 to the internet.

Settings are validated as a whole and saved atomically; an invalid change is rejected with a message and
leaves the previous settings in place.

## Files on the TV

| File | Content |
|---|---|
| `<app data>/tizenhalo-settings.json` | Saved settings |
| `<app data>/tizenhalo-service-state.json` | Heartbeat read by the menu (status, rates, settings URL) |
| `/home/owner/tizenhalo/last.log` | Log tail written after a crash |
| `/home/owner/tizenhalo/platform.txt` | Platform version of the last run; a change is logged, since a firmware update can alter the capture interface |

## Diagnostics

- `/api/trace` keeps the last 20,000 capture calls: timings (native call and wrapper), result code,
  returned size and pitches, format flags, a content fingerprint and the pacing sleep before the call.
  Pages of up to 1,000 records.
- `/api/trace/summary` gives percentiles and outcome counts for the retained records.
- Optional collector: set `probeEndpoint` in `config.json` to an HTTP endpoint on your own network and
  the service posts its statistics and log tail every `statsIntervalSec` seconds. Empty by default; no
  data leaves your network unless you set it.

## Build from source

Requirements: .NET 8 SDK, Python 3, and optionally Node.js for the editor test. The Tizen SDK for .NET
(`Tizen.NET.Sdk`) is restored from NuGet on the first build.

```bash
./build.sh
```

The script builds the package, runs the host tests and checks the result. Output:
`dist/TizenHalo-<version>.tpk`, signed with the Tizen SDK's default certificate. To bake your own values
into the package, copy `app/config.example.json` to `app/config.json` first (it is ignored by git).

To install with your own certificate instead of Apps2Samsung: enable Developer Mode on the TV with your
computer's IP, create a Samsung certificate profile for the TV in Tizen Studio's Certificate Manager,
then re-sign and install with the Tizen CLI:

```bash
tizen package -t tpk -s <profile> -- dist/TizenHalo-1.0.0.tpk
sdb connect <tv-ip>
tizen install -n dist/TizenHalo-1.0.0.tpk -t <device-name>
```

The tests run on the host (`tests/`): settings validation and persistence, layouts and LED mapping,
retry rules, interpolation over loopback UDP, trace bookkeeping and the editor's offline mode. Native
capture calls are guarded and only run on the TV.

## Project layout

```
app/          Tizen project: service code (src/), web pages and icon (res/), manifest, config template
launcher/     TV menu (ElmSharp) and the start-up role selection
tests/        Host tests
tools/        Package verification
docs/         This documentation
```
