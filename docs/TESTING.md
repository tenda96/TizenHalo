# TizenHalo public alpha testing

Release: **v1.1.0-alpha.1**. Numeric TV package version: **1.1.0**.
These builds passed compilation and offline checks. **No on-device test of this
alpha revision has been recorded yet**, including the refactored Q74T profile.
Upstream reports are listed in [the device catalogue](https://github.com/tenda96/TizenHalo/blob/v1.1.0-alpha.1/docs/DEVICES.md); they are not
TizenHalo validation of the new models.

The stable [v1.0.0 release](https://github.com/tenda96/TizenHalo/releases/tag/v1.0.0)
remains the known Q74T baseline. Keep it available for rollback.

## Choose a package

| Asset | Intended first test | Boundary |
|---|---|---|
| `TizenHalo-1.1.0-q74t-55.tpk` | Existing QE55Q74T / T-NKMDEUC-2743.0 setup, Tizen 5.5 | Refactored reference adapter; do not assume another 5.5 TV uses its full-frame layout |
| `TizenHalo-1.1.0-rgb-55.tpk` | Opt-in RGB sampler test on Tizen 5.5 or newer, preferably with a matching library report | Forces RGB measurement, never the Q74T full-frame ABI; newer-runtime startup is unverified |
| `TizenHalo-1.1.0-s84f-90.tpk` | Opt-in QN77S84FAEXZA / Tizen 9 test | Experimental, main video plane only, up to 960×540; not for arbitrary Tizen 9 TVs |
| `TizenHalo-Compatibility-Probe-0.1.0-tizen50.tpk` | Read-only inventory on Tizen 5.0+, especially the UN43RU7100 | Diagnostic only: does not capture or drive LEDs; startup on RU7100 is still untested |

The two 5.5 app profiles use identical application code. The RGB artifact is a
restricted first-test variant, not a permanent per-model fork. All three app
profiles share application IDs: install **one at a time**. The diagnostic uses
a separate ID and is independent.

No full application profile is available for RU7100/Tizen 5.0 or Q80A/Tizen 6.
For another model, start with its exact model/firmware and the read-only probe;
do not select a full-frame contract from the year or Tizen version alone.

## Install and perform a short test

1. Record the exact SKU, firmware and installed Tizen version from the TV's
   information pages. Record the asset filename and alpha version.
2. Note your WLED address, LED count and layout before any clean uninstall. The
   app retains compatible saved settings, but an installer may remove application data.
3. Use [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung) and its normal
   per-TV signing/install flow, or your own Samsung/Tizen certificate process.
   Bundled SDK default signatures are not a universal retail-TV certificate.
4. After upgrading, perform a full TV restart so an earlier service is not left
   running. Open the app from the TV icon. Report if installation fails, the menu closes,
   no QR/address appears, or the service never becomes ready. These are separate
   results from capture failure.
5. If ready, open the displayed local settings page (port 8793), configure your
   WLED/LED layout and play an identified SDR video for **30–60 seconds**.
   State the source/app and the video's FPS if known. Do not switch sources mid-test.
6. Check whether colors move on the correct sides and whether output off/on,
   return to video, standby and icon restart work. Stop if the app misbehaves;
   disable capture and return to the known-working package using the normal installer.

The probe has a different address, port **8794**. Leave its UI open to download
the compatibility JSON; Back closes its server. Native libraries, if requested,
should be exchanged privately rather than attached to a public issue.

## Send a useful report

Open an [issue](https://github.com/tenda96/TizenHalo/issues/new/choose). Use the
compatibility form if available, or copy this text into a blank issue:

```text
Release and full asset filename:
Exact TV model / SKU:
Firmware version:
Installed Tizen version (or unknown):
Installer / certificate method:
Installation: accepted / exact error
Menu and service: ready / closes / waiting / other
Source: HDMI / TV / named app; video title or test clip; video FPS if known
Test duration:
LEDs: correct colors and placement / dark / wrong / stuttering
Reported capture or measurement rate:
Reported LED output rate:
Output off/on, standby, icon restart:
Exact error, status JSON or compatibility report:
```

With the service running, save `http://YOUR-TV-IP:8793/api/status` after the short
test. Include it as a JSON attachment or code block. It identifies the build,
installed capture profile, active method/ABI and RGB symbol/initialization state.
For the new app packages, confirm the reported version is `tizenhalo-1.1.0-alpha.1`;
an old value means the intended alpha service is not running yet.
Mask addresses/identifiers you do not want public. Do not attach certificates,
passwords, full firmware or proprietary libraries to a public issue.

**An interpolated LED send rate of 30/60 Hz is not a capture rate.** RGB sampling
refreshes a few measurement slots in batches; report that separately from output.
If only the menu works, report exactly that rather than marking the model supported.

## Maintainer publication checklist

- Publish as **pre-release** `v1.1.0-alpha.1`, leaving stable v1.0.0 available.
- Tag the source commit used for these artifacts, not an unrelated default-branch
  head. Keep source, release notes, checksums and package profiles together.
- Upload only the prepared allowlisted assets, not `dist/` wholesale; it contains
  older builds and internal staging too.
- The new source commit/branch and tag must be pushed before public release.
- Ask for reports from a small number of owners first. Add each result with its
  package version, source, firmware and limitations to the evidence catalogue.
- Promote a model/profile to supported only after its actual owner test; reserve
  the stable release for device-validated behavior.
