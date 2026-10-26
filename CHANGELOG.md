# Changelog

## 1.0.0 — 2026-10-26

First public release. Differences from [TizenAmbilight](https://github.com/RitwickVerma/TizenAmbilight):

**Platform**
- Targets 2020 Samsung TVs on Tizen 5.5 (32-bit ARM), with the native capture interface of this firmware
  family. Tested on a QE55Q74T, firmware T-NKMDEUC-2743.0.
- Seven selectable capture methods across the two capture libraries of this firmware, switchable at
  runtime. Default: `screen_video`, the best in measurements.

**Apps and settings**
- A menu app on the TV, next to the background service: status, QR code to the settings page and a
  lights on/off button.
- A settings page served by the TV on port 8793, saved on the TV and kept across reboots.
- An LED layout editor: generate a frame, draw extra segments (for example under a stand), move color
  zones, import HyperHDR layouts, work offline.
- `config.json` values (for example from an install form) become the first-run settings.

**Picture and output**
- Pacing on a monotonic clock with a fractional period, so the service never asks the firmware for bursts
  of captures it cannot serve.
- Smoothing scaled by the real time between frames on every capture path.
- Optional interpolated output at 30 or 60 Hz, on by default.
- The strip holds its last colors through short capture gaps (up to 1.1 s) before blanking.

**Known limitations**
- About 15 new frames per second, set by the firmware.
- Protected content is not captured, and the TV's color sampler fallback is not available on this firmware.
