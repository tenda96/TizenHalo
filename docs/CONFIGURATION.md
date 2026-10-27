# Configuration

## Where settings come from

Each setting is resolved in this order, first match wins:

1. **Saved settings**: what you save on the settings page (`http://<tv-ip>:8793`). Stored on the TV, kept
   across restarts and reboots.
2. **`config.json`** inside the package: install-time values, for example filled in by the Apps2Samsung
   form or baked into your own build (copy `app/config.example.json` to `app/config.json`).
3. **Built-in defaults**, listed below.

`config.json` only provides the starting point: once a value is saved on the settings page, the saved
value is used. Invalid values in `config.json` are ignored one by one and the default is kept.

## Settings page

| Key | Default | Range | Meaning |
|---|---|---|---|
| `wledIp` | empty | IPv4 | WLED controller address. Nothing is sent until it is set. |
| `wledPort` | `21324` | 1–65535 | WLED UDP realtime port. |
| `ledCount` | `100` | 4–490 | LEDs on the strip. With an editor layout it follows the layout. |
| `outputEnabled` | `true` | | Lights on or off. Also toggled from the TV menu. |
| `layoutMode` | `standard` | `standard`, `custom` | Frame around the screen, or the layout uploaded from the editor. |
| `mapOffset` | `0` | 0 to LEDs − 1 | Position of the first LED, counted clockwise from the top-left corner. |
| `mapReverse` | `false` | | The strip runs counter-clockwise. |
| `manualSides` | `false` | | Set the LEDs per side instead of splitting them by aspect ratio. |
| `ledsTop`, `ledsRight`, `ledsBottom`, `ledsLeft` | `30`, `20`, `30`, `20` | 1–490 | Used with `manualSides`; must add up to `ledCount`. |
| `captureMethod` | `screen_video` | see below | Firmware capture path. |
| `targetFps` | `15` | 5–60 | Requested captures per second. The TV delivers about 15 new frames per second. |
| `interpolation` | `true` | | Blend between captured frames for smoother LEDs. |
| `outputFps` | `30` | 30 or 60 | LED update rate while interpolating. |
| `hdrMode` | `auto` | `auto`, `sdr`, `hdr`, `hlg` | Video format. `auto` asks the TV. |
| `alpha` | `0.5` | 0.05–1 | Responsiveness: lower is softer, higher is quicker. |
| `alphaFast` | `0.85` | 0.05–1 | Responsiveness on scene cuts (never below `alpha`). |
| `saturation` | `1.25` | 0–3 | 1 is neutral. |
| `gain` | `1.0` | 0.1–2 | Brightness. |
| `gamma` | `1.15` | 1–3 | Higher values lift dark scenes. |
| `calRed`, `calGreen`, `calBlue` | `1.0` | 0–2 | **Colour calibration** per channel, applied at the output for every capture method. 1.0 = unchanged. Lower a channel to fix a tint (e.g. blue looking cyan → lower `calGreen`). |
| `calSat` | `1.0` | 0–3 | Saturation applied at the output, so the file/screenshot backend (RU7100) gets saturation too. 1.0 = unchanged. |
| `adaptiveGamma` | `false` | | Adapt the tone curve to each scene's brightness. |
| `black` | `18` | 0–60 | Below this level an LED is switched off. |
| `bars` | `true` | | Detect black bars at the sides and sample the picture inside them. |
| `barRows` | `true` | | Also detect black bars above and below. |
| `barThresh` | `18` | 17–40 | Luma level treated as a black bar. |
| `captureWidth`, `captureHeight` | `960`, `540` | even, up to 1280 × 720 | Size requested from the firmware. The normal paths return at most 720 × 576. |
| `precisePacing` | `true` | | Use a precise sleep between captures. |
| `punch` | `0` | 0–1 | Short brightness boost on hard cuts. |

### Capture methods

| `captureMethod` | Label | Notes |
|---|---|---|
| `screen_video` | Screen · video only | Default. Best measured result. |
| `screen_full` | Screen · full composition | Includes TV menus and overlays; lightest per call. |
| `main` | Video plane | Reliable but heavier per call. |
| `main_no_flip` | Video plane · no flip | The picture may come out mirrored. |
| `legacy_main`, `legacy_screen_video`, `legacy_no_flip` | Legacy · … | Older firmware library; slower. |

Measurements are in [TECHNICAL.md](TECHNICAL.md#capture-rate).

## LED mapping

Look at the TV from the front. Count LEDs clockwise starting from the top-left corner: the LED where the
data cable enters the strip is LED 0 of the strip, and `mapOffset` is its position in that count. If
the strip continues counter-clockwise from there, enable `mapReverse`.

If the colors come out mirrored, toggle `mapReverse`; if they are shifted along the frame, adjust
`mapOffset`. For anything irregular, the layout editor is easier.

## Layout editor

Open `http://<tv-ip>:8793/layout`. You can generate a frame (LEDs per side, starting corner, direction),
draw extra segments (for example under a TV stand), move each LED's color zone and import or export the
layout as JSON. **Apply to TV** saves it and switches `layoutMode` to `custom`.

The editor also works offline: open `app/res/Layout.html` in a browser, then import the exported file
on the TV later.

### Layout file format

```json
{
  "schema": 1,
  "name": "My layout",
  "leds": [
    { "hmin": 0.00, "hmax": 0.05, "vmin": 0.92, "vmax": 1.00, "x": 0.02, "y": 1.30 }
  ]
}
```

- The array index is the physical LED address (LED 0 first).
- `hmin`, `hmax`, `vmin`, `vmax`: the picture rectangle the LED takes its color from, as fractions of
  the width and height (0 to 1).
- `x`, `y` (optional): where the LED is mounted, for the editor's preview only. `y` greater than 1 is
  below the screen.
- 4 to 490 LEDs. A plain HyperHDR LED array (without `schema` and `name`) is accepted on import.

## Install-time keys (`config.json`)

Any key from the table above can go in `config.json`. These are only read from there:

| Key | Default | Meaning |
|---|---|---|
| `zoneCount` | `ledCount` | Fewer zones than LEDs leaves the extra LEDs black (for example LEDs hidden behind a stand). |
| `bandFrac` | `0.08` | Depth of the border band sampled for each zone, as a fraction of the picture. |
| `alphaCut` | `18` | Frame change above which `alphaFast` is used. |
| `spatial` | `0.5` | Blend between neighbouring zones (standard layout only). |
| `floor` | `0` | Minimum LED level, so the room never goes fully dark. |
| `staleMs`, `staleLevel` | `6000`, `0.35` | Dim to this level when the picture has not changed for this long. |
| `whiteR`, `whiteG`, `whiteB` | `1.0` | White balance per channel. |
| `wledTimeout` | `1` | Seconds after which WLED leaves realtime mode if packets stop. |
| `captureBackend` | `video_plane` | `ppi` forces the RGB point sampler independently of video capture; `file` uses the slower screenshot-to-PNG path. New TV/family combinations are experimental. |
| `probeEndpoint`, `statsIntervalSec` | empty, `5` | Optional diagnostics collector; see [TECHNICAL.md](TECHNICAL.md#diagnostics). |
| `controlUrl`, `enableLegacyControl` | empty, `false` | Optional development control channel. Off unless both are set. |

`ppiZones` controls the number of positions measured by the built-in RGB sampler, if available.
It is also kept compatible with TizenAmbilight's install form. With two hardware slots and a
20 ms settle time, 24 positions need at least 240 ms for a complete set, before other overhead.
Interpolated LED sends can run faster; they do not add measurements.

For an opt-in RGB test, the `rgb-55` [test profile](BUILD-PROFILES.md) forces the
sampler without a full-frame ABI. It has the same application DLL as the Q74T-5.5
profile; runtime compatibility on other TVs still needs an owner test.

Alternatively, in the Q74T profile, copy `app/config.example.json` to the ignored
`app/config.json`, retain your actual WLED/layout values and add `"captureBackend": "ppi"`.
Build and install that package. The choice is currently an install-time/developer setting,
not a persistent selector in the browser UI. It must be explicit on unknown full-frame ABIs;
the default `video_plane` still uses the Q74T native contract.
Package profiles constrain these settings: RGB-only cannot switch to video/file;
S84F exposes only the `main` full-frame method and requests up to 960×540. Native
ABI selection is package metadata, not an editable browser setting. When replacing
a profile, incompatible saved capture methods/requests are normalized while WLED
and layout values are retained. The sampler selects the first complete `ppi_ve`,
`ve`, or `cs_ve` triplet, in that order.
`/api/status` reports `capture_backend`, `rgb_api_family`, `rgb_api_symbols` and
`rgb_init_error`; the larger telemetry report retains the sampler condition and counters.
Availability of symbols is not proof of working measurements. See [native contracts](CAPTURE-CONTRACTS.md).

A few more developer-only keys are read in `app/src/Config.cs`.
