# RU7100 owner diagnostic and native contract review

Received and inspected: 2026-10-27. This is diagnostic/static evidence, **not a
successful capture test**. No new TV capture was invoked in this investigation.
The owner's report explicitly records `native_capture_invoked: false`.

## Attribution and verified inputs

The conversation identifies the TV as UN43RU7100FXZC / T-MSLAKUC-1500.5. The JSON
itself reports model `unknown` and a firmware placeholder, so the exact SKU/firmware
remain owner attribution, not independently verified report fields.

The probe successfully ran and read files on **Tizen 5.0**, in a **32-bit process**.
All attached ELF files are ARM32/EABI5; their byte counts and SHA-256 hashes match
the probe report exactly. Five supplied library files contain three distinct binaries:

| Binary | Bytes | SHA-256 |
|---|---:|---|
| `libsec-video-capture.so.0.1.0` | 34416 | `aaa972ffff9a5e96236abbd3c48ce3420e09f69621811ac49b1d840c80dde2fd` |
| `libavoc.so` | 722936 | `a6413cb76017890e880e4929206a59fadea4c6f19836a83b481ed51e1f8d245d` |
| `libdisplay-capture-api.so.0.0` | 18024 | `c01a0254f3a11ad96aac28c40db262971010cb99dab059e6128d41d7550de188` |

The supplied `.so.0` capture/display files are byte-identical to their versioned
counterparts. That establishes equal content, not the original filesystem link type.
The capture Build ID is `f661ba018308aae0e3ca03c2678311ea8992e56b`.
It differs from the previously inspected Q74T legacy library hash.

The modern `libvideo-capture` and two probed `libvideoenhance` names are
`missing_or_inaccessible`. Do not turn that into a claim of physical absence.
The report did not call `dlopen`, `dlsym`, capture or HDR functions; readability
and exported symbols do not establish runtime permission or working pixels.

## Recovered legacy capture path

Ghidra 12.1.4 inspection of this exact owner binary finds:

- `secvideo_api_capture_video_main` at analysis address `0x11168` forwards three
  arguments through dispatch-table slot `+0x0c`.
- Dispatcher initializer `0x15ca4` returns table `0x28094`; slot `+0x0c` contains
  `api_capture_video_main` (`0x158f8`). These are analysis addresses, not live pointers.
- The wrapper assembly preserves r0/r1/r2 and supplies four zero crop arguments
  before calling the crop implementation at `0x157c8` via its PLT stub.
- Crop implementation calls the core at `0x13798` with input/video-main selection
  `(1, 5, requestedWidth, requestedHeight, 0, 1, callerHeader, ...)`.
- The core opens `sec_dri_card1` through the native library, uses synchronization
  ioctl `0xc00c6460`, capture ioctl `0xc0686452`, and copies into caller-provided
  Y/chroma pointers after checking advertised capacities. An app adapter should
  call this existing native entry point, not reproduce direct driver access.
- Protected capture takes an explicit `-4` return path. No protected-content
  fallback is established on this model.

Header fields observed in this core (byte offsets):

| Offset | Meaning / observation |
|---|---|
| `+0x00`, `+0x04` | Caller Y/chroma capacities, checked before copy |
| `+0x08`, `+0x0c` | Returned width/height written on successful copy |
| `+0x10`, `+0x14` | Caller-owned Y/chroma pointers, used as copy destinations |
| `+0x18` | Returned format enum: 0=420, 1=422, 2=444, 3=unknown |
| `+0x1c` | Returned protection flag |
| `+0x20` | Returned 3D mode |
| `+0x30`, `+0x34` | Timestamp words |
| `+0x3c` | Flip indication |

This copy path does **not** write the Q74T-modern interlace/pitch fields at
`+0x24/+0x28/+0x2c`. It packs output rows itself when driver pitch differs from
image width. Keep a separate legacy readback policy; do not shift S84F fields here.

### Important color distinction

The core has an explicit FourCC `0x3132564e` **NV21** branch while reporting enum
0 for both this branch and planar YUV420. Therefore enum 0 does not prove NV12
UV order. The current application's zone extractor assumes NV12. A RU7100 test
adapter must establish the actual VU/UV order and converted-planar output, add
synthetic color fixtures, and verify primary colors on the TV before claiming
correct colors. Do not silently reuse the current NV12 conversion.

## Pacing, HDR and remaining limits

`0x15fdc` computes `previousTimestamp + 40000 - now` from shared memory and checks
whether calls are less than 30001 microseconds apart. The core may sleep for the
computed delay before its ioctl; `0x16108` stores pid, success, timestamp and a
104-byte ioctl descriptor. This is evidence of shared pacing/caching, not an
observed frame rate or proof of the Q74T's 150-responses/~10-second behavior.

`avoc_get_hdr_type` writes one 32-bit result through its pointer argument and calls
the AVOC DBus proxy. Assembly shows a status return, including `-7` on DBus-init
failure. This is a plausible match for `out int` plus status, but no runtime call
or SDR/HDR numeric mapping was validated. Keep the manual SDR option available.

The display-capture file exports the expected sync/file/periodic entry points.
It was inventoried, not decompiled in this pass; its request ABI, runtime service
permissions, returned format and video coverage still need review.

## Next concrete implementation/test gate

1. Confirm the owner's exact current SKU/firmware alongside this report hash.
2. Add an explicit **Tizen 5.0** experimental profile using the legacy library
   directly (`libsec-video-capture.so.0` / `.so.0.1.0`). The current loader requires
   the missing modern library before legacy method selection, so selecting
   `legacy_main` alone is insufficient.
3. Factor library/entry/legacy-copy policy by profile. Do not broaden the Q74T
   ABI or guess from the Tizen version. Preserve the public alpha tag/assets.
4. Implement header/bounds fixtures for this binary and explicit chroma-order
   handling. Allow only 420 initially; reject 422/444/interlace/oversized readback
   instead of treating it as existing NV12 data. Advertised capacities must
   account for the native pre-copy pitch checks, not only packed output bytes.
5. Start with a bounded opt-in capture probe (for example a few serialized calls
   requesting 640×360). That is a proposed first test, **not a proven supported
   resolution**. Record `dlopen`/symbol status, rc, dimensions, format, protection,
   pointer ownership, duration and a small pixel sample. No concurrent native calls.
6. Only after that succeeds, build the LED-output profile and test identified SDR
   video, RGB primaries, mapping, restart and sustained rate. Capture and output Hz
   remain separate. No 15/25/30-FPS claim is currently possible for this TV.

## Private evidence and checkpoint

Original binaries/report and full decompiler Evidence are kept outside this Git
repository under `../tizen-ru7100-research/owner-report-2026-10-27/`. That folder
contains an input manifest, unique binaries, capture dispatch/core/allocator/pacing
analysis, wrapper assembly, HDR dossier and REA snapshots. Do not add those
proprietary binaries or full decompiled functions to the public repository.
The firmware remains encrypted, but access to these owner libraries removes
firmware decryption as the blocker for this native-API investigation.

## Capture probe prepared (2026-10-27) — offline only

A separate experimental Tizen 5.0 capture probe is now implemented under
`tools/capture-probe/`, distinct from the read-only inventory probe. It is **built and host-tested
offline; it has not run on the TV**, and the owner has not returned a capture result.

- `CaptureContract.cs`: the caller-owned 256-byte header contract recovered above (capacities
  `+0/+4`, returned dimensions `+8/+0c`, pointers `+0x10/+0x14`, format `+0x18`, protection `+0x1c`,
  3D `+0x20`, timestamp `+0x30`, flip `+0x3c`). It deliberately does **not** read the modern
  `+0x24/+0x28/+0x2c` fields, which this copy path never writes. Operates on a managed `byte[]` so
  the logic is host-testable.
- `LegacyCapture.cs`: loads `libsec-video-capture.so.0.1.0`/`.so.0`/`.so` **directly** and calls
  `secvideo_api_capture_video_main`, with no dependency on the modern library (the legacy entry point
  initialises its own dispatch table). Caller-owned Y/chroma buffers are sized for UHD so the native
  pre-copy capacity check passes instead of failing early; that check prevents any overflow.
- The probe makes **three serialized calls** at 640×360 (a proposed first request, not a proven
  resolution), only on an explicit owner action from its web page (port 8795). No LEDs, no settings,
  no concurrency. It records library/symbol status, `rc`, returned dimensions, format enum,
  protection, duration, caller-pointer ownership and a centre-pixel sample.
- Colour order is left unresolved on purpose: the firmware reports enum `0` for both planar YUV420
  and NV21, so the report gives both the NV12 and NV21 reading of the centre chroma bytes. The real
  VU/UV order must be settled from a known captured colour before any conversion is written.
- Host tests (`tools/capture-probe/host-tests`) cover header offsets and round-trip, the ignored
  modern-pitch region, caller-pointer ownership, buffer bounds and the NV12/NV21 chroma order on
  synthetic data. `bash tools/capture-probe/build.sh` runs them, builds and verifies the package.

Owner instructions are in `tools/capture-probe/README.md` and `OWNER-MESSAGE.txt`. The package is not
published: it is a local deliverable awaiting a real capture result on the TV. Only after a successful
capture (usable picture, correct colours, stable rate) should an LED-output profile be built.

## First live capture result (2026-10-27) — capture works, picture was dark

The owner ran capture-probe v0.1 on the TV. This is the **first real on-hardware capture** for this
family, and it succeeded mechanically:

- `libsec-video-capture.so.0.1.0` loaded directly (no modern library), symbol resolved.
- All three `secvideo_api_capture_video_main` calls returned `rc = 0`, `protected = 0`, with our
  caller pointers unchanged and the returned geometry inside the advertised buffers. The recovered
  caller-owned header contract is therefore correct **on hardware**, not only in static analysis.
- Returned geometry was **640×240** for a 640×360 request; durations 31–64 ms.

But the frame was essentially black: centre Y was 1, 3, then 27 across the calls, chroma 128/128
(neutral). The owner had the probe app in the foreground with no video playing, so the **video plane**
was dark — `secvideo_api_capture_video_main` reads the video plane, not the on-screen composition, so a
still image or menu on the graphics plane does not appear. Colour order (NV12 vs NV21) therefore
remains unresolved: there was no real colour to read.

### Capture probe v0.2

To settle colour without depending on a video being played, v0.2:

- also calls `secvideo_api_capture_screen` (whole-screen composition), so a solid-colour still image
  is captured too, alongside `secvideo_api_capture_video_main`;
- samples a 16×9 grid instead of a single centre pixel, reporting Y min/max/mean, the lit fraction and
  the colour at the brightest point (both NV12 and NV21 readings), so a dark centre no longer hides
  real content.

Built and host-tested offline (grid-sampling fixtures added). Not yet run on the TV. The owner is asked
to show a bright solid red or blue picture and re-run. Only once a captured frame carries a real,
known colour can the VU/UV order be fixed and an LED-output profile considered.

### Capture probe v0.3 — self-drawn colours

A usability flaw in v0.2: to show a colour the owner would have to leave the probe foreground (Tizen
then pauses the app, so the server stops), and whole-screen capture would otherwise grab the probe's
own dark window. v0.3 removes the dependency on external content: the probe **paints full-screen red,
green and blue on its own window** and captures the screen composition for each, plus one video-plane
capture for comparison. Because the painted colour is known, the brightest-point sample settles the
VU/UV order and shows whether `secvideo_api_capture_screen` even includes the graphics plane. One
button, no photo, no background. Built and host-tested offline; not yet run on the TV.

### v0.3 result and probe v0.4 (PNG screenshot path)

v0.3 ran on the TV: all three painted colours captured through `secvideo_api_capture_screen` came back
**all-zero** (y_max 0), confirming that secvideo screen capture does not include the graphics plane on
this firmware; the video plane stayed dark with no video. So neither secvideo path sees on-screen
colour.

`dc_request_capture_to_file_sync` is confirmed **exported** in the owner's `libdisplay-capture-api.so.0.0`
(alongside `dc_request_capture` and `dc_request_capture_sync`). It is the display-capture/screenshot
API and normally captures the whole composition to a PNG, so it is the candidate that may see on-screen
colour — and PNG gives RGB directly, removing the NV12/NV21 question. v0.4 adds `DisplayCapture.cs`: for
each painted colour it writes a PNG via this API, decodes it with the existing PNG decoder and samples
the RGB; it also keeps the secvideo and video-plane captures for comparison. The `_dc_capt_req_data`
offsets match the Q74T family and the entry point is confirmed on this TV, but the struct ABI is
assumed — a wrong field fails cleanly (the library validates app type / capture mode). Built and
host-tested offline (RGBA grid-sampling fixtures added); not yet run on the TV. If the screenshot
captures the painted colours, both the colour order and a usable capture method are settled at once.

### Confirmed working + real app profile (2026-10-27)

A good v0.4 run (one of several; others were black from a display-timing race the owner identified)
proved capture works: the PNG screenshot decoded red/green/blue exactly as painted (red → RGB
255,58,13), and `secvideo_api_capture_screen` captured them too. Chroma order is **NV12** (red U88/V244,
blue U242/V109), matching TizenHalo's extractor. The earlier "secvideo can't see graphics" was the same
timing artifact and is withdrawn.

A real application profile `ru7100-50` (tizen50 / API 5.0) now exists, using the file/screenshot backend
(`dc_request_capture_to_file_sync` → PNG → `PngDecoder`). It drives the LEDs via the normal TizenHalo
service. Built and fully host-tested offline; it assumes the screenshot captures video as it captured the
painted colours — to be confirmed on the TV. See `docs/RU7100-OWNER-SETUP.md` and `docs/BUILD-PROFILES.md`.
