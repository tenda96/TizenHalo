# Capture probe — experimental Tizen 5.0 capture test

A separate app that makes a **bounded, opt-in** capture test on a Tizen 5.0 TV, to find out whether
TizenHalo can read the picture on that model before any Ambilight port is attempted. It is distinct
from the read-only [compatibility probe](../compatibility-probe/README.md): this one can invoke the
legacy capture library.

What it does, and does not do:

- It **paints full-screen red, green and blue on its own window** and, for each colour, tries a PNG
  screenshot (`dc_request_capture_to_file_sync`, decoded to RGB) and the secvideo screen path, plus a
  video-plane capture for comparison. Because it knows which colour it drew, the
  real chroma order and screen coverage can be settled with no photo or video needed, and without
  relying on the app staying alive in the background (Tizen pauses backgrounded UI apps).
- It loads `libsec-video-capture.so.0.1.0` (or `.so.0` / `.so`) **directly**, independent of the modern
  capture library.
- It **never** captures on launch — only when you press the button on its web page.
- It **never** drives the LEDs, changes any TV setting, or runs concurrent capture calls.
- The caller buffers are sized for a UHD panel, so the library's own capacity check passes; that check
  runs before any copy, so the call cannot overflow the buffers.

The contract comes from static analysis of the owner's own library. A first live run of an earlier
version confirmed that capture returns frames (`rc = 0`) on the owner's TV; colours are what this
version is for.

## For the TV owner

1. Sideload `TizenHalo-Capture-Probe-0.4.0-tizen50.tpk` with Apps2Samsung, using its normal per-TV
   signing/install process. It coexists with other TizenHalo packages (different id and port).
2. Open **TizenHalo Capture Probe** and leave its screen open.
3. On a phone/computer on the same network, open `http://YOUR-TV-IP:8795/`.
4. Press **Run capture test**. The TV screen briefly flashes red, then green, then blue — that is the
   test painting the colours it captures. The report downloads automatically.
5. Send that JSON back, with your exact model and firmware version.
6. Press Back on the TV to close the probe and stop its local server.

Nothing to show, nothing to keep open in the background. If the app refuses to install or closes
before showing an address, send the exact error or describe what appeared on screen — a failure is
still a useful result.

## What the report tells us

For each painted colour, and for the video-plane capture: the return code (`rc`), the returned
width/height, the colour-format enum, the protection flag, the call duration, whether our buffer
pointers were left in place, and a grid of pixel samples (Y min/max/mean, lit fraction, and the
colour at the brightest point, reported both as NV12 and as NV21). Since we know we painted a strong
red/green/blue, that brightest-point colour settles the VU/UV order and shows whether screen capture
includes the graphics plane at all. `rc = -4` means the content was protected.

## Build and verify

From the repository root, with .NET 8 installed:

```sh
bash tools/capture-probe/build.sh
```

This runs the host tests, builds the `tizen50` package, verifies it and copies
`dist/TizenHalo-Capture-Probe-0.4.0-tizen50.tpk`. The host tests cover the header offsets, the ignored
modern pitch region, caller-pointer ownership, buffer bounds, the NV12/NV21 chroma order and the grid
sampling on synthetic data — no ARM code and no Samsung library are involved.
