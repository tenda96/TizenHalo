# TizenHalo

**Bias lighting for 2020 Samsung TVs (Tizen 5.5).** A small app runs on the TV, reads the picture and
drives a [WLED](https://kno.wled.ge/) LED strip over your network. No capture card, no HDMI splitter,
no computer or Raspberry Pi.

TizenHalo is a fork of [TizenAmbilight](https://github.com/RitwickVerma/TizenAmbilight) by Ritwick
Verma, ported to the Tizen 5.5 firmware of the 2020 line-up.

> [!WARNING]
> **Tested on one TV:** QE55Q74T, firmware T-NKMDEUC-2743.0. The app uses undocumented Samsung system
> libraries that may be missing or different on other models and firmware versions. If you try it on
> another TV, please open an issue and say what happened, whether it worked or not.

> [!NOTE]
> **Development alpha: v1.1.0-alpha.1.** The new RGB/S84F compatibility profiles
> are compiled and checked offline, but this revision has no on-TV validation yet.
> Read [TESTING.md](docs/TESTING.md) to choose a test package and report results.
> [Stable v1.0.0](https://github.com/tenda96/TizenHalo/releases/tag/v1.0.0) remains
> the Q74T baseline; the reference behavior and limitations below describe that setup.

## What it does

- Runs entirely on the TV. You adjust it from your phone: the app menu shows a QR code to the settings page.
- Captures about 15 new frames per second (the firmware's limit) and smooths the LED output to 30 or 60 Hz.
- Uses a standard frame around the screen, or a layout you draw in the built-in editor
  (HyperHDR layouts can be imported).
- Detects black bars, handles HDR10 and HLG, and adapts its smoothing to the scene.

## Requirements

- A 2020 Samsung TV on Tizen 5.5 (tested: QE55Q74T).
- A WLED controller with UDP realtime enabled (default port 21324), up to 490 LEDs.
- TV and controller on the same local network.

## Install

Samsung TVs only install packages signed for that specific TV. The `.tpk` from a release (or from
`./build.sh`) is signed with the Tizen SDK's default certificate, which a retail TV rejects, so it has to
be re-signed for your TV first:

- **[Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung)** re-signs a package for your TV and
  installs it, or
- **your own certificate:** create a Samsung certificate profile for your TV in Tizen Studio's Certificate
  Manager, re-sign the package with it and install it with the Tizen CLI.

To build the package yourself, see [Build from source](docs/TECHNICAL.md#build-from-source).

## First setup

1. Open **TizenHalo** from the TV's apps. The menu shows a QR code and an address like
   `http://<tv-ip>:8793`.
2. Open it on your phone or computer. Enter the WLED controller's IP and the number of LEDs, then set
   where the first LED is and which way the strip runs. Save.
3. Play a video. The lights follow it; press Back on the remote to return to the video.

For anything other than a plain frame around the screen (LEDs under a stand, gaps, odd shapes), open
the **LED layout editor** from the settings page. Every setting is described in
[docs/CONFIGURATION.md](docs/CONFIGURATION.md).

## Limitations

- **About 15 new frames per second.** This is how often the firmware delivers a fresh picture, whatever
  capture method is used. Requesting more only returns empty frames. Interpolation keeps the LEDs smooth.
- **Protected content is not captured.** Many streaming apps withhold their picture; the lights then go
  dark. There is no fallback on this firmware.
- **Menus and thumbnails** often provide no picture, except with the "Screen · full composition" method,
  which also sends TV menus and overlays to the LEDs.
- **The settings page has no password.** Anyone on your local network can change the settings.

## Troubleshooting

| Symptom | Check |
|---|---|
| Lights never turn on | WLED IP and port on the settings page; UDP realtime enabled in WLED; a video is playing |
| Colors appear on the wrong side | "First LED position" and "The strip runs counter-clockwise", or draw the layout in the editor |
| Lights stay dark in one app | The content is probably protected |
| Lights stutter | Keep "Requested updates" at 15 with interpolation on; try the "Screen · video only" method |

The status line on the settings page and `http://<tv-ip>:8793/api/status` show what the service is doing.

## Documentation

- [Configuration](docs/CONFIGURATION.md): settings, defaults, LED mapping, layout file format.
- [Technical notes](docs/TECHNICAL.md): architecture, capture methods and measurements, local API, building.
- [Compatibility research](docs/COMPATIBILITY.md): model/firmware evidence, the RU7100 investigation and the separate Tizen 5.0 diagnostic.
- [Device evidence catalogue](docs/DEVICES.md): source-linked upstream results, firmware limitations and actual TizenHalo validation status.
- [Portability plan and handoff](docs/PORTABILITY-PLAN.md): staged implementation, native contracts and instructions for continuing the work.
- [Development build profiles](docs/BUILD-PROFILES.md): shared runtime packages, experimental RGB/S84F profiles and owner-test limits.
- [Changelog](CHANGELOG.md)

## License and credits

MIT, see [LICENSE](LICENSE). Based on TizenAmbilight by Ritwick Verma (MIT). The QR code in the TV menu
uses [Net.Codecrete.QrCodeGenerator](https://github.com/manuelbl/QrCodeGenerator) (MIT).

TizenHalo is an independent project, not affiliated with or endorsed by Samsung, WLED or Philips.
Samsung, Tizen, WLED and Ambilight are trademarks of their respective owners. It does not bypass content
protection. Use it at your own risk; it relies on undocumented system interfaces that a firmware update
can change or remove.
