# Capture adapter contracts

Review date: 2026-10-26. Device results and pinned repositories are in
[DEVICES.md](DEVICES.md). This document separates a published interface from an
adapter actually validated in TizenHalo.

## RGB point measurement — first portability integration

Implemented here in `app/src/RgbMeasureApi.cs`, `PpiCapture.cs` and
`CaptureBackendStartup.cs`. The resolver is original code in this MIT repository.
It looks up symbols but never calls them. It chooses a complete triplet, preserving
priority `ppi_ve`, then `ve`, then `cs_ve`; partial families are never combined.

| Member | Expected native signature |
|---|---|
| `<family>_get_rgb_measure_condition` | `int (void *condition)` |
| `<family>_set_rgb_measure_position` | `int (int slot, int x, int y)` |
| `<family>_get_rgb_measure_pixel` | `int (int slot, void *color)` |

Library: `/usr/lib/libvideoenhance.so`, then the same bare soname. Existing Cdecl
delegates are used. The expected condition starts with six int32 fields: slots,
block width, block height, settle milliseconds, full width and full height. Color
starts with int32 R/G/B (0–1023), followed by an extra int32 diagnostic field.
Both buffers are caller-owned, zeroed and padded to 256 bytes. Padding is a bound
on our allocation, **not proof that an arbitrary firmware writes this ABI**.

Existing validation rejects failed condition calls and nonpositive dimensions or
slot counts. Addressed slots are bounded by the reported count and the existing
16-slot sanity cap; reported settle time is clamped to 1–100 ms unless explicitly
overridden by existing developer settings. Read errors and invalid colors remain
visible in counters. New prefixes retain all these existing sampler checks.

The existing sampler serializes native calls on its worker and parks between
capture modes. Forced `captureBackend: ppi` now reaches that worker without
loading the video-plane/file-capture libraries first. Video-plane method selection
is skipped while this backend is selected. A switch to the file backend initializes
the file library lazily. The Q74T default remains `video_plane`.

Runtime telemetry includes family, completeness diagnostics and initialization
error. This is **capability discovery, not automatic device validation**. A known
symbol name alone cannot prove condition-layout compatibility, native permission,
fresh pixels, source coverage or runtime compatibility. On an unknown device,
start with the [read-only probe](../tools/compatibility-probe/README.md) and use an
explicit RGB test package rather than the Q74T full-frame default.

Useful independent references:

- [TizenGlow resolver/probe tree](https://github.com/DuckieOnQuacks/TizenGlow/tree/f9caf54e7c6c9ad57e985785b7be4fe6231e2066): GPL reference for complete-family discovery; README requires 6.5+ and TizenBrew.
- [R95H Tizen 10 PR](https://github.com/reisxd/HyperTizen/pull/14) and [The Frame Tizen 9 PR](https://github.com/reisxd/HyperTizen/pull/15): `ppi_ve`, two slots, 16×16 blocks and 20 ms reported settle. Both PRs were open when checked.
- [MIT RGB implementation](https://github.com/exzos28/tizen-os-ambilight/blob/c8ce69c5ae1aa2af914f48dff2a0a1ee238d090a/tv/service/Capture.cs): QN90D/Q60D project; downstream transport is its own ESP32 protocol.

Full-set refresh has a lower-bound time of `ceil(points / slots) × settle` before
processing/network overhead. With 24 points, two slots and 20 ms, that is 240 ms,
or at most about 4.17 complete sets/s. Rolling snapshots and 30/60 Hz output do
not change that measurement budget. Actual source freshness needs an owner test.

## Q74T full frames — existing verified ABI

Reference: QE55Q74T, T-NKMDEUC-2743.0, Tizen 5.5, 32-bit ARM process.
Current `VideoPlaneCapture.cs` owns persistent request and Y/chroma buffers and
serializes capture calls. Several function names are selectable for this researched
firmware. They are not a version-wide native ABI registry.

The Q74T modern header has color format at `+0x18`, protection at `+0x1c`, 3D at
`+0x20`, interlace at `+0x24`, Y/chroma pitch at `+0x28/+0x2c` and timestamp at
`+0x30`. Returned dimensions, pitch and capacities must pass the existing bounds
checks before processing. Do not substitute the newer upstream layout based only
on an exported function name.

Only this combination is locally validated. Its observed response budget is not
an assumption for other firmware families. Current work does not change its
capture timing or attempt firmware modifications.

## S84F full frames — separate adapter required

Reference: [upstream MIT implementation](https://github.com/RitwickVerma/TizenAmbilight/blob/ccb646600286a02150aa353db261ca3753a44a9e/TizenAmbilight/src/VideoPlaneCapture.cs),
QN77S84FAEXZA, installed Tizen 9, exact firmware not supplied.

The published newer layout adds `no_lock_no_copy` at `+0x18`; color format,
protection, 3D, interlace and pitches move to `+0x1c/+0x20/+0x24/+0x28/+0x2c/+0x30`.
The timestamp tail must be checked against real alignment/offsets rather than a
source comment. Inputs at `+0x00/+0x04` are sizes, `+0x08/+0x0c` dimensions and
`+0x10/+0x14` are 32-bit pointers. This is distinct from Q74T.

Next gate: factor a named full-frame layout contract, add synthetic header/stride
and capacity tests, verify pointer width and timestamp alignment, then build an
opt-in adapter profile. No auto-selection from `Tizen 9` alone. Upstream support
and the MIT license make this a useful next implementation candidate, but it is
not integrated in this branch.

## Q80A full frames — promising, incomplete native contract

Reference: [open lowryn PR #2](https://github.com/lowryn/HyperTizen/pull/2), source
[SDK/SecVideoCapture.cs](https://github.com/dferchaud/HyperTizen/blob/331fd870f6b935d9e0edde0b43e1fef661e0d6a4/HyperTizen/SDK/SecVideoCapture.cs)
and [VideoCapture.cs](https://github.com/dferchaud/HyperTizen/blob/331fd870f6b935d9e0edde0b43e1fef661e0d6a4/HyperTizen/VideoCapture.cs).
QE55Q80AATXXC, Tizen 6.0; exact firmware unknown. This is GPL reference material.

- Library `/usr/lib/libsec-video-capture.so.0`; function
  `secvideo_api_capture_screen(width, height, infoPointer)`.
- Original 36-byte info allocation caused overwrite; the port uses a zeroed
  **256-byte** native header.
- Buffers are **caller-owned** Y/UV allocations. Before each call the port writes
  sizes at `+0/+4` and pointers at `+16/+20`; it reads pointers back and copies
  fixed requested capacities after the call.
- A TV image in `/frame.bmp` and HyperHDR delivery are reported. No quantified FPS,
  long-run stability, returned stride contract or protected-video coverage is given.
- The same TV reportedly lacks useful RGB exports in its libvideoenhance wrapper.

Do not copy a fixed-length readback from a returned pointer into a generic adapter:
first prove lifetime, ownership, returned geometry/format, pitch and capacities.
The padded header prevents the specific reported small-allocation bug, but does
not establish the whole ABI. This review corrects the earlier suspicion that the
port used native-owned image allocations: its source allocates them in the caller.

The PR's TizenBrew/local-SDB launch workaround is packaging-specific. TizenHalo's
single-package menu/service architecture should be evaluated independently.

## Excluded as drop-in native adapters

- BigSant S95F UI mirroring does not capture Plex video. Its PC/FFmpeg bridge is
  external capture; the current TV package is control-only. RM/encoder experiments
  that wedged the pipeline should not become an automatic fallback.
- [libSambilight](https://github.com/Tasshack/libSambilight) is a separate SamyGO
  project. This review collected no device/runtime/ABI evidence making it a
  TizenHalo adapter; do not extrapolate from the project name.
- Android HyperHDR grabbers require another operating system/runtime and do not
  establish a private Samsung TV capture interface.

## Runtime and signing gates

The application and launcher currently target `tizen55`; the separate read-only
diagnostic targets `tizen50`. Supporting an interface does not automatically make
the main package valid on 5.0 or older releases. Install, menu/service start,
library load, native call success, source coverage and sustained measurements
must be recorded separately. OS updates can change a TV's installed platform.

The Q7F issue reports a certificate fix; the QN90A issue's later comment reports
a Partner certificate already in use and capture still denied. Keep those results
separate; do not prescribe signing as a universal workaround.
