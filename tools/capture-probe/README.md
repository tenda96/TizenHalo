# Capture probe — experimental Tizen 5.0 capture test

A separate app that makes a **bounded, opt-in** capture test on a Tizen 5.0 TV, to find out whether
TizenHalo can read the picture on that model before any Ambilight port is attempted. It is distinct
from the read-only [compatibility probe](../compatibility-probe/README.md): this one can invoke the
legacy capture library.

What it does, and does not do:

- It loads `libsec-video-capture.so.0.1.0` (or `.so.0` / `.so`) **directly**, independent of the
  modern capture library, and calls `secvideo_api_capture_video_main` three times in a row, requesting
  640×360 into caller-owned buffers.
- It **never** captures on launch — only when you press the button on its web page.
- It **never** drives the LEDs, changes any TV setting, or runs concurrent capture calls.
- The caller buffers are sized for a UHD panel, so the library's own capacity check passes; that check
  runs before any copy, so the call cannot overflow the buffers.

This is the **first live invocation** of this path on this TV family. The contract comes from static
analysis of the owner's own library; nothing about colours, dimensions or stability is proven yet.

## For the TV owner

1. Sideload `TizenHalo-Capture-Probe-0.1.0-tizen50.tpk` with Apps2Samsung, using its normal per-TV
   signing/install process. It coexists with other TizenHalo packages (different id and port).
2. Start a known, bright, colourful picture on the TV — ideally a still image or a paused frame, not
   a black screen or a menu.
3. Open **TizenHalo Capture Probe** and leave its screen open.
4. On a phone/computer on the same network, open `http://YOUR-TV-IP:8795/`.
5. Press **Run capture test**. The report downloads automatically. Send that JSON back, together with:
   - your exact model and firmware version,
   - what was on screen during the test (app/source and roughly what colours).
6. Press Back on the TV to close the probe and stop its local server.

If the app refuses to install or closes before showing an address, send the exact error or describe
what appeared on screen. A failure is still a useful result.

## What the report tells us

Per call: the return code (`rc`), the returned width/height, the colour-format enum, the protection
flag, the call duration, whether our buffer pointers were left in place, and a small centre-pixel
sample. `rc = -4` means the content was protected. Because the firmware reports the same enum `0` for
planar YUV420 and NV21, the sample reports both the NV12 and NV21 reading of the centre chroma bytes,
so the real byte order can be settled from a known captured colour before any colour conversion is
written.

## Build and verify

From the repository root, with .NET 8 installed:

```sh
bash tools/capture-probe/build.sh
```

This runs the host tests, builds the `tizen50` package, verifies it and copies
`dist/TizenHalo-Capture-Probe-0.1.0-tizen50.tpk`. The host tests cover the header offsets, the ignored
modern pitch region, caller-pointer ownership, buffer bounds and the NV12/NV21 chroma order on
synthetic data — no ARM code and no Samsung library are involved.
