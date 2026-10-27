# TizenHalo on the RU7100 — first real build

This is the actual Ambilight app for your UN43RU7100FXZC (Tizen 5.0), not a test probe. It runs as a
background service on the TV, captures the screen with the PNG screenshot API your capture tests
confirmed works, averages the picture into LED zones and drives a WLED strip.

It is built **assuming** the screenshot captures video the same way it captured the painted colours in
the probe. If the lights follow the on-screen picture, we're there. If they stay dark while a video
plays, that single assumption is wrong and we roll back to a short test — nothing here changes the TV.

## Install

1. Sideload `TizenHalo-1.1.0-ru7100-50.tpk` with Apps2Samsung (same per-TV signing/install as the
   probes). Install **one** TizenHalo package at a time; this replaces the capture probe if present.
2. Open **TizenHalo** on the TV. The menu shows a QR code and an address like `http://<TV-IP>:8793`.

## Set up (from your phone)

1. Open `http://<TV-IP>:8793` on a phone/computer on the same network.
2. Enter your **WLED controller IP** and the **number of LEDs**. Nothing is sent to the strip until the
   IP is set.
3. Set where the first LED is and which way the strip runs, or open the **LED layout editor** to draw
   it / import a HyperHDR layout.
4. Play a video on the TV. The lights should follow the picture. Press Back on the remote to return to
   the video; the service keeps running.

## What to tell us

- Do the LEDs follow the on-screen video (colours roughly match the edges)?
- The status line on the settings page, or `http://<TV-IP>:8793/api/status`: the reported capture rate
  (`fps`), the `resolution`, and any error.
- Your exact model and firmware version.

## Notes

- Capture uses the screenshot path (`dc_request_capture_to_file_sync` → PNG → decoded). On your TV it
  returned ~480×270; the app adapts to whatever size comes back.
- Expect a modest capture rate (the screenshot + decode is heavier than a raw plane read). LED output is
  smoothed by interpolation, so the light stays fluid even if the captured frames are a few per second.
- Protected content (some streaming apps) may not be captured; the lights go dark there.
- This profile is experimental and tested on no RU7100 yet. Keep a known-good package for rollback.
